using NetTrack.Domain.Models;
using System.Threading.Tasks;

namespace NetTrack.Application.Interfaces
{
    public interface IPacketPipelineService
    {
        void Start();
        Task StopAsync();
        
        // UI subscribes to this to get processed packets
        // We use an event for simplicity in the UI layer, 
        // but internally it comes from a channel reader.
        event System.Action<PacketModel> PacketProcessed;
        void SetSessionId(int sessionId);
        void InjectPacket(NetTrack.Domain.Models.RawPacket packet);
    }
}
