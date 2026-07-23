using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using TaskManager.Billing;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services.Billing;

namespace TaskManager.Services;

public static class WebhookEvents
{
    public const string TaskCreated = "task.created";
    public const string TaskStatusChanged = "task.status_changed";
    public const string TaskCompleted = "task.completed";
}

public interface IOutboundEventPublisher
{
    Task PublishAsync(int organizationId, string eventType, object payload, CancellationToken ct = default);
}

public sealed class OutboundEventPublisher : IOutboundEventPublisher
{
    private readonly ApplicationDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly IBackgroundJobClient _jobs;

    public OutboundEventPublisher(
        ApplicationDbContext db,
        IEntitlementService entitlements,
        IBackgroundJobClient jobs)
    {
        _db = db;
        _entitlements = entitlements;
        _jobs = jobs;
    }

    public async Task PublishAsync(int organizationId, string eventType, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);

        if (await _entitlements.HasFeatureAsync(organizationId, FeatureKeys.Integrations, ct))
        {
            var hooks = await _db.OutboundWebhooks
                .IgnoreQueryFilters()
                .Where(w => w.OrganizationId == organizationId && w.IsEnabled)
                .ToListAsync(ct);

            foreach (var hook in hooks.Where(h => Subscribes(h.Events, eventType)))
            {
                var delivery = new WebhookDelivery
                {
                    OutboundWebhookId = hook.Id,
                    OrganizationId = organizationId,
                    EventType = eventType,
                    PayloadJson = json,
                    AttemptCount = 0,
                    Succeeded = false,
                    CreatedAt = DateTime.UtcNow,
                    NextAttemptAt = DateTime.UtcNow
                };
                _db.WebhookDeliveries.Add(delivery);
                await _db.SaveChangesAsync(ct);
                _jobs.Enqueue<Phase7Jobs>(j => j.DeliverWebhookAsync(delivery.Id));
            }

            // Slack / GitHub-style incoming webhook URLs
            var integrations = await _db.IntegrationConnections
                .IgnoreQueryFilters()
                .Where(i => i.OrganizationId == organizationId && i.IsEnabled)
                .ToListAsync(ct);

            foreach (var integ in integrations)
                _jobs.Enqueue<Phase7Jobs>(j => j.DeliverIntegrationAsync(integ.Id, eventType, json));
        }
    }

    private static bool Subscribes(string events, string eventType)
    {
        if (string.IsNullOrWhiteSpace(events) || events.Trim() == "*")
            return true;
        return events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));
    }
}

public class Phase7Jobs
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Phase7Jobs> _logger;

    public Phase7Jobs(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<Phase7Jobs> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task DeliverWebhookAsync(int deliveryId)
    {
        var delivery = await _db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Include(d => d.OutboundWebhook)
            .FirstOrDefaultAsync(d => d.Id == deliveryId);

        if (delivery is null || delivery.Succeeded || delivery.OutboundWebhook is null)
            return;

        var hook = delivery.OutboundWebhook;
        if (!hook.IsEnabled)
            return;

        delivery.AttemptCount++;
        var client = _httpClientFactory.CreateClient("OutboundWebhooks");
        using var req = new HttpRequestMessage(HttpMethod.Post, hook.TargetUrl);
        req.Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
        req.Headers.TryAddWithoutValidation("X-TaskManager-Event", delivery.EventType);
        req.Headers.TryAddWithoutValidation("X-TaskManager-Delivery", delivery.Id.ToString());
        req.Headers.TryAddWithoutValidation("X-TaskManager-Signature", Sign(hook.Secret, delivery.PayloadJson));

        try
        {
            var res = await client.SendAsync(req);
            delivery.LastStatusCode = (int)res.StatusCode;
            if (res.IsSuccessStatusCode)
            {
                delivery.Succeeded = true;
                delivery.CompletedAt = DateTime.UtcNow;
                delivery.LastError = null;
                hook.LastDeliveredAt = DateTime.UtcNow;
            }
            else
            {
                delivery.LastError = await res.Content.ReadAsStringAsync();
                ScheduleRetry(delivery);
            }
        }
        catch (Exception ex)
        {
            delivery.LastError = ex.Message;
            ScheduleRetry(delivery);
            _logger.LogWarning(ex, "Webhook delivery {Id} failed", deliveryId);
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeliverIntegrationAsync(int integrationId, string eventType, string payloadJson)
    {
        var integ = await _db.IntegrationConnections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == integrationId && i.IsEnabled);
        if (integ is null) return;

        string? url = null;
        try
        {
            using var doc = JsonDocument.Parse(integ.ConfigJson);
            if (doc.RootElement.TryGetProperty("webhookUrl", out var el))
                url = el.GetString();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
            return;

        object body = integ.Provider.ToLowerInvariant() switch
        {
            "slack" => BuildSlackPayload(eventType, payloadJson),
            "github" => new { event_type = eventType, payload = JsonSerializer.Deserialize<JsonElement>(payloadJson) },
            _ => new { eventType, payload = JsonSerializer.Deserialize<JsonElement>(payloadJson) }
        };

        var client = _httpClientFactory.CreateClient("OutboundWebhooks");
        try
        {
            var res = await client.PostAsJsonAsync(url, body);
            if (!res.IsSuccessStatusCode)
                _logger.LogWarning("Integration {Id} returned {Status}", integrationId, (int)res.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integration delivery {Id} failed", integrationId);
        }
    }

    private static void ScheduleRetry(WebhookDelivery delivery)
    {
        if (delivery.AttemptCount >= 5)
        {
            delivery.CompletedAt = DateTime.UtcNow;
            delivery.NextAttemptAt = null;
            return;
        }

        var delayMinutes = Math.Pow(2, delivery.AttemptCount); // 2,4,8,16…
        delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    /// <summary>Retry pending failed deliveries (Hangfire recurring).</summary>
    public async Task RetryPendingWebhooksAsync()
    {
        var now = DateTime.UtcNow;
        var pending = await _db.WebhookDeliveries
            .IgnoreQueryFilters()
            .Where(d => !d.Succeeded && d.NextAttemptAt != null && d.NextAttemptAt <= now && d.AttemptCount < 5)
            .OrderBy(d => d.NextAttemptAt)
            .Take(50)
            .Select(d => d.Id)
            .ToListAsync();

        foreach (var id in pending)
            await DeliverWebhookAsync(id);
    }

    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static object BuildSlackPayload(string eventType, string payloadJson)
    {
        string text;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : "Task";
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            text = eventType switch
            {
                WebhookEvents.TaskCreated => $":new: Task created: *{title}*",
                WebhookEvents.TaskCompleted => $":white_check_mark: Task completed: *{title}*",
                WebhookEvents.TaskStatusChanged => $":arrows_counterclockwise: *{title}* → `{status}`",
                _ => $":bell: {eventType}: *{title}*"
            };
        }
        catch
        {
            text = $":bell: {eventType}";
        }

        return new { text };
    }
}
