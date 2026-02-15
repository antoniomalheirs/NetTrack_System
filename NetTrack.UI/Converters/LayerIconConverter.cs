using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace NetTrack.Client.Converters
{
    public class LayerIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string layerName)
            {
                // Normalize
                string name = layerName.ToUpperInvariant();

                if (name.Contains("ETHERNET")) return PackIconKind.EthernetCable;
                if (name.Contains("IP")) return PackIconKind.Web;
                if (name.Contains("TCP")) return PackIconKind.LanConnect;
                if (name.Contains("UDP")) return PackIconKind.PackageVariant;
                if (name.Contains("DNS")) return PackIconKind.Dns;
                if (name.Contains("HTTP")) return PackIconKind.Web;
                if (name.Contains("TLS") || name.Contains("SSL")) return PackIconKind.Lock;
                if (name.Contains("ARP")) return PackIconKind.LanPending;
                if (name.Contains("PAYLOAD")) return PackIconKind.FileCode;
            }
            return PackIconKind.Layers; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
