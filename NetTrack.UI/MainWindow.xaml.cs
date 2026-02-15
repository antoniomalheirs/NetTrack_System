using System.Windows;
using MahApps.Metro.Controls;
using NetTrack.Client.ViewModels;

namespace NetTrack.Client;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}