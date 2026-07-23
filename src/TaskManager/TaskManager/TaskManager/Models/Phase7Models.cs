using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

/// <summary>Org-scoped public API key. Only the hash is stored; plaintext shown once at creation.</summary>
public class OrganizationApiKey
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short prefix for UI, e.g. tm_ab12…</summary>
    [Required, MaxLength(20)]
    public string KeyPrefix { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string KeyHash { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}

/// <summary>HTTP endpoint that receives signed JSON event payloads.</summary>
public class OutboundWebhook
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>HMAC secret for X-TaskManager-Signature.</summary>
    [Required, MaxLength(128)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma-separated event names, or * for all.</summary>
    [Required, MaxLength(500)]
    public string Events { get; set; } = "*";

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveredAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}

public class WebhookDelivery
{
    public int Id { get; set; }
    public int OutboundWebhookId { get; set; }
    public int OrganizationId { get; set; }

    [Required, MaxLength(80)]
    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public int AttemptCount { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public bool Succeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public OutboundWebhook OutboundWebhook { get; set; } = null!;
}

/// <summary>Third-party connection config (Slack incoming webhook, GitHub notify URL, etc.).</summary>
public class IntegrationConnection
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    /// <summary>slack | github | custom</summary>
    [Required, MaxLength(40)]
    public string Provider { get; set; } = "slack";

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON: {"webhookUrl":"https://hooks.slack.com/..."} etc.</summary>
    public string ConfigJson { get; set; } = "{}";

    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
}
