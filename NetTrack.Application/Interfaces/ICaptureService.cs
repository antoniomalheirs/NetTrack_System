using NetTrack.Domain.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace NetTrack.Application.Interfaces
{
    public interface ICaptureService
    {
        event EventHandler<RawPacket>? PacketReceived;
        event Action? OnCaptureStopped;
        event Action<string>? OnError;
        Task StartCaptureAsync(string deviceName);
        Task StopCaptureAsync();
        ObservableCollection<string> GetAvailableDevices();
    }
}
