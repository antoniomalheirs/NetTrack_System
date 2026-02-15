using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NetTrack.Domain.Models
{
    public partial class AlertRule : ObservableObject
    {
        [ObservableProperty]
        private string _name = "New Rule";

        [ObservableProperty]
        private string _protocol = "*"; // TCP, UDP, ICMP, *

        [ObservableProperty]
        private string _sourceIP = "*";

        [ObservableProperty]
        private string _destinationIP = "*";

        [ObservableProperty]
        private int _minLength = 0;

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
