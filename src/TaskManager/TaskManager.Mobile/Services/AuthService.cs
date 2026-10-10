using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public class AuthService : IAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISecureTokenStorage _storage;

    public AuthService(HttpClient httpClient, ISecureTokenStorage storage)
    {
        _httpClient = httpClient;
        _storage = storage;
    }

    public async Task<AuthResult> LoginAsync(LoginDto loginDto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);
        return await ReadLoginResultAsync(response, "Invalid username or password.");
    }

    public async Task<AuthResult> VerifyLoginTwoFactorAsync(string challengeToken, string code)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/login/verify",
            new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
        return await ReadTokenResultAsync(response, "The verification code is invalid or expired.");
    }

    public async Task<TwoFactorStatusDto?> GetTwoFactorStatusAsync()
    {
        try { return await _httpClient.GetFromJsonAsync<TwoFactorStatusDto>("api/auth/two-factor", JsonOptions); }
        catch { return null; }
    }

    public Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorEnableAsync(string password) =>
        StartTwoFactorChallengeAsync("api/auth/two-factor/enable/start", password);

    public async Task<(TwoFactorRecoveryCodesDto? RecoveryCodes, string? Error)> VerifyTwoFactorEnableAsync(string challengeToken, string code)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/enable/verify",
            new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
        if (!response.IsSuccessStatusCode) return (null, await ReadErrorDetailAsync(response));
        var result = await response.Content.ReadFromJsonAsync<TwoFactorRecoveryCodesDto>(JsonOptions);
        if (result?.Session is not null) await _storage.SaveSessionAsync(result.Session);
        return (result, null);
    }

    public Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorDisableAsync(string password) =>
        StartTwoFactorChallengeAsync("api/auth/two-factor/disable/start", password);

    public async Task<(bool Success, string? Error)> VerifyTwoFactorDisableAsync(string challengeToken, string code)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/two-factor/disable/verify",
            new VerifyTwoFactorDto { ChallengeToken = challengeToken, Code = code });
        if (!response.IsSuccessStatusCode) return (false, await ReadErrorDetailAsync(response));
        var result = await response.Content.ReadFromJsonAsync<TwoFactorUpdateDto>(JsonOptions);
        if (result?.Session is not null) await _storage.SaveSessionAsync(result.Session);
        return (true, null);
    }

    private async Task<(TwoFactorChallengeDto? Challenge, string? Error)> StartTwoFactorChallengeAsync(string endpoint, string password)
    {
        var response = await _httpClient.PostAsJsonAsync(endpoint, new TwoFactorPasswordDto { Password = password });
        if (!response.IsSuccessStatusCode) return (null, await ReadErrorDetailAsync(response));
        return (await response.Content.ReadFromJsonAsync<TwoFactorChallengeDto>(JsonOptions), null);
    }

    public async Task<AuthResult> RegisterAsync(WorkspaceRegistrationDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", dto);
        return await ReadTokenResultAsync(response, "Could not create workspace.");
    }

    public async Task<AuthResult> AcceptInviteAsync(AcceptInviteDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invites/accept", dto);
        return await ReadTokenResultAsync(response, "Could not accept invite.");
    }

    public async Task<(bool Ok, string Message)> ForgotPasswordAsync(string email)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password",
            new ForgotPasswordDto { Email = email });
        var message = await ReadMessageAsync(response)
                      ?? "If that email exists, a reset link was sent.";
        return (response.IsSuccessStatusCode, message);
    }

    public async Task<(bool Ok, string Message)> ResetPasswordAsync(string token, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password",
            new ResetPasswordDto { Token = token, Password = password });
        var message = await ReadMessageAsync(response)
                      ?? (response.IsSuccessStatusCode
                          ? "Password updated. You can sign in now."
                          : "Reset link is invalid or expired.");
        return (response.IsSuccessStatusCode, message);
    }

    public async Task<InvitePreviewDto?> PreviewInviteAsync(string token)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<InvitePreviewDto>(
                $"api/invites/preview?token={Uri.EscapeDataString(token)}",
                JsonOptions);
        }
        catch
        {
            return null;
        }
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

    public async Task<AuthResult> CompleteSsoLoginAsync(string exchangeCode)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sso/exchange",
            new SsoExchangeDto { Code = exchangeCode });
        return await ReadLoginResultAsync(response, "SSO sign-in failed or expired.");
    }

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

        var token = await response.Content.ReadFromJsonAsync<TokenDto>(JsonOptions);
        if (token == null)
            return false;

        await _storage.SaveSessionAsync(token);
        return true;
    }

    private async Task<AuthResult> ReadTokenResultAsync(HttpResponseMessage response, string fallbackError)
    {
        if (response.IsSuccessStatusCode)
        {
            var token = await response.Content.ReadFromJsonAsync<TokenDto>(JsonOptions);
            if (token == null)
                return AuthResult.Fail(fallbackError);

            await _storage.SaveSessionAsync(token);
            return AuthResult.Ok(token.User);
        }

        var detail = await ReadErrorDetailAsync(response);
        return AuthResult.Fail(detail ?? fallbackError);
    }

    private async Task<AuthResult> ReadLoginResultAsync(HttpResponseMessage response, string fallbackError)
    {
        if (!response.IsSuccessStatusCode)
            return AuthResult.Fail(await ReadErrorDetailAsync(response) ?? fallbackError);

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions);
        if (result is null) return AuthResult.Fail(fallbackError);
        if (result.RequiresTwoFactor && !string.IsNullOrWhiteSpace(result.ChallengeToken))
            return AuthResult.TwoFactor(result.ChallengeToken);
        if (result.Tokens is null) return AuthResult.Fail(fallbackError);
        await _storage.SaveSessionAsync(result.Tokens);
        return AuthResult.Ok(result.Tokens.User);
    }

    private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("message", out var message))
                return message.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static async Task<string?> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text))
                return MapStatus(response.StatusCode);

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail))
                return detail.GetString();
            if (root.TryGetProperty("title", out var title))
                return title.GetString();
            if (root.TryGetProperty("message", out var message))
                return message.GetString();
        }
        catch
        {
            // ignore
        }

        return MapStatus(response.StatusCode);
    }

    private static string MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Conflict => "Username or email is already in use.",
        HttpStatusCode.PaymentRequired => "Seat limit reached for this workspace.",
        HttpStatusCode.BadRequest => "Invalid request. Check your details and try again.",
        HttpStatusCode.NotFound => "Not found.",
        _ => "Request failed. Please try again."
    };
}
