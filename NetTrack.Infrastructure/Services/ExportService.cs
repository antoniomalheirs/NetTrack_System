using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace NetTrack.Infrastructure.Services
{
    public class ExportService : IExportService
    {
        public async Task ExportToJsonAsync(IEnumerable<PacketModel> packets, string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(packets, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task ExportToCsvAsync(IEnumerable<PacketModel> packets, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Timestamp,Source,Destination,Protocol,Length,Info");
            
            foreach (var p in packets)
            {
                sb.AppendLine($"{p.Id},{p.Timestamp},{p.SourceIP}:{p.SourcePort},{p.DestinationIP}:{p.DestinationPort},{p.Protocol},{p.Length},\"{p.Info.Replace("\"", "\"\"")}\"");
            }

            await File.WriteAllTextAsync(filePath, sb.ToString());
        }

        public async Task ExportToPcapAsync(IEnumerable<PacketModel> packets, string filePath)
        {
            // Simple mock PCAP export for now since raw data reconstruction is complex
            // Ideally we use SharpPcap.LibPcap.LibPcapDumper but logic is heavy.
            // For v1, we will just save raw bytes to a file.
            // Or better, skip PCAP for now if too complex, but user asked for it.
            // Let's implement a basic Global Header + Packet Header structure manually.
            
            using var fs = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // Global Header (24 bytes)
            writer.Write((uint)0xa1b2c3d4); // Magic number
            writer.Write((ushort)2); // Major Version
            writer.Write((ushort)4); // Minor Version
            writer.Write((int)0); // Snaplen
            writer.Write((uint)0); // Sigfigs
            writer.Write((uint)65535); // Snaplen
            writer.Write((uint)1); // LinkType (Ethernet)

            foreach (var p in packets)
            {
                if (p.OriginalData == null || p.OriginalData.Length == 0) continue;

                // Packet Header (16 bytes)
                long ticks = p.Timestamp.ToUniversalTime().Ticks - 621355968000000000;
                uint seconds = (uint)(ticks / 10000000);
                uint microseconds = (uint)((ticks % 10000000) / 10);

                writer.Write(seconds);
                writer.Write(microseconds);
                writer.Write((uint)p.OriginalData.Length); // Captured Length
                writer.Write((uint)p.OriginalData.Length); // Original Length
                
                writer.Write(p.OriginalData);
            }
        }
    }
}
