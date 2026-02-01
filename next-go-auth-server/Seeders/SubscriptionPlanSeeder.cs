using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;

namespace next_go_auth_server.Seeders;

public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if already seeded
        if (await context.SubscriptionPlans.AnyAsync())
        {
            Console.WriteLine("Subscription plans already exist. Skipping seed.");
            return;
        }

        Console.WriteLine("Seeding subscription plans...");

        var plans = new[]
        {
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = "starter",
                DisplayName = "Starter",
                MaxUsers = 5,
                MaxCVUploads = 50,
                FeaturesJson = "[\"Up to 5 users\",\"50 CV analyses per month\",\"Email support\",\"Basic reporting\",\"Organization dashboard\"]",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = "professional",
                DisplayName = "Professional",
                MaxUsers = 20,
                MaxCVUploads = 200,
                FeaturesJson = "[\"Up to 20 users\",\"200 CV analyses per month\",\"Priority support\",\"Advanced analytics\",\"Custom branding\",\"API access\"]",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = "enterprise",
                DisplayName = "Enterprise",
                MaxUsers = 100,
                MaxCVUploads = 1000,
                FeaturesJson = "[\"Up to 100 users\",\"1000 CV analyses per month\",\"Dedicated support\",\"Advanced analytics\",\"Custom branding\",\"API access\",\"SSO integration\",\"Custom integrations\"]",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await context.SubscriptionPlans.AddRangeAsync(plans);
        await context.SaveChangesAsync();

        Console.WriteLine($"Successfully seeded {plans.Length} subscription plans.");
    }
}
