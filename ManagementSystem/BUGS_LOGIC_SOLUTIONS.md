# TÀI LIỆU CHI TIẾT PHƯƠNG ÁN GIẢI QUYẾT 8 BUG LOGIC PHÂN QUYỀN PHỤ HUYNH (USER ROLE)

> **Dự án:** Hệ thống Quản lý Trung tâm Tiếng Anh (English Center Management System)  
> **Phạm vi tác động:** `StudentController.cs`, `NotificationController.cs`, `AppDbContext.cs`, `Notification.cs`  
> **Thời gian khởi tạo:** 2026-09-13  

---

## 📌 BẢNG TỔNG HỢP 8 BUG LOGIC

| STT | Tên Bug Logic | Mức độ | Vị trí phát sinh | File |
| :---: | :--- | :---: | :--- | :--- |
| **01** | Khôi phục (Restore) học sinh tạo ra duplicate 2 học sinh trên tài khoản phụ huynh | **Cao (P1)** | `RecoverStudents` (L252 - L277) | `StudentController.cs` |
| **02** | Xóa mềm học sinh không khóa/xử lý tài khoản phụ huynh (Tài khoản zombie) | **Cao (P1)** | `DeleteStudent` (L194 - L208) | `StudentController.cs` |
| **03** | Xóa hàng loạt (`BulkDeleteStudents`) không đồng bộ trạng thái tài khoản phụ huynh | **Cao (P1)** | `BulkDeleteStudents` (L210 - L226) | `StudentController.cs` |
| **04** | Đổi số điện thoại học sinh (`PutStudent`) bỏ rơi tài khoản phụ huynh cũ | **Nghiêm trọng (P0)** | `PutStudent` (L141 - L192) | `StudentController.cs` |
| **05** | Thông báo học phí kiểm tra trùng lặp bằng họ tên thay vì `StudentId` | **Trung bình (P2)** | `UpdateRemainingSessionsAsync` (L497 - L525) | `StudentController.cs` |
| **06** | Import học sinh không kiểm tra học sinh đã bị xóa mềm (`IgnoreQueryFilters`) | **Trung bình (P2)** | `ImportStudents` (L390 - L495) | `StudentController.cs` |
| **07** | Đoạn kiểm tra điều kiện `if` lồng nhau dư thừa trong `SyncAccounts` | **Thấp (P3)** | `SyncAccounts` (L280 - L328) | `StudentController.cs` |
| **08** | `NotificationController` chặn quyền truy cập đối với tài khoản kiêm nhiệm | **Nghiêm trọng (P0)** | `[Authorize(Roles = "User")]` (L12) | `NotificationController.cs` |

---

## 🛠️ CHI TIẾT NGUYÊN NHÂN VÀ PHƯƠNG ÁN GIẢI QUYẾT TỪNG BUG

---

### 1. BUG #01: Khôi phục (Restore) học sinh gây duplicate 2 học sinh cho tài khoản phụ huynh

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* **Kịch bản lỗi:**
  1. Phụ huynh `0912345678` có học sinh **Nguyễn Văn A**.
  2. Admin xóa học sinh A (Soft Delete: `IsDeleted = true`, `DeletedAt = DateTime.UtcNow`).
  3. Sau đó, Admin (hoặc người quản trị khác) vô tình tạo lại học sinh **Nguyễn Văn A** hoặc học sinh mới cùng số điện thoại `0912345678`. Hệ thống cho phép vì `PostStudent` không tìm các bản ghi đã xóa.
  4. Sau đó Admin vào màn hình Thùng rác bấm **Khôi phục (Restore)** học sinh A.
  5. `RecoverStudents` đơn thuần đặt `student.IsDeleted = false` mà **không kiểm tra xung đột trùng lặp**.
  6. **Hậu quả:** Tài khoản phụ huynh `0912345678` khi gọi `GetMyChildren` sẽ nhận về **2 bản ghi học sinh trùng lặp**, gây sai lệch dữ liệu học phí, điểm danh và giao diện hiển thị.

#### 🟢 Phương án giải quyết
* Trước khi đổi `IsDeleted = false` cho bất kỳ học sinh nào trong danh sách khôi phục, kiểm tra xem trong hệ thống đã tồn tại học sinh **Active** (`!IsDeleted`) có cùng `FullName` và `ParentPhoneNumber` hay chưa.
* Nếu phát hiện trùng lặp: Ngăn chặn thao tác và trả về thông báo lỗi `409 Conflict` nêu rõ tên học sinh đã tồn tại, hoặc yêu cầu xóa/sửa bản ghi trùng trước khi restore.
* Đồng thời, tự động mở khóa (Unlock) tài khoản phụ huynh nếu trước đó tài khoản bị khóa do không còn học sinh nào.

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `RecoverStudents`)
```csharp
[HttpPut("recover")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> RecoverStudents([FromBody] List<int> ids)
{
    if (ids == null || !ids.Any())
        return BadRequest("Không có học sinh nào được chọn.");

    var studentsToRecover = await _context.Students
        .IgnoreQueryFilters()
        .Where(s => ids.Contains(s.Id) && s.IsDeleted)
        .ToListAsync();
    
    if (!studentsToRecover.Any())
        return NotFound("Không tìm thấy học sinh nào trong thùng rác.");

    var conflictErrors = new List<string>();

    foreach (var student in studentsToRecover)
    {
        // Kiểm tra xem đã có học sinh active cùng tên và số điện thoại phụ huynh chưa
        var isDuplicate = await _context.Students
            .AnyAsync(s => !s.IsDeleted 
                        && s.Id != student.Id 
                        && s.FullName.Trim().ToLower() == student.FullName.Trim().ToLower()
                        && s.ParentPhoneNumber == student.ParentPhoneNumber);

        if (isDuplicate)
        {
            conflictErrors.Add($"Học sinh '{student.FullName}' (SĐT: {student.ParentPhoneNumber}) đã có bản ghi đang hoạt động trong hệ thống.");
            continue;
        }

        student.IsDeleted = false;
        student.DeletedAt = null;
        _context.Entry(student).State = EntityState.Modified;

        // Nếu tài khoản phụ huynh đang bị khóa (Lockout), mở khóa lại
        if (!string.IsNullOrWhiteSpace(student.ParentPhoneNumber))
        {
            var parentUser = await _userManager.FindByNameAsync(student.ParentPhoneNumber);
            if (parentUser != null && await _userManager.IsLockedOutAsync(parentUser))
            {
                await _userManager.SetLockoutEndDateAsync(parentUser, null);
            }
        }
    }

    if (conflictErrors.Any())
    {
        return Conflict(new { message = "Không thể khôi phục một số học sinh do bị trùng lặp dữ liệu.", errors = conflictErrors });
    }
    
    await _context.SaveChangesAsync();
    return NoContent();
}
```

---

### 2. BUG #02: Xóa học sinh không xử lý tài khoản phụ huynh (Tài khoản zombie)

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Khi Admin xóa một học sinh qua endpoint `DeleteStudent(int id)`:
  * Bản ghi học sinh được chuyển sang `IsDeleted = true`.
  * Tài khoản `AppUser` của phụ huynh (ứng với `ParentPhoneNumber`) **vẫn tồn tại và vẫn active bình thường**.
* Nếu phụ huynh đó chỉ có duy nhất 1 người con này tại trung tâm, tài khoản của họ trở thành **tài khoản vô chủ (zombie)**:
  * Phụ huynh vẫn đăng nhập được vào ứng dụng mobile/web.
  * Màn hình danh sách con (`GetMyChildren`) trả về mảng rỗng `[]`.
  * Gây hoang mang cho người dùng, chiếm tài nguyên hệ thống và tiềm ẩn nguy cơ bảo mật nếu phụ huynh cũ vẫn truy cập được hệ thống trung tâm.

#### 🟢 Phương án giải quyết
* Sau khi đánh dấu xóa học sinh, kiểm tra xem số điện thoại phụ huynh đó còn người con nào khác **đang hoạt động** (`!IsDeleted`) hay không.
* **Lưu ý nghiệp vụ quan trọng:** Không nên xóa cứng (`DeleteAsync`) tài khoản `AppUser` ngay lập tức, vì nếu sau này Admin bấm **Khôi phục (Recover)** học sinh, phụ huynh sẽ mất toàn bộ tài khoản và mật khẩu đã tạo.
* **Giải pháp chuẩn:** Thực hiện **Khóa tài khoản (Lockout)**:
  * `await _userManager.SetLockoutEndDateAsync(parentUser, DateTimeOffset.MaxValue);`
  * Khi phụ huynh đăng nhập, API `AuthController.Login` đã có sẵn logic kiểm tra `IsLockedOutAsync` và sẽ chặn với thông báo thích hợp.
  * Nếu tài khoản đó còn có quyền khác (ví dụ: `Lecture`), tuyệt đối không khóa mà chỉ giữ nguyên quyền `Lecture`.

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `DeleteStudent`)
```csharp
[HttpDelete("{id}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> DeleteStudent(int id)
{
    var existing = await _context.Students.FindAsync(id);
    if (existing == null)
    {
        return NotFound();
    }

    var parentPhone = existing.ParentPhoneNumber;
    _context.Students.Remove(existing);
    await _context.SaveChangesAsync();

    // Kiểm tra xem phụ huynh còn con nào khác đang theo học không
    if (!string.IsNullOrWhiteSpace(parentPhone))
    {
        var hasOtherActiveChildren = await _context.Students
            .AnyAsync(s => s.ParentPhoneNumber == parentPhone && !s.IsDeleted);

        if (!hasOtherActiveChildren)
        {
            var parentUser = await _userManager.FindByNameAsync(parentPhone);
            if (parentUser != null)
            {
                // Chỉ khóa nếu người này không phải Admin hoặc Giáo viên (Lecture)
                var roles = await _userManager.GetRolesAsync(parentUser);
                if (!roles.Contains("Admin") && !roles.Contains("Lecture"))
                {
                    await _userManager.SetLockoutEndDateAsync(parentUser, DateTimeOffset.MaxValue);
                }
            }
        }
    }

    return NoContent();
}
```

---

### 3. BUG #03: Xóa hàng loạt (`BulkDeleteStudents`) không đồng bộ trạng thái tài khoản phụ huynh

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Endpoint `BulkDeleteStudents([FromBody] List<int> ids)` chỉ thực hiện `_context.Students.RemoveRange(studentsToDelete)` rồi lưu DB.
* Toàn bộ các phụ huynh của những học sinh bị xóa hàng loạt không được kiểm tra, dẫn tới hàng loạt tài khoản phụ huynh rơi vào trạng thái "zombie" cùng một lúc.

#### 🟢 Phương án giải quyết
* Lấy danh sách các số điện thoại phụ huynh `distinctParentPhones` từ tập học sinh chuẩn bị xóa.
* Thực hiện xóa mềm học sinh.
* Duyệt qua từng số điện thoại phụ huynh trong danh sách:
  * Đếm số học sinh còn active của phụ huynh đó.
  * Nếu `activeCount == 0`, tiến hành khóa tài khoản phụ huynh đó (nếu không phải Admin/Lecture).

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `BulkDeleteStudents`)
```csharp
[HttpDelete("bulk")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> BulkDeleteStudents([FromBody] List<int> ids)
{
    if (ids == null || !ids.Any())
        return BadRequest("Không có học sinh nào được chọn.");

    var studentsToDelete = await _context.Students
        .Where(s => ids.Contains(s.Id))
        .ToListAsync();
    
    if (!studentsToDelete.Any())
        return NotFound("Không tìm thấy học sinh nào.");

    var parentPhones = studentsToDelete
        .Select(s => s.ParentPhoneNumber)
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Distinct()
        .ToList();

    _context.Students.RemoveRange(studentsToDelete);
    await _context.SaveChangesAsync();

    // Đồng bộ kiểm tra khóa tài khoản các phụ huynh không còn con học
    foreach (var phone in parentPhones)
    {
        var hasOtherActiveChildren = await _context.Students
            .AnyAsync(s => s.ParentPhoneNumber == phone && !s.IsDeleted);

        if (!hasOtherActiveChildren)
        {
            var parentUser = await _userManager.FindByNameAsync(phone);
            if (parentUser != null)
            {
                var roles = await _userManager.GetRolesAsync(parentUser);
                if (!roles.Contains("Admin") && !roles.Contains("Lecture"))
                {
                    await _userManager.SetLockoutEndDateAsync(parentUser, DateTimeOffset.MaxValue);
                }
            }
        }
    }

    return NoContent();
}
```

---

### 4. BUG #04: Đổi số điện thoại học sinh (`PutStudent`) bỏ rơi tài khoản phụ huynh cũ

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Khi chỉnh sửa học sinh: Admin nhập số điện thoại mới cho phụ huynh.
* Hiện tại trong `PutStudent`:
  1. Gán `existing.ParentPhoneNumber = validPhone`.
  2. Tạo mới tài khoản cho `validPhone` (nếu chưa có).
  3. **Hoàn toàn bỏ quên số điện thoại cũ (`oldPhone`)!**
* **Hậu quả nghiêm trọng:**
  * Tài khoản mang tên `oldPhone` vẫn còn, phụ huynh vẫn đăng nhập bằng số cũ nhưng vào trong không thấy con đâu (vì con đã được đổi sang SĐT mới).
  * Phụ huynh không biết là đã có tài khoản mang số mới được tạo với mật khẩu mặc định, dẫn đến không thể truy cập tài khoản, khiếu nại trung tâm.

#### 🟢 Phương án giải quyết
* Lưu lại `oldPhone = existing.ParentPhoneNumber` trước khi gán giá trị mới.
* Nếu `oldPhone != validPhone`:
  * **Trường hợp A (SĐT mới chưa có tài khoản):** 
    Nếu phụ huynh cũ chỉ có 1 học sinh này, cách xử lý mượt mà nhất là **đổi tên đăng nhập của tài khoản cũ sang SĐT mới** (update `UserName`, `Email`, `PhoneNumber`). Phụ huynh không bị đổi mật khẩu, không bị mất lịch sử thông báo.
  * **Trường hợp B (SĐT mới đã có tài khoản, hoặc phụ huynh cũ vẫn còn con khác):**
    * Gán học sinh sang SĐT mới (tài khoản SĐT mới sẽ tự nhận thêm học sinh này).
    * Kiểm tra xem SĐT cũ có còn con nào khác không. Nếu SĐT cũ không còn con nào, thực hiện khóa tài khoản SĐT cũ (`Lockout`).

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `PutStudent`)
```csharp
[HttpPut("{id}")]
[Authorize(Roles = "Admin,Lecture")]
public async Task<IActionResult> PutStudent(int id, StudentDto dto)
{
    var existing = await _context.Students.FindAsync(id);
    if (existing == null)
    {
        return NotFound();
    }

    var oldPhone = existing.ParentPhoneNumber;
    var rawPhone = dto.ParentPhoneNumber ?? "";
    var validPhone = ExtractValidPhoneNumber(rawPhone);
    var parentName = dto.ParentName ?? "";
    if (rawPhone != validPhone && Regex.IsMatch(rawPhone, @"[a-zA-Z]"))
    {
        parentName = string.IsNullOrWhiteSpace(parentName) ? rawPhone : $"{parentName} ({rawPhone})";
    }

    existing.FullName = dto.FullName;
    existing.DateOfBirth = dto.DateOfBirth;
    existing.ParentName = parentName;
    existing.ParentPhoneNumber = validPhone;
    existing.TotalAttendedSessions = dto.TotalAttendedSessions;
    existing.TotalPaidSessions = dto.TotalPaidSessions;
    existing.ClassId = dto.ClassId;

    await UpdateRemainingSessionsAsync(existing);
    await _context.SaveChangesAsync();

    // Xử lý đồng bộ tài khoản khi thay đổi số điện thoại phụ huynh
    if (!string.IsNullOrWhiteSpace(oldPhone) && oldPhone != validPhone)
    {
        var oldUser = await _userManager.FindByNameAsync(oldPhone);
        var newUser = !string.IsNullOrWhiteSpace(validPhone) ? await _userManager.FindByNameAsync(validPhone) : null;

        // Kiểm tra xem số cũ còn học sinh nào khác không
        var oldPhoneStillHasStudents = await _context.Students
            .AnyAsync(s => s.ParentPhoneNumber == oldPhone && !s.IsDeleted);

        if (oldUser != null && !oldPhoneStillHasStudents)
        {
            if (newUser == null && !string.IsNullOrWhiteSpace(validPhone))
            {
                // Di chuyển tài khoản cũ sang SĐT mới để bảo lưu mật khẩu và thông báo
                oldUser.UserName = validPhone;
                oldUser.PhoneNumber = validPhone;
                oldUser.Email = validPhone + "@example.com";
                oldUser.FullName = parentName;
                await _userManager.UpdateAsync(oldUser);
            }
            else
            {
                // SĐT mới đã có tài khoản riêng -> Khóa tài khoản cũ không còn học sinh
                var oldRoles = await _userManager.GetRolesAsync(oldUser);
                if (!oldRoles.Contains("Admin") && !oldRoles.Contains("Lecture"))
                {
                    await _userManager.SetLockoutEndDateAsync(oldUser, DateTimeOffset.MaxValue);
                }
            }
        }
    }

    // Đảm bảo SĐT mới có tài khoản đăng nhập
    if (!string.IsNullOrWhiteSpace(validPhone))
    {
        var parentUser = await _userManager.FindByNameAsync(validPhone);
        if (parentUser == null)
        {
            var newUser = new AppUser
            {
                UserName = validPhone,
                Email = validPhone + "@example.com",
                PhoneNumber = validPhone,
                FullName = parentName ?? "Phụ huynh " + existing.FullName
            };
            var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "User");
            }
        }
        else if (await _userManager.IsLockedOutAsync(parentUser))
        {
            // Nếu tài khoản phụ huynh mới từng bị khóa thì mở lại
            await _userManager.SetLockoutEndDateAsync(parentUser, null);
        }
    }

    return NoContent();
}
```

---

### 5. BUG #05: Thông báo học phí kiểm tra trùng lặp bằng họ tên thay vì `StudentId`

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Tại hàm `UpdateRemainingSessionsAsync`:
  ```csharp
  var existingNotif = await _context.Notifications
      .FirstOrDefaultAsync(n => n.UserId == parentUser.Id 
                             && !n.IsRead 
                             && n.Message.Contains(student.FullName));
  ```
* **Lỗi:** 
  * Dùng `n.Message.Contains(student.FullName)` để lọc xem đã có thông báo học phí chưa.
  * Nếu một phụ huynh có 2 con trùng tên (hoặc trung tâm có học sinh trùng họ tên), con thứ 2 sắp hết buổi sẽ **không bao giờ nhận được thông báo** vì hệ thống tưởng thông báo của con thứ nhất là đã gửi rồi.
  * Ngoài ra, nếu tin nhắn sau này thay đổi nội dung (ví dụ đổi cấu trúc câu văn), điều kiện `Contains` rất dễ sai hoặc vỡ logic.

#### 🟢 Phương án giải quyết
* Thêm trường `StudentId` (nullable int) vào entity `Notification`.
* Khi tạo thông báo nhắc phí, gán `notif.StudentId = student.Id`.
* Khi truy vấn thông báo trùng chưa đọc, lọc chính xác theo `n.UserId == parentUser.Id && n.StudentId == student.Id && !n.IsRead`.

#### 💻 Code giải pháp mẫu
1. **Bổ sung vào `Models/Notification.cs`:**
```csharp
public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int? StudentId { get; set; } // Khóa định danh chính xác học sinh
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public bool HidePopup { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

2. **Cập nhật hàm `UpdateRemainingSessionsAsync` (`StudentController.cs`):**
```csharp
private async Task UpdateRemainingSessionsAsync(Student student)
{
    student.TotalRemainingSessions = student.TotalPaidSessions - student.TotalAttendedSessions;
    if (student.TotalRemainingSessions < 3 && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber))
    {
        var parentUser = await _userManager.FindByNameAsync(student.ParentPhoneNumber);
        if (parentUser != null)
        {
            // Kiểm tra chính xác theo UserId và StudentId
            var existingNotif = await _context.Notifications
                .FirstOrDefaultAsync(n => n.UserId == parentUser.Id 
                                       && n.StudentId == student.Id 
                                       && !n.IsRead);
            
            if (existingNotif == null)
            {
                string message = student.TotalRemainingSessions < 0
                    ? $"Học sinh {student.FullName} đã học lố {-student.TotalRemainingSessions} buổi. Quý phụ huynh vui lòng hoàn thành đóng học phí."
                    : $"Học sinh {student.FullName} chỉ còn {student.TotalRemainingSessions} buổi học. Quý phụ huynh vui lòng đóng thêm học phí để không gián đoạn việc học.";

                _context.Notifications.Add(new Notification
                {
                    UserId = parentUser.Id,
                    StudentId = student.Id,
                    Message = message,
                    IsRead = false,
                    HidePopup = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }
}
```

---

### 6. BUG #06: Import học sinh không kiểm tra học sinh đã bị xóa mềm (`IgnoreQueryFilters`)

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Tại endpoint `ImportStudents`:
  ```csharp
  var student = await _context.Students.FirstOrDefaultAsync(s => 
      s.ParentPhoneNumber == dto.ParentPhoneNumber && s.FullName == dto.FullName && !s.IsDeleted);
  ```
* Mặc định EF Core áp dụng Global Query Filter `!s.IsDeleted`. Do đó nếu học sinh Nguyễn Văn A đã bị xóa mềm trước đó, câu truy vấn trên sẽ trả về `null`.
* Code sẽ tưởng đây là học sinh mới và thực hiện `_context.Students.Add(student)`.
* **Hậu quả:** 
  * Trong Database tồn tại 2 bản ghi cho cùng một người: 1 bản ghi cũ (`IsDeleted = true`) và 1 bản ghi mới (`IsDeleted = false`).
  * Nếu sau này Admin vào thùng rác bấm Khôi phục học sinh cũ, tài khoản phụ huynh sẽ lập tức gặp BUG #01 (hiển thị 2 học sinh giống hệt nhau).

#### 🟢 Phương án giải quyết
* Sử dụng `.IgnoreQueryFilters()` để kiểm tra sự tồn tại trong DB bao gồm cả các bản ghi đã xóa mềm.
* Nếu bản ghi đã tồn tại nhưng `IsDeleted == true`:
  * Khôi phục học sinh (`IsDeleted = false`, `DeletedAt = null`).
  * Cập nhật lại số buổi học, lớp học theo file import mới.
  * Tái kích hoạt tài khoản phụ huynh nếu đang bị khóa.

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `ImportStudents`)
```csharp
// Tìm kiếm cả bản ghi đang active và đã xóa mềm
var student = await _context.Students
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(s => s.ParentPhoneNumber == validPhone 
                           && s.FullName.Trim().ToLower() == dto.FullName.Trim().ToLower());

if (student == null)
{
    // Chưa từng tồn tại: Tạo mới
    student = new Student
    {
        FullName = dto.FullName,
        ParentName = parentName,
        ParentPhoneNumber = validPhone,
        DateOfBirth = dto.DateOfBirth,
        TotalAttendedSessions = dto.TotalAttendedSessions,
        TotalPaidSessions = dto.TotalPaidSessions,
        TotalRemainingSessions = dto.TotalRemainingSessions,
        ClassId = cls.Id
    };
    _context.Students.Add(student);
}
else
{
    // Đã tồn tại (kể cả trong thùng rác): Tái sử dụng & Khôi phục
    student.IsDeleted = false;
    student.DeletedAt = null;
    student.ParentName = !string.IsNullOrWhiteSpace(parentName) ? parentName : student.ParentName;
    student.DateOfBirth = dto.DateOfBirth;
    student.TotalAttendedSessions = dto.TotalAttendedSessions;
    student.TotalPaidSessions = dto.TotalPaidSessions;
    student.ClassId = cls.Id;
    _context.Entry(student).State = EntityState.Modified;
}
```

---

### 7. BUG #07: Đoạn kiểm tra điều kiện `if` lồng nhau dư thừa trong `SyncAccounts`

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Trong `SyncAccounts` (L291 và L304):
  ```csharp
  if (!string.IsNullOrWhiteSpace(validPhone)) // Điều kiện 1
  {
      if (rawPhone != validPhone && Regex.IsMatch(rawPhone ?? "", @"[a-zA-Z]"))
      { ... }
      if (student.ParentPhoneNumber != validPhone)
      { ... }

      if (!string.IsNullOrWhiteSpace(validPhone)) // ❌ Điều kiện 2: Trùng lặp hoàn toàn
      {
          var parentUser = await _userManager.FindByNameAsync(validPhone);
          ...
      }
  }
  ```
* `validPhone` đã được kiểm tra ở cấp ngoài cùng, việc lặp lại làm code rối rắm, khó bảo trì và dễ sinh lỗi khi refactor.

#### 🟢 Phương án giải quyết
* Gỡ bỏ hoàn toàn khối `if` lồng nhau dư thừa bằng **Early Return pattern** (`if (string.IsNullOrWhiteSpace(validPhone)) continue;`).
* Tách biệt 2 pha xử lý rõ ràng:
  - **Phase 1:** Chuẩn hóa dữ liệu học sinh trong Database (`ParentPhoneNumber`, `ParentName`).
  - **Phase 2:** Tối ưu hóa truy vấn tài khoản Identity bằng `HashSet<string> processedPhones` (chỉ tương tác với `UserManager` đúng 1 lần cho mỗi số điện thoại duy nhất, tránh tình trạng lặp query khi phụ huynh có nhiều con).
* Cơ chế tự chữa lành (**Self-healing / Data Repair**): Tự động mở khóa các tài khoản phụ huynh đang bị lockout nhưng vẫn còn ít nhất 1 học sinh đang active trong hệ thống:
  - Bổ sung kiểm tra vai trò (Role Guard): không can thiệp vào tài khoản `Admin` hoặc `Lecture`.
  - Mở khóa bằng `SetLockoutEndDateAsync(parentUser, null)` và xóa số lần thử đăng nhập sai bằng `ResetAccessFailedCountAsync(parentUser)`.
* Cập nhật response object đầy đủ: Trả về cả chuỗi `message` thân thiện (phù hợp `alert` của Frontend) lẫn các trường số liệu (`createdCount`, `unlockedCount`).

#### 💻 Code giải pháp mẫu (`StudentController.cs` - `SyncAccounts`)
```csharp
[HttpPost("sync-accounts")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> SyncAccounts()
{
    // 1. Lấy toàn bộ học sinh active (áp dụng Global Query Filter !IsDeleted)
    var students = await _context.Students.ToListAsync();
    int createdCount = 0;
    int unlockedCount = 0;

    // Lưu các SĐT đã xử lý tài khoản Identity để tránh query DB lặp lại đối với phụ huynh nhiều con
    var processedPhones = new HashSet<string>();

    foreach (var student in students)
    {
        var rawPhone = student.ParentPhoneNumber;
        var validPhone = ExtractValidPhoneNumber(rawPhone);

        // Bỏ qua nếu không trích xuất được số điện thoại hợp lệ
        if (string.IsNullOrWhiteSpace(validPhone)) continue;

        // === PHASE 1: Chuẩn hóa bản ghi học sinh trong DB (áp dụng cho từng học sinh) ===
        if (rawPhone != validPhone && Regex.IsMatch(rawPhone ?? "", @"[a-zA-Z]"))
        {
            student.ParentName = string.IsNullOrWhiteSpace(student.ParentName)
                ? (rawPhone ?? "")
                : $"{student.ParentName} ({rawPhone})";
        }

        if (student.ParentPhoneNumber != validPhone)
        {
            student.ParentPhoneNumber = validPhone;
        }

        // === PHASE 2: Xử lý tài khoản Identity (Đảm bảo mỗi SĐT duy nhất chỉ query UserManager đúng 1 lần) ===
        if (!processedPhones.Add(validPhone)) continue;

        var parentUser = await _userManager.FindByNameAsync(validPhone);
        if (parentUser == null)
        {
            var newUser = new AppUser
            {
                UserName = validPhone,
                Email = validPhone + "@example.com",
                PhoneNumber = validPhone,
                FullName = !string.IsNullOrWhiteSpace(student.ParentName)
                    ? student.ParentName
                    : ("Phụ huynh " + student.FullName)
            };

            var result = await _userManager.CreateAsync(newUser, validPhone + "@123Aa");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "User");
                createdCount++;
            }
        }
        else if (await _userManager.IsLockedOutAsync(parentUser))
        {
            // [Self-healing/Repair]: Tự động mở khóa tài khoản bị kẹt lockout
            // nhưng hiện vẫn còn ít nhất 1 học sinh active trong hệ thống.
            var roles = await _userManager.GetRolesAsync(parentUser);
            if (!roles.Contains("Admin") && !roles.Contains("Lecture"))
            {
                await _userManager.SetLockoutEndDateAsync(parentUser, null);
                await _userManager.ResetAccessFailedCountAsync(parentUser);
                unlockedCount++;
            }
        }
    }

    // Lưu tất cả các thay đổi về ParentPhoneNumber/ParentName của học sinh
    await _context.SaveChangesAsync();

    return Ok(new
    {
        message = $"Đã đồng bộ thành công: tạo mới {createdCount} và mở khóa {unlockedCount} tài khoản phụ huynh.",
        createdCount,
        unlockedCount
    });
}
```

---

### 8. BUG #08: `NotificationController` chặn quyền truy cập đối với tài khoản kiêm nhiệm

#### 🔴 Hiện tượng & Nguyên nhân gốc rễ
* Khai báo Controller:
  ```csharp
  [Route("api/[controller]")]
  [ApiController]
  [Authorize(Roles = "User")] // ❌ Chỉ cho phép Role User
  public class NotificationController : ControllerBase
  ```
* Trong `UserController.CreateLecture`, hệ thống hỗ trợ trường hợp một phụ huynh (`User`) được cấp thêm quyền Giáo viên (`Lecture`):
  ```csharp
  if (!await _userManager.IsInRoleAsync(existingUser, "Lecture"))
  {
      await _userManager.AddToRoleAsync(existingUser, "Lecture");
  }
  ```
* Khi người này đăng nhập, `AuthController.Login` tính toán:
  * `primaryRole = "Lecture"` (do phân cấp Admin > Lecture > User).
  * Frontend lưu `role = "Lecture"` và gửi Token.
* Khi giáo viên kiêm phụ huynh này muốn xem thông báo học phí hoặc nhắc nhở của con mình qua API `/api/notification/my`, hệ thống trả về lỗi **`403 Forbidden`** vì Controller yêu cầu cứng `Roles = "User"`.

#### 🟢 Phương án giải quyết
* Thông báo được quản lý theo `UserId` cá nhân của mỗi người dùng (dựa trên JWT Claim `NameIdentifier` / `Name`). Bất kỳ người dùng nào đã xác thực đều có quyền xem thông báo cá nhân của chính mình.
* Đổi `[Authorize(Roles = "User")]` thành `[Authorize]` (cho phép tất cả các tài khoản đã đăng nhập: Admin, Lecture, User đều truy cập được hệ sinh thái thông báo).
* Truy vấn người dùng theo `UserId` (NameIdentifier) để đạt chuẩn tối đa thay vì chỉ phụ thuộc vào `User.Identity?.Name`.

#### 💻 Code giải pháp mẫu (`NotificationController.cs`)
```csharp
namespace ManagementSystem.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Cho phép mọi tài khoản đã đăng nhập
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public NotificationController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications()
        {
            var user = await _userManager.GetUserAsync(User) 
                       ?? await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new {
                    n.Id,
                    n.StudentId,
                    n.Message,
                    n.IsRead,
                    n.HidePopup,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User) 
                       ?? await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
            if (notif == null) return NotFound();

            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id}/hide")]
        public async Task<IActionResult> HidePopup(int id)
        {
            var user = await _userManager.GetUserAsync(User) 
                       ?? await _userManager.FindByNameAsync(User.Identity?.Name ?? "");
            if (user == null) return Unauthorized();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
            if (notif == null) return NotFound();

            notif.HidePopup = true;
            notif.IsRead = true;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
```

---

## 🚀 KẾ HOẠCH TRIỂN KHAI & KIỂM THỬ (TEST PLAN)

1. **Giai đoạn 1: Model & Migration**
   - Bổ sung trường `StudentId` vào Model `Notification`.
   - Chạy lệnh migration hoặc update database context tương ứng.

2. **Giai đoạn 2: Cập nhật `StudentController.cs`**
   - Thay thế các phương thức: `RecoverStudents`, `DeleteStudent`, `BulkDeleteStudents`, `PutStudent`, `ImportStudents`, `SyncAccounts`, `UpdateRemainingSessionsAsync`.

3. **Giai đoạn 3: Cập nhật `NotificationController.cs`**
   - Chuyển `[Authorize(Roles = "User")]` thành `[Authorize]`.

4. **Kịch bản kiểm thử tích hợp (Integration Test Cases):**
   - ✅ **TC-01:** Tạo học sinh A -> Xóa học sinh A -> Thử khôi phục A (thành công). Tạo mới học sinh A -> Thử khôi phục A từ thùng rác -> Hệ thống chặn lỗi 409 Conflict.
   - ✅ **TC-02:** Xóa học sinh duy nhất của phụ huynh -> Kiểm tra tài khoản phụ huynh bị khóa (Lockout). Đăng nhập thử -> Báo tài khoản bị khóa. Khôi phục học sinh -> Tài khoản phụ huynh tự động mở khóa.
   - ✅ **TC-03:** Sửa số điện thoại học sinh -> Kiểm tra phụ huynh đăng nhập bằng số mới nhận đúng con, số cũ không bị rò rỉ dữ liệu.
   - ✅ **TC-04:** Tạo giáo viên kiêm phụ huynh -> Gọi API `/api/notification/my` -> Kiểm tra 200 OK và danh sách thông báo chính xác.
