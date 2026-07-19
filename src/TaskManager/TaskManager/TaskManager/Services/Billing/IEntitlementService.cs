using TaskManager.Billing;

namespace TaskManager.Services.Billing
{
    /// <summary>
    /// Resolves what an organization is entitled to based on its active subscription.
    /// Results are cached per organization and invalidated when the subscription changes.
    /// </summary>
    public interface IEntitlementService
    {
        /// <summary>Resolves the effective plan definition for an organization (Free if none).</summary>
        Task<PlanDefinition> GetPlanAsync(int? organizationId, CancellationToken ct = default);

        /// <summary>True if the organization's plan enables the given boolean feature.</summary>
        Task<bool> HasFeatureAsync(int? organizationId, string featureKey, CancellationToken ct = default);

        /// <summary>Returns the numeric limit for a key, or null when unlimited.</summary>
        Task<long?> GetLimitAsync(int? organizationId, string limitKey, CancellationToken ct = default);

        /// <summary>True if adding <paramref name="additional"/> keeps usage within the limit.</summary>
        Task<bool> IsWithinLimitAsync(int? organizationId, string limitKey, long currentUsage, long additional = 1, CancellationToken ct = default);

        /// <summary>Clears cached entitlements for an organization (call after subscription changes).</summary>
        void Invalidate(int organizationId);
    }
}
