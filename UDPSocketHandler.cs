using System.Buffers.Binary;
using System.Collections.Generic;
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

        public record UDPReceivedData(string Name, int DataIdentifier, object Data);

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

        //Only used in udp thread
        private Task<UdpReceiveResult>? udpReceiveTask;
        private Task<int>? udpSendTask;

        private long lastPacketTimestamp;
        private long lastPingTimestamp;

        private UdpClient? udpClient = null;
        private List<UDPReceivedData>? receivedDataList;

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
                bool oldConnectionStatus = false;
                while (!udpWorker.CancellationPending)
                {
                    try
                    {
                        UDPSend();
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDPSend Exception: " + ex.ToString());

                        clientConnected = false;

                        udpClient?.Close();
                        udpClient = null;
                    }

                    Thread.Sleep(1);

                    try
                    {
                        UDPReceive();
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDPReceive Exception: " + ex.ToString());

                        clientConnected = false;

                        udpClient?.Close();
                        udpClient = null;
                    }

                    Thread.Sleep(1);

                    try
                    {
                        if (udpClient != null)
                        {
                            var now = Utils.Now();
                            if (now - lastPacketTimestamp > PACKET_TIMEOUT)
                            {
                                clientConnected = false;
                            }

                            if (clientConnected && (now - lastPingTimestamp) >= PING_TIMEOUT)
                            {
                                ping = true;
                            }
                        }

                        if (this.receivedDataList != null)
                        {
                            udpWorker.ReportProgress(0, new UdpWorkerGridDataReport(receivedDataList));
                            this.receivedDataList = null;
                        }

                        if (oldConnectionStatus != clientConnected)
                        {
                            oldConnectionStatus = clientConnected;

                            udpWorker.ReportProgress(0, new UdpWorkeConnectionStatusReport(clientConnected));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Write("UDP Handling Exception: " + ex.ToString());

                        clientConnected = false;

                        udpClient?.Close();
                        udpClient = null;
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

        private void UDPSend()
        {
            if (udpSendTask != null && (udpSendTask.IsCompleted || udpClient == null))
            {
                udpSendTask = null;
            }
            else if (udpSendTask == null)
            {
                if (connect)
                {
                    connect = false;

                    if (IPAddress.TryParse(this.ipString, out IPAddress? IP) && IP != null && localPort > 0 && remotePort > 0)
                    {
                        udpClient?.Close();

                        udpClient = new(localPort);
                        udpClient.Connect(IP, remotePort);

                        var sendBuffer = new byte[3];
                        sendBuffer[0] = STX;
                        sendBuffer[1] = CMD_CONN;
                        sendBuffer[2] = ETX;
                        udpSendTask = udpClient.SendAsync(sendBuffer, sendBuffer.Length);

                        lastPacketTimestamp = lastPingTimestamp = Utils.Now();
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

                        lastPingTimestamp = Utils.Now();
                    }
                }
                else if (disconnect)
                {
                    disconnect = false;
                    if (udpClient != null && clientConnected)
                    {
                        var sendBuffer = new byte[3];
                        sendBuffer[0] = STX;
                        sendBuffer[1] = CMD_STOP;
                        sendBuffer[2] = ETX;
                        udpClient.Send(sendBuffer, sendBuffer.Length); //This is done SYNC since later i will close the Socket.

                        clientConnected = false;

                        udpClient.Close();
                        udpClient = null;
                    }
                }
            }
        }

        private bool bufferParsed = false;
        private void UDPReceive()
        {
            if (udpClient == null)
            {
                udpReceiveTask = null;
                return;
            }

            udpReceiveTask ??= udpClient.ReceiveAsync();
            if (udpReceiveTask.IsCompletedSuccessfully)
            {
                var buffer = new ReadOnlySpan<byte>(udpReceiveTask.Result.Buffer);
                ParseReceivedBuffer(buffer);

                udpReceiveTask = udpClient.ReceiveAsync();
            }
            else if(udpReceiveTask.IsCompleted) //This also takes care of task faulted!
            {
                udpReceiveTask = udpClient.ReceiveAsync();

                if(udpReceiveTask.Exception != null)
                {
                    Debug.WriteLine(udpReceiveTask.Exception.Message + '\n' + udpReceiveTask.Exception.StackTrace);
                }
            }
        }

        private void ParseReceivedBuffer(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length <= 2 || buffer[0] != STX || buffer[^1] != ETX)
            {
                return;
            }

            var totalPacketLen = BinaryPrimitives.ReadUInt16LittleEndian(buffer[1..3]);
            if(totalPacketLen != buffer.Length)
            {
                return;
            }

            var checksumRecv = BinaryPrimitives.ReadUInt16LittleEndian(buffer[^3..^1]);
            var checksumCalc = 0;

            var valuesBuffer = buffer[3..^3];
            foreach (var i in valuesBuffer)
            {
                checksumCalc ^= i;
            }
            
            if(checksumRecv != checksumCalc)
            {
                return;
            }

            if (valuesBuffer.Length == 2 && valuesBuffer[0] == 0xB0 && valuesBuffer[1] == 0x0B)
            {
                lastPingTimestamp = lastPacketTimestamp = Utils.Now();
                clientConnected = true;
            }
            else if (clientConnected)
            {
                try
                {
                    ParseData(valuesBuffer);
                }
                catch (Exception ex)
                {
                    Debug.Write("ParseBuffer Exception: " + ex.ToString());
                    disconnect = true; //This is to not trigger infinite exception causing lag and high cpu load.
                }

                lastPacketTimestamp = Utils.Now();
            }
        }

        private void ParseData(ReadOnlySpan<byte> buffer)
        {
            List<UDPReceivedData> dataList = [];

            int offset = 0;
            while (true)
            {
                var nameLen = buffer[offset++];

                var nameBuffer = buffer[offset..(offset + nameLen)];
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

                dataList.Add(new(paramName, dataIdentifier, data));

                if (offset >= buffer.Length)
                {//If the offset goes above the buffer length it means everything has been parsed correctly and move the parsed data to be sent.
                    this.receivedDataList = dataList;
                    break;
                }
            }
        }
    }
}
