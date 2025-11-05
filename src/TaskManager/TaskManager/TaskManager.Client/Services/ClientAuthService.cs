using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public class ClientAuthService : IAuthServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;
        private readonly CustomAuthStateProvider _authStateProvider;
        private UserDto? _currentUser;

        public ClientAuthService(
            HttpClient httpClient,
            LocalStorageService localStorage,
            AuthenticationStateProvider authStateProvider)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
        }

        public async Task<bool> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenDto>();

                    if (tokenResponse != null)
                    {
                        await _localStorage.SetItemAsync("accessToken", tokenResponse.AccessToken);
                        await _localStorage.SetItemAsync("refreshToken", tokenResponse.RefreshToken);
                        await _localStorage.SetItemAsync("currentUser", tokenResponse.User);
                        _currentUser = tokenResponse.User;

                        // Set the authorization header
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

                        // Notify authentication state changed
                        _authStateProvider.NotifyAuthenticationStateChanged();

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                var token = await _localStorage.GetItemAsync<string>("accessToken");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    await _httpClient.PostAsync("api/auth/logout", null);
                }
            }
            catch
            {
                // Ignore errors during logout
            }
            finally
            {
                await _localStorage.RemoveItemAsync("accessToken");
                await _localStorage.RemoveItemAsync("refreshToken");
                await _localStorage.RemoveItemAsync("currentUser");
                _currentUser = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;

                // Notify authentication state changed
                _authStateProvider.NotifyAuthenticationStateChanged();
            }
        }

        public async Task<bool> RefreshTokenAsync()
        {
            try
            {
                var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");

                if (string.IsNullOrEmpty(refreshToken))
                    return false;

                var response = await _httpClient.PostAsJsonAsync("api/auth/refresh",
                    new RefreshTokenDto { RefreshToken = refreshToken });

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenDto>();

                    if (tokenResponse != null)
                    {
                        await _localStorage.SetItemAsync("accessToken", tokenResponse.AccessToken);
                        await _localStorage.SetItemAsync("refreshToken", tokenResponse.RefreshToken);
                        await _localStorage.SetItemAsync("currentUser", tokenResponse.User);
                        _currentUser = tokenResponse.User;

                        // Set the authorization header
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

                        // Notify authentication state changed
                        _authStateProvider.NotifyAuthenticationStateChanged();

                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UserDto?> GetCurrentUserAsync()
        {
            if (_currentUser != null)
                return _currentUser;

            _currentUser = await _localStorage.GetItemAsync<UserDto>("currentUser");
            return _currentUser;
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken");
            return !string.IsNullOrEmpty(token);
        }
    }
}