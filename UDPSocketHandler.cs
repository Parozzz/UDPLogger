using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using static UDPLogger.MainWindow;

namespace UDPLogger
{
    public class UDPSocketHandler
    {
        private record UdpWorkerGridDataReport(List<UDPReceivedData> ReceivedDataList);
        private record UdpWorkeConnectionStatusReport(bool ConnectionStatus);


        public delegate void ReceivedDataEventHandler(object? sender, ReceivedDataEventArgs args);
        public record ReceivedDataEventArgs(List<UDPReceivedData> ReceivedDataList);

        public delegate void ConnectionStatusEventHandler(object? sender, ConnectionStatusEventArgs args);
        public record ConnectionStatusEventArgs(bool ConnectionStatus);

        public record UDPReceivedData(string Name, object Data);

        public event ReceivedDataEventHandler ReceivedDataEvent = delegate { };
        public event ConnectionStatusEventHandler ConnectionStatusEvent = delegate { };

        public const byte STX = 0x02; //START
        public const byte ETX = 0x03; //END
        public const byte DNE = 0x04; //DATA NAME ESCAPE
        public const byte DVE = 0x05; //DATA VALUE ESCAPE

        public const byte CMD_STOP = 1;
        public const byte CMD_CONN = 2;
        public const byte CMD_PING = 3;

        public const int PING_TIMEOUT = 1000; //ms
        public const int PACKET_TIMEOUT = 2500; //ms

        private readonly BackgroundWorker udpWorker;

        private volatile bool connect;

        private volatile int remotePort = -1;
        private volatile int localPort = -1;
        private volatile string? ipString;

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

        public void Connect(string ipAddress, int remotePort, int localPort)
        {
            this.ipString = ipAddress;
            this.remotePort = remotePort;
            this.localPort = localPort;

            this.connect = true; //AFTER! FOR ASYNC MATTERS!
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
                UdpClient? udpClient = null;
                bool oldConnectionStatus = false;
                while (!udpWorker.CancellationPending)
                {
                    try
                    {
                        UDPSend(ref udpClient);
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDPSend Exception: " + ex.ToString());
                        disconnect = true;
                    }

                    Thread.Sleep(1);

                    try
                    {
                        var receivedDataList = UDPReceive(ref udpClient);
                        if (receivedDataList != null && receivedDataList.Count > 0)
                        {
                            udpWorker.ReportProgress(0, new UdpWorkerGridDataReport(receivedDataList));
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
                        if(udpClient != null)
                        {
                            var now = Now();

                            if (now - lastPacketTimestamp > PACKET_TIMEOUT || disconnect)
                            {
                                disconnect = false;
                                clientConnected = false;

                                udpClient.Close();
                                udpClient = null;
                            }

                            var lastPingDiff = (now - lastPingTimestamp);
                            if (clientConnected && lastPingDiff >= PING_TIMEOUT)
                            {
                                ping = true;
                            }
                        }
                        else
                        {
                            disconnect = false;
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
                    ReceivedDataEvent(this, new(gridDataReport.ReceivedDataList));
                }
                else if (args.UserState is UdpWorkeConnectionStatusReport connectionStatusReport)
                {
                    ConnectionStatusEvent(this, new(connectionStatusReport.ConnectionStatus));
                }
            };

            udpWorker.RunWorkerAsync();
        }

        private void UDPSend(ref UdpClient? udpClient)
        {
            if (udpSendTask == null)
            {
                if (connect && !string.IsNullOrEmpty(this.ipString))
                {
                    connect = false;

                    var sendBuffer = new byte[3];
                    sendBuffer[0] = STX;
                    sendBuffer[1] = CMD_CONN;
                    sendBuffer[2] = ETX;

                    if(IPAddress.TryParse(this.ipString, out IPAddress? IP) && IP != null && localPort > 0 && remotePort > 0)
                    {
                        udpClient?.Close();

                        udpClient = new(localPort);
                        udpClient.Connect(IP, remotePort);
                        udpSendTask = udpClient.SendAsync(sendBuffer, sendBuffer.Length);

                        lastPacketTimestamp = lastPingTimestamp = Now();
                    }
                }
                else if (ping)
                {
                    ping = false;

                    if (udpClient != null && clientConnected)
                    {
                        var sendBuffer = new byte[3];
                        sendBuffer[0] = STX;
                        sendBuffer[1] = CMD_PING;
                        sendBuffer[2] = ETX;

                        udpSendTask = udpClient.SendAsync(sendBuffer, sendBuffer.Length);

                        lastPingTimestamp = Now();
                    }
                }
            }
            else if (udpSendTask.IsCompleted)
            {
                udpSendTask = null;
            }

        }

        private List<UDPReceivedData>? UDPReceive(ref UdpClient? udpClient)
        {
            if (udpClient == null)
            {
                udpReceiveTask = null;
                return null;
            }

            List<UDPReceivedData>? receivedDataList = null;

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
                            lastPingTimestamp = lastPacketTimestamp = Now();
                            clientConnected = true;
                        }
                        else if (clientConnected)
                        {
                            try
                            {
                                receivedDataList = ParseDataBuffer(valuesBuffer);
                            }
                            catch (Exception ex)
                            {
                                Debug.Write("ParseBuffer Exception: " + ex.ToString());
                                disconnect = true; //This is to not trigger infinite exception causing lag and high cpu load.
                            }

                            lastPacketTimestamp = Now();
                        }
                    }
                }
            }

            if (udpReceiveTask == null || udpReceiveTask.IsCompleted || udpReceiveTask.IsCanceled)
            {
                udpReceiveTask = udpClient.ReceiveAsync();
            }

            return receivedDataList;
        }

        private static List<UDPReceivedData> ParseDataBuffer(ReadOnlySpan<byte> buffer)
        {
            List<UDPReceivedData> recvDataList = [];

            int offset = 0;
            while (true)
            {
                var nameLen = buffer[offset++];

                var nameBuffer = buffer[offset .. (offset + nameLen)];
                offset += nameLen;

                var nameEscape = buffer[offset++];

                string? paramName = UDPTypeConverter.ConvertString(nameBuffer);
                if (paramName == null || nameEscape != DNE) //If after name there is no escape, it will be considered invalid!
                {
                    break;
                }

                var dataIdentifier = buffer[offset++];
                var dataLen = buffer[offset++];

                var dataBuffer = buffer[offset..(offset + dataLen)];
                offset += dataLen;

                var dataEscape = buffer[offset++];

                var data = UDPTypeConverter.Convert(dataIdentifier, dataBuffer);
                if (data == null || dataEscape != DVE) //If after data there is no escape, it will be considered invalid!
                {
                    break;
                }

                recvDataList.Add(new(paramName, data));

                if (offset >= buffer.Length)
                {
                    break;
                }
            }

            return recvDataList;
        }

        private static long Now()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
    }
}
