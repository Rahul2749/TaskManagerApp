using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services;

public sealed class TwoFactorService : ITwoFactorService
{
    public const string LoginPurpose = "login";
    public const string EnablePurpose = "enable";
    public const string DisablePurpose = "disable";
    private const int CodeLifetimeMinutes = 5;
    private const int MaxAttempts = 5;
    private const int MaxChallengesPerHour = 5;
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(30);

    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly byte[] _hashKey;
    private readonly TimeProvider _timeProvider;

    public TwoFactorService(
        ApplicationDbContext db,
        IEmailService email,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _db = db;
        _email = email;
        _timeProvider = timeProvider;
        var secret = configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JwtSettings:SecretKey must be configured to use two-factor authentication.");
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public async Task<string> StartChallengeAsync(User user, string purpose, CancellationToken ct = default)
    {
        if (purpose is not (LoginPurpose or EnablePurpose or DisablePurpose))
            throw new ArgumentOutOfRangeException(nameof(purpose));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var recentChallenges = await _db.TwoFactorChallenges.IgnoreQueryFilters()
            .Where(c => c.UserId == user.Id && c.CreatedAt > now.AddHours(-1))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        if (recentChallenges.Count >= MaxChallengesPerHour)
            throw new TwoFactorThrottleException("Too many verification codes were requested. Try again later.");

        var lastChallenge = recentChallenges.FirstOrDefault(c => c.Purpose == purpose && c.UsedAt == null);
        if (lastChallenge is not null && now - lastChallenge.LastSentAt < ResendDelay)
            throw new TwoFactorThrottleException("Please wait 30 seconds before requesting another code.");

        foreach (var prior in recentChallenges.Where(c => c.Purpose == purpose && c.UsedAt == null))
            prior.UsedAt = now;

        var (rawChallenge, challengeHash) = SecureTokenFactory.Create();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _db.TwoFactorChallenges.Add(new TwoFactorChallenge
        {
            UserId = user.Id,
            ChallengeTokenHash = challengeHash,
            CodeHash = HashCode(challengeHash, code),
            Purpose = purpose,
            CreatedAt = now,
            LastSentAt = now,
            ExpiresAt = now.AddMinutes(CodeLifetimeMinutes)
        });
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendTwoFactorCodeAsync(user.Email, user.FirstName, code, ct);
        }
        catch
        {
            await _db.TwoFactorChallenges.IgnoreQueryFilters()
                .Where(c => c.ChallengeTokenHash == challengeHash && c.UsedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, now), ct);
            throw;
        }
        return rawChallenge;
    }

    public async Task<bool> VerifyChallengeAsync(
        string challengeToken,
        string code,
        string purpose,
        CancellationToken ct = default,
        int? expectedUserId = null)
    {
        if (string.IsNullOrWhiteSpace(challengeToken) || string.IsNullOrWhiteSpace(code))
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var challengeHash = SecureTokenFactory.Hash(challengeToken.Trim());
        var challenge = await _db.TwoFactorChallenges.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ChallengeTokenHash == challengeHash && c.Purpose == purpose &&
                                      (expectedUserId == null || c.UserId == expectedUserId), ct);

        if (challenge is null || challenge.UsedAt is not null || challenge.ExpiresAt <= now || challenge.FailedAttempts >= MaxAttempts)
            return false;

        var supplied = code.Trim();
        var validOtp = supplied.Length == 6 && supplied.All(char.IsAsciiDigit) &&
                       FixedTimeEquals(challenge.CodeHash, HashCode(challengeHash, supplied));

        if (validOtp)
        {
            return await _db.TwoFactorChallenges.IgnoreQueryFilters()
                .Where(c => c.Id == challenge.Id && c.UsedAt == null && c.ExpiresAt > now && c.FailedAttempts < MaxAttempts)
                .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, now), ct) == 1;
        }

        if (purpose == LoginPurpose)
        {
            var normalizedRecoveryCode = NormalizeRecoveryCode(supplied);
            if (normalizedRecoveryCode.Length > 0)
            {
                var recoveryHash = SecureTokenFactory.Hash(normalizedRecoveryCode);
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                var recoveryUsed = await _db.TwoFactorRecoveryCodes.IgnoreQueryFilters()
                    .Where(c => c.UserId == challenge.UserId && c.CodeHash == recoveryHash && c.UsedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, now), ct);
                if (recoveryUsed == 1)
                {
                    var challengeUsed = await _db.TwoFactorChallenges.IgnoreQueryFilters()
                        .Where(c => c.Id == challenge.Id && c.UsedAt == null && c.ExpiresAt > now && c.FailedAttempts < MaxAttempts)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, now), ct);
                    if (challengeUsed == 1)
                    {
                        await transaction.CommitAsync(ct);
                        return true;
                    }
                }
                await transaction.RollbackAsync(ct);
            }
        }

        await _db.TwoFactorChallenges.IgnoreQueryFilters()
            .Where(c => c.Id == challenge.Id && c.UsedAt == null && c.FailedAttempts < MaxAttempts)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1), ct);
        return false;
    }

    public async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(int userId, CancellationToken ct = default)
    {
        var existing = await _db.TwoFactorRecoveryCodes.IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
        _db.TwoFactorRecoveryCodes.RemoveRange(existing);

        var codes = Enumerable.Range(0, 8)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(10)))
            .Select(value => string.Join('-', Enumerable.Range(0, 4).Select(i => value.Substring(i * 5, 5))))
            .ToArray();

        foreach (var code in codes)
        {
            _db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                UserId = userId,
                CodeHash = SecureTokenFactory.Hash(NormalizeRecoveryCode(code)),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }

        await _db.SaveChangesAsync(ct);
        return codes;
    }

    public Task<int> GetRecoveryCodesRemainingAsync(int userId, CancellationToken ct = default) =>
        _db.TwoFactorRecoveryCodes.IgnoreQueryFilters()
            .CountAsync(c => c.UserId == userId && c.UsedAt == null, ct);

    private string HashCode(string challengeHash, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(_hashKey, Encoding.UTF8.GetBytes($"{challengeHash}:{code}")));

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Convert.FromHexString(left);
        var rightBytes = Convert.FromHexString(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string NormalizeRecoveryCode(string code) =>
        new(code.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}

public sealed class TwoFactorThrottleException(string message) : Exception(message) { }
