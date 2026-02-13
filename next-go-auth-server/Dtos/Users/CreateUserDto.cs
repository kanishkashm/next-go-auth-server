using System.ComponentModel.DataAnnotations;

namespace next_go_auth_server.Dtos.Users
{
    public record CreateUserDto(
        [property: Required, EmailAddress] string Email,
        [property: Required]
        [property: MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [property: RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "Password must include uppercase and lowercase letters and at least one number.")]
        string Password,
        [property: Required] string FirstName,
        [property: Required] string LastName,
        [property: Required] string UserRole,
        string? RequestedOrgName
    );
}
