using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TaskManager.Services.Billing
{
    /// <summary>
    /// Razorpay implementation of <see cref="IBillingProvider"/> using the REST API
    /// directly (Basic auth with Key ID / Key Secret) so no extra SDK dependency is needed.
    /// </summary>
    public class RazorpayBillingProvider : IBillingProvider
    {
        private const string BaseUrl = "https://api.razorpay.com/v1/";

        private readonly HttpClient _http;
        private readonly RazorpayOptions _options;
        private readonly ILogger<RazorpayBillingProvider> _logger;

        public RazorpayBillingProvider(
            HttpClient http,
            IOptions<RazorpayOptions> options,
            ILogger<RazorpayBillingProvider> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;

            if (_options.IsConfigured)
            {
                _http.BaseAddress = new Uri(BaseUrl);
                var token = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}"));
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", token);
            }
        }

        public string Name => "razorpay";

        public bool IsConfigured => _options.IsConfigured;

        public async Task<string> EnsureCustomerAsync(BillingCustomer customer, CancellationToken ct = default)
        {
            EnsureConfigured();

            if (!string.IsNullOrWhiteSpace(customer.ProviderCustomerId))
                return customer.ProviderCustomerId!;

            var body = new Dictionary<string, object?>
            {
                ["name"] = customer.Name,
                ["email"] = customer.Email,
                ["contact"] = customer.Contact,
                ["fail_existing"] = 0
            };

            using var response = await _http.PostAsync(
                "customers", JsonContent(body), ct);
            var json = await ReadAsync(response, ct);
            return json.GetProperty("id").GetString()!;
        }

        public async Task<ProviderSubscription> CreateSubscriptionAsync(
            CreateSubscriptionRequest request, CancellationToken ct = default)
        {
            EnsureConfigured();

            var body = new Dictionary<string, object?>
            {
                ["plan_id"] = request.ProviderPlanId,
                ["total_count"] = request.TotalCount,
                ["quantity"] = request.Seats,
                ["customer_notify"] = 1,
                ["notes"] = string.IsNullOrWhiteSpace(request.Notes)
                    ? null
                    : new Dictionary<string, string> { ["notes"] = request.Notes! }
            };

            using var response = await _http.PostAsync(
                "subscriptions", JsonContent(body), ct);
            var json = await ReadAsync(response, ct);

            return new ProviderSubscription(
                json.GetProperty("id").GetString()!,
                json.TryGetProperty("status", out var s) ? s.GetString() ?? "created" : "created",
                json.TryGetProperty("short_url", out var u) ? u.GetString() : null);
        }

        public async Task CancelSubscriptionAsync(
            string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        {
            EnsureConfigured();

            var body = new Dictionary<string, object?>
            {
                ["cancel_at_cycle_end"] = atPeriodEnd ? 1 : 0
            };

            using var response = await _http.PostAsync(
                $"subscriptions/{providerSubscriptionId}/cancel", JsonContent(body), ct);
            await ReadAsync(response, ct);
        }

        public bool VerifyWebhookSignature(string payload, string signature)
        {
            if (string.IsNullOrWhiteSpace(_options.WebhookSecret) || string.IsNullOrWhiteSpace(signature))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            // Constant-time comparison.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
        }

        private void EnsureConfigured()
        {
            if (!IsConfigured)
                throw new BillingNotConfiguredException(Name);
        }

        private static StringContent JsonContent(object body) =>
            new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        private async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay API error {Status}: {Body}", response.StatusCode, content);
                throw new InvalidOperationException($"Razorpay API error ({(int)response.StatusCode}).");
            }

            return JsonSerializer.Deserialize<JsonElement>(content);
        }
    }
}
