using TaskManager.Models;

namespace TaskManager.Services;

public interface ITwoFactorService
{
    Task<string> StartChallengeAsync(User user, string purpose, CancellationToken ct = default);
    Task<bool> VerifyChallengeAsync(string challengeToken, string code, string purpose, CancellationToken ct = default, int? expectedUserId = null);
    Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(int userId, CancellationToken ct = default);
    Task<int> GetRecoveryCodesRemainingAsync(int userId, CancellationToken ct = default);
}
