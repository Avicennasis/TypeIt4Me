using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows.Input;
using TypeIt4Me.Models;

namespace TypeIt4Me.ViewModels
{
    public partial class SnippetEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _content = string.Empty;

        [ObservableProperty]
        private string _category = string.Empty;

        [ObservableProperty]
        private Key _triggerKey;

        [ObservableProperty]
        private ModifierKeys _triggerModifiers;

        public Snippet CurrentSnippet { get; private set; }

        public SnippetEditorViewModel(Snippet? snippet = null)
        {
             if (snippet != null)
             {
                 CurrentSnippet = snippet;
                 Name = snippet.Name;
                 Content = snippet.Content;
                 Category = snippet.Category;
                 TriggerKey = snippet.TriggerKey;
                 TriggerModifiers = snippet.TriggerModifiers;
             }
             else
             {
                 CurrentSnippet = new Snippet();
                 Name = "New Snippet";
             }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            CurrentSnippet.Name = Name;
            CurrentSnippet.Content = Content;
            CurrentSnippet.Category = Category;
            CurrentSnippet.TriggerKey = TriggerKey;
            CurrentSnippet.TriggerModifiers = TriggerModifiers;
            
            OnRequestClose(true);
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrEmpty(Content);

        [RelayCommand]
        private void Cancel()
        {
            OnRequestClose(false);
        }

        [RelayCommand]
        private void ClearHotkey()
        {
            TriggerKey = Key.None;
            TriggerModifiers = ModifierKeys.None;
        }

        public Action<bool> RequestClose { get; set; }

        private void OnRequestClose(bool result)
        {
            RequestClose?.Invoke(result);
        }

        // Logic to capture key can be handled in View code-behind to update VM properties
        public void UpdateHotkey(Key key, ModifierKeys modifiers)
        {
            TriggerKey = key;
            TriggerModifiers = modifiers;
        }
    }
}
