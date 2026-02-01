namespace next_go_auth_server.Database;

public class NormalUserQuota
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public int CvUploadsUsed { get; set; }
    public int CvUploadsLimit { get; set; } = 2; // Default 2 for normal users

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
