using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using next_go_api.Dtos.Users;
using next_go_auth_server.Database;
using next_go_auth_server.Dtos.Users;

namespace next_go_api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<User> _userManager;

        public AccountController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("info")]
        public async Task<ActionResult<InfoResponseDto>> Info()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            return Ok(new InfoResponseDto
            {
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsEmailConfirmed = user.EmailConfirmed
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangePasswordAsync(
                user, dto.OldPassword, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok();
        }
    }

}
