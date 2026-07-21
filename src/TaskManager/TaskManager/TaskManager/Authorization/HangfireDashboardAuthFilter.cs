using Hangfire.Dashboard;

namespace TaskManager.Authorization;

/// <summary>
/// Hangfire dashboard access: open in Development; SuperAdmin JWT/cookie in Production.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var env = http.RequestServices.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
            return true;

        return http.User.Identity?.IsAuthenticated == true
               && http.User.IsInRole("SuperAdmin");
    }
}
