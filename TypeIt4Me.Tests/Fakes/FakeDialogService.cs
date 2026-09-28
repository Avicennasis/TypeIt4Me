using System.Collections.Generic;
using TypeIt4Me.Services;

namespace TypeIt4Me.Tests.Fakes
{
    public class FakeDialogService : IDialogService
    {
        public string? OpenFileDialogResult { get; set; }
        public string? SaveFileDialogResult { get; set; }
        public bool ShowConfirmationResult { get; set; } = true;

        public List<string> InformationMessages { get; } = new List<string>();
        public List<string> ConfirmationMessages { get; } = new List<string>();

        public string? OpenFileDialog(string filter, string defaultExt)
        {
            return OpenFileDialogResult;
        }

        public string? SaveFileDialog(string filter, string defaultExt, string fileName)
        {
            return SaveFileDialogResult;
        }

        public bool ShowConfirmation(string message, string title)
        {
            ConfirmationMessages.Add(message);
            return ShowConfirmationResult;
        }

        public void ShowInformation(string message, string title)
        {
            InformationMessages.Add(message);
        }
    }
}
