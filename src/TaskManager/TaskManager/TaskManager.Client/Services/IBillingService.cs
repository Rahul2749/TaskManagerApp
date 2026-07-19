using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Client.Services
{
    public interface IBillingService
    {
        Task<List<PlanDto>> GetPlansAsync();
        Task<SubscriptionDto?> GetSubscriptionAsync();
        Task<List<InvoiceDto>> GetInvoicesAsync();
        Task<CheckoutSessionDto?> StartCheckoutAsync(CheckoutRequestDto request);
        Task<bool> CancelSubscriptionAsync();
    }
}
