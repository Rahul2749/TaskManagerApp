using TaskManager.Shared.DTOs;

namespace TaskManager.Services
{
    public interface IAuthService
    {
        Task<TokenDto?> LoginAsync(LoginDto loginDto);
        Task<TokenDto?> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(int userId);
    }
}
