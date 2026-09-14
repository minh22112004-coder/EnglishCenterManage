# 🚨 BUGS LOGIC – ROLE LECTURE

> **Phạm vi kiểm tra:** Toàn bộ luồng đăng nhập, tạo/sửa tài khoản, điểm danh, lịch dạy, và phân quyền Frontend liên quan đến role `Lecture`.
> **Thời điểm phát hiện:** 2026-09-13
> **Mức độ:** Có 3 bug Chí Mạng (P1), 3 bug Nghiêm trọng (P2), 2 bug Trung bình (P3)

---

## TỔNG QUAN CÁC BUG

| # | Bug | File | Mức độ | Triệu chứng |
|---|-----|------|--------|-------------|
| L01 | `UpdateLecture` không đồng bộ `UserName` khi đổi SĐT | `UserController.cs` | 🔴 P1 - Chí mạng | Đăng nhập sai tài khoản |
| L02 | `CreateLecture` không mở khóa tài khoản Lockout | `UserController.cs` | 🔴 P1 - Chí mạng | Đăng nhập bị chặn |
| L03 | `AuthController` thiếu tìm kiếm theo `PhoneNumber` | `AuthController.cs` | 🔴 P1 - Chí mạng | Đăng nhập sai tài khoản |
| L04 | Không chuẩn hóa SĐT khi tạo/sửa giáo viên | `UserController.cs` | 🟠 P2 - Nghiêm trọng | Đăng nhập không tìm thấy user |
| L05 | Không có API reset mật khẩu cho giáo viên | `UserController.cs` | 🟠 P2 - Nghiêm trọng | Admin bó tay khi giáo viên quên MK |
| L06 | `ProtectedRoute` Frontend chỉ kiểm tra `primaryRole` | `App.tsx` | 🟠 P2 - Nghiêm trọng | Tài khoản kiêm nhiệm bị chặn |
| L07 | `Lecture` bị chặn bởi `NotificationController` | `NotificationController.cs` | 🟡 P3 - Trung bình | Giáo viên kiêm phụ huynh không xem được thông báo |
| L08 | `Lecture` có thể sửa thông tin học sinh và lớp không cần thiết | `StudentController.cs`, `ClassController.cs` | 🟡 P3 - Trung bình | Giáo viên có quá nhiều quyền ghi |

---

## CHI TIẾT TỪNG BUG VÀ GIẢI PHÁP

---

### BUG L01 🔴 P1: `UpdateLecture` không đồng bộ `UserName`/`Email` khi đổi SĐT

#### Hiện tượng & Nguyên nhân gốc rễ

Khi Admin vào form sửa Giáo viên và đổi số điện thoại, code chỉ cập nhật trường `PhoneNumber` vật lý trên entity, **hoàn toàn bỏ quên** `UserName` và `Email` — đây chính là 2 trường mà `AuthController.Login` dùng để tìm kiếm người dùng.

```csharp
// TRƯỚC: UserController.cs - UpdateLecture
user.FullName = dto.FullName;
user.PhoneNumber = dto.PhoneNumber; // Chỉ cập nhật trường vật lý
user.BankNumber = dto.BankNumber;
user.BankName = dto.BankName;
// user.UserName vẫn là "0911111111" cũ!
// user.Email vẫn là "0911111111@example.com" cũ!

await _userManager.UpdateAsync(user);
```

**Kịch bản gây lỗi (100% reproduce):**
1. Tạo Giáo viên SĐT `0911111111` → `UserName = "0911111111"`, `Email = "0911111111@example.com"`.
2. Admin edit, đổi SĐT thành `0922222222`.
3. `UserName` vẫn là `0911111111`.
4. Giáo viên đăng nhập bằng `0922222222` → `FindByNameAsync("0922222222")` trả về `null` → HTTP 401.

#### Giải pháp

Khi phát hiện `dto.PhoneNumber` thay đổi so với `user.UserName`, dùng `SetUserNameAsync` và cập nhật `Email`. Cần đồng bộ đủ 3 trường.

```csharp
// SAU: UserController.cs - UpdateLecture
[HttpPut("lectures/{id}")]
public async Task<IActionResult> UpdateLecture(string id, [FromBody] LectureDto dto)
{
    var user = await _userManager.FindByIdAsync(id);
    if (user == null) return NotFound();

    var newPhone = NormalizePhone(dto.PhoneNumber);
    if (string.IsNullOrEmpty(newPhone))
        return BadRequest(new { message = "Số điện thoại không hợp lệ." });

    // Đồng bộ UserName & Email nếu SĐT thay đổi
    if (user.UserName != newPhone)
    {
        // Kiểm tra SĐT mới chưa có ai dùng
        var conflictUser = await _userManager.FindByNameAsync(newPhone);
        if (conflictUser != null && conflictUser.Id != user.Id)
            return Conflict(new { message = "Số điện thoại mới đã được dùng bởi tài khoản khác." });

        await _userManager.SetUserNameAsync(user, newPhone);
        await _userManager.SetEmailAsync(user, newPhone + "@example.com");
    }

    user.PhoneNumber = newPhone;
    user.FullName = dto.FullName;
    user.BankNumber = dto.BankNumber;
    user.BankName = dto.BankName;
    if (!string.IsNullOrEmpty(dto.QrCodeImage)) user.QrCodeImage = dto.QrCodeImage;

    var result = await _userManager.UpdateAsync(user);
    if (result.Succeeded)
        return Ok(new { message = "Cập nhật thành công" });

    return BadRequest(result.Errors);
}
```

---

### BUG L02 🔴 P1: `CreateLecture` không mở khóa tài khoản Lockout của phụ huynh cũ

#### Hiện tượng & Nguyên nhân gốc rễ

Khi người có tài khoản phụ huynh bị Lockout (do con đã xóa mềm, theo logic BUG #02/#03) được tuyển làm Giáo viên, Admin nhập SĐT của họ. Hệ thống tìm thấy tài khoản, thêm role `Lecture` nhưng **không xóa trạng thái Lockout**.

```csharp
// TRƯỚC: UserController.cs - CreateLecture
var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);
if (existingUser != null)
{
    if (!await _userManager.IsInRoleAsync(existingUser, "Lecture"))
        await _userManager.AddToRoleAsync(existingUser, "Lecture");
    // LOCKOUT VẪN ACTIVE! Giáo viên đăng nhập vẫn bị chặn!
    await _userManager.UpdateAsync(existingUser);
    return Ok(...);
}
```

**Hậu quả:** `AuthController.Login` gặp điều kiện:
```csharp
if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
    return Unauthorized("Tài khoản đang bị tạm khóa..."); // Chặn ngay!
```

#### Giải pháp

Khi nâng cấp phụ huynh lên Giáo viên, tự động xóa lockout và reset AccessFailedCount.

```csharp
// SAU: UserController.cs - CreateLecture
var existingUser = await _userManager.FindByNameAsync(newPhone);
if (existingUser != null)
{
    if (!await _userManager.IsInRoleAsync(existingUser, "Lecture"))
        await _userManager.AddToRoleAsync(existingUser, "Lecture");

    existingUser.FullName = dto.FullName ?? existingUser.FullName;
    existingUser.BankNumber = dto.BankNumber ?? existingUser.BankNumber;
    existingUser.BankName = dto.BankName ?? existingUser.BankName;
    if (!string.IsNullOrEmpty(dto.QrCodeImage)) existingUser.QrCodeImage = dto.QrCodeImage;

    // Mở khóa tài khoản nếu đang bị Lockout (trường hợp phụ huynh cũ bị khóa)
    if (await _userManager.IsLockedOutAsync(existingUser))
    {
        await _userManager.SetLockoutEndDateAsync(existingUser, null);
        await _userManager.ResetAccessFailedCountAsync(existingUser);
    }

    await _userManager.UpdateAsync(existingUser);
    return Ok(new {
        message = "Số điện thoại đã tồn tại trong hệ thống. Đã tích hợp thêm quyền Giáo viên và kích hoạt tài khoản.",
        id = existingUser.Id
    });
}
```

---

### BUG L03 🔴 P1: `AuthController` thiếu tìm kiếm theo `PhoneNumber` làm fallback

#### Hiện tượng & Nguyên nhân gốc rễ

```csharp
// TRƯỚC: AuthController.cs - Login
var user = await _userManager.FindByEmailAsync(username)
           ?? await _userManager.FindByNameAsync(username)
           ?? await _userManager.FindByEmailAsync(normalizedUsername)
           ?? await _userManager.FindByNameAsync(normalizedUsername);
// Không bao giờ tìm theo cột PhoneNumber!
```

Nếu `UserName` không khớp với SĐT nhập vào (do BUG L01 chưa được fix, hoặc tài khoản seed `lecture@example.com` có `PhoneNumber = null`), người dùng gõ số điện thoại sẽ không tìm thấy user.

#### Giải pháp

```csharp
// SAU: AuthController.cs - Login
var user = await _userManager.FindByEmailAsync(username)
           ?? await _userManager.FindByNameAsync(username)
           ?? await _userManager.FindByEmailAsync(normalizedUsername)
           ?? await _userManager.FindByNameAsync(normalizedUsername)
           // Fallback: tìm theo số điện thoại (trường hợp UserName không đồng bộ)
           ?? await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == username);
```

---

### BUG L04 🟠 P2: Không chuẩn hóa số điện thoại khi tạo/sửa Giáo viên

#### Hiện tượng & Nguyên nhân gốc rễ

`StudentController` dùng `ExtractValidPhoneNumber` chuẩn hóa toàn bộ. `UserController` chỉ có:

```csharp
// TRƯỚC: UserController.cs
dto.PhoneNumber = dto.PhoneNumber?.Replace(" ", ""); // Chỉ bỏ khoảng trắng!
```

**Các format Admin hay nhập gây lỗi:**

| Input | UserName lưu vào DB | Giáo viên gõ khi login | Kết quả |
|-------|---------------------|------------------------|---------|
| `0912.345.678` | `0912.345.678` | `0912345678` | Không tìm thấy |
| `0912-345-678` | `0912-345-678` | `0912345678` | Không tìm thấy |
| `84912345678` | `84912345678` | `0912345678` | Không tìm thấy |
| `+84912345678` | `+84912345678` | `0912345678` | Không tìm thấy |

#### Giải pháp

Thêm hàm `NormalizePhone` vào `UserController.cs` tương đương `ExtractValidPhoneNumber` trong `StudentController`:

```csharp
// Thêm vào UserController.cs
private static string NormalizePhone(string? rawPhone)
{
    if (string.IsNullOrWhiteSpace(rawPhone)) return "";
    var cleaned = Regex.Replace(rawPhone, @"[\s\.\-\(\)\+]+", "");
    var match = Regex.Match(cleaned, @"(0|84)\d{9}");
    if (!match.Success) return "";
    var phoneStr = match.Value;
    if (phoneStr.StartsWith("84") && phoneStr.Length == 11)
        return "0" + phoneStr.Substring(2);
    return phoneStr;
}
```

Áp dụng tại đầu cả `CreateLecture` và `UpdateLecture`:
```csharp
var newPhone = NormalizePhone(dto.PhoneNumber);
if (string.IsNullOrEmpty(newPhone))
    return BadRequest(new { message = "Số điện thoại không hợp lệ hoặc không đúng định dạng." });
```

---

### BUG L05 🟠 P2: Không có API Reset Password cho Giáo viên

#### Hiện tượng & Nguyên nhân gốc rễ

Khi Admin thăng cấp tài khoản phụ huynh cũ lên Giáo viên, mật khẩu là mật khẩu cũ của phụ huynh (có thể đã đổi). Admin không biết mật khẩu cũ, và hiện tại `UserController` **hoàn toàn thiếu endpoint reset mật khẩu**.

```
Tình huống thực tế:
1. Giáo viên Nguyễn Văn A (trước đây là phụ huynh) đã đổi mật khẩu cũ.
2. Admin thăng cấp họ lên Giáo viên.
3. Admin gửi: "Mật khẩu mặc định: 0912345678@123Aa"
4. Giáo viên đăng nhập → Sai mật khẩu!
5. Admin không có công cụ nào để reset → Bế tắc!
```

#### Giải pháp

Bổ sung endpoint `POST /api/user/lectures/{id}/reset-password`:

```csharp
// Thêm vào UserController.cs
[HttpPost("lectures/{id}/reset-password")]
public async Task<IActionResult> ResetLecturePassword(string id)
{
    var user = await _userManager.FindByIdAsync(id);
    if (user == null) return NotFound();

    // Không cho reset tài khoản Admin
    if (await _userManager.IsInRoleAsync(user, "Admin"))
        return Forbid();

    var phone = user.PhoneNumber ?? user.UserName ?? "";
    if (string.IsNullOrEmpty(phone))
        return BadRequest(new { message = "Không thể xác định SĐT để tạo mật khẩu mặc định." });

    var removeResult = await _userManager.RemovePasswordAsync(user);
    if (!removeResult.Succeeded)
        return BadRequest(new { message = "Không thể xóa mật khẩu cũ." });

    var addResult = await _userManager.AddPasswordAsync(user, phone + "@123Aa");
    if (addResult.Succeeded)
        return Ok(new { message = $"Đã reset mật khẩu về mặc định: {phone}@123Aa" });

    var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
    return BadRequest(new { message = $"Reset thất bại: {errors}" });
}
```

**Lưu ý Frontend:** Thêm nút "Reset MK" bên cạnh mỗi giáo viên trong `LectureList.tsx`.

---

### BUG L06 🟠 P2: `ProtectedRoute` Frontend chỉ kiểm tra `primaryRole`, chặn tài khoản kiêm nhiệm

#### Hiện tượng & Nguyên nhân gốc rễ

```tsx
// TRƯỚC: App.tsx
const ProtectedRoute = ({ children, allowedRoles }) => {
  const role = localStorage.getItem('role'); // Chỉ là primaryRole
  if (!allowedRoles.includes(role)) {
    if (role === 'Admin') return <Navigate to="/admin" />;
    if (role === 'Lecture') return <Navigate to="/lecture" />;
    return <Navigate to="/login" />;
  }
  return <>{children}</>;
};
```

**2 kịch bản gây lỗi:**

| Người dùng | `role` (primaryRole) | Truy cập | `allowedRoles` | Kết quả |
|---|---|---|---|---|
| Admin kiêm Giáo viên | `"Admin"` | `/lecture` | `['Lecture']` | Redirect về `/admin` |
| Giáo viên kiêm Phụ huynh | `"Lecture"` | `/user` | `['User']` | Redirect về `/lecture` |

#### Giải pháp

Kiểm tra mảng `roles` đầy đủ thay vì chỉ `role` (primaryRole):

```tsx
// SAU: App.tsx
const ProtectedRoute = ({ children, allowedRoles }: { children: React.ReactNode, allowedRoles: string[] }) => {
  const role = localStorage.getItem('role');
  const rolesJson = localStorage.getItem('roles');
  const roles: string[] = rolesJson ? JSON.parse(rolesJson) : (role ? [role] : []);

  if (roles.length === 0) {
    return <Navigate to="/login" replace />;
  }

  // Kiểm tra MẢNG roles đầy đủ thay vì chỉ primaryRole
  const hasAccess = allowedRoles.some(r => roles.includes(r));

  if (!hasAccess) {
    if (role === 'Admin') return <Navigate to="/admin" replace />;
    if (role === 'Lecture') return <Navigate to="/lecture" replace />;
    if (role === 'User') return <Navigate to="/user" replace />;
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
};
```

---

### BUG L07 🟡 P3: `NotificationController` chặn Giáo viên kiêm Phụ huynh

#### Hiện tượng & Nguyên nhân gốc rễ

```csharp
// TRƯỚC: NotificationController.cs
[Authorize(Roles = "User")] // Chỉ User!
public class NotificationController : ControllerBase { ... }
```

Giáo viên có con học tại trung tâm (kiêm phụ huynh) có role `["Lecture", "User"]`. Khi `primaryRole = "Lecture"`, frontend không thể dẫn họ vào trang User (BUG L06) và API Notification cũng chỉ cho `User` truy cập.

#### Giải pháp

```csharp
// SAU: NotificationController.cs
[Authorize(Roles = "User,Lecture,Admin")]
public class NotificationController : ControllerBase { ... }
```

---

### BUG L08 🟡 P3: Giáo viên có quyền ghi `PutStudent` và `PutClass` không cần thiết

#### Hiện tượng & Nguyên nhân gốc rễ

```csharp
// StudentController.cs (L141-L142)
[HttpPut("{id}")]
[Authorize(Roles = "Admin,Lecture")] // Giáo viên được sửa thông tin học sinh!

// ClassController.cs (L94-L95)
[HttpPut("{id}")]
[Authorize(Roles = "Admin,Lecture")] // Giáo viên được sửa lớp học!
```

Giáo viên chỉ cần quyền đọc thông tin học sinh (để điểm danh). Quyền sửa tên, SĐT phụ huynh, số buổi học của học sinh là quyền thuần Admin. Vi phạm nguyên tắc **Least Privilege**.

#### Giải pháp

**Phương án A (Đơn giản):** Đổi về chỉ `Admin`:
```csharp
[HttpPut("{id}")]
[Authorize(Roles = "Admin")]
```

**Phương án B (Linh hoạt):** Giữ Lecture nhưng giới hạn scope — Giáo viên chỉ được cập nhật số buổi học, không được đổi thông tin cá nhân:
```csharp
[HttpPut("{id}")]
[Authorize(Roles = "Admin,Lecture")]
public async Task<IActionResult> PutStudent(int id, StudentDto dto)
{
    var existing = await _context.Students.FindAsync(id);
    if (existing == null) return NotFound();

    if (!User.IsInRole("Admin") && User.IsInRole("Lecture"))
    {
        // Giáo viên chỉ được cập nhật số buổi
        existing.TotalAttendedSessions = dto.TotalAttendedSessions;
        existing.TotalPaidSessions = dto.TotalPaidSessions;
        await UpdateRemainingSessionsAsync(existing);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Admin: cập nhật toàn bộ
    // ... full update logic
}
```

---

## 🗺️ SƠ ĐỒ LUỒNG ĐĂNG NHẬP GIÁO VIÊN (Sau khi fix)

```
Giáo viên gõ SĐT + Password
         ↓
AuthController.Login
  ├─ FindByEmailAsync(SĐT)
  ├─ FindByNameAsync(SĐT)
  ├─ FindByEmailAsync(SĐT.ToLower())
  ├─ FindByNameAsync(SĐT.ToLower())
  └─ [FIX L03] Users.FirstOrDefault(u => u.PhoneNumber == SĐT)
         ↓
  User tìm thấy?
  ├─ KHÔNG → HTTP 401 "Sai tài khoản"
  └─ CÓ
       ↓
  [FIX L02] IsLockedOut?
  ├─ CÓ → HTTP 401 "Tài khoản bị khóa"
  └─ KHÔNG
       ↓
  CheckPassword?
  ├─ SAI → AccessFailedAsync → HTTP 401
  └─ ĐÚNG
       ↓
  GetRoles → primaryRole (Admin > Lecture > User)
  Trả về { token, role: "Lecture", roles: ["Lecture", "User"] }
         ↓
Login.tsx lưu localStorage: role = "Lecture", roles = ["Lecture","User"]
         ↓
navigate('/lecture') → LectureDashboard
         ↓
[FIX L06] ProtectedRoute kiểm tra roles.includes('Lecture') → PASS
```

---

## 📋 DANH SÁCH FILE CẦN SỬA

| File | Thay đổi |
|------|---------|
| `UserController.cs` | Fix L01 (SetUserNameAsync), L02 (unlock khi tạo), L04 (NormalizePhone), thêm L05 (reset-password endpoint) |
| `AuthController.cs` | Fix L03 (thêm PhoneNumber fallback) |
| `NotificationController.cs` | Fix L07 (mở rộng Authorize roles) |
| `App.tsx` | Fix L06 (ProtectedRoute dùng mảng roles) |
| `StudentController.cs` | (Tùy chọn) Fix L08 - giới hạn scope Lecture |
| `ClassController.cs` | (Tùy chọn) Fix L08 - giới hạn scope Lecture |
| `LectureList.tsx` | Thêm nút Reset Password (phục vụ L05) |

---

## 🎯 ĐỀ XUẤT THỨ TỰ XỬ LÝ

**Sprint 1 – Blocking (Phải làm ngay):**
- L01 → L02 → L04 (UserController) + L03 (AuthController)

**Sprint 2 – UX/Security (Nên làm):**
- L05 (Reset Password API + Frontend button)
- L06 (App.tsx ProtectedRoute)

**Sprint 3 – Hardening (Tùy chọn):**
- L07 (NotificationController)
- L08 (PutStudent/PutClass scope giới hạn)
