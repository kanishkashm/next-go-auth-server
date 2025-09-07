using Microsoft.EntityFrameworkCore;

namespace next_go_api.Extensions
{
    public static class MigrationExtensions
    {
        public static void ApplyMigrations(this IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetService<Database.ApplicationDbContext>();
                context?.Database.Migrate();
            }
        }
    }
}       
