using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Mobile.Services;

public interface IEntitlementService
{
    SubscriptionDto? Current { get; }
    Task EnsureLoadedAsync(bool forceReload = false);
    bool HasFeature(string featureKey);
    void Clear();
}
