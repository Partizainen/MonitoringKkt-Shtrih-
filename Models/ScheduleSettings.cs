using System;

namespace KKTMonitor.Models
{
    public class ScheduleSettings
    {
        public int Id { get; set; }
        public string? ScheduleTime { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}