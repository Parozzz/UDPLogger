using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using UDPLogger.Configuration;
using UDPLogger.SQLite;
using UDPLogger.UDP;
using UDPLogger.Utility;

namespace UDPLogger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string VERSION = "1.0.0";

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

        private bool lastValuesLoaded = false;

        public MainWindow()
        {
            this.Configuration = ConfigurationFile.Load();
            this.Configuration.Save();

            InitializeComponent(); //Since components are binded to configuration directly, this assure they are loaded with current values.

            this.databaseHandler = new();
            this.databaseHandler.QueryResult += (sender, args) =>
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
                lastValuesLoaded = true;
            };
            this.databaseHandler.StartWorker(Configuration.GetFullDatabasePath());

            this.udpSocketHandler = new();
            this.udpSocketHandler.ReceivedDataEvent += (sender, args) =>
            {
                var now = DateTime.Now;

                var receivedDataList = args.ReceivedDataList;

                var anyChanged = false;
                foreach (var receivedData in receivedDataList)
                {
                    bool isChanged = false;

                    var gridData = gridDataItemSource.Where(data => receivedData.Name == data.Name).FirstOrDefault();
                    if (gridData == null)
                    {
                        gridDataItemSource.Add(new(receivedData.Name, receivedData.Data, now));

                        isChanged = true;
                    }
                    else if (gridData.Value == null || !gridData.Value.Equals(receivedData.Data))
                    {
                        gridData.Value = receivedData.Data;
                        gridData.LastUpdate = now;

                        isChanged = true;
                    }

                    if(isChanged || receivedData.Flags.ForceInsert)
                    {
                        this.databaseHandler.AddRecord(new(receivedData.Name, receivedData.DataIdentifier, "" + receivedData.Data, now));
                    }
                    anyChanged |= isChanged;
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
            this.udpSocketHandler.StartWorker();

            this.StartButton.Click += (sender, args) =>
            {
                if(lastValuesLoaded)
                {
                    udpSocketHandler.Connect(this.Configuration.IPAddress, this.Configuration.RemotePort, this.Configuration.LocalPort);
                }
            };
            this.StopButton.Click += (sender, args) => udpSocketHandler.Disconnect();
            this.PurgeDatabaseButton.Click += (sender, args) =>
            {
                new PurgeDatabaseWindow(databaseHandler) { Owner = this }.ShowDialog();
            };

            this.DataGrid.ItemsSource = gridDataItemSource;

            this.databaseHandler.QueryLastValues();

            DispatcherTimer dispatcherTimer = new() { Interval = TimeSpan.FromSeconds(1) };
            dispatcherTimer.Tick += (sender, args) =>
            {
                try
                {
                    var fileInfo = new FileInfo(Configuration.GetFullDatabasePath());
                    this.DatabaseSizeTextBox.Text = fileInfo.FormatBytes();
                }
                catch (Exception ex)
                {
                    LoggerTXT.AddException("Database Size Handlig Exception", ex);
                }
            };
            dispatcherTimer.Start();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new ConfigurationWindow(this.Configuration) { Owner = this }.ShowDialog();
        }
    }
}