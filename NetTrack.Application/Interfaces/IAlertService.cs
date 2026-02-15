using System.Collections.ObjectModel;
using NetTrack.Domain.Models;

namespace NetTrack.Application.Interfaces
{
    public interface IAlertService
    {
        ObservableCollection<AlertRule> Rules { get; }
        ObservableCollection<AlertNotification> Alerts { get; }

        void AddRule(AlertRule rule);
        void RemoveRule(AlertRule rule);
        void ClearAlerts();
        
        // Method to process packet (called by pipeline or capture service)
        void ProcessPacket(PacketModel packet);
    }
}
