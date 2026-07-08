using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// A file attached to a task. The binary is stored externally (local disk in dev,
    /// Azure Blob / S3 in prod); this row holds the metadata + a relative path to it.
    /// </summary>
    public class Attachment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int UploadedById { get; set; }

        [Required, MaxLength(300)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>Original MIME type from the upload (e.g. image/png).</summary>
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        /// <summary>Size in bytes.</summary>
        public long FileSize { get; set; }

        /// <summary>Storage key/path where the blob lives (relative to the configured store root).</summary>
        [Required, MaxLength(500)]
        public string StoragePath { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public TaskItem Task { get; set; } = null!;
        public User UploadedBy { get; set; } = null!;
    }
}
