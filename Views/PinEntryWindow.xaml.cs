using System.Windows;

namespace TypeIt4Me.Views
{
    public partial class PinEntryWindow : Window
    {
        public string Pin { get; private set; } = string.Empty;

        public PinEntryWindow(string title = "Enter PIN")
        {
            InitializeComponent();
            this.Title = title;
            PinBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Pin = PinBox.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
