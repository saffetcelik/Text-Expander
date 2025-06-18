using System.Windows;

namespace OtomatikMetinGenisletici.Views
{
    public partial class EditBigramDialog : Window
    {
        public string NewBigram { get; private set; } = "";
        public int NewCount { get; private set; } = 0;

        public EditBigramDialog(string originalBigram, int originalCount)
        {
            InitializeComponent();
            
            OriginalBigramTextBox.Text = originalBigram;
            NewBigramTextBox.Text = originalBigram;
            CountTextBox.Text = originalCount.ToString();
            
            NewBigramTextBox.Focus();
            NewBigramTextBox.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewBigramTextBox.Text))
            {
                MessageBox.Show("Bigram boş olamaz!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var words = NewBigramTextBox.Text.Trim().Split(' ');
            if (words.Length != 2)
            {
                MessageBox.Show("Bigram tam olarak iki kelimeden oluşmalıdır!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(CountTextBox.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Kullanım sayısı geçerli bir pozitif sayı olmalıdır!", "Hata", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewBigram = NewBigramTextBox.Text.Trim();
            NewCount = count;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
