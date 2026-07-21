using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Authorize]
[Route("api/organizations")]
[ApiController]
public class OrganizationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantService _tenant;

    public OrganizationsController(ApplicationDbContext context, ITenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [Authorize(Roles = "OrganizationAdmin,Manager")]
    [HttpGet("current")]
    public async Task<ActionResult<OrganizationSettingsDto>> GetCurrent(CancellationToken ct)
    {
        var org = await GetTenantOrgAsync(ct);
        if (org is null)
            return NotFound();

        return Ok(ToDto(org));
    }

    [Authorize(Roles = "OrganizationAdmin")]
    [HttpPut("current")]
    public async Task<ActionResult<OrganizationSettingsDto>> UpdateCurrent(
        [FromBody] UpdateOrganizationSettingsDto dto,
        CancellationToken ct)
    {
        var org = await GetTenantOrgAsync(ct);
        if (org is null)
            return NotFound();

        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Organization name is required.");

        var color = string.IsNullOrWhiteSpace(dto.BrandPrimaryColor)
            ? null
            : dto.BrandPrimaryColor.Trim();

        if (color is not null && !IsValidHexColor(color))
            return BadRequest("Brand color must be a hex value like #4F46E5.");

        org.Name = name;
        org.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        org.LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim();
        org.TimeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "Asia/Kolkata" : dto.TimeZoneId.Trim();
        org.BrandPrimaryColor = color;
        org.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return Ok(ToDto(org));
    }

    [Authorize(Roles = "OrganizationAdmin")]
    [HttpGet("current/onboarding")]
    public async Task<ActionResult<OnboardingStatusDto>> GetOnboarding(CancellationToken ct)
    {
        var org = await GetTenantOrgAsync(ct);
        if (org is null)
            return NotFound();

        var now = DateTime.UtcNow;
        var projectCount = await _context.Projects.CountAsync(ct);
        var memberCount = await _context.Users.CountAsync(u => u.IsActive, ct);
        var pendingInvites = await _context.OrganizationInvites.CountAsync(
            i => i.AcceptedAt == null && i.ExpiresAt > now, ct);

        return Ok(new OnboardingStatusDto
        {
            Completed = org.OnboardingCompletedAt is not null,
            OrganizationName = org.Name,
            ProjectCount = projectCount,
            PendingInviteCount = pendingInvites,
            MemberCount = memberCount
        });
    }

    [Authorize(Roles = "OrganizationAdmin")]
    [HttpPost("current/complete-onboarding")]
    public async Task<ActionResult<OnboardingStatusDto>> CompleteOnboarding(CancellationToken ct)
    {
        var org = await GetTenantOrgAsync(ct);
        if (org is null)
            return NotFound();

        if (org.OnboardingCompletedAt is null)
        {
            org.OnboardingCompletedAt = DateTime.UtcNow;
            org.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return await GetOnboarding(ct);
    }

    private async Task<Organization?> GetTenantOrgAsync(CancellationToken ct)
    {
        if (_tenant.OrganizationId is not int orgId)
            return null;

        return await _context.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
    }

    private static OrganizationSettingsDto ToDto(Organization org) => new()
    {
        Id = org.Id,
        Name = org.Name,
        Slug = org.Slug,
        Description = org.Description,
        LogoUrl = org.LogoUrl,
        TimeZoneId = org.TimeZoneId,
        BrandPrimaryColor = org.BrandPrimaryColor,
        OnboardingCompleted = org.OnboardingCompletedAt is not null,
        CreatedAt = org.CreatedAt
    };

    private static bool IsValidHexColor(string value) =>
        value.Length is 4 or 7
        && value[0] == '#'
        && value[1..].All(c => Uri.IsHexDigit(c));
}
