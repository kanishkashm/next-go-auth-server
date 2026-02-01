using next_go_api.Models.Enums;

namespace next_go_api.Dtos.Users
{
    public class ChangeUserStatusDto
    {
        public string Email { get; set; } = default!;
        public UserStatus Status { get; set; }
    }

}
