using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    /// <summary>
    /// A comment in a task's discussion thread. Supports threaded replies via
    /// <see cref="ParentCommentId"/> and lightweight @mentions via the plain text body.
    /// </summary>
    public class Comment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public int AuthorId { get; set; }

        /// <summary>Optional parent comment for threaded replies (null = top-level).</summary>
        public int? ParentCommentId { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        /// <summary>True after the author edited the comment after its initial post.</summary>
        public bool IsEdited { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public TaskItem Task { get; set; } = null!;
        public User Author { get; set; } = null!;
        public Comment? ParentComment { get; set; }
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
