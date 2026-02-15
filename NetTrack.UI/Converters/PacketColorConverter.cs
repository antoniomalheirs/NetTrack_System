using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NetTrack.Domain.Models;

namespace NetTrack.Client.Converters
{
    public class PacketColorConverter : IValueConverter
    {
        // NO STATIC FIELDS to prevent TypeInitializer errors
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PacketModel packet)
            {
                var proto = packet.Protocol.ToUpperInvariant();

                if (proto.Contains("HTTP")) return CreateFrozenBrush(230, 255, 204); // Light Green
                if (proto.Contains("TCP")) return CreateFrozenBrush(230, 230, 250);  // Lavender
                if (proto.Contains("UDP")) return CreateFrozenBrush(224, 240, 255);  // Light Blue
                if (proto.Contains("ICMP")) return CreateFrozenBrush(252, 224, 255); // Light Pink
                if (proto.Contains("ARP")) return CreateFrozenBrush(250, 240, 215);  // Light Orange/Tan
                if (proto.Contains("TLS") || proto.Contains("SSL")) return CreateFrozenBrush(224, 224, 224); // Light Gray
            }
            else if (value is string protocol)
            {
                var proto = protocol.ToUpperInvariant();
                if (proto.Contains("HTTP")) return CreateFrozenBrush(230, 255, 204);
                if (proto.Contains("TCP")) return CreateFrozenBrush(230, 230, 250);
                if (proto.Contains("UDP")) return CreateFrozenBrush(224, 240, 255);
                if (proto.Contains("ICMP")) return CreateFrozenBrush(252, 224, 255);
                if (proto.Contains("ARP")) return CreateFrozenBrush(250, 240, 215);
                if (proto.Contains("TLS") || proto.Contains("SSL")) return CreateFrozenBrush(224, 224, 224);
            }

            return Brushes.Transparent;
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
