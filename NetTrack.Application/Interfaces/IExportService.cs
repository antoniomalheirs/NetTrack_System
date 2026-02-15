using NetTrack.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetTrack.Application.Interfaces
{
    public interface IExportService
    {
        Task ExportToPcapAsync(IEnumerable<PacketModel> packets, string filePath);
        Task ExportToJsonAsync(IEnumerable<PacketModel> packets, string filePath);
        Task ExportToCsvAsync(IEnumerable<PacketModel> packets, string filePath);
    }
}
