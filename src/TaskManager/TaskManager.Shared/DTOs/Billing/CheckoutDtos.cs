using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs.Billing
{
    /// <summary>Request to start a subscription checkout.</summary>
    public class CheckoutRequestDto
    {
        [Required]
        public string PlanCode { get; set; } = string.Empty;

        /// <summary>"monthly" or "annual".</summary>
        [Required]
        public string BillingInterval { get; set; } = "monthly";

        [Range(1, 100000)]
        public int Seats { get; set; } = 1;
    }

    /// <summary>
    /// Data the client needs to open the provider's hosted checkout
    /// (Razorpay subscription + key id).
    /// </summary>
    public class CheckoutSessionDto
    {
        public string Provider { get; set; } = "razorpay";
        public string ProviderKeyId { get; set; } = string.Empty;
        public string ProviderSubscriptionId { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
    }
}
