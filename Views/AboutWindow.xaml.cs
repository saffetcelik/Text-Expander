using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace OtomatikMetinGenisletici.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CloseButton_Click(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void Email_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mailto:iletisim@saffetcelik.com.tr",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Email client bulunamadı, sessizce geç
            }
        }

        private void GitHub_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/saffetcelik",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Tarayıcı bulunamadı, sessizce geç
            }
        }

        private void Instagram_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://instagram.com/kamucoder",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Tarayıcı bulunamadı, sessizce geç
            }
        }
    }
}
