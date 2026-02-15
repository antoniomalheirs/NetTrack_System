using NetTrack.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetTrack.Application.Interfaces
{
    public interface IStorageService
    {
        Task InitializeAsync();
        Task<int> SaveSessionAsync(SessionModel session);
        Task SavePacketsAsync(int sessionId, IEnumerable<PacketModel> packets);
        Task<IEnumerable<SessionModel>> GetSessionsAsync();
        Task<IEnumerable<PacketModel>> GetPacketsAsync(int sessionId);
        Task UpdateSessionAsync(SessionModel session);
    }
}
