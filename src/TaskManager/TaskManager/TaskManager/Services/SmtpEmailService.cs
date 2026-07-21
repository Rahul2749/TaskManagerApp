using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace TaskManager.Services;

/// <summary>
/// SMTP email sender. When Email:Host is empty, messages are logged and treated as sent
/// so local/dev and free-tier deploys keep working without a mail provider.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(string toEmail, string firstName, string workspaceName, CancellationToken ct = default)
    {
        var subject = $"Welcome to {workspaceName}";
        var body = Wrap($"Hi {WebUtility.HtmlEncode(firstName)},",
            $"Your workspace <strong>{WebUtility.HtmlEncode(workspaceName)}</strong> is ready. You can invite teammates and start organizing work anytime.");
        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string firstName, string resetUrl, CancellationToken ct = default)
    {
        var subject = "Reset your TaskManager password";
        var body = Wrap($"Hi {WebUtility.HtmlEncode(firstName)},",
            $"We received a request to reset your password.<br/><br/>" +
            $"<a href=\"{WebUtility.HtmlEncode(resetUrl)}\">Reset password</a><br/><br/>" +
            "This link expires in 1 hour. If you did not request it, you can ignore this email.");
        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendInviteAsync(string toEmail, string workspaceName, string role, string inviteUrl, CancellationToken ct = default)
    {
        var subject = $"You're invited to {workspaceName}";
        var body = Wrap("You've been invited.",
            $"Join <strong>{WebUtility.HtmlEncode(workspaceName)}</strong> as <strong>{WebUtility.HtmlEncode(role)}</strong>.<br/><br/>" +
            $"<a href=\"{WebUtility.HtmlEncode(inviteUrl)}\">Accept invitation</a><br/><br/>" +
            "This invite expires in 7 days.");
        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendReceiptAsync(string toEmail, string invoiceNumber, decimal amount, string currency, CancellationToken ct = default)
    {
        var subject = $"Payment receipt {invoiceNumber}";
        var body = Wrap("Payment received.",
            $"Invoice <strong>{WebUtility.HtmlEncode(invoiceNumber)}</strong> for " +
            $"<strong>{WebUtility.HtmlEncode(currency)} {amount:N2}</strong> was paid successfully.");
        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "Email not configured. Would send to {To}: {Subject}. Body: {Body}",
                toEmail, subject, htmlBody);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, ct);
        _logger.LogInformation("Sent email to {To} with subject {Subject}", toEmail, subject);
    }

    private static string Wrap(string heading, string content) =>
        $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#111;">
          <h2 style="margin:0 0 12px;">{heading}</h2>
          <p style="margin:0;">{content}</p>
          <hr style="margin:24px 0;border:none;border-top:1px solid #eee;" />
          <p style="margin:0;color:#666;font-size:12px;">TaskManager</p>
        </div>
        """;
}
