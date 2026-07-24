using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace TaskManager.Shared.Http;

public static class HttpErrorReader
{
    public static async Task<string> ReadDetailAsync(
        HttpResponseMessage response,
        string fallback = "Request failed. Please try again.")
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                {
                    var d = detail.GetString();
                    if (!string.IsNullOrWhiteSpace(d)) return d!;
                }
                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    var t = title.GetString();
                    if (!string.IsNullOrWhiteSpace(t)) return t!;
                }
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    var m = message.GetString();
                    if (!string.IsNullOrWhiteSpace(m)) return m!;
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return MapStatus(response.StatusCode, fallback);
    }

    private static string MapStatus(HttpStatusCode status, string fallback) => status switch
    {
        HttpStatusCode.PaymentRequired => "Plan limit reached. Upgrade to continue.",
        HttpStatusCode.Forbidden => "You do not have permission for this action.",
        HttpStatusCode.Unauthorized => "Your session expired. Please sign in again.",
        HttpStatusCode.BadRequest => "Invalid request. Check your details and try again.",
        HttpStatusCode.NotFound => "Not found.",
        HttpStatusCode.Conflict => "This conflicts with existing data.",
        _ => fallback
    };
}
