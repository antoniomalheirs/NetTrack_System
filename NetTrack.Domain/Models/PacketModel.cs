using System;

namespace NetTrack.Domain.Models
{
    public class PacketModel
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public int Length { get; set; }
        public string Info { get; set; } = string.Empty;
        public byte[] OriginalData { get; set; } = Array.Empty<byte>();
    }
}
