namespace TypeIt4Me.Services
{
    public interface IDialogService
    {
        string? OpenFileDialog(string filter, string defaultExt);
        string? SaveFileDialog(string filter, string defaultExt, string fileName);
        bool ShowConfirmation(string message, string title);
        void ShowInformation(string message, string title);
    }
}
