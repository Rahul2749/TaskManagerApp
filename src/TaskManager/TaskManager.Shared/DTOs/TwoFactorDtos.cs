namespace TaskManager.Shared.DTOs;

public sealed class LoginResponseDto
{
    public bool RequiresTwoFactor { get; set; }
    public string? ChallengeToken { get; set; }
    public TokenDto? Tokens { get; set; }
}

public sealed class TwoFactorChallengeDto
{
    public string ChallengeToken { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class VerifyTwoFactorDto
{
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class TwoFactorPasswordDto
{
    public string Password { get; set; } = string.Empty;
}

public sealed class TwoFactorStatusDto
{
    public bool Enabled { get; set; }
    public int RecoveryCodesRemaining { get; set; }
}

public sealed class TwoFactorRecoveryCodesDto
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = Array.Empty<string>();
    public TokenDto? Session { get; set; }
}

public sealed class TwoFactorUpdateDto
{
    public TokenDto? Session { get; set; }
}
