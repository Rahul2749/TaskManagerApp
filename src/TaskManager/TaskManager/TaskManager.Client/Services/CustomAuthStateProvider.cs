using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
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

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken");

            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // Set the token in the HttpClient
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            try
            {
                // Parse JWT token to get claims
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // Check if token is expired
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    await ClearAuthenticationAsync();
                    return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
                }

                // Extract and normalize claims
                var claims = new List<Claim>();

                foreach (var claim in jwtToken.Claims)
                {
                    // Map role claim properly - JWT uses "role" but .NET expects ClaimTypes.Role
                    if (claim.Type == "role" || claim.Type == ClaimTypes.Role)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, claim.Value));
                    }
                    // Map name identifier
                    else if (claim.Type == JwtRegisteredClaimNames.Sub)
                    {
                        claims.Add(new Claim(ClaimTypes.NameIdentifier, claim.Value));
                    }
                    // Map username
                    else if (claim.Type == JwtRegisteredClaimNames.UniqueName || claim.Type == ClaimTypes.Name)
                    {
                        claims.Add(new Claim(ClaimTypes.Name, claim.Value));
                    }
                    // Map email
                    else if (claim.Type == JwtRegisteredClaimNames.Email || claim.Type == ClaimTypes.Email)
                    {
                        claims.Add(new Claim(ClaimTypes.Email, claim.Value));
                    }
                    // Keep other claims as-is
                    else
                    {
                        claims.Add(claim);
                    }
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                var principal = new ClaimsPrincipal(identity);

                return new AuthenticationState(principal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auth error: {ex.Message}");
                await ClearAuthenticationAsync();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public void NotifyAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
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