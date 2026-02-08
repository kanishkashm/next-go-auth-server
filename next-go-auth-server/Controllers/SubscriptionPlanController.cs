using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using next_go_auth_server.Database;
using System.Text.Json;

namespace next_go_auth_server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionPlanController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionPlanController> _logger;

    public SubscriptionPlanController(
        ApplicationDbContext context,
        ILogger<SubscriptionPlanController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // PUBLIC: Get all active plans (for pricing page)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllPlans()
    {
        try
        {
            var plans = await _context.SubscriptionPlans
                .Where(sp => sp.IsActive)
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
                    sp.IsPopular
                })
                .ToListAsync();

            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plans");
            return StatusCode(500, new { error = "Failed to retrieve subscription plans" });
        }
    }

    // SUPERADMIN: Get all plans including inactive
    [HttpGet("admin")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAllPlansAdmin()
    {
        try
        {
            var plans = await _context.SubscriptionPlans
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
                    sp.IsActive,
                    OrganizationsCount = _context.Organizations.Count(o => o.SubscriptionPlanId == sp.Id),
                    sp.CreatedAt,
                    sp.UpdatedAt
                })
                .ToListAsync();

            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plans for admin");
            return StatusCode(500, new { error = "Failed to retrieve subscription plans" });
        }
    }

    // SUPERADMIN: Get single plan by ID
    [HttpGet("{planId}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetPlanById(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans
                .Where(sp => sp.Id == planId)
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
                    sp.IsActive,
                    OrganizationsCount = _context.Organizations.Count(o => o.SubscriptionPlanId == sp.Id),
                    sp.CreatedAt,
                    sp.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (plan == null)
                return NotFound(new { error = "Subscription plan not found" });

            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plan {PlanId}", planId);
            return StatusCode(500, new { error = "Failed to retrieve subscription plan" });
        }
    }

    // SUPERADMIN: Create new plan
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request)
    {
        try
        {
            // Check if name already exists
            var existingPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(sp => sp.Name.ToLower() == request.Name.ToLower());

            if (existingPlan != null)
                return BadRequest(new { error = "A plan with this name already exists" });

            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = request.Name.ToLower().Replace(" ", "-"),
                DisplayName = request.DisplayName,
                MaxUsers = request.MaxUsers,
                MaxCVUploads = request.MaxCVUploads,
                FeaturesJson = JsonSerializer.Serialize(request.Features ?? new List<string>()),
                MonthlyPrice = request.MonthlyPrice,
                DisplayOrder = request.DisplayOrder,
                IsPopular = request.IsPopular,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SubscriptionPlans.AddAsync(plan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created subscription plan {PlanName}", plan.Name);

            return Ok(new
            {
                message = "Subscription plan created successfully",
                plan = new
                {
                    plan.Id,
                    plan.Name,
                    plan.DisplayName,
                    plan.MaxUsers,
                    plan.MaxCVUploads,
                    Features = request.Features ?? new List<string>(),
                    plan.MonthlyPrice,
                    plan.DisplayOrder,
                    plan.IsPopular,
                    plan.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription plan");
            return StatusCode(500, new { error = "Failed to create subscription plan" });
        }
    }

    // SUPERADMIN: Update plan
    [HttpPut("{planId}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdatePlan(Guid planId, [FromBody] UpdatePlanRequest request)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                return NotFound(new { error = "Subscription plan not found" });

            // Update fields if provided
            if (!string.IsNullOrEmpty(request.DisplayName))
                plan.DisplayName = request.DisplayName;

            if (request.MaxUsers.HasValue)
                plan.MaxUsers = request.MaxUsers.Value;

            if (request.MaxCVUploads.HasValue)
                plan.MaxCVUploads = request.MaxCVUploads.Value;

            if (request.Features != null)
                plan.FeaturesJson = JsonSerializer.Serialize(request.Features);

            if (request.MonthlyPrice.HasValue)
                plan.MonthlyPrice = request.MonthlyPrice.Value;

            if (request.DisplayOrder.HasValue)
                plan.DisplayOrder = request.DisplayOrder.Value;

            if (request.IsPopular.HasValue)
                plan.IsPopular = request.IsPopular.Value;

            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated subscription plan {PlanName}", plan.Name);

            return Ok(new
            {
                message = "Subscription plan updated successfully",
                plan = new
                {
                    plan.Id,
                    plan.Name,
                    plan.DisplayName,
                    plan.MaxUsers,
                    plan.MaxCVUploads,
                    Features = ParseFeatures(plan.FeaturesJson),
                    plan.MonthlyPrice,
                    plan.DisplayOrder,
                    plan.IsPopular,
                    plan.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription plan {PlanId}", planId);
            return StatusCode(500, new { error = "Failed to update subscription plan" });
        }
    }

    // SUPERADMIN: Activate/Deactivate plan
    [HttpPost("{planId}/toggle-active")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> TogglePlanActive(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                return NotFound(new { error = "Subscription plan not found" });

            plan.IsActive = !plan.IsActive;
            plan.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Toggled subscription plan {PlanName} active status to {IsActive}",
                plan.Name, plan.IsActive);

            return Ok(new
            {
                message = plan.IsActive ? "Plan activated successfully" : "Plan deactivated successfully",
                isActive = plan.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling subscription plan {PlanId}", planId);
            return StatusCode(500, new { error = "Failed to toggle subscription plan" });
        }
    }

    // SUPERADMIN: Check if plan can be deleted
    [HttpGet("{planId}/can-delete")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CanDeletePlan(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                return NotFound(new { error = "Subscription plan not found" });

            var organizationsCount = await _context.Organizations
                .CountAsync(o => o.SubscriptionPlanId == planId);

            return Ok(new
            {
                canDelete = organizationsCount == 0,
                organizationsCount,
                reason = organizationsCount > 0
                    ? $"Cannot delete plan: {organizationsCount} organization(s) are using this plan"
                    : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if plan can be deleted {PlanId}", planId);
            return StatusCode(500, new { error = "Failed to check plan deletion status" });
        }
    }

    // SUPERADMIN: Delete plan (only if no orgs use it)
    [HttpDelete("{planId}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> DeletePlan(Guid planId)
    {
        try
        {
            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null)
                return NotFound(new { error = "Subscription plan not found" });

            var organizationsCount = await _context.Organizations
                .CountAsync(o => o.SubscriptionPlanId == planId);

            if (organizationsCount > 0)
                return BadRequest(new
                {
                    error = $"Cannot delete plan: {organizationsCount} organization(s) are using this plan",
                    organizationsCount
                });

            _context.SubscriptionPlans.Remove(plan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted subscription plan {PlanName}", plan.Name);

            return Ok(new { message = "Subscription plan deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription plan {PlanId}", planId);
            return StatusCode(500, new { error = "Failed to delete subscription plan" });
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
public class CreatePlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MaxUsers { get; set; }
    public int MaxCVUploads { get; set; }
    public List<string>? Features { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPopular { get; set; }
}

public class UpdatePlanRequest
{
    public string? DisplayName { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxCVUploads { get; set; }
    public List<string>? Features { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsPopular { get; set; }
}
