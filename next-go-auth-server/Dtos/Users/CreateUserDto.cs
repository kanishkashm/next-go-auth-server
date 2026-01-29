namespace next_go_auth_server.Dtos.Users
{
    public record CreateUserDto(
       string Email,
       string Password,
       string FirstName,
       string LastName,
       string UserRole
   );
}
