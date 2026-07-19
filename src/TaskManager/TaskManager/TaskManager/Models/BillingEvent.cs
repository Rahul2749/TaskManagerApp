using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// Records inbound payment-provider webhook events for idempotency and audit.
    /// A unique index on (Provider, EventId) prevents processing the same event twice.
    /// </summary>
    public class BillingEvent
    {
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string Provider { get; set; } = "razorpay";

        [Required, MaxLength(120)]
        public string EventId { get; set; } = string.Empty;

        [Required, MaxLength(80)]
        public string EventType { get; set; } = string.Empty;

        public string? Payload { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        [MaxLength(500)]
        public string? Error { get; set; }
    }
}
