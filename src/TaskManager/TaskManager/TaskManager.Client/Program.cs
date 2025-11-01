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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApiService, ApiService>();

await builder.Build().RunAsync();