using NetTrack.Domain.Models;

namespace NetTrack.Application.Interfaces
{
    public interface IPacketParser
    {
        PacketModel Parse(RawPacket rawPacket);
    }
}
