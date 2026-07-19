using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/superadmin")]
public sealed class SuperAdminController : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Active", "Suspended", "Archived" };

    private readonly ApplicationDbContext _context;

    public SuperAdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<PlatformSummaryDto>> GetSummary()
    {
        var subscriptions = _context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "active");

        var summary = new PlatformSummaryDto
        {
            TotalOrganizations = await _context.Organizations.CountAsync(),
            ActiveOrganizations = await _context.Organizations.CountAsync(o => o.Status == "Active"),
            SuspendedOrganizations = await _context.Organizations.CountAsync(o => o.Status == "Suspended"),
            TotalUsers = await _context.Users.IgnoreQueryFilters().CountAsync(u => u.Role != Roles.SuperAdmin),
            ActiveSubscriptions = await subscriptions.CountAsync(),
            EstimatedMonthlyRecurringRevenue = await subscriptions
                .Where(s => s.Plan.MonthlyPricePerSeat > 0 || s.Plan.AnnualPricePerSeat > 0)
                .SumAsync(s => s.BillingInterval == "annual"
                    ? (s.Plan.AnnualPricePerSeat / 12m) * s.Seats
                    : s.Plan.MonthlyPricePerSeat * s.Seats)
        };

        return Ok(summary);
    }

    [HttpGet("organizations")]
    public async Task<ActionResult<IReadOnlyList<PlatformOrganizationDto>>> GetOrganizations()
    {
        var organizations = await _context.Organizations
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new PlatformOrganizationDto
            {
                Id = o.Id,
                Name = o.Name,
                Slug = o.Slug,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                UserCount = _context.Users.IgnoreQueryFilters().Count(u => u.OrganizationId == o.Id),
                ProjectCount = _context.Projects.IgnoreQueryFilters().Count(p => p.OrganizationId == o.Id),
                PlanName = _context.Subscriptions.IgnoreQueryFilters()
                    .Where(s => s.OrganizationId == o.Id)
                    .Select(s => s.Plan.Name)
                    .FirstOrDefault() ?? "Free",
                SubscriptionStatus = _context.Subscriptions.IgnoreQueryFilters()
                    .Where(s => s.OrganizationId == o.Id)
                    .Select(s => s.Status)
                    .FirstOrDefault() ?? "active",
                Seats = _context.Subscriptions.IgnoreQueryFilters()
                    .Where(s => s.OrganizationId == o.Id)
                    .Select(s => s.Seats)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(organizations);
    }

    [HttpPut("organizations/{id:int}/status")]
    public async Task<IActionResult> UpdateOrganizationStatus(
        int id,
        [FromBody] PlatformOrganizationStatusDto dto)
    {
        if (!AllowedStatuses.Contains(dto.Status))
            return BadRequest("Status must be Active, Suspended, or Archived.");

        var organization = await _context.Organizations.FindAsync(id);
        if (organization is null)
            return NotFound();

        organization.Status = AllowedStatuses.First(status =>
            status.Equals(dto.Status, StringComparison.OrdinalIgnoreCase));
        organization.UpdatedAt = DateTime.UtcNow;

        if (organization.Status is "Suspended" or "Archived")
        {
            var userIds = _context.Users.IgnoreQueryFilters()
                .Where(user => user.OrganizationId == organization.Id)
                .Select(user => user.Id);

            var refreshTokens = await _context.RefreshTokens
                .Where(token => userIds.Contains(token.UserId) && !token.IsRevoked)
                .ToListAsync();

            foreach (var token in refreshTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }
}
