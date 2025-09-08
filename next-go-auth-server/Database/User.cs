using Microsoft.AspNetCore.Identity;

namespace next_go_auth_server.Database
{

    public class User :IdentityUser 
    {
        public string? Initials { get; set; }
        public string? FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; } = string.Empty;

    }
}
