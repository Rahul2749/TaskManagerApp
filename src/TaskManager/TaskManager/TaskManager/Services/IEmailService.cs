namespace TaskManager.Services;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);

    Task SendWelcomeAsync(string toEmail, string firstName, string workspaceName, CancellationToken ct = default);

    Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl, CancellationToken ct = default);

    Task SendInviteAsync(string toEmail, string workspaceName, string role, string inviteUrl, CancellationToken ct = default);

    Task SendReceiptAsync(string toEmail, string invoiceNumber, decimal amount, string currency, CancellationToken ct = default);

    Task SendPaymentFailedAsync(string toEmail, string firstName, string workspaceName, string billingUrl, int graceDaysRemaining, CancellationToken ct = default);
}
