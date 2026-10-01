using System.Windows;
using System.Windows.Media;

namespace TypeIt4Me.Views;

public partial class MessageWindow : Window
{
    public MessageWindow(string message, string title, bool confirmation = false, bool destructive = false)
    {
        InitializeComponent(); Title = title + " · TypeIt4Me";
        HeadingText.Text = title; MessageText.Text = message;
        CancelButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.Content = !confirmation ? "_Done" : title switch
        {
            "Delete snippet" => "_Delete",
            "Remove PIN" => "_Remove PIN",
            _ => "_Continue"
        };
        if (destructive)
        {
            ConfirmButton.Style = (Style)FindResource("DangerButton");
            MessageIcon.Data = (Geometry)FindResource(title == "Remove PIN" ? "IconLock" : "IconDelete");
            MessageIcon.SetResourceReference(ForegroundProperty, "DangerBrush");
        }
        else if (confirmation) MessageIcon.Data = (Geometry)FindResource("IconHelp");
        ConfirmButton.IsDefault = !destructive;
        CancelButton.IsDefault = destructive;
    }
    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
