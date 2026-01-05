using System.Windows;
using System.Diagnostics;

namespace TypeIt4Me.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void Link_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://github.com/avicennasis/TypeIt4Me",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
