using System.Windows;
using System.Windows.Input;
using TypeIt4Me.ViewModels;

namespace TypeIt4Me.Views
{
    public partial class SnippetEditorWindow : Window
    {
        public SnippetEditorWindow()
        {
            InitializeComponent();
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore modifier keys by themselves
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            if (DataContext is SnippetEditorViewModel vm)
            {
                vm.UpdateHotkey(key, Keyboard.Modifiers);
            }
        }
    }
}
