using System.ComponentModel.DataAnnotations;

namespace SpaceCraft.API.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        public string Theme { get; set; } = "light";
    }
} 