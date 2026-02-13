using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using next_go_api.Models.Enums;
using next_go_auth_server.Database;
using next_go_auth_server.Dtos.Users;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace next_go_api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _config;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
        }

        // ---------------- REGISTER ----------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(CreateUserDto dto)
        {
            if (!new EmailAddressAttribute().IsValid(dto.Email))
                return BadRequest("Invalid email");

            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, dto.UserRole);

            return Ok();
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized();

            var result = await _signInManager.CheckPasswordSignInAsync(
                user, dto.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
                return Unauthorized();

            var token = await GenerateJwtAsync(user);
            return Ok(token);
        }

        // ---------------- LOGOUT (JWT) ----------------
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // JWT logout = client deletes token
            return Ok();
        }

        // ---------------- TOKEN GENERATION ----------------
        private async Task<AccessTokenResponse> GenerateJwtAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        fullName = $"{user.FirstName} {user.LastName}",
                        roles = roles.ToArray(),
                        createdAt = user.CreatedAt.ToString("o")
                    }
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { error = $"Token validation failed: {ex.Message}" });
            }
        }

        // ---------------- CHANGE PASSWORD ----------------
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(new { error = "Current password and new password are required" });

            // Get current user from token
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
                return Unauthorized(new { error = "No authorization header" });

            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var jwtToken = handler.ReadJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { error = "Invalid token" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                // Verify current password
                var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, request.CurrentPassword, false);
                if (!passwordCheck.Succeeded)
                    return BadRequest(new { error = "Current password is incorrect" });

                // Change password
                var changeResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!changeResult.Succeeded)
                {
                    var errors = string.Join(", ", changeResult.Errors.Select(e => e.Description));
                    return BadRequest(new { error = $"Failed to change password: {errors}" });
                }

                // Clear the MustChangePassword flag
                user.MustChangePassword = false;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("User {Email} changed their password", user.Email);

                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, new { error = "Failed to change password" });
            }
        }

        // ---------------- UPDATE PROFILE ----------------
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName))
                return BadRequest(new { error = "First name and last name are required" });

            // Get current user from token
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
                return Unauthorized(new { error = "No authorization header" });

            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var jwtToken = handler.ReadJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { error = "Invalid token" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                // Update user profile
                user.FirstName = request.FirstName;
                user.LastName = request.LastName;
                user.Initials = $"{request.FirstName?[0]}{request.LastName?[0]}";

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return BadRequest(new { error = $"Failed to update profile: {errors}" });
                }

                _logger.LogInformation("User {Email} updated their profile", user.Email);

                return Ok(new
                {
                    message = "Profile updated successfully",
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        fullName = $"{user.FirstName} {user.LastName}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                return StatusCode(500, new { error = "Failed to update profile" });
            }
        }

        // ---------------- TOKEN GENERATION ----------------
        private (string Token, int ExpiresIn) GenerateAccessToken(User user, IList<string> roles)
        {
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(
                Convert.ToDouble(_config["Jwt:ExpireMinutes"] ?? "60"));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new AccessTokenResponse
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds,
                RefreshToken = ""
            };

            _context.RefreshTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            return refreshToken;
        }
    }

    // Request DTOs
    public class RefreshTokenRequest
    {
        public string? RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        public string? RefreshToken { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "New password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "New password must include uppercase and lowercase letters and at least one number.")]
        public string NewPassword { get; set; } = string.Empty;
    }

}
