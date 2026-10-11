using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Client.Services
{
    public class ClientAuthService : IAuthServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;
        private readonly CustomAuthStateProvider _authStateProvider;
        private readonly IEntitlementState _entitlements;
        private UserDto? _currentUser;
        private long _userSessionVersion;

        public ClientAuthService(
            HttpClient httpClient,
            LocalStorageService localStorage,
            AuthenticationStateProvider authStateProvider,
            IEntitlementState entitlements)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authStateProvider = (CustomAuthStateProvider)authStateProvider;
            _entitlements = entitlements;
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

                if (!response.IsSuccessStatusCode) return null;
                var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
                if (result?.Tokens is not null) await StoreTokenAsync(result.Tokens);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> VerifyLoginTwoFactorAsync(string challengeToken, string code)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/login/verify",
                    new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
                if (!response.IsSuccessStatusCode) return false;
                var tokens = await response.Content.ReadFromJsonAsync<TokenDto>();
                if (tokens is null) return false;
                await StoreTokenAsync(tokens);
                await _localStorage.RemoveItemAsync("pendingLoginChallenge");
                return true;
            }
            catch { return false; }
        }

        public async Task SetPendingLoginChallengeAsync(string challengeToken) =>
            await _localStorage.SetItemAsync("pendingLoginChallenge", challengeToken);

        public Task<string?> GetPendingLoginChallengeAsync() =>
            _localStorage.GetItemAsync<string>("pendingLoginChallenge");

        public async Task ClearPendingLoginChallengeAsync() =>
            await _localStorage.RemoveItemAsync("pendingLoginChallenge");

        public async Task<(bool Success, string? Error)> RegisterAsync(WorkspaceRegistrationDto registration)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", registration);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenDto>();
                    if (tokenResponse is not null)
                    {
                        await StoreTokenAsync(tokenResponse);
                        return (true, null);
                    }
                }

                return (false, await ReadErrorAsync(response));
            }
            catch
            {
                return (false, "Registration is temporarily unavailable. Please try again later.");
            }
        }

        public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/auth/forgot-password",
                    new ForgotPasswordDto { Email = email });

                if (response.IsSuccessStatusCode)
                    return (true, null);

                return (false, await ReadErrorAsync(response));
            }
            catch
            {
                return (false, "Unable to send reset email right now. Please try again later.");
            }
        }

        public async Task<(bool Success, string? Error)> ResetPasswordAsync(string token, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/auth/reset-password",
                    new ResetPasswordDto { Token = token, Password = password });

                if (response.IsSuccessStatusCode)
                    return (true, null);

                return (false, await ReadErrorAsync(response));
            }
            catch
            {
                return (false, "Unable to reset password right now. Please try again later.");
            }
        }

        public async Task<(bool Success, string? Error)> AcceptInviteAsync(AcceptInviteDto dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/invites/accept", dto);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenDto>();
                    if (tokenResponse is not null)
                    {
                        await StoreTokenAsync(tokenResponse);
                        return (true, null);
                    }
                }

                return (false, await ReadErrorAsync(response));
            }
            catch
            {
                return (false, "Unable to accept invite right now. Please try again later.");
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
                _entitlements.Clear();
                await _authStateProvider.UpdateSessionAsync(null, async () =>
                {
                    _userSessionVersion++;
                    await _localStorage.RemoveItemAsync("accessToken");
                    await _localStorage.RemoveItemAsync("refreshToken");
                    await _localStorage.RemoveItemAsync("currentUser");
                    _currentUser = null;
                });
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
                        await StoreTokenAsync(tokenResponse);

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

            var version = _userSessionVersion;
            var user = await _localStorage.GetItemAsync<UserDto>("currentUser");
            if (version != _userSessionVersion) return _currentUser;
            _currentUser = user;
            return _currentUser;
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken");
            return !string.IsNullOrEmpty(token);
        }

        public async Task<LoginResponseDto?> CompleteSsoLoginAsync(string exchangeCode)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/sso/exchange", new SsoExchangeDto { Code = exchangeCode });
                if (!response.IsSuccessStatusCode) return null;
                var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
                if (result?.Tokens is not null) await StoreTokenAsync(result.Tokens);
                return result;
            }
            catch
            {
                return null;
            }
        }

        public async Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync()
        {
            try { return await _httpClient.GetFromJsonAsync<TwoFactorStatusDto>("api/auth/two-factor"); }
            catch { return null; }
        }

        public async Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorEnableAsync(string password) =>
            await StartTwoFactorChallengeAsync("api/auth/two-factor/enable/start", password);

        public async Task<(TwoFactorRecoveryCodesDto? RecoveryCodes, string? Error)> VerifyTwoFactorEnableAsync(string challengeToken, string code)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/enable/verify",
                    new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
                if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));
                var result = await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>();
                if (result?.Session is not null) await StoreTokenAsync(result.Session);
                return (result, null);
            }
            catch { return (null, "Could not verify the code. Please try again."); }
        }

        public async Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorDisableAsync(string password) =>
            await StartTwoFactorChallengeAsync("api/auth/two-factor/disable/start", password);

        public async Task<(bool Success, string? Error)> VerifyTwoFactorDisableAsync(string challengeToken, string code)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/disable/verify",
                    new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
                if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response));
                var result = await response.Content.ReadFromJsonAsync<TwoFactorUpdateDto>();
                if (result?.Session is not null) await StoreTokenAsync(result.Session);
                return (true, null);
            }
            catch { return (false, "Could not verify the code. Please try again."); }
        }

        private async Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorChallengeAsync(string endpoint, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, new TwoFactorPasswordDto { Password = password });
                if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));
                return (await response.Content.ReadFromJsonAsync<TwoFactorChallengeDto>(), null);
            }
            catch { return (null, "Could not send a verification code. Please try again."); }
        }

        private async Task StoreTokenAsync(TokenDto tokenResponse)
        {
            await _authStateProvider.UpdateSessionAsync(tokenResponse.AccessToken, async () =>
            {
                _userSessionVersion++;
                if (_currentUser?.Id != tokenResponse.User.Id)
                    _entitlements.Clear();
                await _localStorage.SetItemAsync("accessToken", tokenResponse.AccessToken);
                await _localStorage.SetItemAsync("refreshToken", tokenResponse.RefreshToken);
                await _localStorage.SetItemAsync("currentUser", tokenResponse.User);
                _currentUser = tokenResponse.User;
            });
        }

        private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;
                    if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                        return detail.GetString() ?? "Request failed.";
                    if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                        return message.GetString() ?? "Request failed.";
                    if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        return title.GetString() ?? "Request failed.";
                }
                catch
                {
                    // fall through
                }
            }

            return "Request failed. Please review your details and try again.";
        }
    }
}
