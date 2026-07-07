using ManagementSystem.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace ManagementSystem.Api.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

            string[] roleNames = { "Admin", "Lecture", "User" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var adminEmail = "admin@example.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator"
                };
                var createPowerUser = await userManager.CreateAsync(newAdmin, "Admin@123");
                if (createPowerUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

            var lectureEmail = "lecture@example.com";
            var lectureUser = await userManager.FindByEmailAsync(lectureEmail);
            if (lectureUser == null)
            {
                var newLecture = new AppUser
                {
                    UserName = lectureEmail,
                    Email = lectureEmail,
                    FullName = "Jane Doe"
                };
                var createResult = await userManager.CreateAsync(newLecture, "Lecture@123");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newLecture, "Lecture");
                }
            }
        }
    }
}
