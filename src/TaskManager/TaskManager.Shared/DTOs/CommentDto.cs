using System.ComponentModel.DataAnnotations;

namespace TaskManager.Shared.DTOs
{
    public class CommentDto
    {
        public int? Id { get; set; }

        public int TaskId { get; set; }

        public int? ParentCommentId { get; set; }

        public UserDto? Author { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsEdited { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Populated only when fetching a thread with replies.</summary>
        public List<CommentDto> Replies { get; set; } = new();
    }
}
