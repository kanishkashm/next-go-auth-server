namespace next_go_auth_server.Database;

public enum UpgradeRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public class UpgradeRequest
{
    public Guid Id { get; set; }

    // Organization making the request
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    // Current plan at time of request
    public Guid CurrentPlanId { get; set; }
    public SubscriptionPlan CurrentPlan { get; set; } = null!;

    // Requested plan
    public Guid RequestedPlanId { get; set; }
    public SubscriptionPlan RequestedPlan { get; set; } = null!;

    // User who made the request (OrgAdmin)
    public string RequestedById { get; set; } = string.Empty;
    public User RequestedBy { get; set; } = null!;

    // Request details
    public string Reason { get; set; } = string.Empty;
    public UpgradeRequestStatus Status { get; set; } = UpgradeRequestStatus.Pending;

    // Resolution details
    public string? ProcessedById { get; set; }
    public User? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
