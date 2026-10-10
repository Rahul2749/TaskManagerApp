namespace TaskManager.Services;

internal sealed record EmailContent(string Subject, string Html, string Text);

internal static class EmailTemplates
{
    public static EmailContent Welcome(string firstName, string workspaceName) => Create(
        $"Welcome to {workspaceName}",
        $"Hi {firstName},",
        $"Your workspace {workspaceName} is ready. Invite teammates and start organizing your work whenever you're ready.");

    public static EmailContent PasswordReset(string firstName, string resetUrl) => Create(
        "Reset your TaskManager password",
        $"Hi {firstName},",
        "We received a request to reset your password. This link expires in 1 hour. If you did not request it, you can ignore this email.",
        "Reset password",
        resetUrl);

    public static EmailContent Invite(string workspaceName, string role, string inviteUrl) => Create(
        $"You're invited to {workspaceName}",
        "You've been invited.",
        $"Join {workspaceName} as {role}. This invitation expires in 7 days.",
        "Accept invitation",
        inviteUrl);

    public static EmailContent Receipt(string invoiceNumber, decimal amount, string currency) => Create(
        $"Payment receipt {invoiceNumber}",
        "Payment received.",
        $"Invoice {invoiceNumber} for {currency} {amount:N2} was paid successfully.");

    public static EmailContent PaymentFailed(
        string firstName, string workspaceName, string billingUrl, int graceDaysRemaining) => Create(
        $"Action needed: payment failed for {workspaceName}",
        $"Hi {firstName},",
        $"We could not collect payment for {workspaceName}. Please update your payment method within {graceDaysRemaining} day(s) to keep paid features.",
        "Open billing",
        billingUrl);

    public static EmailContent TwoFactorCode(string firstName, string code) => Create(
        "Your TaskManager sign-in code",
        $"Hi {firstName},",
        $"Your six-digit verification code is {code}. It expires in 5 minutes and can only be used once. If you did not try to sign in, secure your account.");

    public static EmailContent Generic(string subject, string html) =>
        new(subject, Layout(subject, html), ToPlainText(html));

    private static EmailContent Create(
        string subject,
        string greeting,
        string message,
        string? buttonText = null,
        string? buttonUrl = null)
    {
        var safeGreeting = E(greeting);
        var safeMessage = E(message);
        var text = System.Net.WebUtility.HtmlDecode($"{StripTags(safeGreeting)}\n\n{StripTags(safeMessage)}");
        var buttonHtml = "";

        if (!string.IsNullOrWhiteSpace(buttonText) && Uri.TryCreate(buttonUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http")
        {
            buttonHtml = $"<p style=\"margin:28px 0\"><a href=\"{E(uri.AbsoluteUri)}\" style=\"display:inline-block;background:#2563eb;color:#fff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:600\">{E(buttonText)}</a></p>";
            text += $"\n\n{buttonText}: {uri.AbsoluteUri}";
        }

        var body = $"<p style=\"margin:0 0 16px\">{safeGreeting}</p><p style=\"margin:0\">{safeMessage}</p>{buttonHtml}";
        return new EmailContent(subject, Layout(subject, body), text);
    }

    private static string Layout(string title, string body) =>
        $"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(title)}</title></head><body style="margin:0;background:#f4f6fa;font-family:Arial,Helvetica,sans-serif;color:#172033;line-height:1.6"><div style="max-width:600px;margin:32px auto;padding:0 16px"><div style="background:#fff;border:1px solid #e6eaf0;border-radius:12px;padding:32px"><div style="font-size:18px;font-weight:700;color:#1d4ed8;margin-bottom:24px">TaskManager</div>{body}<hr style="margin:28px 0 18px;border:0;border-top:1px solid #e6eaf0"><p style="margin:0;color:#687386;font-size:12px">This is a transactional message from TaskManager.</p></div></div></body></html>""";

    private static string E(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static string StripTags(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "<[^>]*>", "");

    private static string ToPlainText(string html)
    {
        var withBreaks = System.Text.RegularExpressions.Regex.Replace(
            html, "<br\\s*/?>|</p>|</div>|</h[1-6]>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return System.Net.WebUtility.HtmlDecode(StripTags(withBreaks)).Trim();
    }
}
