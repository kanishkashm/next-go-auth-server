namespace next_go_auth_server.Database;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // starter, professional, enterprise
    public string DisplayName { get; set; } = string.Empty;

    // Limits
    public int MaxUsers { get; set; }
    public int MaxCVUploads { get; set; }

    // Features (stored as JSON string)
    public string FeaturesJson { get; set; } = "[]";

    // Pricing & Display
    public decimal? MonthlyPrice { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsPopular { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Relations
    public ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}
