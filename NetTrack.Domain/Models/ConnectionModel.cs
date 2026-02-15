using System;

namespace NetTrack.Domain.Models
{
    public class ConnectionModel
    {
        public string Key { get; set; } = string.Empty; // "SIP:SPort-DIP:DPort"
        public string SourceIP { get; set; } = string.Empty;
        public string DestinationIP { get; set; } = string.Empty;
        public int SourcePort { get; set; }
        public int DestinationPort { get; set; }
        public string State { get; set; } = "UNKNOWN"; // SYN_SENT, ESTABLISHED, FIN_WAIT, CLOSED
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; } // Difficult to track purely from one side unless full stream analysis
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public string ProcessName { get; set; } = string.Empty; // Enriched later
        public int ProcessId { get; set; }
    }
}
