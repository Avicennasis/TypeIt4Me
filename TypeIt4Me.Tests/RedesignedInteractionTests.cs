using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using TypeIt4Me.Tests.Fakes;
using TypeIt4Me.ViewModels;
using Xunit;

namespace TypeIt4Me.Tests;

public class RedesignedInteractionTests
{
    [Fact]
    public void LockDuringDataOperation_RedactsImmediatelyAndClearsPinAfterCompletion()
    {
        var manager = new FakeSnippetManager(); manager.Snippets.Add(new Snippet { Name = "Secret" });
        var vm = Create(manager);
        var operation = vm.BeginDataOperation();
        vm.LockApp();
        Assert.True(vm.IsLocked); Assert.Empty(vm.FilteredSnippets); Assert.Empty(manager.SetPinLog);
        Assert.False(vm.UnlockCommand.CanExecute(null)); Assert.False(vm.UnlockApp());
        operation.Dispose(); operation.Dispose();
        Assert.Equal(new[] { string.Empty }, manager.SetPinLog);
        Assert.True(vm.UnlockCommand.CanExecute(null)); Assert.True(vm.UnlockApp());
    }

    [Fact]
    public void InsertionAndDialogs_BlockOverlappingInsertionCommands()
    {
        var manager = new FakeSnippetManager(); var snippet = new Snippet { Name = "Example" }; manager.Snippets.Add(snippet);
        var vm = Create(manager);
        vm.IsInserting = true; Assert.False(vm.TriggerSnippetCommand.CanExecute(snippet)); Assert.False(vm.UseSelectedSnippetCommand.CanExecute(null));
        vm.IsInserting = false; vm.IsDialogOpen = true; Assert.False(vm.TriggerSnippetCommand.CanExecute(snippet));
    }

    private static MainViewModel Create(FakeSnippetManager manager, FakeDialogService? dialogs = null,
        FakeInputInjector? injector = null, FakeSettingsManager? settings = null) =>
        new(manager, new FakeHotkeyManager(), injector ?? new FakeInputInjector(), new FakeFocusTracker(),
            settings ?? new FakeSettingsManager(), new FakeAutoLockService(), new FakeThemeService(), new FakeLogger(), dialogs ?? new FakeDialogService());

    [Fact]
    public async Task RemovePin_LockingDuringEntryKeepsProtection()
    {
        var settings = new FakeSettingsManager(); settings.Settings.PinHash = "stored-hash";
        var manager = new FakeSnippetManager(); var vm = Create(manager, settings: settings);
        vm.RequestPinInput += callback => { vm.LockApp(); callback("123456".ToCharArray()); };
        await vm.RemovePinCommand.ExecuteAsync(null);
        Assert.Equal("stored-hash", settings.Settings.PinHash);
        Assert.True(vm.IsLocked); Assert.True(vm.HasPin);
    }

    [Fact]
    public async Task RemovePin_LockingDuringVerificationKeepsProtection()
    {
        var settings = new FakeSettingsManager(); settings.Settings.PinSalt = CryptoService.GenerateSalt();
        settings.Settings.PinHash = CryptoService.HashPin("123456".AsSpan(), settings.Settings.PinSalt);
        var manager = new FakeSnippetManager(); var vm = Create(manager, settings: settings);
        vm.RequestPinInput += callback => callback("123456".ToCharArray());
        var removing = vm.RemovePinCommand.ExecuteAsync(null);
        vm.LockApp(); await removing;
        Assert.True(vm.HasPin); Assert.True(vm.IsLocked);
        Assert.Equal(new[] { string.Empty }, manager.SetPinLog);
    }

    [Fact]
    public async Task Import_LockingDuringPinEntryDoesNotStartAnotherImport()
    {
        var manager = new FakeSnippetManager { ImportResult = false };
        var vm = Create(manager, new FakeDialogService { OpenFileDialogResult = "encrypted.json" });
        int calls = 0; manager.AsyncImportHandler = (_, _) => { calls++; return Task.FromResult(false); };
        vm.RequestPinInput += callback => { vm.LockApp(); callback("123456".ToCharArray()); };
        await vm.ImportSnippetsCommand.ExecuteAsync(null);
        Assert.Equal(1, calls); Assert.True(vm.IsLocked);
    }

    [Fact]
    public async Task LockedActions_CannotRevealModifyOrInsertSnippets()
    {
        var manager = new FakeSnippetManager();
        var snippet = new Snippet { Name = "Private", Content = "private text" }; manager.Snippets.Add(snippet);
        var dialogs = new FakeDialogService(); var injector = new FakeInputInjector();
        var vm = Create(manager, dialogs, injector);
        vm.LockApp();
        Assert.Empty(vm.FilteredSnippets);
        Assert.False(vm.TriggerSnippetCommand.CanExecute(snippet));
        Assert.False(vm.AddSnippetCommand.CanExecute(null));
        Assert.False(vm.ImportSnippetsCommand.CanExecute(null));
        Assert.False(vm.ExportSnippetsCommand.CanExecute(null));
        // Generated async commands can be invoked directly: the method also guards the state.
        await vm.TriggerSnippetCommand.ExecuteAsync(snippet);
        await vm.DeleteSnippetCommand.ExecuteAsync(snippet);
        Assert.Null(injector.LastText); Assert.Single(manager.Snippets); Assert.Empty(dialogs.ConfirmationMessages);
        vm.UnlockApp(); Assert.Single(vm.FilteredSnippets);
    }

    [Fact]
    public async Task Delete_CancellationPreservesSnippet()
    {
        var manager = new FakeSnippetManager(); var snippet = new Snippet { Name = "Keep" }; manager.Snippets.Add(snippet);
        var dialogs = new FakeDialogService { ShowConfirmationResult = false }; var vm = Create(manager, dialogs);
        await vm.DeleteSnippetCommand.ExecuteAsync(snippet);
        Assert.Single(manager.Snippets); Assert.Single(dialogs.ConfirmationMessages);
    }

    [Fact]
    public async Task Delete_ConfirmationRemovesSnippetAndUpdatesEmptyState()
    {
        var manager = new FakeSnippetManager(); var snippet = new Snippet { Name = "Remove" }; manager.Snippets.Add(snippet);
        var vm = Create(manager);
        await vm.DeleteSnippetCommand.ExecuteAsync(snippet);
        Assert.Empty(manager.Snippets); Assert.True(vm.IsFirstRun); Assert.False(vm.HasNoResults); Assert.Null(vm.SelectedSnippet);
    }

    [Fact]
    public async Task Delete_SaveFailureRestoresRowAndReportsError()
    {
        var manager = new FakeSnippetManager { RemoveException = new IOException("Synthetic write failure") };
        var snippet = new Snippet { Name = "Keep" }; manager.Snippets.Add(snippet);
        var vm = Create(manager);
        await vm.DeleteSnippetCommand.ExecuteAsync(snippet);
        Assert.Same(snippet, Assert.Single(manager.Snippets)); Assert.Single(vm.FilteredSnippets);
        Assert.True(vm.StatusIsError); Assert.Contains("still available", vm.StatusMessage);
    }

    [Fact]
    public async Task Search_MatchesContentAndTrimmedQueries()
    {
        var manager = new FakeSnippetManager(); manager.Snippets.Add(new Snippet { Name = "Address", Content = "hello@example.com" });
        var vm = Create(manager); vm.SearchText = "  EXAMPLE  ";
        Assert.True(vm.IsSearching); Assert.False(vm.UseSelectedSnippetCommand.CanExecute(null));
        await vm.DeleteSelectedSnippetCommand.ExecuteAsync(null); Assert.Single(manager.Snippets);
        await WaitForSearch(vm);
        Assert.Single(vm.FilteredSnippets); Assert.False(vm.IsSearching); Assert.True(vm.UseSelectedSnippetCommand.CanExecute(null));
        vm.SearchText = "missing"; await WaitForSearch(vm);
        Assert.True(vm.HasNoResults); Assert.False(vm.IsFirstRun); Assert.Null(vm.SelectedSnippet);
    }

    [Fact]
    public void EditingSearchMatch_RefreshesResultsWithoutCollectionChange()
    {
        var manager = new FakeSnippetManager(); var snippet = new Snippet { Name = "Old", Content = "body" }; manager.Snippets.Add(snippet);
        var vm = Create(manager); vm.SearchText = "New";
        snippet.Name = "New"; vm.RefreshAfterEdit();
        Assert.Single(vm.FilteredSnippets); Assert.Equal(snippet, vm.SelectedSnippet);
        snippet.Name = "Other"; vm.RefreshAfterEdit(); Assert.Empty(vm.FilteredSnippets);
    }

    [Fact]
    public async Task PinImport_OwnsBufferUntilAsynchronousDecryptionCompletes()
    {
        bool authenticated = false; char[]? received = null;
        var manager = new FakeSnippetManager { ImportResult = false };
        manager.AsyncImportHandler = async (_, pin) =>
        {
            if (pin == null) return false;
            received = pin; await Task.Delay(40);
            authenticated = new string(pin) == "123456"; return authenticated;
        };
        var vm = Create(manager, new FakeDialogService { OpenFileDialogResult = "encrypted.json" });
        vm.RequestPinInput += callback =>
        {
            char[] temporary = "123456".ToCharArray(); callback(temporary); Array.Clear(temporary);
        };
        await vm.ImportSnippetsCommand.ExecuteAsync(null);
        Assert.True(authenticated); Assert.NotNull(received); Assert.All(received, c => Assert.Equal('\0', c));
    }

    [Fact]
    public void CustomAutoLock_DoesNotAskForPin()
    {
        var vm = Create(new FakeSnippetManager()); bool pinRequested = false;
        vm.RequestPinInput += _ => pinRequested = true;
        vm.RequestInput += (_, _, callback) => callback("42");
        vm.SetCustomAutoLockCommand.Execute(null);
        Assert.False(pinRequested); Assert.Equal(42, vm.AutoLockMinutes);
    }

    [Theory]
    [InlineData("-1")][InlineData("1441")][InlineData("not a number")][InlineData("")]
    public void AutoLock_InvalidDraftDoesNotChangeSavedSetting(string value)
    {
        var vm = Create(new FakeSnippetManager()); vm.AutoLockMinutes = 5; vm.AutoLockDraft = value;
        Assert.False(vm.ApplyAutoLockCommand.CanExecute(null)); Assert.NotEmpty(vm.AutoLockValidation);
        vm.ApplyAutoLockCommand.Execute(null); Assert.Equal(5, vm.AutoLockMinutes);
    }

    [Fact]
    public void RestoreWithPin_CancellationKeepsActionsLocked()
    {
        var settings = new FakeSettingsManager(); settings.Settings.PinHash = "stored"; settings.Settings.LockOnRestore = true;
        var vm = Create(new FakeSnippetManager(), settings: settings); bool requested = false;
        vm.RequestUnlock += () => requested = true;
        vm.RestoreFromTrayCommand.Execute(null);
        Assert.True(requested); Assert.True(vm.IsLocked);
    }

    [Fact]
    public async Task TriggerWithoutTarget_ReportsErrorWithoutSendingInput()
    {
        var injector = new FakeInputInjector(); var manager = new FakeSnippetManager();
        manager.Snippets.Add(new Snippet { Name = "First" });
        var chosen = new Snippet { Name = "Chosen", Content = "body" }; manager.Snippets.Add(chosen);
        var vm = Create(manager, injector: injector);
        await vm.TriggerSnippetCommand.ExecuteAsync(chosen);
        Assert.Same(chosen, vm.SelectedSnippet);
        Assert.Null(injector.LastText); Assert.True(vm.StatusIsError); Assert.Contains("text field", vm.StatusMessage);
    }

    [Fact]
    public void Editor_SizeLimitAndDirtyStateAreMeaningful()
    {
        var snippet = new Snippet { Name = "Name", Content = "original" }; var vm = new SnippetEditorViewModel(snippet);
        Assert.False(vm.HasChanges);
        vm.Content = new string('x', SnippetEditorViewModel.MaxContentLength);
        Assert.True(vm.HasChanges); Assert.True(vm.SaveCommand.CanExecute(null));
        vm.Content += "x"; Assert.False(vm.SaveCommand.CanExecute(null)); Assert.NotEmpty(vm.ValidationMessage);
        vm.SaveCommand.Execute(null); Assert.Equal("original", snippet.Content);
        vm.CancelCommand.Execute(null); Assert.Equal("original", snippet.Content);
        vm.Content = "original"; Assert.False(vm.HasChanges);
    }

    [Fact]
    public void DisplayProperties_DoNotChangeStoredSnippetSchema()
    {
        var snippet = new Snippet { Name = "Name", Content = "first\r\nsecond", TriggerKey = Key.E, TriggerModifiers = ModifierKeys.Control | ModifierKeys.Alt };
        Assert.Equal("first second", snippet.Preview); Assert.Equal("Ctrl + Alt + E", snippet.HotkeyLabel);
        var json = JsonSerializer.Serialize(snippet);
        Assert.DoesNotContain("Preview", json); Assert.DoesNotContain("HotkeyLabel", json);
        var loaded = JsonSerializer.Deserialize<Snippet>(json)!; Assert.Equal(snippet.Content, loaded.Content);
    }

    [Fact]
    public void Theme_HighContrastOverridesManualDarkTheme()
    {
        string? uri = null; using var service = new ThemeService(action => action(), path => uri = path, () => true);
        service.SetTheme(true); Assert.EndsWith("HighContrastTheme.xaml", uri);
    }

    [Fact]
    public void Placement_HandlesRemovedMonitorAndNegativeCoordinates()
    {
        var workArea = new Rect(-1920, 0, 1920, 1040);
        var placed = WindowPlacement.Clamp(new Rect(3000, 2000, 420, 620), workArea);
        Assert.Equal(new Rect(-420, 420, 420, 620), placed);
        Assert.Equal(workArea, WindowPlacement.Clamp(new Rect(-3000, -100, 4000, 2000), workArea));
    }

    [Fact]
    public async Task FocusTracker_ExcludesAllApplicationWindows()
    {
        using var tracker = new FocusTracker(() => new IntPtr(42), window => window == new IntPtr(42));
        tracker.Start(new IntPtr(1)); await Task.Delay(250); tracker.Stop();
        Assert.Equal(IntPtr.Zero, tracker.LastExternalWindowHandle);
    }

    private static async Task WaitForSearch(MainViewModel vm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (vm.IsSearching) await Task.Delay(20, timeout.Token);
    }
}
