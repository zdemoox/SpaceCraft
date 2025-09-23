namespace SpaceCraft.API.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public TaskCategory Category { get; set; }
    public bool IsPinned { get; set; }
    public bool WasPinned { get; set; }
    public int PinOrder { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public enum TaskCategory
{
    ToDoList,
    Calendar,
    Whenever,
    Projects,
    Delegate
} 