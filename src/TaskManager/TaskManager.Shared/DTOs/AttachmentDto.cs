using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class AttachmentDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public UserDto? UploadedBy { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }

        /// <summary>Download URL (time-limited in production).</summary>
        public string? DownloadUrl { get; set; }
    }
}
