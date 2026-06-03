using System.Text.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public class SecureTokenStorage : ISecureTokenStorage
{
    private const string AccessTokenKey = "accessToken";
    private const string RefreshTokenKey = "refreshToken";
    private const string CurrentUserKey = "currentUser";

    public Task<string?> GetAccessTokenAsync() =>
        SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() =>
        SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var json = await SecureStorage.Default.GetAsync(CurrentUserKey);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<UserDto>(json);
    }

    public async Task SaveSessionAsync(TokenDto token)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, token.AccessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, token.RefreshToken);
        await SecureStorage.Default.SetAsync(CurrentUserKey, JsonSerializer.Serialize(token.User));
    }

    public async Task ClearAsync()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(CurrentUserKey);
        await Task.CompletedTask;
    }

    public async Task<bool> HasSessionAsync()
    {
        var token = await GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}
