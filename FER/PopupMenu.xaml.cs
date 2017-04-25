using System;
using System.Windows;
using System.Windows.Forms;

namespace FER
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class PopupMenu : Window
    {
        private String directoryPath;

        public PopupMenu()
        {
            InitializeComponent();
        }

        private void TimerSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double timer = timerSlider.Value;
            counterLabel.Content = timer.ToString();
        }

        private void PopupOkClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void PopupBrowseClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (directoryPath != null)
            {
                dialog.SelectedPath = directoryPath;
            }
            DialogResult result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                directoryPath = dialog.SelectedPath;
            }
        }

        public String GetDirectoryPath()
        {
            return directoryPath;
        }
    }
}
