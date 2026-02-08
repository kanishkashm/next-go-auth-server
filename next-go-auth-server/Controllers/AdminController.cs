using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using next_go_auth_server.Services;
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
    private readonly IEmailService _emailService;

    public AdminController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<AdminController> logger,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _emailService = emailService;
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

            // Send approval email
            try
            {
                await _emailService.SendOrgAdminApprovalEmailAsync(
                    user.Email!,
                    user.FirstName ?? "User",
                    request.OrganizationName);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send approval email to {Email}", user.Email);
                // Continue even if email fails
            }

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

            // Send rejection email
            try
            {
                await _emailService.SendOrgAdminRejectionEmailAsync(
                    user.Email!,
                    user.FirstName ?? "User",
                    request.Reason);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send rejection email to {Email}", user.Email);
                // Continue even if email fails
            }

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


    // ============================================
    // DASHBOARD STATS
    // ============================================

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        try
        {
            // Get all users with their roles
            var users = await _userManager.Users.ToListAsync();
            var userRoleCounts = new Dictionary<string, int>
            {
                { "SuperAdmin", 0 },
                { "OrganizationAdmin", 0 },
                { "OrganizationUser", 0 },
                { "DefaultUser", 0 }
            };

            var statusCounts = new Dictionary<string, int>
            {
                { "Active", 0 },
                { "Pending", 0 },
                { "Inactive", 0 }
            };

            foreach (var user in users)
            {
                // Count by status
                var statusKey = user.Status?.ToString() ?? "Active";
                if (statusCounts.ContainsKey(statusKey))
                    statusCounts[statusKey]++;

                // Count by role
                var roles = await _userManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    if (userRoleCounts.ContainsKey(role))
                        userRoleCounts[role]++;
                }
            }

            // Get organization stats
            var orgs = await _context.Organizations.ToListAsync();
            var activeOrgs = orgs.Count(o => o.IsActive);
            var inactiveOrgs = orgs.Count(o => !o.IsActive);

            // Get pending approvals count
            var pendingApprovals = await _userManager.Users
                .CountAsync(u => u.Status == UserStatus.Pending);

            return Ok(new
            {
                organizations = new
                {
                    total = orgs.Count,
                    active = activeOrgs,
                    inactive = inactiveOrgs
                },
                users = new
                {
                    total = users.Count,
                    byRole = userRoleCounts,
                    byStatus = statusCounts
                },
                pendingApprovals
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return StatusCode(500, new { error = "Failed to retrieve dashboard stats" });
        }
    }

    // ============================================
    // ORGANIZATION MANAGEMENT
    // ============================================

    [HttpGet("organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        try
        {
            var orgs = await _context.Organizations
                .Include(o => o.Owner)
                .Include(o => o.SubscriptionPlan)
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Slug,
                    o.IsActive,
                    o.DeactivatedAt,
                    o.DeactivationReason,
                    Owner = new
                    {
                        o.Owner.Id,
                        o.Owner.Email,
                        FullName = $"{o.Owner.FirstName} {o.Owner.LastName}"
                    },
                    SubscriptionPlan = o.SubscriptionPlan.Name,
                    MemberCount = _context.Users.Count(u => u.OrganizationId == o.Id),
                    o.CvUploadsThisMonth,
                    o.CreatedAt
                })
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting organizations");
            return StatusCode(500, new { error = "Failed to retrieve organizations" });
        }
    }

    [HttpPost("organizations/{orgId}/deactivate")]
    public async Task<IActionResult> DeactivateOrganization(Guid orgId, [FromBody] DeactivateRequest request)
    {
        try
        {
            var org = await _context.Organizations
                .Include(o => o.Owner)
                .FirstOrDefaultAsync(o => o.Id == orgId);
            if (org == null)
                return NotFound(new { error = "Organization not found" });

            if (!org.IsActive)
                return BadRequest(new { error = "Organization is already deactivated" });

            org.IsActive = false;
            org.DeactivatedAt = DateTime.UtcNow;
            org.DeactivationReason = request.Reason;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgName} deactivated. Reason: {Reason}",
                org.Name, request.Reason);

            // Send email to organization owner
            try
            {
                if (org.Owner != null)
                {
                    await _emailService.SendOrganizationDeactivatedEmailAsync(
                        org.Owner.Email!,
                        org.Owner.FirstName ?? "User",
                        org.Name,
                        request.Reason);
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send organization deactivation email to {Email}", org.Owner?.Email);
            }

            return Ok(new
            {
                message = "Organization deactivated successfully",
                organizationId = org.Id,
                organizationName = org.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating organization {OrgId}", orgId);
            return StatusCode(500, new { error = "Failed to deactivate organization" });
        }
    }

    [HttpPost("organizations/{orgId}/reactivate")]
    public async Task<IActionResult> ReactivateOrganization(Guid orgId)
    {
        try
        {
            var org = await _context.Organizations
                .Include(o => o.Owner)
                .FirstOrDefaultAsync(o => o.Id == orgId);
            if (org == null)
                return NotFound(new { error = "Organization not found" });

            if (org.IsActive)
                return BadRequest(new { error = "Organization is already active" });

            org.IsActive = true;
            org.DeactivatedAt = null;
            org.DeactivationReason = null;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgName} reactivated", org.Name);

            // Send email to organization owner
            try
            {
                if (org.Owner != null)
                {
                    await _emailService.SendOrganizationReactivatedEmailAsync(
                        org.Owner.Email!,
                        org.Owner.FirstName ?? "User",
                        org.Name);
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send organization reactivation email to {Email}", org.Owner?.Email);
            }

            return Ok(new
            {
                message = "Organization reactivated successfully",
                organizationId = org.Id,
                organizationName = org.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating organization {OrgId}", orgId);
            return StatusCode(500, new { error = "Failed to reactivate organization" });
        }
    }

    [HttpPost("organizations/{orgId}/change-plan")]
    public async Task<IActionResult> ChangeOrganizationPlan(Guid orgId, [FromBody] ChangePlanRequest request)
    {
        try
        {
            var org = await _context.Organizations
                .Include(o => o.Owner)
                .Include(o => o.SubscriptionPlan)
                .FirstOrDefaultAsync(o => o.Id == orgId);

            if (org == null)
                return NotFound(new { error = "Organization not found" });

            var newPlan = await _context.SubscriptionPlans.FindAsync(request.NewPlanId);
            if (newPlan == null)
                return BadRequest(new { error = "New plan not found" });

            if (!newPlan.IsActive)
                return BadRequest(new { error = "New plan is not active" });

            if (org.SubscriptionPlanId == request.NewPlanId)
                return BadRequest(new { error = "Organization is already on this plan" });

            var oldPlan = org.SubscriptionPlan;

            // Check if downgrade would exceed limits
            var currentMemberCount = await _userManager.Users
                .CountAsync(u => u.OrganizationId == orgId);

            if (newPlan.MaxUsers < currentMemberCount)
                return BadRequest(new
                {
                    error = $"Cannot downgrade: organization has {currentMemberCount} members but new plan only allows {newPlan.MaxUsers}",
                    currentMemberCount,
                    newPlanMaxUsers = newPlan.MaxUsers
                });

            // Update the organization's plan
            org.SubscriptionPlanId = request.NewPlanId;
            org.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Organization {OrgName} plan changed from {OldPlan} to {NewPlan} by admin. Reason: {Reason}",
                org.Name, oldPlan.DisplayName, newPlan.DisplayName, request.Reason ?? "No reason provided");

            // Send email notification to organization owner
            try
            {
                if (org.Owner != null)
                {
                    await _emailService.SendPlanChangedByAdminAsync(
                        org.Owner.Email!,
                        org.Owner.FirstName ?? "User",
                        org.Name,
                        oldPlan.DisplayName,
                        newPlan.DisplayName,
                        request.Reason);
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send plan change email to {Email}", org.Owner?.Email);
            }

            return Ok(new
            {
                message = "Organization plan changed successfully",
                organization = new
                {
                    org.Id,
                    org.Name,
                    oldPlan = new { oldPlan.Id, oldPlan.Name, oldPlan.DisplayName },
                    newPlan = new { newPlan.Id, newPlan.Name, newPlan.DisplayName }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing organization plan {OrgId}", orgId);
            return StatusCode(500, new { error = "Failed to change organization plan" });
        }
    }

    // ============================================
    // USER MANAGEMENT
    // ============================================

    [HttpGet("users/detailed")]
    public async Task<IActionResult> GetUsersDetailed()
    {
        try
        {
            var users = await _userManager.Users
                .Include(u => u.Organization)
                .ToListAsync();

            var result = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new
                {
                    user.Id,
                    user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    user.Status,
                    Roles = roles,
                    user.OrganizationId,
                    OrganizationName = user.Organization?.Name,
                    OrganizationIsActive = user.Organization?.IsActive,
                    user.DeactivatedAt,
                    user.DeactivationReason,
                    user.CreatedAt
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detailed users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }

    [HttpPost("users/{userId}/deactivate")]
    public async Task<IActionResult> DeactivateUser(string userId, [FromBody] DeactivateRequest request)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { error = "User not found" });

            // Prevent deactivating SuperAdmins
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("SuperAdmin"))
                return BadRequest(new { error = "Cannot deactivate a SuperAdmin user" });

            if (user.Status == UserStatus.Inactive)
                return BadRequest(new { error = "User is already deactivated" });

            user.Status = UserStatus.Inactive;
            user.DeactivatedAt = DateTime.UtcNow;
            user.DeactivationReason = request.Reason;

            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {Email} deactivated. Reason: {Reason}",
                user.Email, request.Reason);

            // Send deactivation email
            try
            {
                await _emailService.SendAccountDeactivatedEmailAsync(
                    user.Email!,
                    user.FirstName ?? "User",
                    request.Reason);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send deactivation email to {Email}", user.Email);
            }

            return Ok(new
            {
                message = "User deactivated successfully",
                userId = user.Id,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to deactivate user" });
        }
    }

    [HttpPost("users/{userId}/reactivate")]
    public async Task<IActionResult> ReactivateUser(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { error = "User not found" });

            if (user.Status == UserStatus.Active)
                return BadRequest(new { error = "User is already active" });

            // Check if user's organization is active (if they belong to one)
            if (user.OrganizationId.HasValue)
            {
                var org = await _context.Organizations.FindAsync(user.OrganizationId.Value);
                if (org != null && !org.IsActive)
                    return BadRequest(new { error = "Cannot reactivate user because their organization is deactivated" });
            }

            user.Status = UserStatus.Active;
            user.DeactivatedAt = null;
            user.DeactivationReason = null;

            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User {Email} reactivated", user.Email);

            // Send reactivation email
            try
            {
                await _emailService.SendAccountReactivatedEmailAsync(
                    user.Email!,
                    user.FirstName ?? "User");
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send reactivation email to {Email}", user.Email);
            }

            return Ok(new
            {
                message = "User reactivated successfully",
                userId = user.Id,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to reactivate user" });
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

public class DeactivateRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ChangePlanRequest
{
    public Guid NewPlanId { get; set; }
    public string? Reason { get; set; }
}
