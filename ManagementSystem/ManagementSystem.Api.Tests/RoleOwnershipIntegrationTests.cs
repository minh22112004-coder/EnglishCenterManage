using System.Security.Claims;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Controllers;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Tests;

[Collection("RoleOwnership PostgreSQL integration")]
public class RoleOwnershipIntegrationTests
{
    private static int _phoneSequence = 10_000_000;
    private readonly PostgreSqlFixture _fixture;

    public RoleOwnershipIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task UserToLecture_HasExactRoleSet()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var phone = NewPhone();
        var user = await CreateUserAsync(userManager, phone, AppRoles.User);

        var controller = new UserController(userManager, GetContext(scope));
        var result = await controller.CreateLecture(new LectureDto
        {
            FullName = "User promoted to lecture",
            PhoneNumber = phone,
            BankName = "Test Bank",
            BankNumber = "123456"
        });

        Assert.IsType<OkObjectResult>(result);
        var roles = (await userManager.GetRolesAsync(user)).OrderBy(r => r).ToArray();
        Assert.Equal(new[] { AppRoles.Lecture, AppRoles.User }.OrderBy(r => r), roles);
    }

    [SkippableFact]
    public async Task LectureToUser_WithFutureSchedule_Returns409()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var user = await CreateUserAsync(userManager, NewPhone(), AppRoles.Lecture);
        var testClass = new Class
        {
            ClassCode = $"FUT_{Guid.NewGuid():N}"[..20],
            ClassName = $"Future role class {Guid.NewGuid():N}"
        };
        context.Classes.Add(testClass);
        await context.SaveChangesAsync();
        context.WorkSchedules.Add(new WorkSchedule
        {
            UserId = user.Id,
            Date = DateTime.UtcNow.AddDays(2),
            Classes = new List<Class> { testClass }
        });
        await context.SaveChangesAsync();

        var controller = new UserController(userManager, context);
        var result = await controller.ChangeRole(user.Id, new ChangeRoleDto { TargetRole = AppRoles.User });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(new[] { AppRoles.Lecture }, await userManager.GetRolesAsync(user));
    }

    [SkippableFact]
    public async Task DualRole_RemoveLecture_PreservesUserAccess()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var phone = NewPhone();
        var user = await CreateUserAsync(userManager, phone, AppRoles.Lecture, AppRoles.User);
        var student = new Student
        {
            FullName = $"Child of dual role {Guid.NewGuid():N}",
            ParentName = "Dual role parent",
            ParentPhoneNumber = phone,
            TotalPaidSessions = 10,
            TotalRemainingSessions = 10
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        var userController = new UserController(userManager, context);
        var deleteResult = await userController.DeleteLecture(user.Id);
        Assert.IsType<OkObjectResult>(deleteResult);
        Assert.Equal(new[] { AppRoles.User }, await userManager.GetRolesAsync(user));

        var studentController = new StudentController(context, userManager);
        SetPrincipal(studentController, user, AppRoles.User);
        var childrenResult = await studentController.GetMyChildren();
        var childrenOk = Assert.IsType<OkObjectResult>(childrenResult.Result);
        var children = Assert.IsAssignableFrom<IEnumerable<StudentResponseDto>>(childrenOk.Value);
        Assert.Single(children);
        Assert.Equal(student.Id, children.Single().Id);
    }

    [SkippableFact]
    public async Task OldJwtRejectedAfterRoleChange()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var user = await CreateUserAsync(userManager, NewPhone(), AppRoles.Lecture);
        var oldStamp = await userManager.GetSecurityStampAsync(user);

        using var factory = CreateHttpFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var oldToken = await LoginAsync(client, user, "Test@123");

        var controller = new UserController(userManager, context);
        var result = await controller.ChangeRole(user.Id, new ChangeRoleDto { TargetRole = AppRoles.User });
        Assert.IsType<OkObjectResult>(result);

        var refreshed = await userManager.FindByIdAsync(user.Id);
        Assert.NotNull(refreshed);
        var currentStamp = await userManager.GetSecurityStampAsync(refreshed!);
        Assert.NotEqual(oldStamp, currentStamp);
        Assert.NotEqual(oldStamp, refreshed!.SecurityStamp);

        var response = await GetStudentsWithTokenAsync(client, oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var freshToken = await LoginAsync(client, user, "Test@123");
        var freshResponse = await GetStudentsWithTokenAsync(client, freshToken);
        Assert.Equal(HttpStatusCode.OK, freshResponse.StatusCode);
    }

    [SkippableFact]
    public async Task OldJwtRejectedAfterPasswordReset()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var user = await CreateUserAsync(userManager, NewPhone(), AppRoles.Lecture);
        var oldStamp = await userManager.GetSecurityStampAsync(user);

        using var factory = CreateHttpFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var oldToken = await LoginAsync(client, user, "Test@123");

        var controller = new UserController(userManager, context);
        var result = await controller.ResetLecturePassword(user.Id);
        Assert.IsType<OkObjectResult>(result);

        var refreshed = await userManager.FindByIdAsync(user.Id);
        Assert.NotNull(refreshed);
        Assert.NotEqual(oldStamp, await userManager.GetSecurityStampAsync(refreshed!));

        var response = await GetStudentsWithTokenAsync(client, oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var freshToken = await LoginAsync(client, user, $"{user.PhoneNumber}@123Aa");
        var freshResponse = await GetStudentsWithTokenAsync(client, freshToken);
        Assert.Equal(HttpStatusCode.OK, freshResponse.StatusCode);
    }

    [SkippableFact]
    public async Task ParentCannotReadAnotherParentsChild()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var parentA = await CreateUserAsync(userManager, NewPhone(), AppRoles.User);
        var parentB = await CreateUserAsync(userManager, NewPhone(), AppRoles.User);
        var childA = await AddStudentAsync(context, parentA.PhoneNumber!, "Parent A child");
        var childB = await AddStudentAsync(context, parentB.PhoneNumber!, "Parent B child");

        var controller = new StudentController(context, userManager);
        SetPrincipal(controller, parentA, AppRoles.User);

        var listResult = await controller.GetStudents(null);
        Assert.NotNull(listResult.Value);
        var visibleIds = listResult.Value!.Select(s => s.Id).ToArray();
        Assert.Equal(new[] { childA.Id }, visibleIds);

        var guessedResult = await controller.GetStudent(childB.Id);
        Assert.IsType<ForbidResult>(guessedResult.Result);
    }

    [SkippableFact]
    public async Task LectureCannotAccessAnotherLecturesClass()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var lectureA = await CreateUserAsync(userManager, NewPhone(), AppRoles.Lecture);
        var lectureB = await CreateUserAsync(userManager, NewPhone(), AppRoles.Lecture);
        var classA = new Class { ClassCode = $"A_{Guid.NewGuid():N}"[..20], ClassName = $"Lecture A class {Guid.NewGuid():N}" };
        var classB = new Class { ClassCode = $"B_{Guid.NewGuid():N}"[..20], ClassName = $"Lecture B class {Guid.NewGuid():N}" };
        context.Classes.AddRange(classA, classB);
        await context.SaveChangesAsync();
        var studentA = await AddStudentAsync(context, NewPhone(), "Lecture A student", classA.Id);
        var studentB = await AddStudentAsync(context, NewPhone(), "Lecture B student", classB.Id);
        context.WorkSchedules.AddRange(
            new WorkSchedule { UserId = lectureA.Id, Date = DateTime.UtcNow.AddDays(1), Classes = new List<Class> { classA } },
            new WorkSchedule { UserId = lectureB.Id, Date = DateTime.UtcNow.AddDays(1), Classes = new List<Class> { classB } });
        await context.SaveChangesAsync();

        var controller = new StudentController(context, userManager);
        SetPrincipal(controller, lectureA, AppRoles.Lecture);
        var listResult = await controller.GetStudents(null);
        Assert.NotNull(listResult.Value);
        Assert.Equal(new[] { studentA.Id }, listResult.Value!.Select(s => s.Id).ToArray());
        var forbidden = await controller.GetStudent(studentB.Id);
        Assert.IsType<ForbidResult>(forbidden.Result);
    }

    [SkippableFact]
    public async Task ImportCanonicalDuplicate_Returns409AndRollsBack()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var phone = NewPhone();
        var controller = new StudentController(context, userManager);
        var duplicateRows = new List<ImportStudentDto>
        {
            new() { FullName = "Import duplicate", ParentName = "Parent", ParentPhoneNumber = phone, ClassName = "Import class" },
            new() { FullName = "IMPORT DUPLICATE", ParentName = "Parent", ParentPhoneNumber = $"+84 {phone[1..4]} {phone[4..7]} {phone[7..]}", ClassName = "Import class" }
        };

        var result = await controller.ImportStudents(duplicateRows);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(0, await context.Students.CountAsync(s => s.FullName.ToLower() == "import duplicate"));
    }

    [SkippableFact]
    public async Task RecoverCanonicalDuplicate_Returns409AndRollsBack()
    {
        _fixture.EnsureDockerOrSkip();
        using var scope = CreateScope();
        var userManager = GetUserManager(scope);
        var context = GetContext(scope);
        var phone = NewPhone();
        var active = await AddStudentAsync(context, phone, "Recover duplicate");
        var deleted = await AddStudentAsync(context, phone, "Recover duplicate", isDeleted: true);

        var controller = new StudentController(context, userManager);
        var result = await controller.RecoverStudents(new List<int> { deleted.Id });

        Assert.IsType<ConflictObjectResult>(result);
        await using var verifyContext = await _fixture.CreateContextAsync();
        var persistedDeleted = await verifyContext.Students.IgnoreQueryFilters().SingleAsync(s => s.Id == deleted.Id);
        Assert.True(persistedDeleted.IsDeleted);
        Assert.NotEqual(active.Id, persistedDeleted.Id);
    }

    private IServiceScope CreateScope()
        => _fixture.ServiceProvider!.CreateScope();

    private JwtTestApplicationFactory CreateHttpFactory()
    {
        _fixture.EnsureDockerOrSkip();
        return new JwtTestApplicationFactory(_fixture.ConnectionString!);
    }

    private static async Task<string> LoginAsync(HttpClient client, AppUser user, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = user.UserName,
            password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("token", out var tokenProperty));
        var token = tokenProperty.GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<HttpResponseMessage> GetStudentsWithTokenAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/student");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static AppDbContext GetContext(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private static UserManager<AppUser> GetUserManager(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> userManager, string phone, params string[] roles)
    {
        var user = new AppUser
        {
            UserName = phone,
            Email = $"{phone}@example.com",
            PhoneNumber = phone,
            FullName = $"Test user {phone}"
        };
        var createResult = await userManager.CreateAsync(user, "Test@123");
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));
        foreach (var role in roles)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }
        return user;
    }

    private static async Task<Student> AddStudentAsync(
        AppDbContext context,
        string phone,
        string name,
        int? classId = null,
        bool isDeleted = false)
    {
        var student = new Student
        {
            FullName = name,
            ParentName = "Parent",
            ParentPhoneNumber = phone,
            ClassId = classId,
            TotalPaidSessions = 10,
            TotalRemainingSessions = 10,
            IsDeleted = isDeleted
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();
        return student;
    }

    private static void SetPrincipal(ControllerBase controller, AppUser user, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "integration-test"))
            }
        };
    }

    private static string NewPhone()
        => $"09{Interlocked.Increment(ref _phoneSequence):D8}";

    private sealed class JwtTestApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public JwtTestApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString
                });
            });
            builder.ConfigureTestServices(services =>
            {
                // Replace both application DbContext registrations so the test
                // server cannot accidentally connect to a developer machine DB.
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextFactory<AppDbContext>>();
                services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString), ServiceLifetime.Scoped, ServiceLifetime.Singleton);
            });
        }
    }
}

[CollectionDefinition("RoleOwnership PostgreSQL integration", DisableParallelization = true)]
public sealed class RoleOwnershipPostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
