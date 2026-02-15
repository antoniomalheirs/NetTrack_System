using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NetTrack.Client.Converters
{
    public class ProtocolColorConverter : IValueConverter
    {
        // NO STATIC FIELDS to prevent TypeInitializer errors

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string protocol)
            {
                var p = protocol.ToUpperInvariant();
                
                // Specific Colors
                if (p.Contains("TCP")) return CreateFrozenBrush(30, 144, 255);    // DodgerBlue
                if (p.Contains("UDP")) return CreateFrozenBrush(255, 69, 0);      // OrangeRed
                if (p.Contains("HTTP")) return CreateFrozenBrush(46, 139, 87);    // SeaGreen
                if (p.Contains("HTTPS")) return CreateFrozenBrush(46, 139, 87);   // SeaGreen
                if (p.Contains("TLS")) return CreateFrozenBrush(46, 139, 87);     // SeaGreen
                if (p.Contains("DNS")) return CreateFrozenBrush(147, 112, 219);   // MediumPurple
                if (p.Contains("ICMP")) return CreateFrozenBrush(199, 21, 133);   // MediumVioletRed
                if (p.Contains("ARP")) return CreateFrozenBrush(218, 165, 32);    // Goldenrod
                if (p.Contains("SSH")) return CreateFrozenBrush(139, 0, 0);       // DarkRed
                if (p.Contains("FTP")) return CreateFrozenBrush(65, 105, 225);    // RoyalBlue
                if (p.Contains("SMTP")) return CreateFrozenBrush(60, 179, 113);   // MediumSeaGreen
                if (p.Contains("ERROR")) return CreateFrozenBrush(220, 20, 60);   // Crimson
                
                return CreateFrozenBrush(105, 105, 105); // DimGray default
            }
            return Brushes.White;
        }

        private SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
