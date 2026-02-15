using System.Threading.Tasks;

namespace NetTrack.Application.Interfaces
{
    public interface ISessionReplayService
    {
        Task LoadSessionAsync(string filePath);
        Task PlayAsync();
        Task PauseAsync();
        Task StopAsync();
        
        bool IsPlaying { get; }
        double PlaybackSpeed { get; set; }
        string CurrentFileName { get; }
    }
}
