using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public interface IAuthServiceClient
    {
        Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
        Task<bool> VerifyLoginTwoFactorAsync(string challengeToken, string code);
        Task SetPendingLoginChallengeAsync(string challengeToken);
        Task<string?> GetPendingLoginChallengeAsync();
        Task ClearPendingLoginChallengeAsync();
        Task<(bool Success, string? Error)> RegisterAsync(WorkspaceRegistrationDto registration);
        Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);
        Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string password);
        Task<(bool Success, string? Error)> AcceptInviteAsync(AcceptInviteDto dto);
        Task LogoutAsync();
        Task<bool> RefreshTokenAsync();
        Task<UserDto?> GetCurrentUserAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<LoginResponseDto?> CompleteSsoLoginAsync(string exchangeCode);
        Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync();
        Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorEnableAsync(string password);
        Task<(TwoFactorRecoveryCodesDto? RecoveryCodes, string? Error)> VerifyTwoFactorEnableAsync(string challengeToken, string code);
        Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorDisableAsync(string password);
        Task<(bool Success, string? Error)> VerifyTwoFactorDisableAsync(string challengeToken, string code);
    }
}
