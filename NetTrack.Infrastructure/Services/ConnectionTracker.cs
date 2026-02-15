using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using PacketDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NetTrack.Infrastructure.Services
{
    public class ConnectionTracker : IConnectionTracker
    {
        private readonly ICaptureService _captureService;
        private readonly ConcurrentDictionary<string, ConnectionModel> _activeConnections = new();
        public ObservableCollection<ConnectionModel> Connections { get; } = new();

        // Used to sync ObservableCollection updates to UI
        public object SyncRoot { get; } = new object();

        public ConnectionTracker(ICaptureService captureService)
        {
            _captureService = captureService;
        }

        public void StartTracking()
        {
            _captureService.PacketReceived += OnPacketReceived;
        }

        public void StopTracking()
        {
            _captureService.PacketReceived -= OnPacketReceived;
        }

        private void OnPacketReceived(object? sender, RawPacket e)
        {
            try
            {
                var packet = Packet.ParsePacket((LinkLayers)e.LinkLayerType, e.Data);
                var ipPacket = packet.Extract<IPPacket>();
                
                if (ipPacket == null) return;

                var tcpPacket = packet.Extract<TcpPacket>();
                if (tcpPacket != null)
                {
                    UpdateTcpConnection(ipPacket, tcpPacket);
                }
            }
            catch { }
        }

        private void UpdateTcpConnection(IPPacket ip, TcpPacket tcp)
        {
            string src = ip.SourceAddress.ToString();
            string dst = ip.DestinationAddress.ToString();
            int sport = tcp.SourcePort;
            int dport = tcp.DestinationPort;

            // Normalize key: smaller IP first or something, OR direction aware?
            // For now, direction aware. key = "src:port-dst:port"
            string key = $"{src}:{sport}-{dst}:{dport}";
            string reverseKey = $"{dst}:{dport}-{src}:{sport}";

            // Check if reverse exists (response)
            bool isReverse = _activeConnections.ContainsKey(reverseKey);
            string activeKey = isReverse ? reverseKey : key;

            _activeConnections.AddOrUpdate(activeKey, 
                // Add new
                k => 
                {
                    var c = new ConnectionModel
                    {
                        Key = k,
                        SourceIP = src,
                        DestinationIP = dst,
                        SourcePort = sport,
                        DestinationPort = dport,
                        State = "ESTABLISHED", // Simplified assumption if we see traffic
                        StartTime = DateTime.Now,
                        LastActivity = DateTime.Now,
                        BytesSent = ip.TotalLength
                    };

                    AddToObservable(c);
                    return c;
                },
                // Update
                (k, c) => 
                {
                    c.LastActivity = DateTime.Now;
                    c.BytesSent += ip.TotalLength;
                    
                    if (tcp.Finished) c.State = "CLOSING";
                    if (tcp.Reset) c.State = "RESET";
                    
                    return c;
                });
        }

        private void AddToObservable(ConnectionModel c)
        {
            lock(SyncRoot)
            {
               if (!Connections.Contains(c)) Connections.Add(c);
            }
        }
        public ConnectionModel? GetConnection(string srcIp, int srcPort, string dstIp, int dstPort, string protocol)
        {
            // Currently we only track TCP connections
            if (!protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase)) return null;

            string key = $"{srcIp}:{srcPort}-{dstIp}:{dstPort}";
            string reverseKey = $"{dstIp}:{dstPort}-{srcIp}:{srcPort}";

            if (_activeConnections.TryGetValue(key, out var connection)) return connection;
            if (_activeConnections.TryGetValue(reverseKey, out var reverseConnection)) return reverseConnection;

            return null;
        }
    }
}
