namespace TaskManager.Shared.DTOs
{
    public class TaskHistoryDto
    {
        public int Id { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }
        public DateTime ChangedAt { get; set; }
        public UserDto? ChangedBy { get; set; }
    }
}
