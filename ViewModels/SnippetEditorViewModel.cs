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
        private string _name = string.Empty;

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
        private readonly (string Name, string Content, string Category, Key Key, ModifierKeys Modifiers) _original;
        public const int MaxContentLength = 100 * 1024;
        public bool HasChanges => (Name, Content, Category, TriggerKey, TriggerModifiers) != _original;
        public string HotkeyLabel => string.IsNullOrEmpty(Services.HotkeyDisplay.Format(TriggerKey, TriggerModifiers))
            ? "Click here, then press a shortcut" : Services.HotkeyDisplay.Format(TriggerKey, TriggerModifiers);
        public string CharacterCount => $"{Content?.Length ?? 0:N0} / {MaxContentLength:N0} characters";
        public string ValidationMessage => string.IsNullOrWhiteSpace(Name) ? "Give your snippet a name." :
            string.IsNullOrEmpty(Content) ? "Add the text you want this snippet to type." :
            Content.Length > MaxContentLength ? $"Keep the content within {MaxContentLength:N0} characters." : string.Empty;
        public bool IsValid => CanSave();
        public string EditorTitle => string.IsNullOrEmpty(CurrentSnippet.Name) ? "New snippet" : "Edit snippet";

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
             _original = (Name, Content, Category, TriggerKey, TriggerModifiers);
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName is nameof(Name) or nameof(Content) or nameof(Category) or nameof(TriggerKey) or nameof(TriggerModifiers))
            {
                OnPropertyChanged(nameof(HasChanges));
                OnPropertyChanged(nameof(ValidationMessage));
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(CharacterCount));
                OnPropertyChanged(nameof(HotkeyLabel));
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            if (!CanSave()) return;
            CurrentSnippet.Name = Name;
            CurrentSnippet.Content = Content;
            CurrentSnippet.Category = Category;
            CurrentSnippet.TriggerKey = TriggerKey;
            CurrentSnippet.TriggerModifiers = TriggerModifiers;

            OnRequestClose(true);
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrEmpty(Content) && Content.Length <= MaxContentLength;

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

        public Action<bool>? RequestClose { get; set; }

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
