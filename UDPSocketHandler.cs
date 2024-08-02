using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static UDPLogger.MainWindow;

namespace UDPLogger
{
    public class UDPSocketHandler
    {

        public delegate void ReportGridDataEventHandler(object? sender, ReportGridDataEventArgs args);
        public record ReportGridDataEventArgs(List<GridData> GridDataList);

        public delegate void ReportConnectionStatusEventHandler(object? sender, ReportConnectionStatusEventArgs args);
        public record ReportConnectionStatusEventArgs(bool ConnectionStatus);

        public record UdpWorkerGridDataReport(List<GridData> GridDataList);
        public record UdpWorkeConnectionStatusReport(bool ConnectionStatus);


        public event ReportGridDataEventHandler ReportGridDataEvent = delegate { };
        public event ReportConnectionStatusEventHandler ReportConnectionStatusEvent = delegate { };

        const Int32 PLC_PORT = 8958;
        const Int32 SERVER_PORT = 10000;

        private readonly BackgroundWorker udpWorker;

        private volatile bool connect;
        private volatile string? ipAddress;
        private volatile bool disconnect;
        private volatile bool ping;
        private volatile bool clientConnected;

        private Task<UdpReceiveResult>? udpReceiveTask;
        private Task<int>? udpSendTask;
        private long lastPacketTimestamp;
        private long lastPingTimestamp;

        public UDPSocketHandler() 
        {
            this.udpWorker = new() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

            Application.Current.Dispatcher.ShutdownStarted += (sender, args) => udpWorker.CancelAsync();
        }

        public void Connect(string ipAddress)
        {
            this.connect = true;
            this.ipAddress = ipAddress;
        }

        public void Disconnect()
        {
            this.disconnect = true;
        }

        public void StartWorker()
        {
            var udpWorker = new BackgroundWorker() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            udpWorker.DoWork += (sender, args) =>
            {
                UdpClient udpClient = new(SERVER_PORT);
                bool oldConnectionStatus = false;
                while (!udpWorker.CancellationPending)
                {
                    try
                    {
                        UDPSend(udpClient);
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDPSend Exception: " + ex.ToString());
                        disconnect = true;
                    }

                    Thread.Sleep(1);

                    try
                    {
                        var gridDataList = UDPReceive(udpClient);
                        if (gridDataList != null && gridDataList.Count > 0)
                        {
                            udpWorker.ReportProgress(0, new UdpWorkerGridDataReport(gridDataList));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDPReceive Exception: " + ex.ToString());
                        disconnect = true;
                    }

                    Thread.Sleep(1);

                    try
                    {
                        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        if (now - lastPacketTimestamp > 10000 || disconnect)
                        {
                            disconnect = false;
                            clientConnected = false;
                        }

                        if (clientConnected && now - lastPingTimestamp > 5000)
                        {
                            ping = true;
                            lastPingTimestamp = now;
                        }

                        if (oldConnectionStatus != clientConnected)
                        {
                            oldConnectionStatus = clientConnected;

                            udpWorker.ReportProgress(0, new UdpWorkeConnectionStatusReport(clientConnected));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDP Timeout Handling Exception: " + ex.ToString());
                    }
                }
            };

            udpWorker.ProgressChanged += (sender, args) =>
            {
                if (args.UserState is UdpWorkerGridDataReport gridDataReport)
                {
                    ReportGridDataEvent(this, new(gridDataReport.GridDataList));
                }
                else if (args.UserState is UdpWorkeConnectionStatusReport connectionStatusReport)
                {
                    ReportConnectionStatusEvent(this, new(connectionStatusReport.ConnectionStatus));
                }
            };

            udpWorker.RunWorkerAsync();
        }

        private void UDPSend(UdpClient udpClient)
        {
            if (udpSendTask == null)
            {
                if (connect)
                {
                    connect = false;

                    var sendBuffer = new byte[3];
                    sendBuffer[0] = STX;
                    sendBuffer[1] = CMD_CONN;
                    sendBuffer[2] = ETX;

                    var ipAddress = IPAddress.Parse(this.ipAddress);

                    udpClient.Connect(ipAddress, PLC_PORT);
                    udpSendTask = udpClient.SendAsync(sendBuffer, sendBuffer.Length);
                }
                else if (ping)
                {
                    ping = false;

                    if (clientConnected)
                    {
                        var sendBuffer = new byte[3];
                        sendBuffer[0] = STX;
                        sendBuffer[1] = CMD_PING;
                        sendBuffer[2] = ETX;

                        udpSendTask = udpClient.SendAsync(sendBuffer, sendBuffer.Length);
                    }
                }
            }
            else if (udpSendTask.IsCompleted)
            {
                udpSendTask = null;
            }

        }

        private List<GridData>? UDPReceive(UdpClient udpClient)
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (!udpClient.Client.Connected)
            {
                udpReceiveTask = null;
                return null;
            }

            List<GridData>? gridDataList = null;

            if (udpReceiveTask != null && udpReceiveTask.IsCompletedSuccessfully)
            {
                var buffer = new ReadOnlySpan<byte>(udpReceiveTask.Result.Buffer);
                if (buffer.Length > 2 && buffer[0] == STX && buffer[^1] == ETX)
                {
                    var checksumRecv = BinaryPrimitives.ReadUInt16LittleEndian(buffer[^3..^1]);
                    var checksumCalc = 0;

                    var valuesBuffer = buffer[1..^3];
                    foreach (var i in valuesBuffer)
                    {
                        checksumCalc ^= i;
                    }

                    if (checksumRecv == checksumCalc)
                    {
                        if (valuesBuffer.Length == 2 && valuesBuffer[0] == 0xB0 && valuesBuffer[1] == 0x0B)
                        {
                            lastPingTimestamp = lastPacketTimestamp = now;
                            clientConnected = true;
                        }
                        else if (clientConnected)
                        {
                            try
                            {
                                gridDataList = ParseDataBuffer(valuesBuffer);
                            }
                            catch (Exception ex)
                            {
                                Debug.Write("ParseBuffer Exception: " + ex.ToString());
                                disconnect = true; //This is to not trigger infinite exception causing lag and high cpu load.
                            }

                            lastPacketTimestamp = now;
                        }
                    }
                }
            }

            if (udpReceiveTask == null || udpReceiveTask.IsCompleted || udpReceiveTask.IsCanceled)
            {
                udpReceiveTask = udpClient.ReceiveAsync();
            }

            return gridDataList;
        }

        private static List<GridData> ParseDataBuffer(ReadOnlySpan<byte> buffer)
        {
            List<GridData> recvDataList = [];

            int offset = 0;
            while (true)
            {
                string paramName = "";
                while (true)
                {
                    char recvChar = (char)buffer[offset++];
                    if (recvChar == DNE)
                    {
                        break;
                    }

                    paramName += recvChar;
                }

                var dataIdentifier = buffer[offset++];
                var dataLen = buffer[offset++];

                var dataBuffer = buffer[offset..(offset + dataLen)];
                offset += dataLen;

                var dataEscape = buffer[offset++];

                var type = UDPType.GetByIdentifier(dataIdentifier);
                if (type == null)
                {
                    break;
                }

                var data = type.Convert(dataBuffer);
                if (data == null)
                {
                    break;
                }
                else if (dataEscape != DVE) //If after data there is no escape, i will consider data to be invalid!
                {
                    break;
                }

                recvDataList.Add(new() { Name = paramName, Value = data });

                if (offset >= buffer.Length)
                {
                    break;
                }
            }

            return recvDataList;
        }
    }
}
