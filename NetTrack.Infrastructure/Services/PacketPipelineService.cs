using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetTrack.Infrastructure.Services
{
    public class PacketPipelineService : IPacketPipelineService, IDisposable
    {
        private readonly ILogger<PacketPipelineService> _logger;
        private readonly ICaptureService _captureService;
        private readonly IPacketParser _packetParser;
        private readonly IStorageService _storageService;
        private readonly IAlertService _alertService;

        // Channels
        private readonly Channel<RawPacket> _rawPacketChannel;
        private readonly Channel<PacketModel> _storageChannel;
        private readonly Channel<PacketModel> _uiChannel;

        private CancellationTokenSource? _cts;
        private Task? _pipelineTask;
        private int _currentSessionId;

        public event Action<PacketModel>? PacketProcessed;

        public PacketPipelineService(
            ILogger<PacketPipelineService> logger,
            ICaptureService captureService,
            IPacketParser packetParser,
            IStorageService storageService,
            IAlertService alertService)
        {
            _logger = logger;
            _captureService = captureService;
            _packetParser = packetParser;
            _storageService = storageService;
            _alertService = alertService;

            // Capture -> Parser
            _rawPacketChannel = Channel.CreateUnbounded<RawPacket>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            
            // Parser -> Storage
            _storageChannel = Channel.CreateUnbounded<PacketModel>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            
            // Parser -> UI
            _uiChannel = Channel.CreateUnbounded<PacketModel>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        }

        public void SetSessionId(int sessionId)
        {
            _currentSessionId = sessionId;
        }

        public void Start()
        {
            if (_cts != null && !_cts.IsCancellationRequested) return;

            _cts = new CancellationTokenSource();
            _captureService.PacketReceived += OnRawPacketReceived;

            _pipelineTask = Task.WhenAll(
                ParseWorker(_cts.Token),
                StorageWorker(_cts.Token),
                UiDispatchWorker(_cts.Token)
            );
            _logger.LogInformation("Pipeline started.");
        }

        public async Task StopAsync()
        {
            _captureService.PacketReceived -= OnRawPacketReceived;

            _cts?.Cancel();
            if (_pipelineTask != null)
            {
                try { await _pipelineTask; } catch (OperationCanceledException) { }
            }
            _cts?.Dispose();
            _cts = null;
            _logger.LogInformation("Pipeline stopped.");
        }

        private void OnRawPacketReceived(object? sender, RawPacket raw)
        {
            _rawPacketChannel.Writer.TryWrite(raw);
        }

        private async Task ParseWorker(CancellationToken token)
        {
            var reader = _rawPacketChannel.Reader;
            try
            {
                while (await reader.WaitToReadAsync(token))
                {
                    while (reader.TryRead(out var raw))
                    {
                        try
                        {
                            try
                            {
                                // Ensure PacketParser respects DataLength! 
                                var model = _packetParser.Parse(raw);
                                
                                // Check for Alerts
                                _alertService.ProcessPacket(model);
                                
                                // Fan-out: Send to both consumers
                                _storageChannel.Writer.TryWrite(model);
                                _uiChannel.Writer.TryWrite(model);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error parsing packet.");
                            }
                        }
                        finally
                        {
                            // Return buffer to pool
                            if (raw.RentedBuffer != null)
                            {
                                System.Buffers.ArrayPool<byte>.Shared.Return(raw.RentedBuffer);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task StorageWorker(CancellationToken token)
        {
            var reader = _storageChannel.Reader;
            var batch = new List<PacketModel>(100);
            
            try
            {
                while (await reader.WaitToReadAsync(token))
                {
                    while (reader.TryRead(out var packet))
                    {
                        batch.Add(packet);
                        if (batch.Count >= 100)
                        {
                            await SaveBatchAsync(batch);
                            batch.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Storage worker failed.");
            }
        }

        private async Task SaveBatchAsync(List<PacketModel> batch)
        {
            try
            {
                await _storageService.SavePacketsAsync(_currentSessionId, new List<PacketModel>(batch));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save packet batch to storage.");
            }
        }

        private async Task UiDispatchWorker(CancellationToken token)
        {
            var reader = _uiChannel.Reader;
            try
            {
                while (await reader.WaitToReadAsync(token))
                {
                    while (reader.TryRead(out var packet))
                    {
                        // Fire event to UI (UI typically handles throttling itself, or we could throttle here)
                        // For now, let's fire everything and let MainViewModel's batcher handle it?
                        // Actually, MainViewModel's batcher expects us to enqueue.
                        // We can just invoke the event.
                        try
                        {
                            PacketProcessed?.Invoke(packet);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error dispatching packet to UI subscribers.");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void InjectPacket(RawPacket packet)
        {
            _rawPacketChannel.Writer.TryWrite(packet);
        }

        public void Dispose()
        {
            _cts?.Dispose();
        }
    }
}
