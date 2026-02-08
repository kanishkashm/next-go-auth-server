using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using next_go_api.Models.Enums;
using next_go_auth_server.Database;
using next_go_auth_server.Dtos.Users;
using next_go_auth_server.Services;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration config,
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // ---------------- REGISTER ----------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(CreateUserDto dto)
        {
            if (!new EmailAddressAttribute().IsValid(dto.Email))
                return BadRequest("Invalid email");

            // Determine status based on role
            var status = dto.UserRole == "OrganizationAdmin"
                ? UserStatus.Pending
                : UserStatus.Active;

            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Initials = $"{dto.FirstName?[0]}{dto.LastName?[0]}",
                Status = status,
                RequestedOrgName = dto.UserRole == "OrganizationAdmin" ? dto.RequestedOrgName : null,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, dto.UserRole);

            // Create quota for Normal Users (DefaultUser)
            if (dto.UserRole == "DefaultUser")
            {
                var context = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                await context.NormalUserQuotas.AddAsync(new NormalUserQuota
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CvUploadsUsed = 0,
                    CvUploadsLimit = 2,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // Notify SuperAdmins when an OrgAdmin registers
            if (dto.UserRole == "OrganizationAdmin")
            {
                try
                {
                    // Get all SuperAdmin users
                    var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
                    var applicantName = $"{dto.FirstName} {dto.LastName}";

                    foreach (var superAdmin in superAdmins)
                    {
                        if (!string.IsNullOrEmpty(superAdmin.Email))
                        {
                            try
                            {
                                await _emailService.SendNewOrgRegistrationNotificationAsync(
                                    superAdmin.Email,
                                    superAdmin.FirstName ?? "Admin",
                                    applicantName,
                                    dto.Email,
                                    dto.RequestedOrgName ?? "Unknown Organization");

                                _logger.LogInformation(
                                    "Sent new org registration notification to SuperAdmin {Email} for org {OrgName}",
                                    superAdmin.Email, dto.RequestedOrgName);
                            }
                            catch (Exception emailEx)
                            {
                                _logger.LogError(emailEx,
                                    "Failed to send notification to SuperAdmin {Email}", superAdmin.Email);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify SuperAdmins about new org registration");
                    // Don't fail registration if email notification fails
                }
            }

            return Ok(new
            {
                message = "User registered successfully",
                status = status.ToString(),
                email = user.Email
            });
        }

        // ---------------- LOGIN ----------------
        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginRequest dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { error = "Invalid credentials" });

            // Check if user is active
            if (user.Status != UserStatus.Active)
            {
                return Unauthorized(new
                {
                    error = "Account not active",
                    status = user.Status.ToString(),
                    message = user.Status == UserStatus.Pending
                        ? "Your account is pending approval. Please wait for administrator approval."
                        : "Your account has been deactivated. Please contact support."
                });
            }

            // Check if user's organization is active (if they belong to one)
            if (user.OrganizationId.HasValue)
            {
                var org = await _context.Organizations.FindAsync(user.OrganizationId.Value);
                if (org != null && !org.IsActive)
                {
                    return Unauthorized(new
                    {
                        error = "Organization deactivated",
                        status = "OrganizationInactive",
                        message = "Your organization has been deactivated. Please contact your administrator or support."
                    });
                }
            }

            var result = await _signInManager.CheckPasswordSignInAsync(
                user, dto.Password, lockoutOnFailure: true);

            if (!result.Succeeded)
                return Unauthorized(new { error = "Invalid credentials" });

            // Generate access token
            var accessToken = GenerateAccessToken(user, await _userManager.GetRolesAsync(user));

            // Generate and store refresh token
            var refreshToken = await GenerateRefreshTokenAsync(user.Id);

            var roles = await _userManager.GetRolesAsync(user);

            // Return token + user info (for frontend)
            return Ok(new
            {
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = $"{user.FirstName} {user.LastName}",
                    roles = roles.ToArray(),
                    createdAt = user.CreatedAt.ToString("o") // ISO 8601 format
                },
                accessToken = accessToken.Token,
                expiresIn = accessToken.ExpiresIn,
                tokenType = "Bearer",
                refreshToken = refreshToken,
                mustChangePassword = user.MustChangePassword // Force password change flag
            });
        }

        // ---------------- REFRESH TOKEN ----------------
        [HttpPost("refresh")]
        public async Task<ActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request?.RefreshToken))
                return BadRequest(new { error = "Refresh token is required" });

            // Find the refresh token in database
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedToken == null)
                return Unauthorized(new { error = "Invalid refresh token" });

            if (!storedToken.IsActive)
            {
                // Token is expired or revoked
                return Unauthorized(new { error = "Refresh token expired or revoked" });
            }

            var user = storedToken.User;
            if (user == null)
                return Unauthorized(new { error = "User not found" });

            // Check if user is still active
            if (user.Status != UserStatus.Active)
                return Unauthorized(new { error = "User account is not active" });

            // Check if user's organization is active (if they belong to one)
            if (user.OrganizationId.HasValue)
            {
                var org = await _context.Organizations.FindAsync(user.OrganizationId.Value);
                if (org != null && !org.IsActive)
                    return Unauthorized(new { error = "Organization has been deactivated" });
            }

            // Revoke the old refresh token
            storedToken.RevokedAt = DateTime.UtcNow;

            // Generate new tokens
            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = GenerateAccessToken(user, roles);
            var newRefreshToken = await GenerateRefreshTokenAsync(user.Id);

            // Mark old token as replaced
            storedToken.ReplacedByToken = newRefreshToken;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                accessToken = newAccessToken.Token,
                expiresIn = newAccessToken.ExpiresIn,
                tokenType = "Bearer",
                refreshToken = newRefreshToken
            });
        }

        // ---------------- LOGOUT (JWT) ----------------
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
        {
            // Revoke refresh token if provided
            if (!string.IsNullOrEmpty(request?.RefreshToken))
            {
                var storedToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

                if (storedToken != null && storedToken.IsActive)
                {
                    storedToken.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Logged out successfully" });
        }

        // ---------------- GET CURRENT USER ----------------
        [HttpGet("~/api/me")]
        public async Task<IActionResult> GetMe()
        {
            // Temporarily allow unauthenticated access for debugging
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                return Unauthorized(new { error = "No authorization header" });
            }

            // For now, decode the JWT manually to extract user ID
            var token = authHeader.Replace("Bearer ", "");
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var jwtToken = handler.ReadJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { error = "Invalid token - no user ID" });

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

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

            if (request.NewPassword.Length < 8)
                return BadRequest(new { error = "New password must be at least 8 characters long" });

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

            return (
                Token: new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresIn: (int)(expires - DateTime.UtcNow).TotalSeconds
            );
        }

        private async Task<string> GenerateRefreshTokenAsync(string userId)
        {
            // Generate a secure random token
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var refreshToken = Convert.ToBase64String(randomBytes);

            // Get refresh token expiry from config (default 7 days)
            var refreshExpireDays = Convert.ToInt32(_config["Jwt:RefreshExpireDays"] ?? "7");

            // Create and store the refresh token
            var tokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpireDays),
                CreatedAt = DateTime.UtcNow
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
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}
