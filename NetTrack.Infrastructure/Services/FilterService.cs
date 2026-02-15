using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System;
using System.Linq;

namespace NetTrack.Infrastructure.Services
{
    public class FilterService : IFilterService
    {
        public Predicate<PacketModel> CompileFilter(string filterString)
        {
            if (string.IsNullOrWhiteSpace(filterString))
            {
                return _ => true;
            }

            var terms = filterString.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return packet =>
            {
                bool matches = true;
                foreach (var term in terms)
                {
                    bool termMatch = false;

                    if (term.Contains(":"))
                    {
                        var parts = term.Split(':');
                        if (parts.Length == 2)
                        {
                            var key = parts[0];
                            var value = parts[1];

                            switch (key)
                            {
                                case "ip":
                                    termMatch = packet.SourceIP.Contains(value) || packet.DestinationIP.Contains(value);
                                    break;
                                case "src":
                                    termMatch = packet.SourceIP.Contains(value);
                                    break;
                                case "dst":
                                    termMatch = packet.DestinationIP.Contains(value);
                                    break;
                                case "port":
                                    termMatch = packet.SourcePort.ToString() == value || packet.DestinationPort.ToString() == value;
                                    break;
                                case "proto":
                                case "protocol":
                                    termMatch = packet.Protocol.ToLower().Contains(value);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // General search
                        termMatch = packet.Protocol.ToLower().Contains(term) ||
                                    packet.SourceIP.Contains(term) ||
                                    packet.DestinationIP.Contains(term) ||
                                    packet.Info.ToLower().Contains(term);
                    }

                    if (!termMatch)
                    {
                        matches = false;
                        break; // AND logic by default
                    }
                }
                return matches;
            };
        }
        public System.Collections.Generic.IEnumerable<string> GetSuggestions(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return System.Linq.Enumerable.Empty<string>();

            // extensive list of keys
            var validKeys = new[] { "ip:", "src:", "dst:", "port:", "proto:", "protocol:", "info:" };
            var protocols = new[] { "tcp", "udp", "http", "https", "tls", "dns", "icmp", "arp" };

            // Find the last token being typed
            // e.g. "ip:192 pr" -> "pr"
            var parts = input.Split(' ');
            var lastToken = parts.Last();

            // Case A: Token contains ':' -> Suggest values if we know the key
            if (lastToken.Contains(':'))
            {
                var tokenParts = lastToken.Split(':');
                var key = tokenParts[0].ToLower();
                var val = tokenParts.Length > 1 ? tokenParts[1].ToLower() : "";

                if (key == "proto" || key == "protocol")
                {
                    return protocols
                        .Where(p => p.StartsWith(val))
                        .Select(p => input.Substring(0, input.Length - val.Length) + p); // Replace last part
                }
                return System.Linq.Enumerable.Empty<string>();
            }

            // Case B: Token is a key prefix -> Suggest keys
            return validKeys
                .Where(k => k.StartsWith(lastToken.ToLower()))
                .Select(k => input.Substring(0, input.Length - lastToken.Length) + k);
        }
    }
}
