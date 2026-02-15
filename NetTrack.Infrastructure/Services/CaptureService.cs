using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using SharpPcap;
using Microsoft.Extensions.Logging;

namespace NetTrack.Infrastructure.Services
{
    public class CaptureService : ICaptureService
    {
        private readonly ILogger<CaptureService> _logger;
        private ICaptureDevice? _device;
        public event EventHandler<RawPacket>? PacketReceived;
        public event Action? OnCaptureStopped;
        public event Action<string>? OnError;

        public CaptureService(ILogger<CaptureService> logger)
        {
            _logger = logger;
        }

        public ObservableCollection<string> GetAvailableDevices()
        {
            var devices = new ObservableCollection<string>();
            try
            {
                var allDevices = CaptureDeviceList.Instance;
                if (allDevices.Count == 0)
                {
                    devices.Add("No interfaces found! Check Npcap.");
                }
                foreach (var device in allDevices)
                {
                    // Use Description for friendlier name
                    devices.Add(device.Description); 
                }
            }
            catch (DllNotFoundException)
            {
                devices.Add("Error: wpcap.dll not found. Please install Npcap.");
            }
            catch (Exception ex)
            {
                devices.Add($"Error: {ex.Message}");
            }
            return devices;
        }

        public Task StartCaptureAsync(string deviceName)
        {
            return Task.Run(() =>
            {
                try
                {
                    var devices = CaptureDeviceList.Instance;
                    // Match by Description since that's what we populate now
                    _device = devices.FirstOrDefault(d => d.Description == deviceName);

                    if (_device == null)
                    {
                         // Provide more robust error handling
                         throw new InvalidOperationException($"Device '{deviceName}' not found.");
                    }

                    _device.OnPacketArrival += Device_OnPacketArrival;

                    // Open the device for capturing
                    int readTimeoutMilliseconds = 1000;
                    _device.Open(DeviceModes.Promiscuous, readTimeoutMilliseconds);

                    _device.StartCapture();
                    _logger.LogInformation("Capture started on device: {DeviceName}", deviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start capture on device: {DeviceName}", deviceName);
                    OnError?.Invoke(ex.Message);
                    throw; 
                }
            });
        }

        public Task StopCaptureAsync()
        {
             return Task.Run(() =>
            {
                try
                {
                    if (_device != null)
                    {
                        _device.StopCapture();
                        _device.Close();
                        _device.OnPacketArrival -= Device_OnPacketArrival;
                        _device = null;
                        _logger.LogInformation("Capture stopped.");
                        OnCaptureStopped?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping capture.");
                }
            });
        }

        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            var packet = e.GetPacket();
            var length = packet.Data.Length;
            
            // Rent buffer
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            
            // Copy data
            Array.Copy(packet.Data, buffer, length);
            
            var rawPacket = new RawPacket(packet.Timeval.Date, buffer, length, (int)packet.LinkLayerType);
            
            PacketReceived?.Invoke(this, rawPacket);
        }
    }
}
