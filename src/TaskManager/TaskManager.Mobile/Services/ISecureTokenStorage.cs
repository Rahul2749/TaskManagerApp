using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public interface ISecureTokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task SaveSessionAsync(TokenDto token);
    Task ClearAsync();
    Task<bool> HasSessionAsync();
}
