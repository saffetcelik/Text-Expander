using System.Windows;

namespace OtomatikMetinGenisletici.Views
{
    public partial class EditTrigramDialog : Window
    {
        public string NewTrigram { get; private set; } = "";
        public int NewCount { get; private set; } = 0;

        public EditTrigramDialog(string originalTrigram, int originalCount)
        {
            InitializeComponent();
            
            OriginalTrigramTextBox.Text = originalTrigram;
            NewTrigramTextBox.Text = originalTrigram;
            CountTextBox.Text = originalCount.ToString();
            
            NewTrigramTextBox.Focus();
            NewTrigramTextBox.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewTrigramTextBox.Text))
            {
                MessageBox.Show("Trigram boş olamaz!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var words = NewTrigramTextBox.Text.Trim().Split(' ');
            if (words.Length != 3)
            {
                MessageBox.Show("Trigram tam olarak üç kelimeden oluşmalıdır!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(CountTextBox.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Kullanım sayısı geçerli bir pozitif sayı olmalıdır!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewTrigram = NewTrigramTextBox.Text.Trim();
            NewCount = count;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
