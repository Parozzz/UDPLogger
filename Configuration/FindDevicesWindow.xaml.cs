using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using UDPLogger.UDP;
using UDPLogger.Utility;

namespace UDPLogger.Configuration
{
    /// <summary>
    /// Logica di interazione per FindDevicesWindow.xaml
    /// </summary>
    public partial class FindDevicesWindow : Window
    {
        private record UdpBroadcastResponse(IPEndPoint EndPoint, string Name);

        private class GridData
        {
            public string IPAddress { get; set; } = "";
            public string Name { get; set; } = "";
        }

        private readonly ObservableCollection<GridData> gridDataCollection;
        private readonly BackgroundWorker udpBroadcastWorker;
        private readonly int remotePort;

        private volatile int broadcastPort = 10050;
        private volatile bool refresh = false;

        private UdpClient? udpClient = null;
        private Task<UdpReceiveResult>? udpReceiveTask = null;

        public FindDevicesWindow(ConfigurationFile configurationFile)
        {
            InitializeComponent();

            this.remotePort = configurationFile.RemotePort;

            this.gridDataCollection = [];

            this.ResultGridView.SelectionMode = DataGridSelectionMode.Single;
            this.ResultGridView.ItemsSource = this.gridDataCollection;


            this.BroadcastPortTextBox.Text = "" + broadcastPort;
            this.BroadcastPortTextBox.TextChanged += (sender, args) =>
            {
                if (int.TryParse(this.BroadcastPortTextBox.Text, out int result))
                {
                    this.broadcastPort = result;
                }
            };

            this.RefreshButton.Click += (sender, args) =>
            {
                this.gridDataCollection.Clear();
                this.ResultGridView.Items.Refresh();

                refresh = true;
            };

            this.AcceptSelectedButton.Click += (sender, args) =>
            {
                var selectedItem = this.ResultGridView.SelectedItem;
                if (selectedItem is GridData gridData)
                {
                    configurationFile.IPAddress = gridData.IPAddress;
                    this.Close();
                }
            };

            this.udpBroadcastWorker = new() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            this.udpBroadcastWorker.DoWork += (sender, args) =>
            {
                long waitResponseTimestamp = 0;
                while (true)
                {
                    try
                    {
                        if (refresh)
                        {
                            refresh = false;

                            int broadcastPort = this.broadcastPort;

                            if (udpClient == null)
                            {
                                udpClient = new UdpClient();
                                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, broadcastPort));
                            }

                            var dataBuffer = new byte[4];
                            BinaryPrimitives.WriteUInt32LittleEndian(dataBuffer, 0x420FADED);

                            var buffer = new byte[6];
                            buffer[0] = UDPConstants.STX;
                            dataBuffer.CopyTo(buffer, 1);
                            buffer[5] = UDPConstants.ETX;

                            udpClient.Send(buffer, buffer.Length, "255.255.255.255", remotePort);

                            waitResponseTimestamp = Utils.Now();
                        }

                        if (udpClient != null)
                        {
                            udpReceiveTask ??= udpClient.ReceiveAsync();
                            if (udpReceiveTask.IsCompletedSuccessfully)
                            {
                                var receiveResult = udpReceiveTask.Result;

                                var buffer = receiveResult.Buffer;
                                if (buffer.Length >= 8 && buffer[0] == UDPConstants.STX && buffer[^1] == UDPConstants.ETX)
                                {
                                    var header = BinaryPrimitives.ReadUInt32LittleEndian(buffer[1..5]);
                                    if (header == 0x420FADED)
                                    {
                                        var nameLen = buffer[5];
                                        var nameBuffer = buffer[6..(6 + nameLen)];

                                        var escape = buffer[6 + nameLen];
                                        if(escape == UDPConstants.DNE)
                                        {
                                            var name = UDPTypeConverter.ConvertString(nameBuffer);
                                            if(name != null)
                                            {
                                                this.udpBroadcastWorker.ReportProgress(0, new UdpBroadcastResponse(receiveResult.RemoteEndPoint, name));
                                            }
                                        }
                                    }
                                }

                                udpReceiveTask = udpClient.ReceiveAsync();
                            }
                            else if (udpReceiveTask.IsCompleted)
                            {
                                udpReceiveTask = udpClient.ReceiveAsync();
                            }


                            if (Utils.Now() - waitResponseTimestamp > 15000)
                            {
                                CloseSocket();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerTXT.AddException("UDP Broadcast Exception", ex);
                        CloseSocket();
                    }

                    if (udpBroadcastWorker.CancellationPending)
                    {
                        CloseSocket();
                        break;
                    }

                    Thread.Sleep(2);
                }
            };
            this.udpBroadcastWorker.ProgressChanged += (sender, args) =>
            {
                if (args.UserState is UdpBroadcastResponse response)
                {
                    this.gridDataCollection.Add(new() { IPAddress = response.EndPoint.Address.ToString(), Name = response.Name });
                }
            };
            this.udpBroadcastWorker.RunWorkerAsync();

            this.Closing += (sender, args) =>
            {
                udpBroadcastWorker.CancelAsync();
            };
        }

        private void CloseSocket()
        {
            try
            {
                if (this.udpClient != null)
                {
                    udpClient.Close();
                    udpClient = null;
                }
            }
            catch { }
        }
    }
}
