using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public interface IAuthServiceClient
    {
        Task<bool> LoginAsync(LoginDto loginDto);
        Task<(bool Success, string? Error)> RegisterAsync(WorkspaceRegistrationDto registration);
        Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);
        Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string password);
        Task<(bool Success, string? Error)> AcceptInviteAsync(AcceptInviteDto dto);
        Task LogoutAsync();
        Task<bool> RefreshTokenAsync();
        Task<UserDto?> GetCurrentUserAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<bool> CompleteSsoLoginAsync(string exchangeCode);
    }
}
