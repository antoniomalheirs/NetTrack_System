using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using NetTrack.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NetTrack.Infrastructure.Services
{
    public class PortMonitor : IPortMonitor
    {
        public Task<IEnumerable<PortModel>> GetActivePortsAsync()
        {
            return Task.Run(() =>
            {
                var ports = new List<PortModel>();
                var tcpRows = GetAllTcpConnections();

                foreach (var row in tcpRows)
                {
                    try
                    {
                        var port = (row.localPort[0] << 8) + row.localPort[1];
                        var localAddr = new IPAddress(row.localAddr).ToString();
                        
                        var model = new PortModel
                        {
                            Port = port,
                            Protocol = "TCP",
                            LocalAddress = localAddr,
                            State = ((System.Net.NetworkInformation.TcpState)row.state).ToString(),
                            ProcessId = (int)row.owningPid
                        };

                        try
                        {
                            var process = Process.GetProcessById((int)row.owningPid);
                            model.ProcessName = process.ProcessName;
                        }
                        catch
                        {
                            model.ProcessName = "Unknown/System";
                        }

                        ports.Add(model);
                    }
                    catch { }
                }

                return (IEnumerable<PortModel>)ports;
            });
        }

        private List<NativeMethods.MIB_TCPROW_OWNER_PID> GetAllTcpConnections()
        {
            var tableRows = new List<NativeMethods.MIB_TCPROW_OWNER_PID>();

            int afInet = NativeMethods.AF_INET;
            int buffSize = 0;

            // Get buffer size
            NativeMethods.GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, afInet, NativeMethods.TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);

            IntPtr buffTable = Marshal.AllocHGlobal(buffSize);

            try
            {
                uint ret = NativeMethods.GetExtendedTcpTable(buffTable, ref buffSize, true, afInet, NativeMethods.TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);
                
                if (ret != 0) return tableRows;

                var tab = (NativeMethods.MIB_TCPTABLE_OWNER_PID)Marshal.PtrToStructure(buffTable, typeof(NativeMethods.MIB_TCPTABLE_OWNER_PID))!;
                
                // Manually calculate offset because the array is variable length in C++ but fixed in our struct definition
                // We need to iterate carefully
                int rowSize = Marshal.SizeOf(typeof(NativeMethods.MIB_TCPROW_OWNER_PID));
                IntPtr currentPtr = (IntPtr)((long)buffTable + sizeof(uint)); // Skip dwNumEntries

                for (int i = 0; i < tab.dwNumEntries; i++)
                {
                    var row = (NativeMethods.MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(currentPtr, typeof(NativeMethods.MIB_TCPROW_OWNER_PID))!;
                    tableRows.Add(row);
                    currentPtr = (IntPtr)((long)currentPtr + rowSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffTable);
            }

            return tableRows;
        }
    }
}
