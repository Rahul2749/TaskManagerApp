using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

public sealed class TwoFactorChallenge
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(64)]
    public string ChallengeTokenHash { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string CodeHash { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Purpose { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime LastSentAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public int FailedAttempts { get; set; }

    public User User { get; set; } = null!;
}
