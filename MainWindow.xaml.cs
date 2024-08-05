using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using UDPLogger.Configuration;
using static UDPLogger.UDPSocketHandler;

namespace UDPLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public class GridData(string name, object value, DateTime lastUpdate)
        {
            public string Name { get; set; } = name;
            public object Value { get; set; } = value;
            public DateTime LastUpdate { get; set; } = lastUpdate;
        }

        private readonly ObservableCollection<GridData> gridDataItemSource = [];

        public ConfigurationFile Configuration { get; init; }
        private readonly SQLiteHandler databaseHandler;
        private readonly UDPSocketHandler udpSocketHandler;

        private bool recordLoaded = false;

        public MainWindow()
        {
            this.Configuration = ConfigurationFile.Load();
            this.Configuration.Save();

            InitializeComponent(); //Since components are binded to configuration directly, this assure they are loaded with current values.

            this.databaseHandler = new(Configuration);
            this.databaseHandler.FetchResult += (sender, args) =>
            {
                foreach (var record in args.RecordList)
                {
                    var convertedValue = UDPTypeConverter.ConvertFromString((byte)record.Identifier, record.StringValue);
                    if(convertedValue != null)
                    {
                        gridDataItemSource.Add(new(record.Name, convertedValue, record.Time));
                    }
                }
                this.DataGrid.Items.Refresh();
                recordLoaded = true;
            };
            this.databaseHandler.StartWorker();

            this.udpSocketHandler = new();
            this.udpSocketHandler.ReceivedDataEvent += (sender, args) =>
            {
                var now = DateTime.Now;

                var receivedDataList = args.ReceivedDataList;

                var anyChanged = false;
                foreach (var receivedData in receivedDataList)
                {
                    var gridData = gridDataItemSource.Where(data => receivedData.Name == data.Name).FirstOrDefault();
                    if (gridData == null)
                    {
                        gridDataItemSource.Add(new(receivedData.Name, receivedData.Data, now));
                        anyChanged = true;

                        this.databaseHandler.AddRecord(new(receivedData.Name, receivedData.DataIdentifier, "" + receivedData.Data, now));
                    }
                    else if (gridData.Value == null || !gridData.Value.Equals(receivedData.Data))
                    {
                        gridData.Value = receivedData.Data;
                        gridData.LastUpdate = now;
                        anyChanged = true;

                        this.databaseHandler.AddRecord(new(receivedData.Name, receivedData.DataIdentifier, "" + receivedData.Data, now));
                    }
                }

                if (anyChanged)
                {
                    this.DataGrid.Items.Refresh();
                }
            };

            this.udpSocketHandler.ConnectionStatusEvent += (sender, args) =>
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

            this.StartButton.Click += (sender, args) =>
            {
                if(recordLoaded)
                {
                    udpSocketHandler.Connect(this.Configuration.IPAddress, this.Configuration.RemotePort, this.Configuration.LocalPort);
                }
            };
            this.StopButton.Click += (sender, args) => udpSocketHandler.Disconnect();

            this.DataGrid.ItemsSource = gridDataItemSource;

            this.udpSocketHandler.StartWorker();

            this.databaseHandler.FetchLastValues();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new ConfigurationWindow(this.Configuration)
            {
                Owner = this,
            }.ShowDialog();
        }
    }
}