using System;

namespace NetTrack.Domain.Models
{
    public class SessionModel
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string InterfaceName { get; set; } = string.Empty;
        public int PacketCount { get; set; }
        public string Status { get; set; } = "Active";
    }
}
