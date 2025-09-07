using Microsoft.AspNetCore.Identity;

namespace next_go_api.Database
{

    public class User :IdentityUser 
    {
        public string? Initials { get; set; }

    }
}
