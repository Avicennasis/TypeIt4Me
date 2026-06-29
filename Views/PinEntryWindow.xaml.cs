using System.Windows;

namespace TypeIt4Me.Views
{
    public partial class PinEntryWindow : Window
    {
        public System.Security.SecureString? SecurePin { get; private set; }

        public PinEntryWindow(string title = "Enter PIN")
        {
            InitializeComponent();
            this.Title = title;
            PinBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SecurePin = PinBox.SecurePassword.Copy();
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
