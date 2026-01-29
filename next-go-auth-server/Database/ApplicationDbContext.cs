using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using next_go_api.Models.Enums;

namespace next_go_auth_server.Database
{
    public class ApplicationDbContext :    IdentityDbContext<User, IdentityRole, string>

    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            builder.Entity<User>().Property(u => u.Initials).HasMaxLength(5);

            builder.HasDefaultSchema("identity");

            builder.Entity<User>()
                    .Property(u => u.Status)
                    .HasDefaultValue(UserStatus.Pending);
        }

    }
}
