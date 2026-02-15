using System.Collections.ObjectModel;

namespace NetTrack.Application.Interfaces
{
    public interface IConnectionTracker
    {
        ObservableCollection<NetTrack.Domain.Models.ConnectionModel> Connections { get; }
        void StartTracking();
        void StopTracking();
        NetTrack.Domain.Models.ConnectionModel? GetConnection(string srcIp, int srcPort, string dstIp, int dstPort, string protocol);
    }
}
