using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Services;
using TaskManager.Services.Billing;

namespace TaskManager.Authorization
{
    /// <summary>
    /// Blocks an endpoint unless the caller's organization is entitled to a feature.
    /// SuperAdmin bypasses the check. Returns 402 Payment Required with a hint to upgrade.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequiresFeatureAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _featureKey;

        public RequiresFeatureAttribute(string featureKey) => _featureKey = featureKey;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;
            var tenant = services.GetRequiredService<ITenantService>();

            if (tenant.IsSuperAdmin)
            {
                await next();
                return;
            }

            var entitlements = services.GetRequiredService<IEntitlementService>();
            var allowed = await entitlements.HasFeatureAsync(tenant.OrganizationId, _featureKey, context.HttpContext.RequestAborted);

            if (!allowed)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Upgrade required",
                    Detail = $"Your current plan does not include this feature ({_featureKey}). Upgrade your plan to unlock it.",
                    Status = StatusCodes.Status402PaymentRequired,
                    Type = "https://taskmanager/errors/upgrade-required"
                })
                {
                    StatusCode = StatusCodes.Status402PaymentRequired
                };
                return;
            }

            await next();
        }
    }
}
