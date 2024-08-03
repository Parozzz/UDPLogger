using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using UDPLogger.Configuration;

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
        private readonly UDPSocketHandler udpSocketHandler;

        public MainWindow()
        {
            this.Configuration = ConfigurationFile.Load();
            this.Configuration.Save();

            InitializeComponent(); //Since components are binded to configuration directly, this assure they are loaded with current values.

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
                    }
                    else if (gridData.Value == null || !gridData.Value.Equals(receivedData.Data))
                    {
                        gridData.Value = receivedData.Data;
                        gridData.LastUpdate = now;
                        anyChanged = true;
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

            this.StartButton.Click += (sender, args) => udpSocketHandler.Connect(this.Configuration.IPAddress, this.Configuration.RemotePort, this.Configuration.LocalPort);
            this.StopButton.Click += (sender, args) => udpSocketHandler.Disconnect();

            this.DataGrid.ItemsSource = gridDataItemSource;

            this.udpSocketHandler.StartWorker();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new ConfigurationWindow(this.Configuration).ShowDialog();
        }
    }
}