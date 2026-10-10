using TaskManager.Shared.DTOs;

namespace TaskManager.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
        Task<TokenDto?> IssueTokensForUserAsync(int userId, bool twoFactorVerified = false);
        Task<TokenDto?> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(int userId);

        Task<UserDto?> GetCurrentUserAsync();
        Task LogoutAsync();
    }
}
