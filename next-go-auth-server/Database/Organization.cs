namespace next_go_auth_server.Database;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    // Owner (OrgAdmin)
    public string OwnerId { get; set; } = string.Empty;
    public User Owner { get; set; } = null!;

    // Subscription
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    // Usage tracking
    public int CvUploadsThisMonth { get; set; }
    public DateTime CvUploadsResetAt { get; set; } = DateTime.UtcNow;

    // Members (OrgUsers)
    public ICollection<User> Members { get; set; } = new List<User>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
