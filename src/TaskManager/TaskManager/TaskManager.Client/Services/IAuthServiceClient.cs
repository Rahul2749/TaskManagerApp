using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public interface IAuthServiceClient
    {
        Task<bool> LoginAsync(LoginDto loginDto);
        Task<(bool Success, string? Error)> RegisterAsync(WorkspaceRegistrationDto registration);
        Task LogoutAsync();
        Task<bool> RefreshTokenAsync();
        Task<UserDto?> GetCurrentUserAsync();
        Task<bool> IsAuthenticatedAsync();
    }
}
