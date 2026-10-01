using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Views;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Supply an output directory after --startup-smoke or --input-target.");
            return 2;
        }
        if (args.Length > 0 && args[0] == "--startup-smoke") return StartupSmoke.Run(args[1]);
        if (args.Length > 0 && args[0] == "--input-target") return StartupSmoke.RunInputTarget(args[1]);
        var folder = Path.GetFullPath(args.Length > 0 ? args[0] : "ui-review");
        Directory.CreateDirectory(folder);
        using var bindingLog = new StringWriter();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new TextWriterTraceListener(bindingLog));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        // Initialize the real resource graph, without running production startup or touching AppData.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // App.Run normally supplies this context. Keep async view-model continuations
        // on the WPF dispatcher while the review drives nested frames itself.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        app.Resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/TypeIt4Me;component/Views/Resources/DesignSystem.xaml") };
        using var theme = new ThemeService();
        var manager = new FakeSnippetManager();
        AddSamples(manager);
        var settings = new FakeSettingsManager();
        var logger = new FakeLogger();
        var vm = new MainViewModel(manager, new FakeHotkeyManager(), new FakeInputInjector(), new FakeFocusTracker(),
            settings, new FakeAutoLockService(), theme, logger, new FakeDialogService());
        var main = new MainWindow { DataContext = vm };
        var checks = new List<string>();
        try
        {
            foreach (bool dark in new[] { false, true })
            {
                vm.IsDarkMode = dark; theme.SetTheme(dark);
                var prefix = dark ? "dark" : "light";
                Check(Contrast("ForegroundBrush", "CanvasBrush") >= 4.5 && Contrast("SecondaryBrush", "SurfaceBrush") >= 4.5 &&
                    Contrast("AccentTextBrush", "AccentBrush") >= 4.5 && Contrast("DangerBrush", "CanvasBrush") >= 4.5,
                    "Primary, supporting, accent-button and error text meet 4.5:1 palette contrast");
                Capture(main, prefix + "-main", prepare: window => ((ListBox)window.FindName("SnippetList")).ScrollIntoView(vm.SelectedSnippet));
                Check(main.Background is SolidColorBrush background && background.Color == ((SolidColorBrush)app.FindResource("CanvasBrush")).Color, "Existing main window follows the current palette");
                foreach (var scale in new[] { 1.25, 1.5 }) Capture(main, prefix + "-main", scale);
                vm.IsAlwaysOnTop = false; Capture(main, prefix + "-unpinned"); vm.IsAlwaysOnTop = true;
                using (vm.BeginDataOperation())
                {
                    Capture(main, prefix + "-busy");
                    Check(vm.IsWorking && !vm.TriggerSnippetCommand.CanExecute(manager.Snippets[0]), "Active data operation displays working state and disables insertion");
                }
                vm.TriggerSnippetCommand.Execute(manager.Snippets[0]); Pump(); Capture(main, prefix + "-target-error");
                vm.ReportStatus("Select a text field in another app, then insert a snippet.");
                main.Width = main.MinWidth; main.Height = main.MinHeight; Capture(main, prefix + "-minimum");
                main.Width = 420; main.Height = 620;
                vm.SearchText = "Work"; WaitForSearch(); Capture(main, prefix + "-search");
                Check(((ListBox)main.FindName("SnippetList")).Items.Count == vm.FilteredSnippets.Count,
                    "Rendered result items match the filtered collection");
                vm.SearchText = "no match here"; WaitForSearch(); Capture(main, prefix + "-no-results");
                vm.SearchText = ""; WaitForSearch();
                vm.IsMiniMode = true; main.Width = 300; main.Height = 300; Capture(main, prefix + "-mini");
                main.Width = main.MinWidth; main.Height = main.MinHeight; Capture(main, prefix + "-mini-minimum");
                vm.IsMiniMode = false;
                Check(main.Width == 420 && main.Height == 620, "Full size is restored after Mini Mode");
                Capture(new SnippetEditorWindow { DataContext = new SnippetEditorViewModel(manager.Snippets[0]) }, prefix + "-editor");
                Capture(new SnippetEditorWindow { Width = 460, Height = 520, DataContext = new SnippetEditorViewModel(manager.Snippets[0]) }, prefix + "-editor-minimum");
                Capture(new SnippetEditorWindow { DataContext = new SnippetEditorViewModel(manager.Snippets[0]) }, prefix + "-editor-commands", prepare: window =>
                {
                    ((Expander)window.FindName("CommandReference")).IsExpanded = true;
                    Pump(); ((ScrollViewer)window.FindName("EditorContent")).ScrollToBottom();
                });
                var invalid = new SnippetEditorViewModel(new Snippet()) { Name = "Too long", Content = new string('x', SnippetEditorViewModel.MaxContentLength + 1) };
                var editor = new SnippetEditorWindow { DataContext = invalid }; Capture(editor, prefix + "-validation");
                Check(!invalid.SaveCommand.CanExecute(null), "Oversized snippet cannot be saved");
                Capture(new SettingsWindow { DataContext = vm }, prefix + "-settings");
                Capture(new SettingsWindow { Width = 460, Height = 480, DataContext = vm }, prefix + "-settings-minimum");
                Capture(new SettingsWindow { DataContext = vm }, prefix + "-settings-data", prepare: window => ((ScrollViewer)window.FindName("SettingsContent")).ScrollToBottom());
                Capture(new PinEntryWindow("Unlock TypeIt4Me"), prefix + "-pin");
                var pin = new PinEntryWindow("Protect your snippets", creating: true); pin.ShowValidation("The PINs do not match. Please try again."); Capture(pin, prefix + "-pin-create-validation");
                Capture(new PinEntryWindow("Protect your snippets", creating: true), prefix + "-pin-compact", prepare: window => window.MaxHeight = 360);
                Capture(new PinEntryWindow("Protect your snippets", creating: true), prefix + "-pin-compact-actions", prepare: window =>
                {
                    window.MaxHeight = 360; Pump(); ((Button)window.FindName("SubmitButton")).BringIntoView(); Pump();
                    Check(((ScrollViewer)window.FindName("DialogContent")).VerticalOffset > 0,
                        "PIN actions remain reachable when work-area height constrains the dialog");
                });
                Capture(new InputWindow("Auto-lock after (minutes)", "5"), prefix + "-input");
                Capture(new HelpWindow(), prefix + "-help");
                Capture(new HelpWindow { Width = 460, Height = 480 }, prefix + "-help-minimum");
                Capture(new HelpWindow(), prefix + "-help-about", prepare: window => ((ScrollViewer)window.FindName("HelpContent")).ScrollToBottom());
                Capture(new MessageWindow("Delete “Work email”? This permanently removes the snippet from this device.", "Delete snippet", true, true), prefix + "-confirmation");
                var dirtyEditorVm = new SnippetEditorViewModel(manager.Snippets[0]) { Content = "Unsaved draft" };
                Capture(new SnippetEditorWindow { DataContext = dirtyEditorVm }, prefix + "-discard", prepare: window =>
                {
                    window.Close();
                    Check(window.IsVisible && ((StackPanel)window.FindName("DiscardPanel")).Visibility == Visibility.Visible,
                        "Unsaved editor cancellation uses an inline confirmation");
                });
                Capture(new MessageWindow("Removing the PIN saves your snippets as plain text. Enter your current PIN to confirm.", "Remove PIN", true, true), prefix + "-remove-pin");
                vm.LockApp(); Capture(main, prefix + "-locked");
                Check(vm.FilteredSnippets.Count == 0 && !vm.TriggerSnippetCommand.CanExecute(manager.Snippets[0]), "Lock redacts results and blocks insertion");
                vm.UnlockApp();
                manager.Snippets.Clear(); Capture(main, prefix + "-empty"); AddSamples(manager);
                ((ListBox)main.FindName("SnippetList")).Focus(); Capture(main, prefix + "-keyboard-focus");
                Check(app.TryFindResource("IconSearch") is Geometry && app.TryFindResource("AccentButton") is Style, "Theme switching preserves component resources");
                manager.Snippets.ReplaceAll(Enumerable.Range(1, 1000).Select(index => new Snippet { Name = $"Snippet {index:0000}", Content = "A reusable line of text." }));
                Capture(main, prefix + "-large-collection", prepare: window => ((ListBox)window.FindName("SnippetList")).ScrollIntoView(manager.Snippets[^1]));
                var list = (ListBox)main.FindName("SnippetList");
                Check(Enumerable.Range(0, 1000).Count(index => list.ItemContainerGenerator.ContainerFromIndex(index) != null) < 100, "Large collection keeps fewer than 100 realized rows");
                manager.Snippets.Clear(); AddSamples(manager);
            }
            // Render high-contrast resource treatment using this VM's Windows system palette.
            var highContrast = new ResourceDictionary { Source = new Uri("pack://application:,,,/TypeIt4Me;component/Views/HighContrastTheme.xaml") };
            foreach (System.Collections.DictionaryEntry entry in highContrast)
                if (entry.Value is SolidColorBrush incoming)
                {
                    if (app.Resources[entry.Key] is SolidColorBrush existing && !existing.IsFrozen) existing.Color = incoming.Color;
                    else app.Resources[entry.Key] = incoming.CloneCurrentValue();
                }
            Capture(main, "high-contrast-main", prepare: window => ((ListBox)window.FindName("SnippetList")).ScrollIntoView(vm.SelectedSnippet));
            var sample = manager.Snippets[0];
            var before = sample.Content;
            var cancelVm = new SnippetEditorViewModel(sample) { Content = "unsaved" };
            cancelVm.CancelCommand.Execute(null);
            Check(sample.Content == before, "Cancel does not modify the snippet");
            Check(logger.ErrorLogs.Count == 0, "No service errors during resource and layout review");
            File.WriteAllText(Path.Combine(folder, "services.log"), string.Join(Environment.NewLine, logger.ErrorLogs.Select(entry => entry.Message + ": " + entry.Exception?.GetType().Name)));
            File.WriteAllLines(Path.Combine(folder, "checks.txt"), checks);
            bindingLog.Flush();
            File.WriteAllText(Path.Combine(folder, "bindings.log"), bindingLog.ToString());
            if (bindingLog.ToString().Length != 0) throw new InvalidOperationException("Binding warnings were recorded; inspect bindings.log.");
            Console.WriteLine($"PASS: {checks.Count} UI checks; {Directory.GetFiles(folder, "*.png").Length} WPF client-area screenshots; no binding warnings.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex); return 1;
        }
        finally
        {
            ((IDisposable)main.FindName("TaskbarIcon")).Dispose();
            // Do not call App.Run: the review uses only deterministic fake services.
            app.Shutdown();
        }

        void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add("PASS " + description);
        }
        void WaitForSearch()
        {
            var timeout = Stopwatch.StartNew();
            while (vm.IsSearching && timeout.Elapsed < TimeSpan.FromSeconds(5)) Pump(20);
            if (vm.IsSearching) throw new TimeoutException("Search did not finish within five seconds.");
        }
        double Contrast(string foreground, string background)
        {
            double Luminance(string key)
            {
                var color = ((SolidColorBrush)app.FindResource(key)).Color;
                double Channel(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
                return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
            }
            double a = Luminance(foreground), b = Luminance(background);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }
        void Capture(Window window, string name, double scale = 1, Action<Window>? prepare = null)
        {
            window.Show(); Pump(); window.UpdateLayout();
            if (prepare != null) { prepare(window); Pump(); window.UpdateLayout(); }
            var content = (FrameworkElement)window.Content;
            double width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
            double height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            bitmap.Render(background); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(folder, name + (scale == 1 ? "" : "-" + (int)(scale * 100)) + ".png"))) encoder.Save(stream);
            window.Hide();
            if (window is SnippetEditorWindow snippetEditor) snippetEditor.CloseForLock();
            else if (window != main) window.Close();
        }
    }

    private static void AddSamples(FakeSnippetManager manager)
    {
        manager.Snippets.Add(new Snippet { Name = "Work email", Content = "hello@example.com", Category = "Everyday", TriggerKey = Key.E, TriggerModifiers = ModifierKeys.Control | ModifierKeys.Alt });
        manager.Snippets.Add(new Snippet { Name = "A quick thank you", Content = "Thanks for taking the time to help. I really appreciate it!", Category = "Messages" });
        manager.Snippets.Add(new Snippet { Name = "Meeting follow-up", Content = "Hi team,{ENTER}{ENTER}Here are the next steps from our conversation.", Category = "Work" });
        manager.Snippets.Add(new Snippet { Name = "A longer title that stays readable in a narrow window without taking over", Content = "Long content stays on one preview line. The full content is always available in the editor.", Category = "Work" });
    }

    private static void Pump(int milliseconds = 200)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
