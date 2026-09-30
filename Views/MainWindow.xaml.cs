using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.ViewModels;

namespace TypeIt4Me.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private Size _fullSize = new(420, 620);
    private Size _miniSize = new(300, 300);
    private bool _mini;
    private bool _resizing;
    private bool _exiting;
    private AppSettings? _placement;
    private Action? _savePlacement;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
        _placementTimer.Tick += (_, _) => { _placementTimer.Stop(); SavePlacement(); };
        SizeChanged += (_, _) => SchedulePlacementSave();
        LocationChanged += (_, _) => SchedulePlacementSave();
        Loaded += (_, _) => { WindowPlacement.EnsureVisible(this); SearchBox.Focus(); };
    }

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null) _viewModel.RequestWindowResize -= ApplyMode;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel == null) return;
        _viewModel.RequestWindowResize += ApplyMode;
        ApplyMode(_viewModel.IsMiniMode);
    }

    public void RestorePlacement(AppSettings settings, Action save)
    {
        _placement = settings; _savePlacement = save;
        _fullSize = SafeSize(settings.WindowWidth, settings.WindowHeight, 420, 620, 320, 400);
        _miniSize = SafeSize(settings.MiniWidth, settings.MiniHeight, 300, 300, 268, 200);
        _resizing = true;
        var size = _mini ? _miniSize : _fullSize;
        Width = size.Width; Height = size.Height;
        if (settings.WindowLeft is double left && double.IsFinite(left) &&
            settings.WindowTop is double top && double.IsFinite(top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left; Top = top;
        }
        _resizing = false;
    }

    private static Size SafeSize(double width, double height, double defaultWidth, double defaultHeight, double minWidth, double minHeight) =>
        new(double.IsFinite(width) ? Math.Max(minWidth, width) : defaultWidth,
            double.IsFinite(height) ? Math.Max(minHeight, height) : defaultHeight);

    private void ApplyMode(bool mini)
    {
        if (_mini == mini) return;
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        if (_mini) _miniSize = new Size(Width, Height); else _fullSize = new Size(Width, Height);
        _mini = mini; _resizing = true;
        MinWidth = mini ? 268 : 320; MinHeight = mini ? 200 : 400;
        var size = mini ? _miniSize : _fullSize;
        Width = size.Width; Height = size.Height;
        _resizing = false;
        if (IsLoaded) WindowPlacement.EnsureVisible(this);
        SchedulePlacementSave();
    }

    private void SchedulePlacementSave()
    {
        if (_resizing || !IsLoaded || _placement == null || WindowState != WindowState.Normal) return;
        _placementTimer.Stop(); _placementTimer.Start();
    }

    public void SavePlacement()
    {
        if (_placement == null || WindowState != WindowState.Normal) return;
        if (_mini) _miniSize = new Size(Width, Height); else _fullSize = new Size(Width, Height);
        _placement.WindowLeft = Left; _placement.WindowTop = Top;
        _placement.WindowWidth = _fullSize.Width; _placement.WindowHeight = _fullSize.Height;
        _placement.MiniWidth = _miniSize.Width; _placement.MiniHeight = _miniSize.Height;
        _savePlacement?.Invoke();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true;
        }
        else if (SearchBox.IsKeyboardFocusWithin && e.Key == Key.Down)
        {
            SnippetList.Focus();
            if (SnippetList.SelectedItem != null)
                (SnippetList.ItemContainerGenerator.ContainerFromItem(SnippetList.SelectedItem) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (SearchBox.IsKeyboardFocusWithin && e.Key == Key.Escape)
        {
            _viewModel.ClearSearchCommand.Execute(null); e.Handled = true;
        }
        else if ((SearchBox.IsKeyboardFocusWithin || SnippetList.IsKeyboardFocusWithin) && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Enter && _viewModel.UseSelectedSnippetCommand.CanExecute(null))
            { _viewModel.UseSelectedSnippetCommand.Execute(null); e.Handled = true; }
            else if (SnippetList.IsKeyboardFocusWithin && e.Key == Key.F2)
            { _viewModel.EditSelectedSnippetCommand.Execute(null); e.Handled = true; }
            else if (SnippetList.IsKeyboardFocusWithin && e.Key == Key.Delete)
            { _viewModel.DeleteSelectedSnippetCommand.Execute(null); e.Handled = true; }
        }
    }

    private void SnippetMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;
        SnippetList.SelectedItem = button.DataContext;
        SnippetList.ContextMenu.PlacementTarget = SnippetList;
        SnippetList.ContextMenu.IsOpen = true;
    }

    private void SnippetList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? element = e.OriginalSource as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        if (element is ListBoxItem item) item.IsSelected = true;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    public void ExitApplication()
    {
        _exiting = true; SavePlacement(); TaskbarIcon.Dispose(); Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _viewModel?.MinimizeToTray == true) Hide();
        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        if (!_exiting && _viewModel?.MinimizeToTray == true) { e.Cancel = true; Hide(); }
        else { _placementTimer.Stop(); TaskbarIcon.Dispose(); }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _placementTimer.Stop();
        if (_viewModel != null) _viewModel.RequestWindowResize -= ApplyMode;
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
