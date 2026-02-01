using Microsoft.AspNetCore.Identity;
using next_go_api.Models.Enums;
using next_go_auth_server.Database;

namespace next_go_api.Seeders
{
    public static class IdentitySeeder
    {
        public static async Task SeedAdminUserAsync<TUser>(IServiceProvider serviceProvider)
            where TUser : User, new()
        {
            using var scope = serviceProvider.CreateScope();

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1️⃣ Ensure roles exist
            if (!await roleManager.RoleExistsAsync(AppRoles.SuperAdmin))
                await roleManager.CreateAsync(new IdentityRole(AppRoles.SuperAdmin));

            if (!await roleManager.RoleExistsAsync(AppRoles.DefaultUser))
                await roleManager.CreateAsync(new IdentityRole(AppRoles.DefaultUser));

            // 2️⃣ Admin user details
            var adminEmail = "admin@nextgo.com";
            var adminPassword = "Admin@123!"; // 🔒 Change in production

            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new TUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    Status = UserStatus.Active
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);

                if (!createResult.Succeeded)
                    throw new Exception($"Admin user creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            // 3️⃣ Assign Admin role
            if (!await userManager.IsInRoleAsync(adminUser, AppRoles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(adminUser, AppRoles.SuperAdmin);
            }
        }
    }
    }
