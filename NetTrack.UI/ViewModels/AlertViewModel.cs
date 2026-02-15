using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace NetTrack.Client.ViewModels
{
    public partial class AlertViewModel : ObservableObject
    {
        private readonly IAlertService _alertService;

        public ObservableCollection<AlertRule> Rules => _alertService.Rules;
        public ObservableCollection<AlertNotification> Alerts => _alertService.Alerts;

        [ObservableProperty]
        private AlertRule _newRule = new AlertRule();

        [ObservableProperty]
        private AlertRule? _selectedRule;

        public AlertViewModel(IAlertService alertService)
        {
            _alertService = alertService;
            // Enable collection synchronization for cross-thread updates
            BindingOperations.EnableCollectionSynchronization(_alertService.Alerts, new object());
        }

        [RelayCommand]
        private void AddRule()
        {
            if (string.IsNullOrWhiteSpace(NewRule.Name)) NewRule.Name = "Rule " + (Rules.Count + 1);

            _alertService.AddRule(new AlertRule
            {
                Name = NewRule.Name,
                Protocol = NewRule.Protocol,
                SourceIP = NewRule.SourceIP,
                DestinationIP = NewRule.DestinationIP,
                MinLength = NewRule.MinLength,
                IsEnabled = NewRule.IsEnabled
            });

            // Reset form
            NewRule = new AlertRule();
        }

        [RelayCommand]
        private void RemoveRule()
        {
            if (SelectedRule != null)
            {
                _alertService.RemoveRule(SelectedRule);
            }
        }

        [RelayCommand]
        private void ClearAlerts()
        {
            _alertService.ClearAlerts();
        }
    }
}
