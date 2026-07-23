using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Services.Billing;
using TaskManager.Shared.DTOs;

namespace TaskManager.Controllers;

[Route("api/sso")]
[ApiController]
public class SsoController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantService _tenant;
    private readonly IEntitlementService _entitlements;
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly AppOptions _app;

    public SsoController(
        ApplicationDbContext db,
        ITenantService tenant,
        IEntitlementService entitlements,
        IAuthService auth,
        IAuditService audit,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<AppOptions> app)
    {
        _db = db;
        _tenant = tenant;
        _entitlements = entitlements;
        _auth = auth;
        _audit = audit;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _app = app.Value;
    }

    [Authorize(Roles = "OrganizationAdmin")]
    [HttpGet("config")]
    public async Task<ActionResult<OrganizationSsoConfigDto>> GetConfig(CancellationToken ct)
    {
        if (!await EnsureSsoAsync(ct)) return UpgradeRequired();
        if (_tenant.OrganizationId is not int orgId) return BadRequest();

        var cfg = await _db.OrganizationSsoConfigs.FirstOrDefaultAsync(c => c.OrganizationId == orgId, ct);
        var org = await _db.Organizations.IgnoreQueryFilters().FirstAsync(o => o.Id == orgId, ct);
        if (cfg is null)
        {
            return Ok(new OrganizationSsoConfigDto
            {
                Provider = "google",
                LoginStartPath = $"/api/sso/{org.Slug}/start"
            });
        }

        return Ok(ToDto(cfg, org.Slug));
    }

    [Authorize(Roles = "OrganizationAdmin")]
    [HttpPut("config")]
    public async Task<ActionResult<OrganizationSsoConfigDto>> UpsertConfig(
        [FromBody] UpsertOrganizationSsoConfigDto dto, CancellationToken ct)
    {
        if (!await EnsureSsoAsync(ct)) return UpgradeRequired();
        if (_tenant.OrganizationId is not int orgId) return BadRequest();

        var provider = dto.Provider.Trim().ToLowerInvariant();
        if (provider is not ("google" or "microsoft"))
            return BadRequest("Provider must be google or microsoft.");

        var role = dto.DefaultRole is Roles.User or Roles.Manager or Roles.OrganizationAdmin
            ? dto.DefaultRole
            : Roles.User;

        var cfg = await _db.OrganizationSsoConfigs.FirstOrDefaultAsync(c => c.OrganizationId == orgId, ct);
        if (cfg is null)
        {
            cfg = new OrganizationSsoConfig { OrganizationId = orgId };
            _db.OrganizationSsoConfigs.Add(cfg);
        }

        cfg.Provider = provider;
        cfg.ClientId = dto.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ClientSecret))
            cfg.ClientSecret = dto.ClientSecret.Trim();
        cfg.TenantId = string.IsNullOrWhiteSpace(dto.TenantId) ? "common" : dto.TenantId.Trim();
        cfg.AllowedEmailDomains = dto.AllowedEmailDomains.Trim().ToLowerInvariant();
        cfg.IsEnabled = dto.IsEnabled;
        cfg.AutoProvisionUsers = dto.AutoProvisionUsers;
        cfg.DefaultRole = role;
        cfg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("sso.config.updated", "OrganizationSsoConfig", cfg.Id.ToString(), new { cfg.Provider, cfg.IsEnabled }, orgId, ct);

        var org = await _db.Organizations.IgnoreQueryFilters().FirstAsync(o => o.Id == orgId, ct);
        return Ok(ToDto(cfg, org.Slug));
    }

    [AllowAnonymous]
    [HttpGet("{slug}/info")]
    public async Task<ActionResult<SsoProviderInfoDto>> Info(string slug, CancellationToken ct)
    {
        var org = await _db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Slug == slug, ct);
        if (org is null) return NotFound();

        var cfg = await _db.OrganizationSsoConfigs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == org.Id && c.IsEnabled, ct);
        if (cfg is null)
            return Ok(new SsoProviderInfoDto { OrgSlug = org.Slug, OrgName = org.Name, IsEnabled = false });

        return Ok(new SsoProviderInfoDto
        {
            OrgSlug = org.Slug,
            OrgName = org.Name,
            Provider = cfg.Provider,
            IsEnabled = true,
            StartUrl = $"/api/sso/{org.Slug}/start"
        });
    }

    [AllowAnonymous]
    [HttpGet("{slug}/start")]
    public async Task<IActionResult> Start(string slug, [FromQuery] string? client, CancellationToken ct)
    {
        var org = await _db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Slug == slug, ct);
        if (org is null) return NotFound("Workspace not found.");

        if (!await _entitlements.HasFeatureAsync(org.Id, FeatureKeys.Sso, ct))
            return StatusCode(StatusCodes.Status402PaymentRequired, "SSO requires Business or higher.");

        var cfg = await _db.OrganizationSsoConfigs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == org.Id && c.IsEnabled, ct);
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.ClientId) || string.IsNullOrWhiteSpace(cfg.ClientSecret))
            return BadRequest("SSO is not configured for this workspace.");

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var mobile = string.Equals(client, "mobile", StringComparison.OrdinalIgnoreCase);
        _cache.Set($"sso:{state}", new SsoState(org.Id, cfg.Provider, mobile), TimeSpan.FromMinutes(10));

        var redirectUri = $"{_app.PublicBaseUrl.TrimEnd('/')}/api/sso/callback";
        var url = cfg.Provider == "microsoft"
            ? BuildMicrosoftAuthUrl(cfg, redirectUri, state)
            : BuildGoogleAuthUrl(cfg, redirectUri, state);

        return Redirect(url);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        bool mobile = false;
        if (!string.IsNullOrWhiteSpace(state) &&
            _cache.TryGetValue($"sso:{state}", out SsoState? peek) && peek is not null)
        {
            mobile = peek.Mobile;
            if (!string.IsNullOrEmpty(error))
                _cache.Remove($"sso:{state}");
        }

        if (!string.IsNullOrEmpty(error))
            return RedirectSsoError(error, mobile);

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) ||
            !_cache.TryGetValue($"sso:{state}", out SsoState? ssoState) || ssoState is null)
        {
            return RedirectSsoError("invalid_state", mobile);
        }

        mobile = ssoState.Mobile;
        _cache.Remove($"sso:{state}");

        var cfg = await _db.OrganizationSsoConfigs.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == ssoState.OrgId && c.IsEnabled);
        if (cfg is null)
            return RedirectSsoError("not_configured", mobile);

        var redirectUri = $"{_app.PublicBaseUrl.TrimEnd('/')}/api/sso/callback";
        var profile = ssoState.Provider == "microsoft"
            ? await ExchangeMicrosoftAsync(cfg, code, redirectUri)
            : await ExchangeGoogleAsync(cfg, code, redirectUri);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Email))
            return RedirectSsoError("profile", mobile);

        var email = profile.Email.Trim().ToLowerInvariant();
        if (!DomainAllowed(cfg.AllowedEmailDomains, email))
            return RedirectSsoError("domain", mobile);

        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null)
        {
            if (!cfg.AutoProvisionUsers)
                return RedirectSsoError("no_account", mobile);

            var baseUsername = email.Split('@')[0];
            var username = baseUsername;
            var n = 1;
            while (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username))
                username = $"{baseUsername}{n++}";

            user = new User
            {
                Username = username,
                Email = email,
                FirstName = profile.GivenName ?? baseUsername,
                LastName = profile.FamilyName ?? "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
                Role = cfg.DefaultRole is Roles.Manager or Roles.OrganizationAdmin or Roles.User
                    ? cfg.DefaultRole
                    : Roles.User,
                OrganizationId = ssoState.OrgId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = ssoState.OrgId,
                UserId = user.Id,
                Role = user.Role,
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        else if (user.OrganizationId != ssoState.OrgId)
        {
            return RedirectSsoError("wrong_org", mobile);
        }

        var tokens = await _auth.IssueTokensForUserAsync(user.Id);
        if (tokens is null)
            return RedirectSsoError("token", mobile);

        await _audit.LogAsync(
            "sso.login",
            "User",
            user.Id.ToString(),
            new { provider = cfg.Provider, email },
            ssoState.OrgId,
            actorUserId: user.Id,
            actorEmail: email);

        var exchangeCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        _cache.Set($"sso-ex:{exchangeCode}", tokens, TimeSpan.FromMinutes(3));
        if (mobile)
            return Redirect($"taskmanager://sso-callback?code={exchangeCode}");
        return Redirect($"{_app.PublicBaseUrl.TrimEnd('/')}/sso-callback?code={exchangeCode}");
    }

    private IActionResult RedirectSsoError(string error, bool mobile)
    {
        if (mobile)
            return Redirect($"taskmanager://sso-callback?error={Uri.EscapeDataString(error)}");
        return Redirect($"{_app.PublicBaseUrl.TrimEnd('/')}/login?ssoError={Uri.EscapeDataString(error)}");
    }

    [AllowAnonymous]
    [HttpPost("exchange")]
    public ActionResult<TokenDto> Exchange([FromBody] SsoExchangeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            !_cache.TryGetValue($"sso-ex:{dto.Code}", out TokenDto? tokens) ||
            tokens is null)
        {
            return BadRequest(new { message = "Invalid or expired SSO code." });
        }

        _cache.Remove($"sso-ex:{dto.Code}");
        return Ok(tokens);
    }

    private async Task<bool> EnsureSsoAsync(CancellationToken ct) =>
        _tenant.IsSuperAdmin ||
        await _entitlements.HasFeatureAsync(_tenant.OrganizationId, FeatureKeys.Sso, ct);

    private ObjectResult UpgradeRequired() =>
        StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
        {
            Title = "Upgrade required",
            Detail = "SSO requires Business or higher.",
            Status = StatusCodes.Status402PaymentRequired
        });

    private static OrganizationSsoConfigDto ToDto(OrganizationSsoConfig cfg, string slug) => new()
    {
        Provider = cfg.Provider,
        ClientId = cfg.ClientId,
        HasClientSecret = !string.IsNullOrEmpty(cfg.ClientSecret),
        TenantId = cfg.TenantId,
        AllowedEmailDomains = cfg.AllowedEmailDomains,
        IsEnabled = cfg.IsEnabled,
        AutoProvisionUsers = cfg.AutoProvisionUsers,
        DefaultRole = cfg.DefaultRole,
        LoginStartPath = $"/api/sso/{slug}/start"
    };

    private static bool DomainAllowed(string allowed, string email)
    {
        if (string.IsNullOrWhiteSpace(allowed)) return true;
        var domain = email.Contains('@') ? email.Split('@')[1] : "";
        return allowed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildGoogleAuthUrl(OrganizationSsoConfig cfg, string redirectUri, string state)
    {
        var q = new Dictionary<string, string>
        {
            ["client_id"] = cfg.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account"
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&",
            q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static string BuildMicrosoftAuthUrl(OrganizationSsoConfig cfg, string redirectUri, string state)
    {
        var tenant = string.IsNullOrWhiteSpace(cfg.TenantId) ? "common" : cfg.TenantId;
        var q = new Dictionary<string, string>
        {
            ["client_id"] = cfg.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile User.Read",
            ["state"] = state,
            ["response_mode"] = "query"
        };
        return $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?" + string.Join("&",
            q.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    private async Task<SsoProfile?> ExchangeGoogleAsync(OrganizationSsoConfig cfg, string code, string redirectUri)
    {
        var client = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = cfg.ClientId,
            ["client_secret"] = cfg.ClientSecret ?? "",
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        var tokenRes = await client.PostAsync("https://oauth2.googleapis.com/token", form);
        if (!tokenRes.IsSuccessStatusCode) return null;
        using var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
        var access = tokenDoc.RootElement.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(access)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var profileRes = await client.SendAsync(req);
        if (!profileRes.IsSuccessStatusCode) return null;
        using var profileDoc = JsonDocument.Parse(await profileRes.Content.ReadAsStringAsync());
        return new SsoProfile(
            profileDoc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null,
            profileDoc.RootElement.TryGetProperty("given_name", out var g) ? g.GetString() : null,
            profileDoc.RootElement.TryGetProperty("family_name", out var f) ? f.GetString() : null);
    }

    private async Task<SsoProfile?> ExchangeMicrosoftAsync(OrganizationSsoConfig cfg, string code, string redirectUri)
    {
        var tenant = string.IsNullOrWhiteSpace(cfg.TenantId) ? "common" : cfg.TenantId;
        var client = _httpClientFactory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = cfg.ClientId,
            ["client_secret"] = cfg.ClientSecret ?? "",
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        var tokenRes = await client.PostAsync($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", form);
        if (!tokenRes.IsSuccessStatusCode) return null;
        using var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
        var access = tokenDoc.RootElement.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(access)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var profileRes = await client.SendAsync(req);
        if (!profileRes.IsSuccessStatusCode) return null;
        using var profileDoc = JsonDocument.Parse(await profileRes.Content.ReadAsStringAsync());
        var email = profileDoc.RootElement.TryGetProperty("mail", out var m) ? m.GetString() : null;
        email ??= profileDoc.RootElement.TryGetProperty("userPrincipalName", out var u) ? u.GetString() : null;
        return new SsoProfile(
            email,
            profileDoc.RootElement.TryGetProperty("givenName", out var g) ? g.GetString() : null,
            profileDoc.RootElement.TryGetProperty("surname", out var f) ? f.GetString() : null);
    }

    private sealed record SsoState(int OrgId, string Provider, bool Mobile = false);
    private sealed record SsoProfile(string? Email, string? GivenName, string? FamilyName);
}
