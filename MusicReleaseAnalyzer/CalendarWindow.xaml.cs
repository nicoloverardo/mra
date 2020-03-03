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

namespace MusicReleaseAnalyzer
{
    /// <summary>
    /// Interaction logic for CalendarWindow.xaml
    /// </summary>
    public partial class CalendarWindow : Window
    {
        public CalendarWindow()
        {
            InitializeComponent();

            var onlineDate = OnlineSettings();
            if (onlineDate != new DateTime())
            {
                StartDatePicker.SelectedDate = onlineDate >= Properties.Settings.Default.LastCheck ? onlineDate : Properties.Settings.Default.LastCheck;
            }
            else
            {
                StartDatePicker.SelectedDate = Properties.Settings.Default.LastCheck;
            }

            EndDatePicker.SelectedDate = DateTime.Today;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CalendarWnd_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == null) DialogResult = false;
        }

        private void CalendarWnd_Loaded(object sender, RoutedEventArgs e)
        {
            DialogResult = null;
        }

        public static DateTime OnlineSettings()
        {
            var date = new DateTime();
            var oneDriveFolderPath = Environment.GetEnvironmentVariable("OneDriveConsumer", EnvironmentVariableTarget.User);

            if (!System.IO.Directory.Exists(oneDriveFolderPath)) return date;
            if (!System.IO.Directory.Exists(oneDriveFolderPath + "\\Documents\\MusicReleaseAnalyzer")) return date;

            var settingsPath = oneDriveFolderPath + "\\Documents\\MusicReleaseAnalyzer";

            if (!System.IO.File.Exists(settingsPath + "\\settings.dat")) return date;

            try
            {
                date = DateTime.Parse(System.IO.File.ReadAllText(settingsPath + "\\settings.dat"));
            }
            catch(Exception ex)
            {
                MessageBox.Show("Error while reading online settings:\r\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return date;
            }

            return date;
        }
    }
}
