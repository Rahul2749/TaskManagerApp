namespace TaskManager.Services.Billing
{
    /// <summary>
    /// Razorpay credentials, bound from configuration section "Razorpay".
    /// Keep secrets out of appsettings.json — use user-secrets / environment variables.
    /// </summary>
    public class RazorpayOptions
    {
        public const string SectionName = "Razorpay";

        public string KeyId { get; set; } = string.Empty;
        public string KeySecret { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(KeySecret);
    }
}
