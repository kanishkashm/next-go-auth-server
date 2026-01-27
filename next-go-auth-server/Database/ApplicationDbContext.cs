using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
        }

    }
}
