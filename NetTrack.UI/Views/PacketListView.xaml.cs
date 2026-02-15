using System.Windows.Controls;

namespace NetTrack.Client.Views
{
    public partial class PacketListView : UserControl
    {


        public PacketListView()
        {
            InitializeComponent();
            this.Loaded += PacketListView_Loaded;
        }

        private bool _isScrollPending = false;

        private void PacketListView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                // Subscribe to selection change
                PacketsGrid.SelectionChanged += (s, ev) => {
                    if (PacketsGrid.SelectedItem != null)
                    {
                        vm.IsAutoScrollEnabled = false;
                        PacketsGrid.ScrollIntoView(PacketsGrid.SelectedItem);
                    }
                };

                // Handle manual scroll to toggle auto-scroll
                var scrollViewer = GetScrollViewer(PacketsGrid);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged += (s, ev) => {
                        // Only disable if user scrolls UP significantly
                        if (ev.VerticalChange < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 10) 
                            vm.IsAutoScrollEnabled = false;
                        
                        // Re-enable if user scrolls to bottom
                        if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 2) 
                            vm.IsAutoScrollEnabled = true;
                    };
                }

                // Monitor View for updates with optimizations
                vm.Packets.CollectionChanged += (s, ev) => {
                    if (vm.IsAutoScrollEnabled && ev.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    {
                        if (!_isScrollPending)
                        {
                            _isScrollPending = true;
                            // Use lower priority to avoid freezing the UI thread during high traffic
                            Dispatcher.BeginInvoke(new System.Action(() => {
                                try
                                {
                                    if (PacketsGrid.Items.Count > 0)
                                    {
                                        var lastItem = PacketsGrid.Items[PacketsGrid.Items.Count - 1];
                                        PacketsGrid.ScrollIntoView(lastItem);
                                    }
                                }
                                finally { _isScrollPending = false; }
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                };
            }
        }

        private System.Windows.Controls.ScrollViewer GetScrollViewer(System.Windows.DependencyObject element)
        {
            if (element is System.Windows.Controls.ScrollViewer) return (System.Windows.Controls.ScrollViewer)element;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
