using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UDPLogger.Configuration
{
    /// <summary>
    /// Logica di interazione per ConfigurationWindow.xaml
    /// </summary>
    public partial class ConfigurationWindow : Window
    {
        private readonly ConfigurationFile configurationFile;
        public ConfigurationWindow(ConfigurationFile configurationFile)
        {
            InitializeComponent();

            this.configurationFile = configurationFile;
            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            this.IPAddressTextBox.Text = this.configurationFile.IPAddress;
            this.RemotePortTextBox.Text = "" + this.configurationFile.RemotePort;
            this.LocalPortTextBox.Text = "" + this.configurationFile.LocalPort;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            configurationFile.IPAddress = this.IPAddressTextBox.Text;
            if(int.TryParse(this.RemotePortTextBox.Text, out int remotePort))
            {
                this.configurationFile.RemotePort = remotePort;
            }
            if (int.TryParse(this.LocalPortTextBox.Text, out int localPort))
            {
                this.configurationFile.LocalPort = localPort;
            }

            this.configurationFile.Save();

            this.Close();
        }
    }
}
