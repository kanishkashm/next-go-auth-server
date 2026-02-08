using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using next_go_auth_server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace next_go_auth_server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UpgradeRequestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<UpgradeRequestController> _logger;
    private readonly IEmailService _emailService;

    public UpgradeRequestController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<UpgradeRequestController> logger,
        IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _emailService = emailService;
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

    // ORGADMIN: Get available plans for upgrade
    [HttpGet("available-plans")]
    [Authorize(Roles = "OrganizationAdmin")]
    public async Task<IActionResult> GetAvailablePlans()
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

            var currentPlan = org.SubscriptionPlan;

            // Get all active plans that are higher tier than current (by DisplayOrder or limits)
            var availablePlans = await _context.SubscriptionPlans
                .Where(sp => sp.IsActive && sp.Id != currentPlan.Id)
                .OrderBy(sp => sp.DisplayOrder)
                .Select(sp => new
                {
                    sp.Id,
                    sp.Name,
                    sp.DisplayName,
                    sp.MaxUsers,
                    sp.MaxCVUploads,
                    Features = ParseFeatures(sp.FeaturesJson),
                    sp.MonthlyPrice,
                    sp.DisplayOrder,
                    sp.IsPopular,
                    IsUpgrade = sp.MaxCVUploads > currentPlan.MaxCVUploads || sp.MaxUsers > currentPlan.MaxUsers
                })
                .ToListAsync();

            return Ok(new
            {
                currentPlan = new
                {
                    currentPlan.Id,
                    currentPlan.Name,
                    currentPlan.DisplayName,
                    currentPlan.MaxUsers,
                    currentPlan.MaxCVUploads,
                    Features = ParseFeatures(currentPlan.FeaturesJson),
                    currentPlan.MonthlyPrice
                },
                availablePlans
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available plans");
            return StatusCode(500, new { error = "Failed to retrieve available plans" });
        }
    }

    // ORGADMIN: Get my organization's pending upgrade request (if any)
    [HttpGet("my-request")]
    [Authorize(Roles = "OrganizationAdmin,OrganizationUser")]
    public async Task<IActionResult> GetMyUpgradeRequest()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var pendingRequest = await _context.UpgradeRequests
                .Include(ur => ur.CurrentPlan)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .Where(ur => ur.OrganizationId == user.OrganizationId.Value
                    && ur.Status == UpgradeRequestStatus.Pending)
                .OrderByDescending(ur => ur.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingRequest == null)
                return Ok(new { hasPendingRequest = false });

            return Ok(new
            {
                hasPendingRequest = true,
                request = new
                {
                    pendingRequest.Id,
                    currentPlan = new
                    {
                        pendingRequest.CurrentPlan.Id,
                        pendingRequest.CurrentPlan.Name,
                        pendingRequest.CurrentPlan.DisplayName,
                        pendingRequest.CurrentPlan.MaxUsers,
                        pendingRequest.CurrentPlan.MaxCVUploads
                    },
                    requestedPlan = new
                    {
                        pendingRequest.RequestedPlan.Id,
                        pendingRequest.RequestedPlan.Name,
                        pendingRequest.RequestedPlan.DisplayName,
                        pendingRequest.RequestedPlan.MaxUsers,
                        pendingRequest.RequestedPlan.MaxCVUploads
                    },
                    pendingRequest.Reason,
                    pendingRequest.Status,
                    requestedBy = new
                    {
                        pendingRequest.RequestedBy.Id,
                        pendingRequest.RequestedBy.Email,
                        FullName = $"{pendingRequest.RequestedBy.FirstName} {pendingRequest.RequestedBy.LastName}"
                    },
                    pendingRequest.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending upgrade request");
            return StatusCode(500, new { error = "Failed to retrieve upgrade request" });
        }
    }

    // ORGADMIN: Get upgrade request history for my organization
    [HttpGet("my-history")]
    [Authorize(Roles = "OrganizationAdmin,OrganizationUser")]
    public async Task<IActionResult> GetMyUpgradeHistory()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var requests = await _context.UpgradeRequests
                .Include(ur => ur.CurrentPlan)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .Include(ur => ur.ProcessedBy)
                .Where(ur => ur.OrganizationId == user.OrganizationId.Value)
                .OrderByDescending(ur => ur.CreatedAt)
                .Select(ur => new
                {
                    ur.Id,
                    currentPlan = new
                    {
                        ur.CurrentPlan.Id,
                        ur.CurrentPlan.Name,
                        ur.CurrentPlan.DisplayName
                    },
                    requestedPlan = new
                    {
                        ur.RequestedPlan.Id,
                        ur.RequestedPlan.Name,
                        ur.RequestedPlan.DisplayName
                    },
                    ur.Reason,
                    Status = ur.Status.ToString(),
                    ur.RejectionReason,
                    requestedBy = new
                    {
                        ur.RequestedBy.Id,
                        ur.RequestedBy.Email,
                        FullName = $"{ur.RequestedBy.FirstName} {ur.RequestedBy.LastName}"
                    },
                    processedBy = ur.ProcessedBy != null ? new
                    {
                        ur.ProcessedBy.Id,
                        ur.ProcessedBy.Email,
                        FullName = $"{ur.ProcessedBy.FirstName} {ur.ProcessedBy.LastName}"
                    } : null,
                    ur.ProcessedAt,
                    ur.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upgrade request history");
            return StatusCode(500, new { error = "Failed to retrieve upgrade history" });
        }
    }

    // ORGADMIN: Submit upgrade request
    [HttpPost]
    [Authorize(Roles = "OrganizationAdmin")]
    public async Task<IActionResult> SubmitUpgradeRequest([FromBody] SubmitUpgradeRequestDto request)
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

            // Check for existing pending request
            var existingPending = await _context.UpgradeRequests
                .AnyAsync(ur => ur.OrganizationId == org.Id && ur.Status == UpgradeRequestStatus.Pending);

            if (existingPending)
                return BadRequest(new { error = "You already have a pending upgrade request. Please wait for it to be processed or cancel it first." });

            // Validate requested plan
            var requestedPlan = await _context.SubscriptionPlans.FindAsync(request.RequestedPlanId);
            if (requestedPlan == null)
                return BadRequest(new { error = "Requested plan not found" });

            if (!requestedPlan.IsActive)
                return BadRequest(new { error = "Requested plan is not available" });

            if (requestedPlan.Id == org.SubscriptionPlanId)
                return BadRequest(new { error = "You are already on this plan" });

            var upgradeRequest = new UpgradeRequest
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                CurrentPlanId = org.SubscriptionPlanId,
                RequestedPlanId = request.RequestedPlanId,
                RequestedById = user.Id,
                Reason = request.Reason,
                Status = UpgradeRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.UpgradeRequests.AddAsync(upgradeRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Upgrade request submitted for organization {OrgId} by {UserId}",
                org.Id, user.Id);

            // Send email notifications
            try
            {
                // Notify all SuperAdmins
                var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
                foreach (var admin in superAdmins)
                {
                    await _emailService.SendUpgradeRequestNotificationToAdminAsync(
                        admin.Email!,
                        admin.FirstName ?? "Admin",
                        org.Name,
                        $"{user.FirstName} {user.LastName}",
                        org.SubscriptionPlan.DisplayName,
                        requestedPlan.DisplayName,
                        request.Reason);
                }

                // Send confirmation to requesting user
                await _emailService.SendUpgradeRequestSubmittedToOrgAsync(
                    user.Email!,
                    user.FirstName ?? "User",
                    org.Name,
                    org.SubscriptionPlan.DisplayName,
                    requestedPlan.DisplayName);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send upgrade request notification emails");
            }

            return Ok(new
            {
                message = "Upgrade request submitted successfully",
                requestId = upgradeRequest.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting upgrade request");
            return StatusCode(500, new { error = "Failed to submit upgrade request" });
        }
    }

    // ORGADMIN: Cancel pending request
    [HttpPost("{requestId}/cancel")]
    [Authorize(Roles = "OrganizationAdmin")]
    public async Task<IActionResult> CancelUpgradeRequest(Guid requestId)
    {
        try
        {
            var user = await GetCurrentUserAsync();
            if (user == null || !user.OrganizationId.HasValue)
                return NotFound(new { error = "Organization not found" });

            var request = await _context.UpgradeRequests
                .FirstOrDefaultAsync(ur => ur.Id == requestId && ur.OrganizationId == user.OrganizationId.Value);

            if (request == null)
                return NotFound(new { error = "Upgrade request not found" });

            if (request.Status != UpgradeRequestStatus.Pending)
                return BadRequest(new { error = "Only pending requests can be cancelled" });

            request.Status = UpgradeRequestStatus.Cancelled;
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Upgrade request {RequestId} cancelled by {UserId}",
                requestId, user.Id);

            return Ok(new { message = "Upgrade request cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling upgrade request");
            return StatusCode(500, new { error = "Failed to cancel upgrade request" });
        }
    }

    // SUPERADMIN: Get all pending requests
    [HttpGet("pending")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetPendingRequests()
    {
        try
        {
            var requests = await _context.UpgradeRequests
                .Include(ur => ur.Organization)
                .Include(ur => ur.CurrentPlan)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .Where(ur => ur.Status == UpgradeRequestStatus.Pending)
                .OrderByDescending(ur => ur.CreatedAt)
                .Select(ur => new
                {
                    ur.Id,
                    organization = new
                    {
                        ur.Organization.Id,
                        ur.Organization.Name,
                        ur.Organization.Slug
                    },
                    currentPlan = new
                    {
                        ur.CurrentPlan.Id,
                        ur.CurrentPlan.Name,
                        ur.CurrentPlan.DisplayName,
                        ur.CurrentPlan.MaxUsers,
                        ur.CurrentPlan.MaxCVUploads
                    },
                    requestedPlan = new
                    {
                        ur.RequestedPlan.Id,
                        ur.RequestedPlan.Name,
                        ur.RequestedPlan.DisplayName,
                        ur.RequestedPlan.MaxUsers,
                        ur.RequestedPlan.MaxCVUploads
                    },
                    ur.Reason,
                    requestedBy = new
                    {
                        ur.RequestedBy.Id,
                        ur.RequestedBy.Email,
                        FullName = $"{ur.RequestedBy.FirstName} {ur.RequestedBy.LastName}"
                    },
                    ur.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending upgrade requests");
            return StatusCode(500, new { error = "Failed to retrieve pending requests" });
        }
    }

    // SUPERADMIN: Get all requests with filtering
    [HttpGet("all")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAllRequests([FromQuery] UpgradeRequestStatus? status = null)
    {
        try
        {
            var query = _context.UpgradeRequests
                .Include(ur => ur.Organization)
                .Include(ur => ur.CurrentPlan)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .Include(ur => ur.ProcessedBy)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(ur => ur.Status == status.Value);

            var requests = await query
                .OrderByDescending(ur => ur.CreatedAt)
                .Select(ur => new
                {
                    ur.Id,
                    organization = new
                    {
                        ur.Organization.Id,
                        ur.Organization.Name,
                        ur.Organization.Slug
                    },
                    currentPlan = new
                    {
                        ur.CurrentPlan.Id,
                        ur.CurrentPlan.Name,
                        ur.CurrentPlan.DisplayName
                    },
                    requestedPlan = new
                    {
                        ur.RequestedPlan.Id,
                        ur.RequestedPlan.Name,
                        ur.RequestedPlan.DisplayName
                    },
                    ur.Reason,
                    Status = ur.Status.ToString(),
                    ur.RejectionReason,
                    requestedBy = new
                    {
                        ur.RequestedBy.Id,
                        ur.RequestedBy.Email,
                        FullName = $"{ur.RequestedBy.FirstName} {ur.RequestedBy.LastName}"
                    },
                    processedBy = ur.ProcessedBy != null ? new
                    {
                        ur.ProcessedBy.Id,
                        ur.ProcessedBy.Email,
                        FullName = $"{ur.ProcessedBy.FirstName} {ur.ProcessedBy.LastName}"
                    } : null,
                    ur.ProcessedAt,
                    ur.CreatedAt
                })
                .ToListAsync();

            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all upgrade requests");
            return StatusCode(500, new { error = "Failed to retrieve upgrade requests" });
        }
    }

    // SUPERADMIN: Approve request
    [HttpPost("{requestId}/approve")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ApproveUpgradeRequest(Guid requestId)
    {
        try
        {
            var adminUser = await GetCurrentUserAsync();
            if (adminUser == null)
                return Unauthorized(new { error = "User not found" });

            var request = await _context.UpgradeRequests
                .Include(ur => ur.Organization)
                .Include(ur => ur.CurrentPlan)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .FirstOrDefaultAsync(ur => ur.Id == requestId);

            if (request == null)
                return NotFound(new { error = "Upgrade request not found" });

            if (request.Status != UpgradeRequestStatus.Pending)
                return BadRequest(new { error = "Only pending requests can be approved" });

            // Update the organization's subscription plan
            var org = request.Organization;
            var oldPlan = request.CurrentPlan;
            var newPlan = request.RequestedPlan;

            org.SubscriptionPlanId = newPlan.Id;
            org.UpdatedAt = DateTime.UtcNow;

            // Update the request
            request.Status = UpgradeRequestStatus.Approved;
            request.ProcessedById = adminUser.Id;
            request.ProcessedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Upgrade request {RequestId} approved for organization {OrgId} by admin {AdminId}",
                requestId, org.Id, adminUser.Id);

            // Send email notification
            try
            {
                await _emailService.SendUpgradeRequestApprovedAsync(
                    request.RequestedBy.Email!,
                    request.RequestedBy.FirstName ?? "User",
                    org.Name,
                    oldPlan.DisplayName,
                    newPlan.DisplayName);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send upgrade approval email");
            }

            return Ok(new
            {
                message = "Upgrade request approved successfully",
                organization = new
                {
                    org.Id,
                    org.Name,
                    oldPlan = oldPlan.DisplayName,
                    newPlan = newPlan.DisplayName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving upgrade request");
            return StatusCode(500, new { error = "Failed to approve upgrade request" });
        }
    }

    // SUPERADMIN: Reject request
    [HttpPost("{requestId}/reject")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> RejectUpgradeRequest(Guid requestId, [FromBody] RejectUpgradeRequestDto dto)
    {
        try
        {
            var adminUser = await GetCurrentUserAsync();
            if (adminUser == null)
                return Unauthorized(new { error = "User not found" });

            var request = await _context.UpgradeRequests
                .Include(ur => ur.Organization)
                .Include(ur => ur.RequestedPlan)
                .Include(ur => ur.RequestedBy)
                .FirstOrDefaultAsync(ur => ur.Id == requestId);

            if (request == null)
                return NotFound(new { error = "Upgrade request not found" });

            if (request.Status != UpgradeRequestStatus.Pending)
                return BadRequest(new { error = "Only pending requests can be rejected" });

            // Update the request
            request.Status = UpgradeRequestStatus.Rejected;
            request.RejectionReason = dto.Reason;
            request.ProcessedById = adminUser.Id;
            request.ProcessedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Upgrade request {RequestId} rejected for organization {OrgId} by admin {AdminId}. Reason: {Reason}",
                requestId, request.OrganizationId, adminUser.Id, dto.Reason);

            // Send email notification
            try
            {
                await _emailService.SendUpgradeRequestRejectedAsync(
                    request.RequestedBy.Email!,
                    request.RequestedBy.FirstName ?? "User",
                    request.Organization.Name,
                    request.RequestedPlan.DisplayName,
                    dto.Reason);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send upgrade rejection email");
            }

            return Ok(new { message = "Upgrade request rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting upgrade request");
            return StatusCode(500, new { error = "Failed to reject upgrade request" });
        }
    }

    // SUPERADMIN: Get pending count for dashboard
    [HttpGet("pending/count")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetPendingCount()
    {
        try
        {
            var count = await _context.UpgradeRequests
                .CountAsync(ur => ur.Status == UpgradeRequestStatus.Pending);

            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending upgrade requests count");
            return StatusCode(500, new { error = "Failed to get pending count" });
        }
    }

    private static List<string> ParseFeatures(string featuresJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

// DTOs
public class SubmitUpgradeRequestDto
{
    public Guid RequestedPlanId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RejectUpgradeRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
