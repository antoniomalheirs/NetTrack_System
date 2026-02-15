using System;
using System.Globalization;
using System.Windows.Data;

namespace NetTrack.Client.Converters
{
    public class BytesToSizeConverter : IValueConverter
    {
        private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                double len = (double)bytes;
                int order = 0;
                while (len >= 1024 && order < Sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {Sizes[order]}";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
