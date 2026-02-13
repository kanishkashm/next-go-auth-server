using System.ComponentModel.DataAnnotations;

namespace next_go_api.Dtos.Users
{
    public class ChangePasswordDto
    {
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "New password must include uppercase and lowercase letters and at least one number.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
