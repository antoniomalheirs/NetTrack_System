using System.Collections.Generic;
using System.Threading.Tasks;
using NetTrack.Domain.Models;

namespace NetTrack.Application.Interfaces
{
    public interface IPortMonitor
    {
        Task<IEnumerable<PortModel>> GetActivePortsAsync();
    }
}
