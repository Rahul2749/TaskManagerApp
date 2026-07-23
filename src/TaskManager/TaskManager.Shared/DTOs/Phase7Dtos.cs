using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs;

public sealed class OrganizationApiKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
}

public sealed class CreateApiKeyDto
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateApiKeyResultDto
{
    public OrganizationApiKeyDto Key { get; set; } = new();
    /// <summary>Full secret — shown only once.</summary>
    public string PlaintextKey { get; set; } = string.Empty;
}

public sealed class OutboundWebhookDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string Events { get; set; } = "*";
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDeliveredAt { get; set; }
}

public sealed class UpsertOutboundWebhookDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Events { get; set; } = "*";

    public bool IsEnabled { get; set; } = true;
}

public sealed class CreateOutboundWebhookResultDto
{
    public OutboundWebhookDto Webhook { get; set; } = new();
    /// <summary>Signing secret — shown only once.</summary>
    public string Secret { get; set; } = string.Empty;
}

public sealed class IntegrationConnectionDto
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UpsertIntegrationConnectionDto
{
    [Required, MaxLength(40)]
    public string Provider { get; set; } = "slack";

    [Required, MaxLength(100)]
    public string Name { get; set; } = "Slack";

    [Required, MaxLength(500)]
    public string WebhookUrl { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

public sealed class WebhookDeliveryDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int? LastStatusCode { get; set; }
    public bool Succeeded { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LastError { get; set; }
}
