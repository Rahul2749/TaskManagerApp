using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using TaskManager.Client.Services;
using TaskManager.Shared.DTOs;

internal static class LoginSessionRaceChecks
{
    private static TokenDto Session() => new()
    {
        AccessToken = Token(DateTimeOffset.UtcNow.AddHours(1)), RefreshToken = "new-refresh",
        User = new() { Id = 20, Username = "member", Role = "Manager" }
    };
    private static string Token(DateTimeOffset expiry) => "header." + Convert.ToBase64String(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { sub = "20", role = "Manager", exp = expiry.ToUnixTimeSeconds() })))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
    private static (ClientAuthService Auth, CustomAuthStateProvider Provider, HttpClient Http) Create(IJSRuntime js)
    {
        var http = new HttpClient(new TestHttpHandler(_ => new(System.Net.HttpStatusCode.OK)
        { Content = JsonContent.Create(new LoginResponseDto { Tokens = Session() }) })) { BaseAddress = new("https://regression.example/") };
        var storage = new LocalStorageService(js);
        var provider = new CustomAuthStateProvider(http, storage);
        var entitlements = Fake<IEntitlementState>.Create((method, _) => method.Name == "Clear" ? null : throw new InvalidOperationException(method.Name));
        return (new ClientAuthService(http, storage, provider, entitlements), provider, http);
    }

    public static async Task OldUserReadAsync(Action<bool, string> check)
    {
        var values = new Dictionary<string, string>();
        var oldRead = new TaskCompletionSource<string>();
        var js = Fake<IJSRuntime>.Create((_, args) =>
        {
            var operation = (string)args![0]!;
            var parameters = (object?[])args[^1]!;
            var key = (string)parameters[0]!;
            if (operation == "localStorage.getItem" && key == "currentUser") return new ValueTask<string>(oldRead.Task);
            if (operation == "localStorage.setItem") values[key] = (string)parameters[1]!;
            return new ValueTask<IJSVoidResult>();
        });
        var (auth, _, http) = Create(js);
        using (http)
        {
            var startupUser = auth.GetCurrentUserAsync();
            var login = await auth.LoginAsync(new() { Username = "member", Password = "test" });
            check(login?.Tokens?.User?.Id == 20, "login fixture failed");
            oldRead.SetResult(null!);
            check((await startupUser)?.Id == 20, "old user read returned the pre-login session");
            check((await auth.GetCurrentUserAsync())?.Id == 20, "old user read overwrote the new cached user");
        }
    }

    public static async Task ExpiredCleanupAsync(Action<bool, string> check)
    {
        var values = new Dictionary<string, string> { ["accessToken"] = JsonSerializer.Serialize(Token(DateTimeOffset.UtcNow.AddHours(-1))),
            ["refreshToken"] = "old-refresh", ["currentUser"] = "old-user" };
        var releaseCleanup = new TaskCompletionSource<bool>();
        var cleanupStarted = false;
        async Task<IJSVoidResult> DelayedRemove(string key)
        {
            cleanupStarted = true;
            await releaseCleanup.Task;
            values.Remove(key);
            return default!;
        }
        var js = Fake<IJSRuntime>.Create((_, args) =>
        {
            var operation = (string)args![0]!;
            var parameters = (object?[])args[^1]!;
            var key = (string)parameters[0]!;
            if (operation == "localStorage.getItem") return new ValueTask<string>(values.GetValueOrDefault(key)!);
            if (operation == "localStorage.setItem") values[key] = (string)parameters[1]!;
            if (operation == "localStorage.removeItem")
            {
                if (key == "refreshToken") return new ValueTask<IJSVoidResult>(DelayedRemove(key));
                values.Remove(key);
            }
            return new ValueTask<IJSVoidResult>();
        });
        var (auth, provider, http) = Create(js);
        using (http)
        {
            var startup = provider.GetAuthenticationStateAsync();
            check(cleanupStarted, "expired cleanup fixture did not start");
            var login = auth.LoginAsync(new() { Username = "member", Password = "test" });
            releaseCleanup.SetResult(true);
            await Task.WhenAll(startup, login);
            check((await login)?.Tokens?.User?.Id == 20, "login fixture failed");
            check(values.ContainsKey("accessToken") && values.ContainsKey("refreshToken") && values.ContainsKey("currentUser"), "expired cleanup deleted the newly saved session");
            check((await provider.GetAuthenticationStateAsync()).User.IsInRole("Manager"), "new session lost authentication");
        }
    }
}
