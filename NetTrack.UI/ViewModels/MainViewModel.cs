using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NetTrack.Client.ViewModels;
using NetTrack.Application.Interfaces;
using System;
using System.Threading;
using System.Linq; // Added for Enumerable

namespace NetTrack.Client.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICaptureService _captureService;
        private readonly IConnectionTracker _connectionTracker;
        private readonly IPortMonitor _portMonitor;
        private readonly IFilterService _filterService;
        private readonly IStorageService _storageService;
        private readonly IExportService _exportService;
        private readonly IPacketPipelineService _packetPipelineService;
        private readonly ISessionReplayService _sessionReplayService;

        private readonly System.Collections.Concurrent.ConcurrentQueue<global::NetTrack.Domain.Models.PacketModel> _uiQueue = new();
        private readonly System.Timers.Timer _uiTimer;
        
        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private int _packetCount;

        [ObservableProperty]
        private int _displayedPacketCount;

        [ObservableProperty]
        private string? _selectedInterface;

        public ObservableCollection<global::NetTrack.Domain.Models.PacketModel> Packets { get; } = new();
        public System.ComponentModel.ICollectionView PacketsView { get; }

        public ObservableCollection<global::NetTrack.Domain.Models.ConnectionModel> Connections => _connectionTracker.Connections;
        public ObservableCollection<global::NetTrack.Domain.Models.PortModel> Ports { get; } = new();

        public DashboardViewModel Dashboard { get; }
        public AlertViewModel Alerts { get; } 
        public ISessionReplayService SessionReplay => _sessionReplayService;

        [ObservableProperty]
        private int _currentViewIndex;

        public ObservableCollection<string> NetworkInterfaces { get; } = new();

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _isReplaying;

        [ObservableProperty]
        private bool _hasPendingSession;

        [ObservableProperty]
        private global::NetTrack.Domain.Models.SessionModel? _pendingSession;

        [ObservableProperty]
        private string? _fatalErrorMessage;

        [ObservableProperty]
        private bool _isAutoScrollEnabled = true;

        [RelayCommand]
        private void ApplyQuickFilter(string filter)
        {
            if (FilterText == filter) 
                FilterText = string.Empty; // Toggle off if same
            else
                FilterText = filter;
                
            ApplyFilter();
        }

        public MainViewModel(
            ICaptureService captureService, 
            IConnectionTracker connectionTracker, 
            IPortMonitor portMonitor, 
            IFilterService filterService, 
            IStorageService storageService, 
            IExportService exportService,
            IPacketPipelineService packetPipelineService,
            DashboardViewModel dashboardViewModel,
            AlertViewModel alertViewModel,
            ISessionReplayService sessionReplayService)
        {
            _captureService = captureService;
            _connectionTracker = connectionTracker;
            _portMonitor = portMonitor;
            _filterService = filterService;
            _storageService = storageService;
            _exportService = exportService;
            _packetPipelineService = packetPipelineService;
            _sessionReplayService = sessionReplayService;
            Dashboard = dashboardViewModel;
            Alerts = alertViewModel;
            
            _captureService.OnCaptureStopped += () => StatusMessage = "Capture Stopped";
            _captureService.OnError += (msg) => StatusMessage = $"Error: {msg}";

            LoadInterfaces();

            _uiTimer = new System.Timers.Timer(500); // 500ms
            _uiTimer.Elapsed += OnUiTimerElapsed;

            _ = _storageService.InitializeAsync(); 

            // Subscribe to Pipeline instead of CaptureService directly
            _packetPipelineService.PacketProcessed += OnPipelinePacketProcessed;
            
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Packets, new object());
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Ports, new object());
            
            // Setup CollectionView for Filtering
            PacketsView = System.Windows.Data.CollectionViewSource.GetDefaultView(Packets);
            PacketsView.Filter = FilterPackets;

            if (_connectionTracker is NetTrack.Infrastructure.Services.ConnectionTracker tracker)
            {
                System.Windows.Data.BindingOperations.EnableCollectionSynchronization(tracker.Connections, tracker.SyncRoot);
            }

            _ = InitializeAsync();

        }

        private async Task InitializeAsync()
        {
            await _storageService.InitializeAsync();
            await CheckForCrashedSessions();
        }

        private async Task CheckForCrashedSessions()
        {
            var sessions = await _storageService.GetSessionsAsync();
            foreach (var session in sessions)
            {
                if (session.Status == "Active")
                {
                    PendingSession = session;
                    HasPendingSession = true;
                    StatusMessage = "Warning: Unfinished session detected from previous run.";
                    break; // Only handle one for now
                }
            }
        }

        private void LoadInterfaces()
        {
            var devices = _captureService.GetAvailableDevices();
            NetworkInterfaces.Clear();
            foreach (var device in devices)
            {
                NetworkInterfaces.Add(device);
            }
            if (NetworkInterfaces.Count > 0) SelectedInterface = NetworkInterfaces[0];
        }

        private bool FilterPackets(object item)
        {
            if (string.IsNullOrWhiteSpace(FilterText)) return true;
            
            if (item is global::NetTrack.Domain.Models.PacketModel p)
            {
                // Simple text search for now (Case insensitive)
                if (_currentFilter != null)
                {
                    return _currentFilter(p);
                }
                
                return p.Protocol.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                       p.SourceIP.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                       p.DestinationIP.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                       p.Info.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private int _currentSessionId;
        private DateTime _sessionStartTime;

        [RelayCommand]
        private async Task RefreshPorts()
        {
             var activePorts = await _portMonitor.GetActivePortsAsync();
             System.Windows.Application.Current.Dispatcher.Invoke(() =>
             {
                 Ports.Clear();
                 foreach (var p in activePorts) Ports.Add(p);
             });
        }

        [RelayCommand]
        private async Task StartCapture()
        {
            if (SelectedInterface != null)
            {
                StatusMessage = $"Connecting to {SelectedInterface}...";
                try
                {
                    _sessionStartTime = DateTime.Now;
                    Packets.Clear();
                    PacketCount = 0;
                    
                    while(_uiQueue.TryDequeue(out _));

                    var session = new global::NetTrack.Domain.Models.SessionModel
                    {
                        StartTime = _sessionStartTime,
                        InterfaceName = SelectedInterface
                    };
                    _currentSessionId = await _storageService.SaveSessionAsync(session);

                    // Configure and Start Pipeline
                    _packetPipelineService.SetSessionId(_currentSessionId);
                    _packetPipelineService.Start();

                    _uiTimer.Start();

                    _connectionTracker.StartTracking();
                    await _captureService.StartCaptureAsync(SelectedInterface);
                    StatusMessage = "Capturing (Pipeline Active)...";
                    
                    await RefreshPorts();
                }
                catch (System.Exception ex)
                {
                    StatusMessage = $"Error: {ex.Message}";
                }
            }
        }
        [RelayCommand]
        private async Task StopCapture()
        {
            _connectionTracker.StopTracking();
            await _captureService.StopCaptureAsync();
            
            await _packetPipelineService.StopAsync();

            _uiTimer.Stop();
            ProcessUiQueue(); 

            // Update session status to Completed
            var sessions = await _storageService.GetSessionsAsync();
            var currentSession = System.Linq.Enumerable.FirstOrDefault(sessions, s => s.Id == _currentSessionId);
            if (currentSession != null)
            {
                currentSession.EndTime = DateTime.Now;
                currentSession.PacketCount = PacketCount;
                currentSession.Status = "Completed";
                await _storageService.UpdateSessionAsync(currentSession);
            }
            
            StatusMessage = "Stopped.";
        }

        [RelayCommand]
        private async Task RecoverSession()
        {
            if (PendingSession == null) return;

            StatusMessage = $"Recovering session {PendingSession.Id}...";
            var packets = await _storageService.GetPacketsAsync(PendingSession.Id);
            
            Packets.Clear();
            foreach (var p in packets) Packets.Add(p);
            PacketCount = Packets.Count;

            // Mark it as recovered/completed so we don't prompt again
            PendingSession.Status = "Recovered";
            await _storageService.UpdateSessionAsync(PendingSession);

            HasPendingSession = false;
            PendingSession = null;
            StatusMessage = "Session recovered.";
        }

        [RelayCommand]
        private async Task DiscardPendingSession()
        {
            if (PendingSession == null) return;

            PendingSession.Status = "Discarded";
            await _storageService.UpdateSessionAsync(PendingSession);

            HasPendingSession = false;
            PendingSession = null;
            StatusMessage = "Crashed session archived.";
        }

        [RelayCommand]
        private void Navigate(string index)
        {
            if (int.TryParse(index, out int i))
            {
                CurrentViewIndex = i;
            }
        }

        [RelayCommand]
        private async Task ExportPcap()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PCAP Files (*.pcap)|*.pcap" };
            if (dialog.ShowDialog() == true)
            {
                await _exportService.ExportToPcapAsync(Packets, dialog.FileName);
                StatusMessage = "Exported to PCAP.";
            }
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                await _exportService.ExportToCsvAsync(Packets, dialog.FileName);
                StatusMessage = "Exported to CSV.";
            }
        }

        private void OnUiTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Use Background priority to ensure UI interaction (clicks, scrolls) always comes first
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(ProcessUiQueue), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ProcessUiQueue()
        {
            if (_uiQueue.IsEmpty) return;

            var batch = new List<global::NetTrack.Domain.Models.PacketModel>();
            while (_uiQueue.TryDequeue(out var packet))
            {
                batch.Add(packet);
            }

            PacketCount += batch.Count; 
            
            // Add all packets from the batch (no longer dropping data!)
            foreach (var p in batch)
            {
                Packets.Add(p);
            }
            
            // "PRO" Buffer Size: 100,000 packets
            if (Packets.Count > 100000)
            {
                int itemsToRemove = Packets.Count - 100000;
                for (int i = 0; i < itemsToRemove; i++)
                {
                    Packets.RemoveAt(0);
                }
            }

            // Update displayed count
            if (PacketsView != null)
            {
                // Efficiently get count from the view
                int count = 0;
                foreach(var _ in PacketsView) count++;
                DisplayedPacketCount = count;
            }
            
            StatusMessage = $"Capturing... {PacketCount} packets";
        }

        private CancellationTokenSource? _detailParsingCts;

        private System.Predicate<global::NetTrack.Domain.Models.PacketModel>? _currentFilter = null;

        partial void OnFilterTextChanged(string value)
        {
             UpdateSuggestions();
        }

        [ObservableProperty]
        private ObservableCollection<string> _filterSuggestions = new();

        [ObservableProperty]
        private bool _isFilterPopupOpen;
        
        [ObservableProperty]
        private string? _selectedSuggestion;

        [ObservableProperty]
        private global::NetTrack.Domain.Models.PacketModel? _selectedPacket;

        partial void OnSelectedPacketChanged(global::NetTrack.Domain.Models.PacketModel? value)
        {
            _detailParsingCts?.Cancel();
            _detailParsingCts = new CancellationTokenSource();
            var token = _detailParsingCts.Token;

            if (value != null)
            {
                // Run heavy parsing on background thread
                Task.Run(async () => {
                    await ParsePacketDetailsAsync(value, token);
                    if (!token.IsCancellationRequested)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UpdateStreamStats(value));
                    }
                }, token);
            }
            else
            {
                PacketDetails.Clear();
                HexDump = string.Empty;
                StreamStats.Clear();
            }
        }

        public ObservableCollection<PacketDetailNode> PacketDetails { get; } = new();
        public ObservableCollection<KeyValuePair<string, string>> StreamStats { get; } = new();

        [ObservableProperty]
        private string _hexDump = string.Empty;

        private void UpdateStreamStats(global::NetTrack.Domain.Models.PacketModel packet)
        {
            StreamStats.Clear();
            
            var connection = _connectionTracker.GetConnection(packet.SourceIP, packet.SourcePort, packet.DestinationIP, packet.DestinationPort, packet.Protocol);
            
            if (connection != null)
            {
                StreamStats.Add(new KeyValuePair<string, string>("Status", connection.State));
                StreamStats.Add(new KeyValuePair<string, string>("Start Time", connection.StartTime.ToString("HH:mm:ss")));
                StreamStats.Add(new KeyValuePair<string, string>("Duration", (connection.LastActivity - connection.StartTime).ToString(@"hh\:mm\:ss")));
                StreamStats.Add(new KeyValuePair<string, string>("Bytes Transferred", FormatBytes(connection.BytesSent)));
                StreamStats.Add(new KeyValuePair<string, string>("Process", string.IsNullOrEmpty(connection.ProcessName) ? "Unknown" : connection.ProcessName));
            }
            else
            {
                // Basic packet info if no connection state found
                StreamStats.Add(new KeyValuePair<string, string>("Protocol", packet.Protocol));
                StreamStats.Add(new KeyValuePair<string, string>("Length", $"{packet.Length} bytes"));
                StreamStats.Add(new KeyValuePair<string, string>("Time", packet.Timestamp.ToString("HH:mm:ss.fff")));
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double)bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private async Task ParsePacketDetailsAsync(global::NetTrack.Domain.Models.PacketModel packet, CancellationToken token)
        {
            try
            {
                // Generate HexDump in background
                var hex = await Task.Run(() => FormatHexDump(packet.OriginalData), token);
                
                if (token.IsCancellationRequested) return;

                // Parse Packet Layers in background
                var details = new List<PacketDetailNode>();
                if (packet.OriginalData != null && packet.OriginalData.Length > 0)
                {
                    var p = PacketDotNet.Packet.ParsePacket(PacketDotNet.LinkLayers.Ethernet, packet.OriginalData);
                    var rootNode = new PacketDetailNode($"Frame {packet.Id}: {packet.Length} bytes");
                    AddLayerNodes(p, rootNode);
                    details.Add(rootNode);
                }

                if (token.IsCancellationRequested) return;

                // Final UI Update
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    PacketDetails.Clear();
                    HexDump = hex;
                    foreach (var node in details) PacketDetails.Add(node);
                });
            }
            catch (System.Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        PacketDetails.Clear();
                        PacketDetails.Add(new PacketDetailNode("Error parsing packet", ex.Message));
                    });
                }
            }
        }

        private void AddLayerNodes(PacketDotNet.Packet p, PacketDetailNode parent)
        {
            if (p == null) return;

            var layerName = p.GetType().Name.Replace("Packet", "");
            var node = new PacketDetailNode(layerName, p.ToString());
            
            foreach(var prop in p.GetType().GetProperties())
            {
                if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum)
                {
                    try 
                    {
                        var val = prop.GetValue(p);
                        if (val != null)
                        {
                            node.Children.Add(new PacketDetailNode(prop.Name, val.ToString() ?? ""));
                        }
                    }
                    catch {}
                }
            }

            parent.Children.Add(node);

            if (p.PayloadPacket != null)
            {
                AddLayerNodes(p.PayloadPacket, parent);
            }
        }

        private string FormatHexDump(byte[] data)
        {
            if (data == null || data.Length == 0) return "No Data";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < data.Length; i += 16)
            {
                sb.Append($"{i:X4}  ");

                // Hex
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                        sb.Append($"{data[i + j]:X2} ");
                    else
                        sb.Append("   ");
                }

                sb.Append("  ");

                // ASCII
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < data.Length)
                    {
                        char c = (char)data[i + j];
                        if (c < 32 || c > 126) c = '.';
                        sb.Append(c);
                    }
                }

                sb.AppendLine();
            }
            return sb.ToString();
        }

        [RelayCommand]
        private void CopySourceIp()
        {
            if (SelectedPacket != null)
            {
                System.Windows.Clipboard.SetText(SelectedPacket.SourceIP);
            }
        }

        [RelayCommand]
        private void CopyDestinationIp()
        {
            if (SelectedPacket != null)
            {
                System.Windows.Clipboard.SetText(SelectedPacket.DestinationIP);
            }
        }

        [RelayCommand]
        private void FilterByProtocol()
        {
            if (SelectedPacket != null)
            {
                FilterText = $"proto:{SelectedPacket.Protocol.ToLower()}";
                ApplyFilter();
            }
        }

        [RelayCommand]
        private async Task ExportPackets(string format)
        {
            try 
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.FileName = $"NetTrack_Export_{System.DateTime.Now:yyyyMMdd_HHmmss}";
                
                if (format == "JSON")
                {
                    dialog.DefaultExt = ".json";
                    dialog.Filter = "JSON Files (*.json)|*.json";
                }
                else if (format == "CSV")
                {
                    dialog.DefaultExt = ".csv";
                    dialog.Filter = "CSV Files (*.csv)|*.csv";
                }

                if (dialog.ShowDialog() == true)
                {
                     StatusMessage = "Exporting...";
                     if (format == "JSON") await _exportService.ExportToJsonAsync(Packets, dialog.FileName);
                     else if (format == "CSV") await _exportService.ExportToCsvAsync(Packets, dialog.FileName);
                     StatusMessage = $"Exported to {dialog.FileName}";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Export Error: {ex.Message}";
            }
        }
        private void FilterByStream()
        {
            if (SelectedPacket != null)
            {
                FilterText = $"ip:{SelectedPacket.SourceIP} ip:{SelectedPacket.DestinationIP} port:{SelectedPacket.SourcePort} port:{SelectedPacket.DestinationPort}";
                ApplyFilter();
            }
        }

        private void UpdateSuggestions()
        {
            var suggestions = _filterService.GetSuggestions(FilterText);
            FilterSuggestions.Clear();
            foreach (var s in suggestions)
            {
                FilterSuggestions.Add(s);
            }
            IsFilterPopupOpen = FilterSuggestions.Count > 0;
        }

        [RelayCommand]
        private void ConfirmSuggestion()
        {
            if (SelectedSuggestion != null)
            {
                FilterText = SelectedSuggestion;
                IsFilterPopupOpen = false;
                ApplyFilter();
            }
        }
        
        [RelayCommand]
        private void ApplyFilter()
        {
            try
            {
                var selected = SelectedPacket;
                _currentFilter = _filterService.CompileFilter(FilterText);
                PacketsView.Refresh(); 
                
                if (selected != null)
                {
                    SelectedPacket = selected;
                }
                
                IsFilterPopupOpen = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Filter Error: {ex.Message}";
            }
        }

        private void OnPipelinePacketProcessed(global::NetTrack.Domain.Models.PacketModel model)
        {
            if (_uiQueue.Count < 1000) 
            {
                model.Id = PacketCount + _uiQueue.Count + 1; 
                _uiQueue.Enqueue(model);
            }
        }
    }
}
