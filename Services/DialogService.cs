using System.Windows;

namespace TypeIt4Me.Services
{
    public class DialogService : IDialogService
    {
        public string? OpenFileDialog(string filter, string defaultExt)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? SaveFileDialog(string filter, string defaultExt, string fileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = fileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public bool ShowConfirmation(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void ShowInformation(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
