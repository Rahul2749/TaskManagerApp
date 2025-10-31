using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class RefreshTokenDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
