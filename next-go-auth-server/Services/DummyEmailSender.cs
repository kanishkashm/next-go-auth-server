using Microsoft.AspNetCore.Identity;
using next_go_auth_server.Database;

namespace next_go_api.Services
{
    public class DummyEmailSender : IEmailSender<User>
    {
        public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {
            Console.WriteLine($"CONFIRM EMAIL: {confirmationLink}");
            return Task.CompletedTask;
        }

        public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            Console.WriteLine($"RESET PASSWORD: {resetLink}");
            return Task.CompletedTask;
        }

        public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            return Task.CompletedTask;
        }
    }

}
