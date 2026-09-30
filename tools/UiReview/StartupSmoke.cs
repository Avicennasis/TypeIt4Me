using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Views;

internal static class StartupSmoke
{
    public static int Run(string output)
    {
        string folder = Path.GetFullPath(output);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Console.Error.WriteLine("Use a fresh empty output directory for the production smoke test.");
            return 2;
        }
        Directory.CreateDirectory(folder);
        string data = Path.Combine(folder, "synthetic-profile"); Directory.CreateDirectory(data);
        Constants.SetDataDirectoryForTests(data);
        File.WriteAllText(Path.Combine(data, "settings.json"), JsonSerializer.Serialize(new AppSettings()));
        var sample = new Snippet { Name = "Synthetic email", Content = "hello@example.com", TriggerKey = Key.E, TriggerModifiers = ModifierKeys.Control | ModifierKeys.Alt };
        File.WriteAllText(Path.Combine(data, "snippets.json"), JsonSerializer.Serialize(new[] { sample }));
        var app = new TypeIt4Me.App(); app.InitializeComponent();
        var checks = new List<string>(); Process? target = null; bool passed = false;
        using var bindingLog = new StringWriter();
        var listener = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await Wait(() => app.MainWindow is MainWindow { IsVisible: true });
                var main = (MainWindow)app.MainWindow; var vm = (MainViewModel)main.DataContext;
                Check(vm.FilteredSnippets.Count == 1, "Production startup loads the synthetic profile");
                main.WindowState = WindowState.Minimized; await Task.Delay(100);
                Check(!main.IsVisible, "Minimize-to-tray hides the window");
                vm.RestoreFromTrayCommand.Execute(null); await Task.Delay(100);
                Check(main.IsVisible && main.WindowState == WindowState.Normal, "Tray restore returns a normal visible window");
                main.Close(); Check(!main.IsVisible, "Close-to-tray keeps the app running");
                vm.RestoreFromTrayCommand.Execute(null);
                vm.ShowSettingsCommand.Execute(null); Check(Find<SettingsWindow>()?.IsVisible == true, "Settings opens through the production event wiring");
                Find<SettingsWindow>()!.Close();
                vm.ShowHelpCommand.Execute(null); Check(Find<HelpWindow>()?.IsVisible == true, "Help opens through the production event wiring"); Find<HelpWindow>()!.Close();
                vm.IsDarkMode = true; await Task.Delay(100); Capture(main, "startup-dark-main");
                Check(((SolidColorBrush)main.Background).Color == ((SolidColorBrush)app.FindResource("CanvasBrush")).Color, "Live theme switching reaches the production window");
                int dark = 0; var handle = new WindowInteropHelper(main).Handle;
                Check(DwmGetWindowAttribute(handle, 20, out dark, sizeof(int)) == 0 && dark == 1, "Windows 11 native chrome is in dark mode");
                var originalSize = new Size(main.Width, main.Height); vm.IsMiniMode = true; vm.IsMiniMode = false;
                Check(new Size(main.Width, main.Height) == originalSize, "Mini Mode restores the production window size");
                vm.AddSnippetCommand.Execute(null);
                // Modal event wiring is synchronous; automate it from the nested dispatcher loop.
            }
            catch (Exception ex) { Fail(ex); }
        });
        // The editor above opens a nested dialog loop. This timer completes it,
        // then the remaining scenarios use the same production application.
        var editorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        editorTimer.Tick += async (_, _) =>
        {
            var editor = Find<SnippetEditorWindow>(); if (editor == null) return;
            editorTimer.Stop();
            try
            {
                var editorVm = (SnippetEditorViewModel)editor.DataContext;
                editorVm.Name = "Synthetic reply"; editorVm.Content = "A short reply."; editorVm.SaveCommand.Execute(null);
                await Wait(() => ((MainViewModel)app.MainWindow.DataContext).FilteredSnippets.Count == 2 && !((MainViewModel)app.MainWindow.DataContext).IsDialogOpen);
                var main = (MainWindow)app.MainWindow; var vm = (MainViewModel)main.DataContext;
                Check(vm.FilteredSnippets.Count == 2, "Editor saves through real services");
                Schedule(() => FillPin(Find<PinEntryWindow>()!, "review-only-2026", true));
                vm.SetPinCommand.Execute(null);
                Check(vm.HasPin && File.ReadAllText(Path.Combine(data, "snippets.json")).StartsWith("V3|"), "PIN creation writes the existing encrypted V3 format");
                vm.LockCommand.Execute(null); Check(vm.IsLocked && vm.FilteredSnippets.Count == 0, "Lock redacts the production window"); Capture(main, "startup-locked");
                Schedule(() => FillPin(Find<PinEntryWindow>()!, "incorrect", false));
                var retryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
                retryTimer.Tick += (_, _) =>
                {
                    var pin = Find<PinEntryWindow>(); if (pin == null) return;
                    var error = (TextBlock)pin.FindName("ValidationText");
                    if (error.Visibility != Visibility.Visible) return;
                    retryTimer.Stop(); Check(pin.IsVisible, "Incorrect PIN stays in the same dialog"); Capture(pin, "startup-pin-error"); FillPin(pin, "review-only-2026", false);
                };
                retryTimer.Start(); vm.UnlockCommand.Execute(null);
                Check(!vm.IsLocked && vm.FilteredSnippets.Count == 2, "Correct PIN decrypts and restores the collection");
                // A second process supplies a known target, so no personal app receives input.
                target = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, ArgumentList = { "--input-target", folder } });
                await Wait(() => File.Exists(Path.Combine(folder, "target-handle.txt")));
                var targetHandle = new IntPtr(long.Parse(File.ReadAllText(Path.Combine(folder, "target-handle.txt"))));
                Check(NativeMethods.SetForegroundWindow(targetHandle), "Synthetic input target accepts foreground focus");
                await Task.Delay(500); main.Activate(); await Task.Delay(100);
                bool clipboardHadText = Clipboard.ContainsText(); string? clipboardText = clipboardHadText ? Clipboard.GetText() : null;
                await vm.TriggerSnippetCommand.ExecuteAsync(vm.FilteredSnippets.First(s => s.Name == sample.Name));
                await Wait(() => TargetText().Contains(sample.Content));
                Check(Clipboard.ContainsText() == clipboardHadText && (!clipboardHadText || Clipboard.GetText() == clipboardText), "Native SendInput leaves clipboard text unchanged");
                Check(NativeMethods.SetForegroundWindow(targetHandle), "Global hotkey target accepts focus"); await Task.Delay(300);
                SendHotkey(); await Wait(() => TargetText().Length == sample.Content.Length * 2);
                Check(true, "Registered Ctrl+Alt+E hotkey inserts into the synthetic target");
                // Removal presents a confirmation and then authenticates the current PIN.
                var removalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                removalTimer.Tick += (_, _) =>
                {
                    if (Find<MessageWindow>() is { } message) Click((Button)message.FindName("ConfirmButton"));
                    else if (Find<PinEntryWindow>() is { } pin) { removalTimer.Stop(); FillPin(pin, "review-only-2026", false); }
                };
                removalTimer.Start(); await vm.RemovePinCommand.ExecuteAsync(null);
                Check(!vm.HasPin && JsonSerializer.Deserialize<List<Snippet>>(File.ReadAllText(Path.Combine(data, "snippets.json")))?.Count == 2, "PIN removal preserves the collection as explicitly requested plaintext");
                var encryptedManager = new SnippetManager(new FileLogger(Path.Combine(folder, "smoke-errors.log")));
                using (encryptedManager)
                {
                    await encryptedManager.LoadSnippetsAsync();
                    string exported = Path.Combine(folder, "synthetic-export.json"); await encryptedManager.ExportSnippetsAsync(exported);
                    Check(SnippetManager.TryDeserializeSnippets(File.ReadAllText(exported), ReadOnlySpan<char>.Empty)?.Count == 2, "Real export contains the saved collection");
                    Check(await encryptedManager.ImportSnippetsAsync(exported) && encryptedManager.Snippets.Count == 4, "Real import appends the saved snippets");
                }
                if (bindingLog.ToString().Length != 0) throw new InvalidOperationException("Binding warnings were recorded.");
                passed = true; File.WriteAllLines(Path.Combine(folder, "startup-checks.txt"), checks);
                Console.WriteLine($"PASS: {checks.Count} production startup, tray, editor, PIN, storage, hotkey and input checks; no binding warnings.");
                main.ExitApplication();
            }
            catch (Exception ex) { Fail(ex); }
        };
        editorTimer.Start();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        timeout.Tick += (_, _) => { timeout.Stop(); Fail(new TimeoutException("Startup smoke exceeded 45 seconds.")); }; timeout.Start();
        try { app.Run(); }
        finally
        {
            File.WriteAllText(Path.Combine(folder, "startup-bindings.log"), bindingLog.ToString());
            File.WriteAllText(Path.Combine(folder, "target-stop"), "stop");
            if (target != null && !target.HasExited && !target.WaitForExit(2000)) target.Kill();
            target?.Dispose();
        }
        return passed ? 0 : 1;

        T? Find<T>() where T : Window => app.Windows.OfType<T>().FirstOrDefault(w => w.IsVisible);
        void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); checks.Add("PASS " + description); }
        void Fail(Exception ex) { Console.Error.WriteLine(ex); File.WriteAllLines(Path.Combine(folder, "startup-checks.txt"), checks.Append("FAIL " + ex.Message)); app.Shutdown(1); }
        string TargetText() { try { return File.ReadAllText(Path.Combine(folder, "target-text.txt")); } catch (IOException) { return ""; } }
        void Capture(Window window, string name)
        {
            window.UpdateLayout(); var content = (FrameworkElement)window.Content;
            double width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
            double height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
            bitmap.Render(background); bitmap.Render(content); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream);
        }
    }

    public static int RunInputTarget(string folder)
    {
        var app = new Application(); var input = new TextBox { AcceptsReturn = true, FontSize = 18, Margin = new Thickness(20) };
        var window = new Window { Title = "TypeIt4Me synthetic input target", Width = 480, Height = 240, Content = input };
        window.SourceInitialized += (_, _) => File.WriteAllText(Path.Combine(folder, "target-handle.txt"), new WindowInteropHelper(window).Handle.ToInt64().ToString());
        window.Loaded += (_, _) => input.Focus();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => { File.WriteAllText(Path.Combine(folder, "target-text.txt"), input.Text); if (File.Exists(Path.Combine(folder, "target-stop"))) app.Shutdown(); }; timer.Start();
        return app.Run(window);
    }

    private static void FillPin(PinEntryWindow window, string password, bool confirmation)
    {
        ((PasswordBox)window.FindName("PinBox")).Password = password;
        if (confirmation) ((PasswordBox)window.FindName("ConfirmBox")).Password = password;
        Click((Button)window.FindName("SubmitButton"));
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Schedule(Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); action(); }; timer.Start();
    }
    private static async Task Wait(Func<bool> condition)
    {
        for (int i = 0; i < 100; i++) { if (condition()) return; await Task.Delay(100); }
        throw new TimeoutException("A Windows UI condition did not become true within 10 seconds.");
    }
    private static void SendHotkey()
    {
        ushort[] keys = { 0x11, 0x12, 0x45, 0x45, 0x12, 0x11 };
        var inputs = keys.Select((key, i) => new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = key, dwFlags = i >= 3 ? NativeMethods.KEYEVENTF_KEYUP : 0 } } }).ToArray();
        if (NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size) != inputs.Length) throw new InvalidOperationException("Windows rejected the synthetic hotkey input.");
    }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);
}
