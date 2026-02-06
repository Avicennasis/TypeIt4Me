using System.Windows;

namespace TypeIt4Me.Views
{
    public partial class InputWindow : Window
    {
        public string Result { get; private set; } = string.Empty;

        public InputWindow(string message, string defaultValue = "")
        {
            InitializeComponent();
            MessageText.Text = message;
            InputBox.Text = defaultValue;
            InputBox.SelectAll();
            InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
