using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using next_go_api.Models.Enums;

namespace next_go_auth_server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<AdminController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    FullName = $"{u.FirstName} {u.LastName}",
                    u.Status,
                    u.OrganizationId,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }

    [HttpGet("pending-org-admins")]
    public async Task<IActionResult> GetPendingOrgAdmins()
    {
        try
        {
            var pending = await _userManager.Users
                .Where(u => u.Status == UserStatus.Pending)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.RequestedOrgName,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending org admins");
            return StatusCode(500, new { error = "Failed to retrieve pending organizations" });
        }
    }

    [HttpPost("approve-org-admin")]
    public async Task<IActionResult> ApproveOrgAdmin([FromBody] ApproveOrgAdminRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new { error = "User not found" });

            if (user.Status != UserStatus.Pending)
                return BadRequest(new { error = "User is not in pending status" });

            // Get starter plan (default for new organizations)
            var starterPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.Name == "starter");

            if (starterPlan == null)
                return BadRequest(new { error = "No subscription plan available" });

            // Create organization
            var slug = request.OrganizationName.ToLower()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "");

            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = request.OrganizationName,
                Slug = slug,
                OwnerId = user.Id,
                SubscriptionPlanId = starterPlan.Id,
                CvUploadsThisMonth = 0,
                CvUploadsResetAt = DateTime.UtcNow.AddMonths(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Organizations.AddAsync(org);

            // Update user status and link to organization
            user.Status = UserStatus.Active;
            user.OrganizationId = org.Id;
            await _userManager.UpdateAsync(user);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgName} approved and created for user {Email}",
                request.OrganizationName, user.Email);

            return Ok(new
            {
                message = "Organization approved and created successfully",
                organizationId = org.Id,
                organizationName = org.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving org admin");
            return StatusCode(500, new { error = "Failed to approve organization" });
        }
    }

    [HttpPost("reject-org-admin")]
    public async Task<IActionResult> RejectOrgAdmin([FromBody] RejectOrgAdminRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return NotFound(new { error = "User not found" });

            if (user.Status != UserStatus.Pending)
                return BadRequest(new { error = "User is not in pending status" });

            user.Status = UserStatus.Inactive;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Organization registration rejected for user {Email}. Reason: {Reason}",
                user.Email, request.Reason);

            return Ok(new
            {
                message = "Organization registration rejected",
                reason = request.Reason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting org admin");
            return StatusCode(500, new { error = "Failed to reject organization" });
        }
    }
}

// DTOs
public class ApproveOrgAdminRequest
{
    public string UserId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
}

public class RejectOrgAdminRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
