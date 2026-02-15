using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetTrack.Infrastructure.Services
{
    public class SessionReplayService : ISessionReplayService, IDisposable
    {
        private readonly IPacketPipelineService _pipelineService;
        
        private CaptureFileReaderDevice? _device;
        private CancellationTokenSource? _cts;
        private Task? _replayTask;
        
        private DateTime _sessionStartTime;
        private DateTime _playbackStartTime;
        private DateTime _lastPacketTime;
        
        public bool IsPlaying { get; private set; }
        public double PlaybackSpeed { get; set; } = 1.0;
        public string CurrentFileName { get; private set; } = string.Empty;

        public SessionReplayService(IPacketPipelineService pipelineService)
        {
            _pipelineService = pipelineService;
        }

        public async Task LoadSessionAsync(string filePath)
        {
            await StopAsync();
            
            try
            {
                _device = new CaptureFileReaderDevice(filePath);
                _device.Open();
                CurrentFileName = filePath;
            }
            catch (Exception ex)
            {
                // Handle error
                CurrentFileName = string.Empty;
                throw new Exception($"Failed to load session: {ex.Message}", ex);
            }
        }

        public async Task PlayAsync()
        {
            if (_device == null || IsPlaying) return;

            IsPlaying = true;
            _cts = new CancellationTokenSource();
            
            // If starting fresh
            if (_replayTask == null || _replayTask.IsCompleted)
            {
                _replayTask = Task.Run(() => ReplayLoop(_cts.Token));
            }
        }

        public async Task PauseAsync()
        {
            IsPlaying = false;
            _cts?.Cancel();
            if (_replayTask != null)
            {
                try { await _replayTask; } catch (OperationCanceledException) { }
            }
        }

        public async Task StopAsync()
        {
            IsPlaying = false;
            _cts?.Cancel();
            
             if (_replayTask != null)
            {
                try { await _replayTask; } catch (OperationCanceledException) { }
            }
            
            _device?.Close();
            _device = null;
            CurrentFileName = string.Empty;
        }

        private void ReplayLoop(CancellationToken token)
        {
            try
            {
                if (_device == null) return;

                PacketCapture packetCapture;
                
                // Read first packet to initialize time reference if needed
                // But typically we want to just flow.
                // Let's assume we continue from current position.
                
                while (!token.IsCancellationRequested)
                {
                    if (_device.GetNextPacket(out packetCapture) != GetPacketStatus.PacketRead)
                    {
                        // EOF or error
                        IsPlaying = false;
                        break;
                    }

                    var packetTime = packetCapture.Header.Timeval.Date;
                    
                    if (_lastPacketTime != DateTime.MinValue && packetTime > _lastPacketTime)
                    {
                        var diff = packetTime - _lastPacketTime;
                        var delayMs = diff.TotalMilliseconds / PlaybackSpeed;
                        
                        if (delayMs > 0)
                        {
                             // high precision wait not strictly necessary for UI feel, 
                             // but Task.Delay is good enough for > 15ms.
                             // For smaller, maybe spin? For now Task.Delay is fine.
                             if (delayMs > 5) Thread.Sleep((int)delayMs); 
                        }
                    }
                    
                    _lastPacketTime = packetTime;

                    // Create NetTrack RawPacket
                    // SharpPcap PacketCapture.Data is ReadOnlySpan<byte>. We need array.
                    var raw = new RawPacket(packetTime, packetCapture.Data.ToArray(), (int)_device.LinkType);
                    
                    _pipelineService.InjectPacket(raw);
                }
            }
            catch (Exception)
            {
                IsPlaying = false;
            }
        }

        public void Dispose()
        {
            _cts?.Dispose();
            _device?.Close();
        }
    }
}
