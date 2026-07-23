using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Services.Billing
{
    public class EntitlementService : IEntitlementService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EntitlementService> _logger;
        private readonly AppOptions _app;

        public EntitlementService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<EntitlementService> logger,
            IOptions<AppOptions> app)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _app = app.Value;
        }

        public async Task<PlanDefinition> GetPlanAsync(int? organizationId, CancellationToken ct = default)
        {
            // Platform-wide (SuperAdmin / no tenant) sees everything.
            if (organizationId is null)
                return PlanCatalog.GetByCode(PlanCodes.Enterprise)!;

            if (_cache.TryGetValue(CacheKey(organizationId.Value), out PlanDefinition? cached) && cached is not null)
                return cached;

            var plan = await ResolvePlanAsync(organizationId.Value, ct);
            _cache.Set(CacheKey(organizationId.Value), plan, CacheTtl);
            return plan;
        }

        public async Task<bool> HasFeatureAsync(int? organizationId, string featureKey, CancellationToken ct = default)
        {
            var plan = await GetPlanAsync(organizationId, ct);
            return plan.HasFeature(featureKey);
        }

        public async Task<long?> GetLimitAsync(int? organizationId, string limitKey, CancellationToken ct = default)
        {
            var plan = await GetPlanAsync(organizationId, ct);
            return plan.GetLimit(limitKey);
        }

        public async Task<bool> IsWithinLimitAsync(
            int? organizationId, string limitKey, long currentUsage, long additional = 1, CancellationToken ct = default)
        {
            var limit = await GetLimitAsync(organizationId, limitKey, ct);
            if (limit is null) return true; // unlimited
            return currentUsage + additional <= limit.Value;
        }

        public async Task<bool> TryConsumeApiCallAsync(int organizationId, CancellationToken ct = default)
        {
            var limit = await GetLimitAsync(organizationId, LimitKeys.ApiCallsPerMonth, ct);
            if (limit is null) return true;
            if (limit.Value <= 0) return false;

            var period = DateTime.UtcNow.ToString("yyyy-MM");
            var counter = await _context.UsageCounters
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c =>
                    c.OrganizationId == organizationId &&
                    c.Key == LimitKeys.ApiCallsPerMonth &&
                    c.Period == period, ct);

            if (counter is null)
            {
                counter = new UsageCounter
                {
                    OrganizationId = organizationId,
                    Key = LimitKeys.ApiCallsPerMonth,
                    Period = period,
                    Value = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UsageCounters.Add(counter);
            }

            if (counter.Value >= limit.Value)
                return false;

            counter.Value++;
            counter.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return true;
        }

        public void Invalidate(int organizationId) => _cache.Remove(CacheKey(organizationId));

        private async Task<PlanDefinition> ResolvePlanAsync(int organizationId, CancellationToken ct)
        {
            try
            {
                var sub = await _context.Subscriptions
                    .IgnoreQueryFilters()
                    .Include(s => s.Plan)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

                if (sub is null || !SubscriptionStatus.GrantsAccess(sub.Status))
                    return PlanCatalog.Free;

                // After grace, past_due orgs soft-limit to Free entitlements (billing still shows past_due).
                if (sub.Status == SubscriptionStatus.PastDue && IsPastGrace(sub.PastDueSince))
                    return PlanCatalog.Free;

                return PlanCatalog.GetByCode(sub.Plan?.Code) ?? PlanCatalog.Free;
            }
            catch (Exception ex)
            {
                // Billing tables may not exist yet (pre-migration). Fail open to Free
                // so the rest of the app keeps working.
                _logger.LogWarning(ex, "Entitlement resolution failed for org {OrgId}; defaulting to Free.", organizationId);
                return PlanCatalog.Free;
            }
        }

        private bool IsPastGrace(DateTime? pastDueSince)
        {
            if (pastDueSince is null) return false;
            var days = Math.Max(1, _app.BillingGracePeriodDays);
            return DateTime.UtcNow >= pastDueSince.Value.AddDays(days);
        }

        private static string CacheKey(int organizationId) => $"entitlements:{organizationId}";
    }
}
