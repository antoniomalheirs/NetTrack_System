namespace NetTrack.Domain.Models
{
    public class PortModel
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = string.Empty; // TCP/UDP
        public string LocalAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // Listening, Established
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
    }
}
