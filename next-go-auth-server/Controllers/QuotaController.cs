using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using System.Security.Claims;

namespace next_go_auth_server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<QuotaController> _logger;

    public QuotaController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<QuotaController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("check")]
    public async Task<IActionResult> CheckQuota()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Organization)
                    .ThenInclude(o => o!.SubscriptionPlan)
                .Include(u => u.Quota)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "DefaultUser";

            // Normal User (DefaultUser)
            if (role == "DefaultUser")
            {
                var quota = user.Quota;
                if (quota == null)
                {
                    return BadRequest(new { error = "Quota not found for user" });
                }

                return Ok(new
                {
                    allowed = quota.CvUploadsUsed < quota.CvUploadsLimit,
                    used = quota.CvUploadsUsed,
                    limit = quota.CvUploadsLimit,
                    reason = quota.CvUploadsUsed >= quota.CvUploadsLimit
                        ? "Free quota exceeded (2 uploads)"
                        : null
                });
            }

            // Organization Users (OrganizationAdmin or OrganizationUser)
            if (role == "OrganizationAdmin" || role == "OrganizationUser")
            {
                if (user.Organization == null)
                {
                    return BadRequest(new { error = "Organization not found for user" });
                }

                var org = user.Organization;
                return Ok(new
                {
                    allowed = org.CvUploadsThisMonth < org.SubscriptionPlan.MaxCVUploads,
                    used = org.CvUploadsThisMonth,
                    limit = org.SubscriptionPlan.MaxCVUploads,
                    organizationName = org.Name,
                    planName = org.SubscriptionPlan.DisplayName,
                    reason = org.CvUploadsThisMonth >= org.SubscriptionPlan.MaxCVUploads
                        ? "Organization quota exceeded"
                        : null
                });
            }

            // SuperAdmin has unlimited
            return Ok(new
            {
                allowed = true,
                used = 0,
                limit = -1,
                reason = (string?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking quota");
            return StatusCode(500, new { error = "Failed to check quota" });
        }
    }

    [HttpPost("increment")]
    public async Task<IActionResult> IncrementQuota()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Organization)
                .Include(u => u.Quota)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "DefaultUser";

            // Increment for Normal User
            if (role == "DefaultUser")
            {
                var quota = user.Quota;
                if (quota != null)
                {
                    quota.CvUploadsUsed++;
                    quota.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Quota incremented for user {Email}. Used: {Used}/{Limit}",
                        user.Email, quota.CvUploadsUsed, quota.CvUploadsLimit);
                }
            }
            // Increment for Organization
            else if (role == "OrganizationAdmin" || role == "OrganizationUser")
            {
                if (user.Organization != null)
                {
                    user.Organization.CvUploadsThisMonth++;
                    user.Organization.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Quota incremented for organization {OrgName}. Used: {Used}/{Limit}",
                        user.Organization.Name,
                        user.Organization.CvUploadsThisMonth,
                        user.Organization.SubscriptionPlan.MaxCVUploads);
                }
            }

            return Ok(new { message = "Quota incremented successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing quota");
            return StatusCode(500, new { error = "Failed to increment quota" });
        }
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userManager.Users
                .Include(u => u.Organization)
                    .ThenInclude(o => o!.SubscriptionPlan)
                .Include(u => u.Quota)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { error = "User not found" });

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "DefaultUser";

            if (role == "DefaultUser")
            {
                var quota = user.Quota;
                return Ok(new
                {
                    userType = "Normal User",
                    used = quota?.CvUploadsUsed ?? 0,
                    limit = quota?.CvUploadsLimit ?? 2,
                    percentage = quota != null ? (quota.CvUploadsUsed * 100.0 / quota.CvUploadsLimit) : 0
                });
            }

            if (role == "OrganizationAdmin" || role == "OrganizationUser")
            {
                var org = user.Organization;
                if (org == null)
                    return BadRequest(new { error = "Organization not found" });

                return Ok(new
                {
                    userType = role,
                    organizationName = org.Name,
                    planName = org.SubscriptionPlan.DisplayName,
                    used = org.CvUploadsThisMonth,
                    limit = org.SubscriptionPlan.MaxCVUploads,
                    percentage = (org.CvUploadsThisMonth * 100.0 / org.SubscriptionPlan.MaxCVUploads),
                    resetDate = org.CvUploadsResetAt
                });
            }

            return Ok(new
            {
                userType = "SuperAdmin",
                used = 0,
                limit = -1,
                percentage = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage");
            return StatusCode(500, new { error = "Failed to get usage statistics" });
        }
    }
}
