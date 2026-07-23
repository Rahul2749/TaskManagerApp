using TaskManager.Shared.DTOs;

namespace TaskManager.Mobile.Services;

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public UserDto? User { get; init; }

    public static AuthResult Ok(UserDto? user = null) => new() { Success = true, User = user };
    public static AuthResult Fail(string error) => new() { Success = false, Error = error };
}

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginDto loginDto);
    Task<AuthResult> RegisterAsync(WorkspaceRegistrationDto dto);
    Task<AuthResult> AcceptInviteAsync(AcceptInviteDto dto);
    Task<(bool Ok, string Message)> ForgotPasswordAsync(string email);
    Task<(bool Ok, string Message)> ResetPasswordAsync(string token, string password);
    Task<InvitePreviewDto?> PreviewInviteAsync(string token);
    Task LogoutAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<bool> RefreshTokenAsync();
    Task<AuthResult> CompleteSsoLoginAsync(string exchangeCode);
}
