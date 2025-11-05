using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TaskManager.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// HttpClient with base address
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register CLIENT services (these run in the browser)
builder.Services.AddScoped<LocalStorageService>();

// Add Authorization FIRST
builder.Services.AddAuthorizationCore();

// Register AuthenticationStateProvider
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthStateProvider>());

// Then register other services that depend on it
builder.Services.AddScoped<IAuthServiceClient, ClientAuthService>();
builder.Services.AddScoped<IApiService, ApiService>();

await builder.Build().RunAsync();