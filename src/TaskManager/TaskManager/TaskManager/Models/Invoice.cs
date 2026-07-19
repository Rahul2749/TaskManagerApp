using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models
{
    /// <summary>
    /// A billing invoice for an organization, mirrored from the payment provider.
    /// </summary>
    public class Invoice
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        public int? SubscriptionId { get; set; }

        [Required, MaxLength(60)]
        public string Number { get; set; } = string.Empty;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "INR";

        /// <summary>paid, due, failed, void.</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "due";

        [MaxLength(100)]
        public string? ProviderInvoiceId { get; set; }

        public string? PdfUrl { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        public Organization Organization { get; set; } = null!;
        public Subscription? Subscription { get; set; }
    }
}
