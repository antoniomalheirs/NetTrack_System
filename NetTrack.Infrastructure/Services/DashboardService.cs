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
    public class DashboardService : IDashboardService, IDisposable
    {
        private readonly IPacketPipelineService _pipelineService;
        private long _totalPackets;
        private long _totalBytes;
        
        // Sliding window for rate calculation (buckets of 1 second)
        // Actually, for simplicity and performance, we can just track counts in the last second.
        // Or use accurate windowing. Let's use a simple "Last Second" counter that resets.
        
        private int _packetsLastSecond;
        private long _bytesLastSecond;
        
        private double _currentPacketRate;
        private double _currentBandwidth;

        private readonly ConcurrentDictionary<string, int> _protocolCounts = new();
        private readonly Timer _tickTimer;

        public DashboardService(IPacketPipelineService pipelineService)
        {
            _pipelineService = pipelineService;
            _pipelineService.PacketProcessed += OnPacketProcessed;
            
            // Update rates every 1 second
            _tickTimer = new Timer(OnTick, null, 1000, 1000);
        }

        private void OnPacketProcessed(PacketModel packet)
        {
            // Console.WriteLine($"[DashboardService] Packet Processed: {packet.Length} bytes");
            Interlocked.Increment(ref _packetsLastSecond);
            Interlocked.Add(ref _bytesLastSecond, packet.Length);
            
            // Update Protocol Distribution
            // Use AddOrUpdate for concurrency
            _protocolCounts.AddOrUpdate(packet.Protocol, 1, (key, oldValue) => oldValue + 1);
        }

        private void OnTick(object? state)
        {
            // Snapshot and reset atomic counters
            var packets = Exchange(ref _packetsLastSecond, 0);
            var bytes = Exchange(ref _bytesLastSecond, 0);

            _currentPacketRate = packets; // Since it's per 1 second
            _currentBandwidth = bytes;
            
            // Console.WriteLine($"[DashboardService] Tick: {packets} pkts/s, {bytes} bytes/s");
        }

        // Helper for simpler exchange
        private int Exchange(ref int location, int value) => Interlocked.Exchange(ref location, value);
        private long Exchange(ref long location, long value) => Interlocked.Exchange(ref location, value);

        public double GetCurrentPacketRate() => _currentPacketRate;
        
        public double GetCurrentBandwidth() => _currentBandwidth;

        public Dictionary<string, int> GetProtocolDistribution()
        {
            return new Dictionary<string, int>(_protocolCounts);
        }

        public void ResetStats()
        {
            _packetsLastSecond = 0;
            _bytesLastSecond = 0;
            _currentPacketRate = 0;
            _currentBandwidth = 0;
            _protocolCounts.Clear();
        }

        public void Dispose()
        {
            _pipelineService.PacketProcessed -= OnPacketProcessed;
            _tickTimer.Dispose();
        }
    }
}
