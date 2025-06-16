using System.Windows;
using OtomatikMetinGenisletici.Models;

namespace OtomatikMetinGenisletici.Views
{
    public partial class ShortcutDialog : Window
    {
        public string ShortcutKey { get; private set; } = string.Empty;
        public string ExpansionText { get; private set; } = string.Empty;

        public ShortcutDialog()
        {
            InitializeComponent();
            ShortcutTextBox.Focus();
        }

        public ShortcutDialog(Shortcut shortcut) : this()
        {
            ShortcutTextBox.Text = shortcut.Key;
            ExpansionTextBox.Text = shortcut.Expansion;
            Title = "Kısayol Düzenle";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ShortcutTextBox.Text))
            {
                MessageBox.Show("Kısayol boş olamaz!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ShortcutTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(ExpansionTextBox.Text))
            {
                MessageBox.Show("Genişletilmiş metin boş olamaz!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ExpansionTextBox.Focus();
                return;
            }

            ShortcutKey = ShortcutTextBox.Text.Trim();
            ExpansionText = ExpansionTextBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
