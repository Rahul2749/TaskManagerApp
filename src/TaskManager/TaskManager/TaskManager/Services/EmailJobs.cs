namespace TaskManager.Services;

/// <summary>Hangfire entry points for transactional email.</summary>
public sealed class EmailJobs
{
    private readonly IEmailService _email;

    public EmailJobs(IEmailService email)
    {
        _email = email;
    }

    public Task SendWelcome(string toEmail, string firstName, string workspaceName) =>
        _email.SendWelcomeAsync(toEmail, firstName, workspaceName);

    public Task SendPasswordReset(string toEmail, string firstName, string resetUrl) =>
        _email.SendPasswordResetAsync(toEmail, firstName, resetUrl);

    public Task SendInvite(string toEmail, string workspaceName, string role, string inviteUrl) =>
        _email.SendInviteAsync(toEmail, workspaceName, role, inviteUrl);

    public Task SendReceipt(string toEmail, string invoiceNumber, decimal amount, string currency) =>
        _email.SendReceiptAsync(toEmail, invoiceNumber, amount, currency);

    public Task SendPaymentFailed(
        string toEmail, string firstName, string workspaceName, string billingUrl, int graceDaysRemaining) =>
        _email.SendPaymentFailedAsync(toEmail, firstName, workspaceName, billingUrl, graceDaysRemaining);
}
