namespace TaskManager.Shared.DTOs.Billing
{
    public class InvoiceDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string Status { get; set; } = string.Empty;
        public string? PdfUrl { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
