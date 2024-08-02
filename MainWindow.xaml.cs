using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;

namespace UDPLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class GridData
        {
            public string? Name { get; set; }
            public object? Value { get; set; }
        }

        public const byte STX = 0x02; //START
        public const byte ETX = 0x03; //END
        public const byte DNE = 0x04; //DATA NAME ESCAPE
        public const byte DVE = 0x05; //DATA VALUE ESCAPE

        public const byte CMD_STOP = 1;
        public const byte CMD_CONN = 2;
        public const byte CMD_PING = 3;

        private readonly ObservableCollection<GridData> gridDataItemSource = [];

        public string IPAddressProperty { get => ipString; set => ipString = value; }
        private volatile string ipString = "172.16.9.179";

        private readonly UDPSocketHandler udpSocketHandler;


        public MainWindow()
        {
            InitializeComponent();

            udpSocketHandler = new();
            udpSocketHandler.ReportGridDataEvent += (sender, args) =>
            {
                var receivedDataList = args.GridDataList;

                var anyChanged = false;
                foreach (var receivedData in receivedDataList)
                {
                    var gridData = gridDataItemSource.Where(data => receivedData.Name == data.Name).FirstOrDefault();
                    if (gridData == null)
                    {
                        gridDataItemSource.Add(receivedData);
                        anyChanged = true;
                    }
                    else if (gridData.Value == null || !gridData.Value.Equals(receivedData.Value))
                    {
                        gridData.Value = receivedData.Value;
                        anyChanged = true;
                    }
                }

                if (anyChanged)
                {
                    this.DataGrid.Items.Refresh();
                }
            };

            udpSocketHandler.ReportConnectionStatusEvent += (sender, args) =>
            {
                if (args.ConnectionStatus)
                {
                    StartButton.Foreground = new SolidColorBrush(Colors.Lime);
                    StopButton.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    StartButton.Foreground = new SolidColorBrush(Colors.Black);
                    StopButton.Foreground = new SolidColorBrush(Colors.Red);
                }
            };

            StartButton.Click += (sender, args) => udpSocketHandler.Connect(this.ipString);
            StopButton.Click += (sender, args) => udpSocketHandler.Disconnect();

            this.DataGrid.ItemsSource = gridDataItemSource;

            udpSocketHandler.StartWorker();
        }
    }
}