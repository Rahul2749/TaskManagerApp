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
        private int _generation;
        private Task? _refreshTask;

        public async Task EnsureLoadedAsync()
        {
            if (IsLoaded) return;
            await RefreshAsync();
        }

        public Task RefreshAsync() =>
            _refreshTask is { IsCompleted: false } ? _refreshTask : _refreshTask = LoadAsync(_generation);

        private async Task LoadAsync(int generation)
        {
            try
            {
                var subscription = await _billing.GetSubscriptionAsync();
                if (generation != _generation) return;
                Subscription = subscription;
                IsLoaded = subscription is not null;
            }
            catch
            {
                if (generation == _generation) IsLoaded = false;
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
            _generation++;
            _refreshTask = null;
            Subscription = null;
            IsLoaded = false;
        }
    }
}
