using System;
using System.Collections.ObjectModel;
using System.Linq;
using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;

namespace NetTrack.Infrastructure.Services
{
    public class AlertService : IAlertService
    {
        public ObservableCollection<AlertRule> Rules { get; } = new ObservableCollection<AlertRule>();
        public ObservableCollection<AlertNotification> Alerts { get; } = new ObservableCollection<AlertNotification>();

        // Lock for thread safety if accessed from multiple threads (UI + Pipeline)
        private readonly object _alertsLock = new object();

        public void AddRule(AlertRule rule)
        {
            if (!Rules.Contains(rule))
            {
                Rules.Add(rule);
            }
        }

        public void RemoveRule(AlertRule rule)
        {
            if (Rules.Contains(rule))
            {
                Rules.Remove(rule);
            }
        }

        public void ClearAlerts()
        {
            lock (_alertsLock)
            {
                Alerts.Clear();
            }
        }

        public void ProcessPacket(PacketModel packet)
        {
            foreach (var rule in Rules)
            {
                if (!rule.IsEnabled) continue;

                if (IsMatch(rule, packet))
                {
                    CreateAlert(rule, packet);
                }
            }
        }

        private bool IsMatch(AlertRule rule, PacketModel packet)
        {
            // Protocol
            if (rule.Protocol != "*" && !string.Equals(rule.Protocol, packet.Protocol, StringComparison.OrdinalIgnoreCase))
                return false;

            // Source IP
            if (rule.SourceIP != "*" && !packet.SourceIP.Contains(rule.SourceIP)) // Simple substring logic for now, or use full match
                return false;

            // Dest IP
            if (rule.DestinationIP != "*" && !packet.DestinationIP.Contains(rule.DestinationIP))
                return false;

            // Min Length
            if (packet.Length < rule.MinLength)
                return false;

            return true;
        }

        private void CreateAlert(AlertRule rule, PacketModel packet)
        {
            var notification = new AlertNotification
            {
                Timestamp = DateTime.Now,
                Message = $"Rule '{rule.Name}' Triggered: {packet.Protocol} {packet.SourceIP} -> {packet.DestinationIP} ({packet.Length} bytes)",
                PacketId = packet.Id,
                Severity = "Warning"
            };

            lock (_alertsLock)
            {
                // Dispatch to UI thread if we were purely in MVVM, but since this is infrastructure,
                // we rely on BindingOperations.EnableCollectionSynchronization in UI layer 
                // OR we accept that this might throw if bound directly. 
                // Best practice: Use a UI-safe collection wrapper in ViewModel, or use BindingOperations.EnableCollectionSynchronization.
                // For simplicity here, we add it. The ViewModel should enable sync.
                Alerts.Insert(0, notification);
                
                // Keep log size manageable
                if (Alerts.Count > 100)
                {
                    Alerts.RemoveAt(Alerts.Count - 1);
                }
            }
        }
    }
}
