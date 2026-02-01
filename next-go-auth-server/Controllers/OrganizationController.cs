using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using next_go_api.Models.Enums;
using System.IdentityModel.Tokens.Jwt;

namespace next_go_auth_server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<OrganizationController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader))
            return null;

        var token = authHeader.Replace("Bearer ", "");
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var jwtToken = handler.ReadJwtToken(token);
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userId))
                return null;

            return await _userManager.FindByIdAsync(userId);
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("current")]
    [Authorize(Roles = "OrganizationAdmin,OrganizationUser")]
    public async Task<IActionResult> GetCurrentOrganization()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var org = await _context.Organizations
                .Include(o => o.SubscriptionPlan)
                .Include(o => o.Owner)
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);

            if (org == null)
                return NotFound(new { error = "Organization not found" });

            return Ok(new
            {
                id = org.Id,
                name = org.Name,
                slug = org.Slug,
                subscriptionPlan = new
                {
                    id = org.SubscriptionPlan.Id,
                    name = org.SubscriptionPlan.Name,
                    displayName = org.SubscriptionPlan.DisplayName,
                    maxUsers = org.SubscriptionPlan.MaxUsers,
                    maxCVUploads = org.SubscriptionPlan.MaxCVUploads
                },
                cvUploadsThisMonth = org.CvUploadsThisMonth,
                cvUploadsResetAt = org.CvUploadsResetAt.ToString("o"),
                owner = new
                {
                    id = org.Owner.Id,
                    email = org.Owner.Email,
                    fullName = $"{org.Owner.FirstName} {org.Owner.LastName}"
                },
                createdAt = org.CreatedAt.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current organization");
            return StatusCode(500, new { error = "Failed to retrieve organization" });
        }
    }

    [HttpGet("members")]
    [Authorize(Roles = "OrganizationAdmin,OrganizationUser")]
    public async Task<IActionResult> GetOrganizationMembers()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var members = await _userManager.Users
                .Where(u => u.OrganizationId == user.OrganizationId.Value)
                .ToListAsync();

            var membersList = new List<object>();
            foreach (var member in members)
            {
                var roles = await _userManager.GetRolesAsync(member);
                membersList.Add(new
                {
                    id = member.Id,
                    email = member.Email,
                    fullName = $"{member.FirstName} {member.LastName}",
                    roles = roles.ToArray(),
                    status = member.Status.ToString(),
                    createdAt = member.CreatedAt.ToString("o")
                });
            }

            return Ok(membersList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization members");
            return StatusCode(500, new { error = "Failed to retrieve members" });
        }
    }

    [HttpGet("stats")]
    [Authorize(Roles = "OrganizationAdmin,OrganizationUser")]
    public async Task<IActionResult> GetOrganizationStats()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var org = await _context.Organizations
                .Include(o => o.SubscriptionPlan)
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);

            if (org == null)
                return NotFound(new { error = "Organization not found" });

            var memberCount = await _userManager.Users
                .CountAsync(u => u.OrganizationId == user.OrganizationId.Value);

            // Get recent uploads (if CVUpload table exists)
            // For now, return basic stats
            return Ok(new
            {
                memberCount,
                uploadsThisMonth = org.CvUploadsThisMonth,
                uploadsLimit = org.SubscriptionPlan.MaxCVUploads,
                maxUsers = org.SubscriptionPlan.MaxUsers,
                resetDate = org.CvUploadsResetAt.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organization stats");
            return StatusCode(500, new { error = "Failed to retrieve stats" });
        }
    }

    [HttpPost("invite")]
    [Authorize(Roles = "OrganizationAdmin")]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserRequest request)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null || !currentUser.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var org = await _context.Organizations
                .Include(o => o.SubscriptionPlan)
                .FirstOrDefaultAsync(o => o.Id == currentUser.OrganizationId.Value);

            if (org == null)
                return NotFound(new { error = "Organization not found" });

            // Check member limit
            var currentMemberCount = await _userManager.Users
                .CountAsync(u => u.OrganizationId == currentUser.OrganizationId.Value);

            if (currentMemberCount >= org.SubscriptionPlan.MaxUsers)
                return BadRequest(new { error = $"Organization has reached maximum user limit ({org.SubscriptionPlan.MaxUsers})" });

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                return BadRequest(new { error = "User with this email already exists" });

            // Generate temporary password
            var tempPassword = GenerateTemporaryPassword();

            // Create new user
            var newUser = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Initials = $"{request.FirstName?[0]}{request.LastName?[0]}",
                Status = UserStatus.Active,
                OrganizationId = currentUser.OrganizationId,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(newUser, tempPassword);
            if (!result.Succeeded)
                return BadRequest(new { error = "Failed to create user", details = result.Errors });

            await _userManager.AddToRoleAsync(newUser, "OrganizationUser");

            _logger.LogInformation("User {Email} invited to organization {OrgId} by {AdminEmail}",
                request.Email, currentUser.OrganizationId, currentUser.Email);

            // Send invitation email
            try
            {
                await SendInvitationEmailAsync(newUser.Email, org.Name, tempPassword, newUser.FirstName);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send invitation email to {Email}", newUser.Email);
                // Continue even if email fails
            }

            return Ok(new
            {
                message = "User invited successfully. An email has been sent with login credentials.",
                user = new
                {
                    id = newUser.Id,
                    email = newUser.Email,
                    fullName = $"{newUser.FirstName} {newUser.LastName}"
                },
                temporaryPassword = tempPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting user");
            return StatusCode(500, new { error = "Failed to invite user" });
        }
    }

    [HttpDelete("members/{userId}")]
    [Authorize(Roles = "OrganizationAdmin")]
    public async Task<IActionResult> RemoveMember(string userId)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null || !currentUser.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var memberToRemove = await _userManager.FindByIdAsync(userId);
            if (memberToRemove == null)
                return NotFound(new { error = "User not found" });

            // Check if user is in the same organization
            if (memberToRemove.OrganizationId != currentUser.OrganizationId)
                return BadRequest(new { error = "User is not in your organization" });

            // Check if trying to remove the owner
            var org = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == currentUser.OrganizationId.Value);

            if (org != null && memberToRemove.Id == org.OwnerId)
                return BadRequest(new { error = "Cannot remove organization owner" });

            // Set user status to Inactive
            memberToRemove.Status = UserStatus.Inactive;
            await _userManager.UpdateAsync(memberToRemove);

            _logger.LogInformation("User {Email} removed from organization {OrgId} by {AdminEmail}",
                memberToRemove.Email, currentUser.OrganizationId, currentUser.Email);

            return Ok(new { message = "Member removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member");
            return StatusCode(500, new { error = "Failed to remove member" });
        }
    }

    [HttpGet("quota/check")]
    public async Task<IActionResult> CheckUploadQuota()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);

            // Check if DefaultUser (Normal User)
            if (roles.Contains("DefaultUser"))
            {
                var quota = await _context.NormalUserQuotas
                    .FirstOrDefaultAsync(q => q.UserId == user.Id);

                if (quota == null)
                {
                    // Create quota if it doesn't exist
                    quota = new NormalUserQuota
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        CvUploadsUsed = 0,
                        CvUploadsLimit = 2,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.NormalUserQuotas.AddAsync(quota);
                    await _context.SaveChangesAsync();
                }

                var allowed = quota.CvUploadsUsed < quota.CvUploadsLimit;
                return Ok(new
                {
                    allowed,
                    used = quota.CvUploadsUsed,
                    limit = quota.CvUploadsLimit,
                    userType = "normal",
                    reason = allowed ? null : "You have reached your upload limit of 2 CVs"
                });
            }

            // Check if Organization User
            if (user.OrganizationId.HasValue)
            {
                var org = await _context.Organizations
                    .Include(o => o.SubscriptionPlan)
                    .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);

                if (org == null)
                    return NotFound(new { error = "Organization not found" });

                // Reset monthly quota if needed
                if (DateTime.UtcNow >= org.CvUploadsResetAt)
                {
                    org.CvUploadsThisMonth = 0;
                    org.CvUploadsResetAt = DateTime.UtcNow.AddMonths(1);
                    await _context.SaveChangesAsync();
                }

                var allowed = org.CvUploadsThisMonth < org.SubscriptionPlan.MaxCVUploads;
                return Ok(new
                {
                    allowed,
                    used = org.CvUploadsThisMonth,
                    limit = org.SubscriptionPlan.MaxCVUploads,
                    resetDate = org.CvUploadsResetAt.ToString("o"),
                    userType = "organization",
                    reason = allowed ? null : $"Your organization has reached the monthly upload limit of {org.SubscriptionPlan.MaxCVUploads} CVs"
                });
            }

            return Unauthorized(new { error = "User type not recognized" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking upload quota");
            return StatusCode(500, new { error = "Failed to check quota" });
        }
    }

    [HttpPost("quota/increment")]
    public async Task<IActionResult> IncrementUploadQuota()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);

            // Increment for DefaultUser (Normal User)
            if (roles.Contains("DefaultUser"))
            {
                var quota = await _context.NormalUserQuotas
                    .FirstOrDefaultAsync(q => q.UserId == user.Id);

                if (quota == null)
                    return BadRequest(new { error = "Quota record not found" });

                quota.CvUploadsUsed++;
                quota.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Quota incremented successfully", used = quota.CvUploadsUsed });
            }

            // Increment for Organization User
            if (user.OrganizationId.HasValue)
            {
                var org = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value);

                if (org == null)
                    return NotFound(new { error = "Organization not found" });

                org.CvUploadsThisMonth++;
                org.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Quota incremented successfully", used = org.CvUploadsThisMonth });
            }

            return Unauthorized(new { error = "User type not recognized" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing upload quota");
            return StatusCode(500, new { error = "Failed to increment quota" });
        }
    }

    private string GenerateTemporaryPassword()
    {
        // Include special characters to meet password requirements
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";

        var random = new Random();

        // Ensure at least one of each required type
        var password = new char[12];
        password[0] = uppercase[random.Next(uppercase.Length)];
        password[1] = lowercase[random.Next(lowercase.Length)];
        password[2] = digits[random.Next(digits.Length)];
        password[3] = special[random.Next(special.Length)];

        // Fill the rest randomly
        const string allChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";
        for (int i = 4; i < 12; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }

        // Shuffle the password
        return new string(password.OrderBy(x => random.Next()).ToArray());
    }

    private async Task SendInvitationEmailAsync(string email, string organizationName, string tempPassword, string firstName)
    {
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var smtpHost = configuration["Email:SmtpHost"];
        var smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
        var smtpUsername = configuration["Email:SmtpUsername"];
        var smtpPassword = configuration["Email:SmtpPassword"];
        var fromEmail = configuration["Email:FromEmail"];
        var fromName = configuration["Email:FromName"] ?? "CKHRC Immigration Platform";

        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogWarning("Email configuration is missing. Skipping email notification.");
            return;
        }

        var loginUrl = configuration["App:FrontendUrl"] ?? "http://localhost:3000";

        var subject = $"You've been invited to join {organizationName}";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .credentials {{ background: white; padding: 20px; border-left: 4px solid #667eea; margin: 20px 0; }}
        .button {{ display: inline-block; padding: 12px 30px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Welcome to CKHRC Immigration Platform</h1>
        </div>
        <div class='content'>
            <p>Hi {firstName},</p>

            <p>You've been invited to join <strong>{organizationName}</strong> on the CKHRC Immigration Platform.</p>

            <div class='credentials'>
                <h3>Your Login Credentials:</h3>
                <p><strong>Email:</strong> {email}</p>
                <p><strong>Temporary Password:</strong> <code style='background: #f0f0f0; padding: 5px 10px; border-radius: 3px; font-size: 16px;'>{tempPassword}</code></p>
            </div>

            <div class='warning'>
                <p><strong>⚠️ Important Security Notice:</strong></p>
                <p>This is a temporary password. For security reasons, you will be required to change your password on your first login.</p>
            </div>

            <p>Click the button below to log in and get started:</p>

            <a href='{loginUrl}' class='button'>Log In Now</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><a href='{loginUrl}'>{loginUrl}</a></p>

            <p>If you have any questions or need assistance, please contact your organization administrator.</p>

            <p>Best regards,<br>CKHRC Immigration Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply directly to this message.</p>
        </div>
    </div>
</body>
</html>";

        using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort);
        client.Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword);
        client.EnableSsl = true;

        var message = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(email);

        await client.SendMailAsync(message);
        _logger.LogInformation("Invitation email sent to {Email}", email);
    }
}

// DTOs
public class InviteUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
