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
            set => SetProperty(ref _content, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public Key TriggerKey
        {
            get => _triggerKey;
            set => SetProperty(ref _triggerKey, value);
        }

        public ModifierKeys TriggerModifiers
        {
            get => _triggerModifiers;
            set => SetProperty(ref _triggerModifiers, value);
        }
    }
}
