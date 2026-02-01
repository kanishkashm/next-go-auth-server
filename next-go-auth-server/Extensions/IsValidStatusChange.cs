using next_go_api.Models.Enums;

namespace next_go_api.Extensions
{
    public static class IsValidStatusChange
    {
        public  static bool IsValidUserStatusChange(UserStatus? current, UserStatus next)
        {
            return current switch
            {
                UserStatus.Pending => next is UserStatus.Active or UserStatus.Inactive,
                UserStatus.Active => next is UserStatus.Inactive,
                UserStatus.Inactive => false,
                null => next is UserStatus.Active or UserStatus.Inactive,
                _ => false
            };
        }

    }
}
