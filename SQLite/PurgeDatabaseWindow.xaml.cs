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

namespace UDPLogger.SQLite
{
    /// <summary>
    /// Logica di interazione per PurgeDatabaseWindow.xaml
    /// </summary>
    public partial class PurgeDatabaseWindow : Window
    {
        private SQLiteHandler databaseHandler;
        public PurgeDatabaseWindow(SQLiteHandler databaseHandler)
        {
            InitializeComponent();

            this.databaseHandler = databaseHandler;

            this.DeleteRowsButton.Click += (sender, args) =>
            {
                if (this.PurgeDateTimePicker.Value is DateTime currentDateTime)
                {
                    databaseHandler.PurgeBeforeOf(currentDateTime);
                }
            };

            this.VacuumDatabaseButton.Click += (sender, args) =>
            {
                databaseHandler.Vacuum();
            };
        }
    }
}
