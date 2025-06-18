using System.Windows;

namespace OtomatikMetinGenisletici.Views
{
    public partial class EditWordDialog : Window
    {
        public string NewWord { get; private set; } = "";
        public int NewCount { get; private set; } = 0;

        public EditWordDialog(string originalWord, int originalCount)
        {
            InitializeComponent();
            
            OriginalWordTextBox.Text = originalWord;
            NewWordTextBox.Text = originalWord;
            CountTextBox.Text = originalCount.ToString();
            
            NewWordTextBox.Focus();
            NewWordTextBox.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewWordTextBox.Text))
            {
                MessageBox.Show("Kelime boş olamaz!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(CountTextBox.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Kullanım sayısı geçerli bir pozitif sayı olmalıdır!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewWord = NewWordTextBox.Text.Trim();
            NewCount = count;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
