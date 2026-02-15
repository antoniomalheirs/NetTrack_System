using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetTrack.Infrastructure.Services
{
    public class PacketBuffer : IDisposable
    {
        private readonly IStorageService _storageService;
        private readonly ConcurrentQueue<PacketModel> _queue = new();
        private readonly Timer _flushTimer;
        private readonly int _batchSize = 100;
        private int _currentSessionId;
        private bool _isCapturing;

        private readonly SemaphoreSlim _flushLock = new(1, 1);

        public PacketBuffer(IStorageService storageService)
        {
            _storageService = storageService;
            _flushTimer = new Timer(OnFlushTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start(int sessionId)
        {
            _currentSessionId = sessionId;
            _isCapturing = true;
            _flushTimer.Change(1000, 1000);
        }

        public async Task StopAsync()
        {
            _isCapturing = false;
            _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
            await FlushAsync();
        }

        public void Add(PacketModel packet)
        {
            if (!_isCapturing) return;
            _queue.Enqueue(packet);
        }

        private void OnFlushTimer(object? state)
        {
            _ = FlushAsync();
        }

        private async Task FlushAsync()
        {
            if (_queue.IsEmpty) return;
            
            // Prevent multiple concurrent flushes
            if (!await _flushLock.WaitAsync(0)) return;

            try 
            {
                var batch = new List<PacketModel>();
                while (_queue.TryDequeue(out var packet))
                {
                    batch.Add(packet);
                    if (batch.Count >= 500) break; 
                }

                if (batch.Count > 0)
                {
                    try
                    {
                        await _storageService.SavePacketsAsync(_currentSessionId, batch);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error flushing packets: {ex.Message}");
                    }
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }

        public void Dispose()
        {
            _flushTimer.Dispose();
        }
    }
}
