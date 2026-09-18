using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ManagementSystem.Api.Controllers;
using ManagementSystem.Api.Models;

namespace ManagementSystem.Api.Tests;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task ChangePassword_WhenSuccessful_Returns200()
    {
        var user = NewUser();
        var userManager = new StubUserManager(user)
        {
            ChangePasswordResult = IdentityResult.Success,
            SecurityStampResult = IdentityResult.Failed(new IdentityError { Description = "stamp failure" })
        };
        var controller = CreateController(userManager, user);

        var result = await controller.ChangePassword(new ChangePasswordDto
        {
            OldPassword = "old-password",
            NewPassword = "new-password"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.True(userManager.ChangePasswordCalled);
        Assert.False(userManager.SecurityStampCalled);
    }

    [Fact]
    public async Task ChangePassword_WhenIdentityRejectsPassword_Returns400()
    {
        var user = NewUser();
        var userManager = new StubUserManager(user)
        {
            ChangePasswordResult = IdentityResult.Failed(new IdentityError { Description = "current password is invalid" }),
            SecurityStampResult = IdentityResult.Success
        };
        var controller = CreateController(userManager, user);

        var result = await controller.ChangePassword(new ChangePasswordDto
        {
            OldPassword = "old-password",
            NewPassword = "new-password"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.True(userManager.ChangePasswordCalled);
        Assert.False(userManager.SecurityStampCalled);
    }

    private static AuthController CreateController(UserManager<AppUser> userManager, AppUser user)
    {
        var controller = new AuthController(userManager, new ConfigurationBuilder().Build());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id)
                }, "test"))
            }
        };
        return controller;
    }

    private static AppUser NewUser() => new()
    {
        Id = "auth-test-user",
        UserName = "0900000000",
        SecurityStamp = "stamp-before"
    };

    private sealed class StubUserManager : UserManager<AppUser>
    {
        private readonly AppUser _user;

        public StubUserManager(AppUser user)
            : base(
                new StubUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<AppUser>(),
                Array.Empty<IUserValidator<AppUser>>(),
                Array.Empty<IPasswordValidator<AppUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<AppUser>>.Instance)
        {
            _user = user;
        }

        public IdentityResult ChangePasswordResult { get; init; } = IdentityResult.Success;
        public IdentityResult SecurityStampResult { get; init; } = IdentityResult.Success;
        public bool ChangePasswordCalled { get; private set; }
        public bool SecurityStampCalled { get; private set; }

        public override Task<AppUser?> FindByIdAsync(string userId)
            => Task.FromResult<AppUser?>(userId == _user.Id ? _user : null);

        public override Task<IdentityResult> ChangePasswordAsync(
            AppUser user,
            string currentPassword,
            string newPassword)
        {
            ChangePasswordCalled = true;
            return Task.FromResult(ChangePasswordResult);
        }

        public override Task<IdentityResult> UpdateSecurityStampAsync(AppUser user)
        {
            SecurityStampCalled = true;
            return Task.FromResult(SecurityStampResult);
        }
    }

    private sealed class StubUserStore : IUserStore<AppUser>
    {
        public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);

        public void Dispose() { }

        public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => Task.FromResult<AppUser?>(null);

        public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => Task.FromResult<AppUser?>(null);

        public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
            => Task.FromResult(IdentityResult.Success);
    }
}
