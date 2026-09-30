using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace TypeIt4Me.Models
{
    public class Snippet : ObservableObject
    {
        private string _name = string.Empty;
        private string _content = string.Empty;
        private string _category = string.Empty;
        private Key _triggerKey = Key.None;
        private ModifierKeys _triggerModifiers = ModifierKeys.None;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Content
        {
            get => _content;
            set
            {
                if (SetProperty(ref _content, value)) OnPropertyChanged(nameof(Preview));
            }
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public Key TriggerKey
        {
            get => _triggerKey;
            set
            {
                if (SetProperty(ref _triggerKey, value)) OnPropertyChanged(nameof(HotkeyLabel));
            }
        }

        public ModifierKeys TriggerModifiers
        {
            get => _triggerModifiers;
            set
            {
                if (SetProperty(ref _triggerModifiers, value)) OnPropertyChanged(nameof(HotkeyLabel));
            }
        }

        [JsonIgnore]
        public string HotkeyLabel => Services.HotkeyDisplay.Format(TriggerKey, TriggerModifiers);

        [JsonIgnore]
        public string Preview
        {
            get
            {
                string content = Content ?? string.Empty;
                // Bound preview work even for very large imported snippets.
                string prefix = content.Substring(0, Math.Min(content.Length, 240));
                return string.Join(" ", prefix.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }
    }
}
