using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Client.Services
{
    /// <summary>
    /// Cached subscription entitlements for the current session. Loaded once after login
    /// and refreshed after checkout or plan changes.
    /// </summary>
    public interface IEntitlementState
    {
        SubscriptionDto? Subscription { get; }
        bool IsLoaded { get; }
        Task EnsureLoadedAsync();
        Task RefreshAsync();
        bool HasFeature(string featureKey);
        long? GetLimit(string limitKey);
        void Clear();
    }

    public class EntitlementStateService : IEntitlementState
    {
        private readonly IBillingService _billing;

        public EntitlementStateService(IBillingService billing) => _billing = billing;

        public SubscriptionDto? Subscription { get; private set; }
        public bool IsLoaded { get; private set; }

        public async Task EnsureLoadedAsync()
        {
            if (IsLoaded) return;
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            try
            {
                Subscription = await _billing.GetSubscriptionAsync();
            }
            catch
            {
                Subscription = null;
            }
            finally
            {
                IsLoaded = true;
            }
        }

        public bool HasFeature(string featureKey) =>
            Subscription?.Features?.Contains(featureKey, StringComparer.OrdinalIgnoreCase) == true;

        public long? GetLimit(string limitKey)
        {
            if (Subscription?.Limits is null) return null;
            return Subscription.Limits.TryGetValue(limitKey, out var v) ? v : null;
        }

        public void Clear()
        {
            Subscription = null;
            IsLoaded = false;
        }
    }
}
