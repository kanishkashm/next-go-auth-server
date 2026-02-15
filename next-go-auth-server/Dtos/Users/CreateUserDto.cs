using System.ComponentModel.DataAnnotations;

namespace next_go_auth_server.Dtos.Users
{
    public record CreateUserDto(
        [Required, EmailAddress] string Email,
        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must include uppercase and lowercase letters and at least one number.")]
        string Password,
        [Required] string FirstName,
        [Required] string LastName,
        [Required] string UserRole,
        string? RequestedOrgName
    );
}
