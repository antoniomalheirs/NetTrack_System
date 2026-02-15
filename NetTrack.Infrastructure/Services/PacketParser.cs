using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using PacketDotNet;
using System;

namespace NetTrack.Infrastructure.Services
{
    public class PacketParser : IPacketParser
    {
        public PacketModel Parse(RawPacket rawPacket)
        {
            // COPY the data because the rawPacket.Data is a rented buffer that will be returned to the pool immediately after this.
            // PacketModel keeps this data for UI/Storage, so it needs its own copy.
            var dataCopy = new byte[rawPacket.DataLength];
            Array.Copy(rawPacket.Data, dataCopy, rawPacket.DataLength);

            var model = new PacketModel
            {
                Timestamp = rawPacket.Timeval,
                Length = rawPacket.DataLength,
                OriginalData = dataCopy
            };

            try
            {
                var packet = Packet.ParsePacket((LinkLayers)rawPacket.LinkLayerType, dataCopy);

                // Ethernet
                if (packet is EthernetPacket ethPacket)
                {
                    // 1. ARP
                    var arpPacket = packet.Extract<ArpPacket>();
                    if (arpPacket != null)
                    {
                        model.Protocol = "ARP";
                        model.Info = $"Op={arpPacket.Operation} Src={arpPacket.SenderProtocolAddress} Dst={arpPacket.TargetProtocolAddress}";
                        model.SourceIP = arpPacket.SenderProtocolAddress.ToString();
                        model.DestinationIP = arpPacket.TargetProtocolAddress.ToString();
                        return model;
                    }

                    // 2. IP (v4/v6)
                    var ipPacket = packet.Extract<IPPacket>();
                    if (ipPacket != null)
                    {
                        model.SourceIP = ipPacket.SourceAddress.ToString();
                        model.DestinationIP = ipPacket.DestinationAddress.ToString();
                        model.Protocol = ipPacket.Protocol.ToString();

                        // 3. ICMP
                        var icmpPacket = packet.Extract<IcmpV4Packet>();
                        if (icmpPacket != null)
                        {
                            model.Protocol = "ICMP";
                            model.Info = $"Type={icmpPacket.TypeCode} Id={icmpPacket.Id} Seq={icmpPacket.Sequence}";
                            return model;
                        }
                        var icmp6Packet = packet.Extract<IcmpV6Packet>();
                        if (icmp6Packet != null)
                        {
                            model.Protocol = "ICMPv6";
                            model.Info = $"Type={icmp6Packet.Type} Code={icmp6Packet.Code}";
                            return model;
                        }

                        // 4. TCP -> HTTP / TLS / Others
                        var tcpPacket = packet.Extract<TcpPacket>();
                        if (tcpPacket != null)
                        {
                            model.SourcePort = tcpPacket.SourcePort;
                            model.DestinationPort = tcpPacket.DestinationPort;
                            model.Protocol = "TCP";
                            model.Info = $"Seq={tcpPacket.SequenceNumber} Ack={tcpPacket.AcknowledgmentNumber} Flags={tcpPacket.Flags}";

                            // Application Layer Detection
                            if (model.SourcePort == 80 || model.DestinationPort == 80)
                            {
                                model.Protocol = "HTTP";
                                if (tcpPacket.PayloadData != null && tcpPacket.PayloadData.Length > 0)
                                {
                                    string payload = System.Text.Encoding.ASCII.GetString(tcpPacket.PayloadData);
                                    if (payload.StartsWith("GET") || payload.StartsWith("POST") || payload.StartsWith("HTTP"))
                                        model.Info = payload.Split('\r', '\n')[0];
                                }
                            }
                            else if (model.SourcePort == 443 || model.DestinationPort == 443)
                            {
                                model.Protocol = "TLS";
                                model.Info = "Encrypted Application Data";
                                if (tcpPacket.PayloadData != null && tcpPacket.PayloadData.Length > 0 && tcpPacket.PayloadData[0] == 0x16)
                                {
                                    model.Info = "TLS Handshake";
                                }
                            }
                            else if (model.SourcePort == 22 || model.DestinationPort == 22) { model.Protocol = "SSH"; model.Info = "Secure Shell"; }
                            else if (model.SourcePort == 21 || model.DestinationPort == 21) { model.Protocol = "FTP"; model.Info = "File Transfer Protocol (Control)"; }
                            else if (model.SourcePort == 23 || model.DestinationPort == 23) { model.Protocol = "TELNET"; model.Info = "Telnet Protocol"; }
                            else if (model.SourcePort == 25 || model.DestinationPort == 25) { model.Protocol = "SMTP"; model.Info = "Simple Mail Transfer"; }
                        }
                        // 5. UDP -> DNS / SNMP / Others
                        else
                        {
                            var udpPacket = packet.Extract<UdpPacket>();
                            if (udpPacket != null)
                            {
                                model.SourcePort = udpPacket.SourcePort;
                                model.DestinationPort = udpPacket.DestinationPort;
                                model.Protocol = "UDP";
                                model.Info = $"Len={udpPacket.Length} Checksum={udpPacket.Checksum}";

                                if (model.SourcePort == 53 || model.DestinationPort == 53)
                                {
                                    model.Protocol = "DNS";
                                    model.Info = "DNS Query/Response";
                                }
                                else if (model.SourcePort == 161 || model.DestinationPort == 161)
                                {
                                    model.Protocol = "SNMP";
                                    model.Info = "Simple Network Management Protocol";
                                }
                                else if (model.SourcePort == 123 || model.DestinationPort == 123)
                                {
                                    model.Protocol = "NTP";
                                    model.Info = "Network Time Protocol";
                                }
                            }
                        }
                    }
                    else
                    {
                        // Non-IP (e.g. valid Ethernet but not IP/ARP)
                        model.Protocol = ethPacket.Type.ToString();
                        model.Info = "Unknown Ethernet Frame";
                    }
                }
            }
            catch (Exception ex)
            {
                model.Info = $"Error parsing: {ex.Message}";
            }

            return model;
        }
    }
}
