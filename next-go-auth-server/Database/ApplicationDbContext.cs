using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using next_go_api.Models.Enums;

namespace next_go_auth_server.Database
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole, string>
    {
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<NormalUserQuota> NormalUserQuotas { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasDefaultSchema("identity");

            // User configuration
            builder.Entity<User>(entity =>
            {
                entity.Property(u => u.Initials).HasMaxLength(5);
                entity.Property(u => u.Status).HasDefaultValue(UserStatus.Pending);
                entity.Property(u => u.RequestedOrgName).HasMaxLength(200);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Organization configuration
            builder.Entity<Organization>(entity =>
            {
                entity.ToTable("Organizations", "identity");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Name).IsRequired().HasMaxLength(200);
                entity.Property(o => o.Slug).IsRequired().HasMaxLength(200);
                entity.HasIndex(o => o.Slug).IsUnique();

                // One organization has one owner (OrgAdmin)
                entity.HasOne(o => o.Owner)
                    .WithOne()
                    .HasForeignKey<Organization>(o => o.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                // One organization has many members (OrgUsers)
                entity.HasMany(o => o.Members)
                    .WithOne(u => u.Organization)
                    .HasForeignKey(u => u.OrganizationId)
                    .OnDelete(DeleteBehavior.SetNull);

                // One organization has one subscription plan
                entity.HasOne(o => o.SubscriptionPlan)
                    .WithMany(sp => sp.Organizations)
                    .HasForeignKey(o => o.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SubscriptionPlan configuration
            builder.Entity<SubscriptionPlan>(entity =>
            {
                entity.ToTable("SubscriptionPlans", "identity");
                entity.HasKey(sp => sp.Id);
                entity.Property(sp => sp.Name).IsRequired().HasMaxLength(50);
                entity.HasIndex(sp => sp.Name).IsUnique();
                entity.Property(sp => sp.DisplayName).IsRequired().HasMaxLength(100);
            });

            // NormalUserQuota configuration
            builder.Entity<NormalUserQuota>(entity =>
            {
                entity.ToTable("NormalUserQuotas", "identity");
                entity.HasKey(q => q.Id);
                entity.HasOne(q => q.User)
                    .WithOne(u => u.Quota)
                    .HasForeignKey<NormalUserQuota>(q => q.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
