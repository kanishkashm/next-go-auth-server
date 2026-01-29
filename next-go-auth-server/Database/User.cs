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
    }
}
