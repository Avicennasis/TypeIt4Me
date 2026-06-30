using System;
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
            const string url = "https://github.com/avicennasis/TypeIt4Me";

            if (Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open help link: {ex.GetType().FullName}");
                }
            }
            else
            {
                Debug.WriteLine($"Invalid help link URL: {url}");
            }
        }
    }
}
