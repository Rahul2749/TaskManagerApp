using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(LoginDto loginDto);
    Task LogoutAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<bool> RefreshTokenAsync();
}
