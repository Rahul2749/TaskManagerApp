namespace TaskManager.Services.Billing
{
    /// <summary>
    /// Abstraction over the payment gateway so the app is not coupled to Razorpay.
    /// A Stripe implementation can be added later for the global market without
    /// changing controllers or entitlement logic.
    /// </summary>
    public interface IBillingProvider
    {
        string Name { get; }

        /// <summary>True when the provider has valid credentials configured.</summary>
        bool IsConfigured { get; }

        /// <summary>Creates (or returns) the provider-side customer for an organization.</summary>
        Task<string> EnsureCustomerAsync(BillingCustomer customer, CancellationToken ct = default);

        /// <summary>Creates a subscription for the given provider plan id and seat quantity.</summary>
        Task<ProviderSubscription> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);

        /// <summary>Cancels a subscription, optionally at period end.</summary>
        Task CancelSubscriptionAsync(string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct = default);

        /// <summary>Validates the signature of an inbound webhook payload.</summary>
        bool VerifyWebhookSignature(string payload, string signature);
    }

    public sealed record BillingCustomer(string? ProviderCustomerId, string Name, string Email, string? Contact);

    public sealed record CreateSubscriptionRequest(
        string ProviderCustomerId,
        string ProviderPlanId,
        int Seats,
        int TotalCount,
        string? Notes,
        int TrialDays = 0);

    public sealed record ProviderSubscription(
        string ProviderSubscriptionId,
        string Status,
        string? ShortUrl);

    /// <summary>Thrown when a billing operation is attempted but the provider has no credentials.</summary>
    public sealed class BillingNotConfiguredException : Exception
    {
        public BillingNotConfiguredException(string provider)
            : base($"The '{provider}' payment provider is not configured. Set the API keys to enable billing.") { }
    }
}
