using Microsoft.AspNetCore.Components.Authorization;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace TaskManager.Client.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;

        public CustomAuthStateProvider(
            HttpClient httpClient,
            LocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        private long _sessionVersion;
        private AuthenticationState _publishedState = Anonymous();
        private readonly SemaphoreSlim _sessionGate = new(1, 1);

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var version = _sessionVersion;
            var token = await _localStorage.GetItemAsync<string>("accessToken");
            await _sessionGate.WaitAsync();
            try
            {
                // An earlier read must not replace or clear a session saved while it was pending.
                if (version != _sessionVersion) return _publishedState;
                AuthenticationState state;
                try { state = ReadToken(token); }
                catch { state = Anonymous(); }
                if (state.User.Identity?.IsAuthenticated != true && !string.IsNullOrEmpty(token))
                    await ClearAuthenticationAsync();
                _httpClient.DefaultRequestHeaders.Authorization = state.User.Identity?.IsAuthenticated == true
                    ? new AuthenticationHeaderValue("Bearer", token) : null;
                return state;
            }
            finally { _sessionGate.Release(); }
        }

        public async Task UpdateSessionAsync(string? token, Func<Task> persistSession)
        {
            // Validate before changing storage; serialize writes with expired-session cleanup.
            _ = ReadToken(token);
            await _sessionGate.WaitAsync();
            try
            {
                _sessionVersion++;
                await persistSession();
                PublishToken(token);
            }
            finally { _sessionGate.Release(); }
        }

        private void PublishToken(string? token)
        {
            var state = ReadToken(token);
            _sessionVersion++;
            _publishedState = state;
            _httpClient.DefaultRequestHeaders.Authorization = state.User.Identity?.IsAuthenticated == true
                ? new AuthenticationHeaderValue("Bearer", token) : null;
            // Publish the already parsed state, without another asynchronous JS storage read.
            NotifyAuthenticationStateChanged(Task.FromResult(state));
        }

        private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

        private static AuthenticationState ReadToken(string? token)
        {
            if (string.IsNullOrEmpty(token)) return Anonymous();
            // Browser claims drive UI only; API signature validation remains on the server.
            var parts = token.Split('.');
            if (parts.Length != 3) throw new FormatException("Invalid token format.");
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = document.RootElement;
            if (!root.TryGetProperty("exp", out var expiry) || !expiry.TryGetInt64(out var seconds)
                || DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow)
                return Anonymous();
            var claims = new List<Claim>();
            foreach (var property in root.EnumerateObject())
            {
                var type = property.Name switch
                {
                    "role" => ClaimTypes.Role,
                    "sub" => ClaimTypes.NameIdentifier,
                    "unique_name" => ClaimTypes.Name,
                    "email" => ClaimTypes.Email,
                    _ => property.Name
                };
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var value in property.Value.EnumerateArray())
                        claims.Add(new Claim(type, value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText()));
                }
                else if (property.Value.ValueKind != JsonValueKind.Null)
                    claims.Add(new Claim(type, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText()));
            }
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt")));
        }

        private async Task ClearAuthenticationAsync()
        {
            await _localStorage.RemoveItemAsync("accessToken");
            await _localStorage.RemoveItemAsync("refreshToken");
            await _localStorage.RemoveItemAsync("currentUser");
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}
