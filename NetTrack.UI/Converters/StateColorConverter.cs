using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NetTrack.Client.Converters
{
    public class StateColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string state)
            {
                return state.ToUpper() switch
                {
                    "ESTABLISHED" => Brushes.LimeGreen,
                    "SYN_SENT" => Brushes.Yellow,
                    "SYN_RECEIVED" => Brushes.Orange,
                    "FIN_WAIT" => Brushes.Cyan,
                    "CLOSE_WAIT" => Brushes.Cyan,
                    "CLOSING" => Brushes.MediumPurple,
                    "LAST_ACK" => Brushes.MediumPurple,
                    "TIME_WAIT" => Brushes.Gray,
                    "CLOSED" => Brushes.Red,
                    "RESET" => Brushes.Red,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
