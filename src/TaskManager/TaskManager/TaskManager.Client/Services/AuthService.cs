﻿using System.Net.Http.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;
        private UserDto? _currentUser;

        public AuthService(HttpClient httpClient, LocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
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
