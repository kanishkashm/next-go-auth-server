using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using next_go_api.Dtos.Users;
using next_go_api.Extensions;
using next_go_api.Models.Enums;
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

        [Authorize(Roles = AppRoles.SuperAdmin)]
        [HttpPost("change-status")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeUserStatusDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
                return NotFound($"User with email '{request.Email}' not found.");


            if (!IsValidStatusChange.IsValidUserStatusChange(user.Status, request.Status))
                return BadRequest("Invalid status transition.");

            user.Status = request.Status;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new
            {
                user.Email,
                OldStatus = user.Status,
                NewStatus = request.Status
            });
        }
    }

}
