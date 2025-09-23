namespace SpaceCraft.API.Models;

public class KnowledgeItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsFolder { get; set; }
    public int? ParentId { get; set; }
    public string Content { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool WasPinned { get; set; }
    public int PinOrder { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
} 