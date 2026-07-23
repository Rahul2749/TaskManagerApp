using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Authorization;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly ApplicationDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApplicationDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? raw = null;
        if (Request.Headers.TryGetValue(HeaderName, out var headerVals))
            raw = headerVals.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw) &&
            Request.Headers.TryGetValue("Authorization", out var authVals))
        {
            var auth = authVals.FirstOrDefault();
            if (auth is not null && auth.StartsWith("Bearer tm_", StringComparison.OrdinalIgnoreCase))
                raw = auth["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("tm_", StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var hash = HashKey(raw);
        var key = await _db.OrganizationApiKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null);

        if (key is null)
            return AuthenticateResult.Fail("Invalid API key.");

        key.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "0"),
            new(ClaimTypes.Name, $"apikey:{key.Id}"),
            new(ClaimTypes.Role, Roles.OrganizationAdmin),
            new("organizationId", key.OrganizationId.ToString()),
            new("api_key_id", key.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    public static string HashKey(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static (string Plaintext, string Prefix, string Hash) GenerateKey()
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "");
        var plaintext = "tm_" + random[..40];
        var prefix = plaintext[..11];
        return (plaintext, prefix, HashKey(plaintext));
    }
}
