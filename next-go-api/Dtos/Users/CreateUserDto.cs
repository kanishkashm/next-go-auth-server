namespace next_go_api.Dtos.Users
{
    public record CreateUserDto(
       string Email,
       string Password,
       string FirstName,
       string LastName
   );
}
