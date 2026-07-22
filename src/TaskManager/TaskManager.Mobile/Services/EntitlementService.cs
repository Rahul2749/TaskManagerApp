using System.Net.Http.Json;
using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Mobile.Services;

public sealed class EntitlementService : IEntitlementService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private SubscriptionDto? _current;
    private bool _loaded;

    public EntitlementService(IHttpClientFactory httpClientFactory) =>
        _httpClientFactory = httpClientFactory;

    public SubscriptionDto? Current => _current;

    public async Task EnsureLoadedAsync(bool forceReload = false)
    {
        if (_loaded && !forceReload)
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("TaskManagerApi");
            _current = await client.GetFromJsonAsync<SubscriptionDto>("api/billing/subscription");
            _loaded = true;
        }
        catch
        {
            _current = null;
            _loaded = true;
        }
    }

    public bool HasFeature(string featureKey) =>
        _current?.Features.Contains(featureKey, StringComparer.OrdinalIgnoreCase) == true;

    public void Clear()
    {
        _current = null;
        _loaded = false;
    }
}
