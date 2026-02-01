using Microsoft.AspNetCore.Identity;

namespace next_go_api.Seeders
{
    public static class IdentityRoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = { "SuperAdmin", "DefaultUser" , "OrganizationUser" , "OrganizationAdmin" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }

}
