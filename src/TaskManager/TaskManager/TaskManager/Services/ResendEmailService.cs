using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TaskManager.Services;

/// <summary>Delivers transactional messages through Resend's HTTPS API.</summary>
public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task SendWelcomeAsync(string toEmail, string firstName, string workspaceName, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.Welcome(firstName, workspaceName), ct);

    public Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.PasswordReset(firstName, resetUrl), ct);

    public Task SendInviteAsync(string toEmail, string workspaceName, string role, string inviteUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.Invite(workspaceName, role, inviteUrl), ct);

    public Task SendReceiptAsync(string toEmail, string invoiceNumber, decimal amount, string currency, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.Receipt(invoiceNumber, amount, currency), ct);

    public Task SendPaymentFailedAsync(
        string toEmail,
        string firstName,
        string workspaceName,
        string billingUrl,
        int graceDaysRemaining,
        CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.PaymentFailed(firstName, workspaceName, billingUrl, graceDaysRemaining), ct);

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) =>
        SendAsync(toEmail, EmailTemplates.Generic(subject, htmlBody), ct);

    private async Task SendAsync(string toEmail, EmailContent email, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Resend is not configured; skipped a transactional email. Set Email:ApiKey and Email:FromAddress.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("Idempotency-Key", CreateIdempotencyKey(toEmail, email));
        request.Content = JsonContent.Create(new
        {
            from = $"{_options.FromName} <{_options.FromAddress}>",
            to = new[] { toEmail },
            subject = email.Subject,
            html = email.Html,
            text = email.Text
        });

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend rejected a transactional email with HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new HttpRequestException(
                $"Resend email request failed with status code {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        _logger.LogInformation("Resend accepted a transactional email.");
    }

    private string CreateIdempotencyKey(string toEmail, EmailContent email)
    {
        var value = $"{_options.FromName}\n{_options.FromAddress}\n{toEmail}\n{email.Subject}\n{email.Html}\n{email.Text}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
