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
            return await client.GetFromJsonAsync<SubscriptionDto>("api/billing/subscription");
        }

        public async Task<List<InvoiceDto>> GetInvoicesAsync()
        {
            var client = await GetClientAsync(requireAuth: true);
            return await client.GetFromJsonAsync<List<InvoiceDto>>("api/billing/invoices") ?? new();
        }

        public async Task<CheckoutSessionDto?> StartCheckoutAsync(CheckoutRequestDto request)
        {
            var client = await GetClientAsync(requireAuth: true);
            var response = await client.PostAsJsonAsync("api/billing/checkout", request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<CheckoutSessionDto>();
        }

        public async Task<bool> CancelSubscriptionAsync()
        {
            var client = await GetClientAsync(requireAuth: true);
            var response = await client.PostAsync("api/billing/cancel", null);
            return response.IsSuccessStatusCode;
        }
    }
}
