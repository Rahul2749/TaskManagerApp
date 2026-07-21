using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;

namespace TaskManager.Services.Billing;

/// <summary>
/// Creates missing Razorpay Plan objects for paid catalog tiers and stores the
/// provider plan IDs on our <see cref="Models.Plan"/> rows so checkout can run.
/// Safe to call repeatedly — already-linked plans are left unchanged.
/// </summary>
public sealed class RazorpayPlanSyncService
{
    private readonly ApplicationDbContext _context;
    private readonly IBillingProvider _provider;
    private readonly ILogger<RazorpayPlanSyncService> _logger;

    public RazorpayPlanSyncService(
        ApplicationDbContext context,
        IBillingProvider provider,
        ILogger<RazorpayPlanSyncService> logger)
    {
        _context = context;
        _provider = provider;
        _logger = logger;
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        if (!_provider.IsConfigured || _provider is not RazorpayBillingProvider razorpay)
        {
            _logger.LogInformation("Skipping Razorpay plan sync; provider is not configured");
            return;
        }

        var localPlans = await _context.Plans.ToListAsync(ct);
        var changed = false;

        foreach (var def in PlanCatalog.Plans.Where(p => !p.IsCustomPricing && p.MonthlyPricePerSeat > 0))
        {
            var plan = localPlans.FirstOrDefault(p => p.Code == def.Code);
            if (plan is null)
                continue;

            if (string.IsNullOrWhiteSpace(plan.ProviderMonthlyPlanId) && def.MonthlyPricePerSeat > 0)
            {
                plan.ProviderMonthlyPlanId = await razorpay.CreatePlanAsync(
                    name: $"{def.Name} Monthly",
                    amountPaise: ToPaise(def.MonthlyPricePerSeat),
                    currency: def.Currency,
                    period: "monthly",
                    interval: 1,
                    description: $"{def.Code}:monthly",
                    ct);
                plan.UpdatedAt = DateTime.UtcNow;
                changed = true;
                _logger.LogInformation("Linked {PlanCode} monthly -> {ProviderPlanId}", def.Code, plan.ProviderMonthlyPlanId);
            }

            if (string.IsNullOrWhiteSpace(plan.ProviderAnnualPlanId) && def.AnnualPricePerSeat > 0)
            {
                plan.ProviderAnnualPlanId = await razorpay.CreatePlanAsync(
                    name: $"{def.Name} Annual",
                    amountPaise: ToPaise(def.AnnualPricePerSeat),
                    currency: def.Currency,
                    period: "yearly",
                    interval: 1,
                    description: $"{def.Code}:annual",
                    ct);
                plan.UpdatedAt = DateTime.UtcNow;
                changed = true;
                _logger.LogInformation("Linked {PlanCode} annual -> {ProviderPlanId}", def.Code, plan.ProviderAnnualPlanId);
            }
        }

        if (changed)
            await _context.SaveChangesAsync(ct);
    }

    private static long ToPaise(decimal rupees) =>
        (long)decimal.Round(rupees * 100m, 0, MidpointRounding.AwayFromZero);
}
