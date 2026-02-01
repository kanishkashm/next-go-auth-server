using Microsoft.AspNetCore.Identity;
using next_go_api.Models.Enums;

namespace next_go_auth_server.Database
{

    public class User :IdentityUser
    {
        public string? Initials { get; set; }
        public string? FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; } = string.Empty;
        public UserStatus? Status { get; set; }

        // NEW: For OrgAdmin pending approval
        public string? RequestedOrgName { get; set; }

        // NEW: Organization relationship
        public Guid? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        // NEW: For Normal Users quota tracking
        public NormalUserQuota? Quota { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
