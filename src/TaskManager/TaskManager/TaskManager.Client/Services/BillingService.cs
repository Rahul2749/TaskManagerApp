using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Client.Services
{
    public class BillingService : IBillingService
    {
        private readonly HttpClient _httpClient;
        private readonly LocalStorageService _localStorage;

        public BillingService(HttpClient httpClient, LocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        private async Task<HttpClient> GetClientAsync(bool requireAuth = false)
        {
            var token = await _localStorage.GetItemAsync<string>("accessToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
            else if (requireAuth)
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }

            return _httpClient;
        }

        public async Task<List<PlanDto>> GetPlansAsync()
        {
            var client = await GetClientAsync();
            return await client.GetFromJsonAsync<List<PlanDto>>("api/billing/plans") ?? new();
        }

        public async Task<SubscriptionDto?> GetSubscriptionAsync()
        {
            var client = await GetClientAsync(requireAuth: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await client.GetFromJsonAsync<SubscriptionDto>("api/billing/subscription", timeout.Token);
        }

        public async Task<List<InvoiceDto>> GetInvoicesAsync()
        {
            var client = await GetClientAsync(requireAuth: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return await client.GetFromJsonAsync<List<InvoiceDto>>("api/billing/invoices", timeout.Token) ?? new();
        }

        public async Task<CheckoutSessionDto?> StartCheckoutAsync(CheckoutRequestDto request)
        {
            var client = await GetClientAsync(requireAuth: true);
            var response = await client.PostAsJsonAsync("api/billing/checkout", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();

            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractError(body) ?? $"Checkout failed ({(int)response.StatusCode}).");
        }

        public Task<SubscriptionDto?> VerifyCheckoutAsync(VerifyCheckoutRequestDto request) =>
            PostSubscriptionAsync("api/billing/verify-checkout", request);

        public Task<SubscriptionDto?> SyncSubscriptionAsync() =>
            PostSubscriptionAsync("api/billing/sync", null);

        private async Task<SubscriptionDto?> PostSubscriptionAsync(string path, VerifyCheckoutRequestDto? request)
        {
            var client = await GetClientAsync(requireAuth: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = request is null
                ? await client.PostAsync(path, null, timeout.Token)
                : await client.PostAsJsonAsync(path, request, timeout.Token);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<SubscriptionDto>(timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            throw new InvalidOperationException(ExtractError(body) ?? "Could not confirm the subscription. Refresh billing to retry.");
        }

        private static string? ExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.TryGetProperty("detail", out var detail))
                    return detail.GetString();
                if (root.TryGetProperty("title", out var title))
                    return title.GetString();
                if (root.ValueKind == System.Text.Json.JsonValueKind.String)
                    return root.GetString();
            }
            catch
            {
                // Fall through to raw body.
            }

            return body.Length <= 240 ? body : body[..240];
        }

        public async Task<bool> CancelSubscriptionAsync()
        {
            var client = await GetClientAsync(requireAuth: true);
            var response = await client.PostAsync("api/billing/cancel", null);
            return response.IsSuccessStatusCode;
        }
    }
}
