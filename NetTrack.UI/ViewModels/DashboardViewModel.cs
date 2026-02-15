using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using NetTrack.Application.Interfaces;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Linq;

namespace NetTrack.Client.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDashboardService _dashboardService;
        private readonly System.Timers.Timer _updateTimer;

        public DashboardViewModel(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;

            // Packet Rate Chart
            PacketRateSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Values = new ObservableCollection<double>(),
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 1,
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 }
                }
            };

            // Protocol Distribution Chart (Column Series)
            ProtocolSeries = new ObservableCollection<ISeries>();

            _updateTimer = new System.Timers.Timer(1000); // 1 sec
            _updateTimer.Elapsed += _updateTimer_Elapsed;
            _updateTimer.Start();

            // Dark Mode Axes (Packet Rate)
            XAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 1 }
                }
            };

            YAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 1 }
                }
            };
            
            // Protocol Axes
            ProtocolXAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.LightGray),
                    SeparatorsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.Transparent) // Hide vertical separators
                }
            };

            ProtocolYAxes = new LiveChartsCore.SkiaSharpView.Axis[]
            {
                new LiveChartsCore.SkiaSharpView.Axis
                {
                    LabelsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.Gray),
                    SeparatorsPaint = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 1 }
                }
            };
        }

        public ObservableCollection<ISeries> PacketRateSeries { get; set; }
        public ObservableCollection<ISeries> ProtocolSeries { get; set; }
        public IEnumerable<ICartesianAxis> XAxes { get; set; }
        public IEnumerable<ICartesianAxis> YAxes { get; set; }
        public IEnumerable<ICartesianAxis> ProtocolXAxes { get; set; }
        public IEnumerable<ICartesianAxis> ProtocolYAxes { get; set; }


        [ObservableProperty]
        private string _currentBandwidthText = "Initializing...";

        private void _updateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            var rate = _dashboardService.GetCurrentPacketRate();
            var bw = _dashboardService.GetCurrentBandwidth();
            var protocols = _dashboardService.GetProtocolDistribution();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Update Packet Rate
                if (PacketRateSeries[0].Values is ObservableCollection<double> values)
                {
                    values.Add(rate);
                    if (values.Count > 60) values.RemoveAt(0); // Keep last 60 seconds
                }

                // Update Bandwidth Text
                CurrentBandwidthText = $"{FormatBytes(bw)}/s ({rate} pkts/s)";

                // Update Protocols
                UpdateProtocolChart(protocols);
            });
        }

        private void UpdateProtocolChart(System.Collections.Generic.Dictionary<string, int> protocols)
        {
            foreach (var kvp in protocols)
            {
                var series = ProtocolSeries.FirstOrDefault(s => s.Name == kvp.Key);
                if (series == null)
                {
                    series = new ColumnSeries<int> 
                    { 
                        Name = kvp.Key, 
                        Values = new ObservableCollection<int> { kvp.Value },
                        MaxBarWidth = 50,
                        Padding = 5
                    };
                    ProtocolSeries.Add(series);
                }
                else
                {
                    if (series.Values is ObservableCollection<int> values && values.Count > 0)
                    {
                        values[0] = kvp.Value;
                    }
                }
            }
        }

        private string FormatBytes(double bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes = bytes / 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
        }
    }
}
