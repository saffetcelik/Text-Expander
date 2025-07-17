using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace OtomatikMetinGenisletici.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            SetVersionInfo();
        }

        /// <summary>
        /// Versiyon bilgisini dinamik olarak ayarlar
        /// </summary>
        private void SetVersionInfo()
        {
            try
            {
                // Assembly'den versiyon bilgisini al
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;

                if (version != null)
                {
                    // Versiyon formatı: Major.Minor.Build (Revision'ı gösterme)
                    string versionString = $"{version.Major}.{version.Minor}.{version.Build}";
                    VersionTextBlock.Text = $"Sürüm {versionString} (.NET 8)";
                }
                else
                {
                    VersionTextBlock.Text = "Sürüm 1.1.4 (.NET 8)";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SetVersionInfo hatası: {ex.Message}");
                VersionTextBlock.Text = "Sürüm 1.1.4 (.NET 8)";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
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
