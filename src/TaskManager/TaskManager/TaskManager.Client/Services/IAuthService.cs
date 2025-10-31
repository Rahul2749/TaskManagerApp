using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public interface IAuthService
    {
        Task<bool> LoginAsync(LoginDto loginDto);
        Task LogoutAsync();
        Task<bool> RefreshTokenAsync();
        Task<UserDto?> GetCurrentUserAsync();
        Task<bool> IsAuthenticatedAsync();
    }
}
