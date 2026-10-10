using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models;

public sealed class TwoFactorRecoveryCode
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(64)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}
