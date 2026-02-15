using System;

namespace NetTrack.Domain.Models
{
    public class AlertNotification
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public int PacketId { get; set; }
        public string Severity { get; set; } = "Info"; // Info, Warning, Critical
    }
}
