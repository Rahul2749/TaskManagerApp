using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISecureTokenStorage _storage;

    public AuthService(HttpClient httpClient, ISecureTokenStorage storage)
    {
        _httpClient = httpClient;
        _storage = storage;
    }

    public async Task<bool> LoginAsync(LoginDto loginDto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);
        if (!response.IsSuccessStatusCode)
            return false;

        var token = await response.Content.ReadFromJsonAsync<TokenDto>();
        if (token == null)
            return false;

        await _storage.SaveSessionAsync(token);
        return true;
    }

    public async Task LogoutAsync()
    {
        try
        {
            var accessToken = await _storage.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(accessToken))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                await _httpClient.SendAsync(request);
            }
        }
        catch
        {
            // Best-effort server logout
        }
        finally
        {
            await _storage.ClearAsync();
        }
    }

    public Task<UserDto?> GetCurrentUserAsync() => _storage.GetCurrentUserAsync();

    public async Task<bool> RefreshTokenAsync()
    {
        var refreshToken = await _storage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        var response = await _httpClient.PostAsJsonAsync("api/auth/refresh",
            new RefreshTokenDto { RefreshToken = refreshToken });

        if (!response.IsSuccessStatusCode)
        {
            await _storage.ClearAsync();
            return false;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenDto>();
        if (token == null)
            return false;

        await _storage.SaveSessionAsync(token);
        return true;
    }
}
