using System.Collections.Generic;

namespace NetTrack.Application.Interfaces
{
    public interface IDashboardService
    {
        double GetCurrentPacketRate(); // Packets per second
        double GetCurrentBandwidth(); // Bytes per second
        Dictionary<string, int> GetProtocolDistribution();
        void ResetStats();
    }
}
