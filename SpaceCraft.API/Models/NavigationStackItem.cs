using System;

namespace SpaceCraft.API.Models
{
    public class NavigationStackItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Url { get; set; }
        public DateTime Timestamp { get; set; }
        public User User { get; set; }
    }
} 