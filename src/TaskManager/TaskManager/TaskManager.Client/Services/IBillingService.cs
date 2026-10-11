using TaskManager.Shared.DTOs.Billing;

namespace TaskManager.Client.Services
{
    public interface IBillingService
    {
        Task<List<PlanDto>> GetPlansAsync();
        Task<SubscriptionDto?> GetSubscriptionAsync();
        Task<List<InvoiceDto>> GetInvoicesAsync();
        Task<CheckoutSessionDto?> StartCheckoutAsync(CheckoutRequestDto request);
        Task<SubscriptionDto?> VerifyCheckoutAsync(VerifyCheckoutRequestDto request);
        Task<SubscriptionDto?> SyncSubscriptionAsync();
        Task<bool> CancelSubscriptionAsync();
    }
}
