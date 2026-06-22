using System;
using System.Threading.Tasks;
using TypeIt4Me.Models;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class MainViewModelTests
    {
        private (MainViewModel viewModel, FakeSnippetManager fakeSnippetManager, FakeHotkeyManager fakeHotkeyManager, FakeInputInjector fakeInputInjector, FakeFocusTracker fakeFocusTracker, FakeSettingsManager fakeSettingsManager, FakeAutoLockService fakeAutoLockService, FakeThemeService fakeThemeService) CreateViewModel()
        {
            var fakeSnippetManager = new FakeSnippetManager();
            var fakeHotkeyManager = new FakeHotkeyManager();
            var fakeInputInjector = new FakeInputInjector();
            var fakeFocusTracker = new FakeFocusTracker();
            var fakeSettingsManager = new FakeSettingsManager();
            var fakeAutoLockService = new FakeAutoLockService();
            var fakeThemeService = new FakeThemeService();

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                fakeHotkeyManager,
                fakeInputInjector,
                fakeFocusTracker,
                fakeSettingsManager,
                fakeAutoLockService,
                fakeThemeService
            );

            return (viewModel, fakeSnippetManager, fakeHotkeyManager, fakeInputInjector, fakeFocusTracker, fakeSettingsManager, fakeAutoLockService, fakeThemeService);
        }

        [Fact]
        public void LockApp_SetsIsLockedToTrue_AndClearsPin()
        {
            // Arrange
            var (viewModel, fakeSnippetManager, _, _, _, _, _, _) = CreateViewModel();

            // Ensure initial state
            Assert.False(viewModel.IsLocked);

            // Act
            viewModel.LockApp();

            // Assert
            Assert.True(viewModel.IsLocked);
            Assert.Contains(string.Empty, fakeSnippetManager.SetPinLog);
        }

        [Fact]
        public void UnlockApp_SetsIsLockedToFalse_AndUpdatesActivity()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, fakeAutoLockService, _) = CreateViewModel();
            viewModel.LockApp(); // Set it to true initially

            // Act
            bool result = viewModel.UnlockApp();

            // Assert
            Assert.True(result);
            Assert.False(viewModel.IsLocked);
            Assert.True(fakeAutoLockService.UpdateLastActivityCalled);
        }

        [Fact]
        public void ToggleCommands_ChangeCorrespondingProperties()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();

            // Assert Initial state
            Assert.False(viewModel.IsAlwaysOnTop);
            Assert.False(viewModel.MinimizeToTray);
            Assert.False(viewModel.IsDarkMode);
            Assert.False(viewModel.IsMiniMode);

            // Act & Assert
            viewModel.ToggleAlwaysOnTopCommand.Execute(null);
            Assert.True(viewModel.IsAlwaysOnTop);

            viewModel.ToggleMinimizeToTrayCommand.Execute(null);
            Assert.True(viewModel.MinimizeToTray);

            viewModel.ToggleDarkModeCommand.Execute(null);
            Assert.True(viewModel.IsDarkMode);

            viewModel.ToggleMiniModeCommand.Execute(null);
            Assert.True(viewModel.IsMiniMode);
        }

        [Fact]
        public async Task DeleteSnippetCommand_RemovesSnippetAndUnregistersHotkey()
        {
            // Arrange
            var (viewModel, fakeSnippetManager, fakeHotkeyManager, _, _, _, _, _) = CreateViewModel();
            var snippet = new Snippet { Id = Guid.NewGuid(), Name = "Test" };
            fakeSnippetManager.AddSnippet(snippet);

            // Act
            await viewModel.DeleteSnippetCommand.ExecuteAsync(snippet);

            // Assert
            Assert.Contains(snippet.Id, fakeHotkeyManager.UnregisteredSnippetIds);
            Assert.Empty(fakeSnippetManager.Snippets);
            Assert.True(fakeSnippetManager.SaveSnippetsCalled);
        }

        [Fact]
        public void AddSnippetCommand_InvokesRequestSnippetEditorEvent()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();
            bool eventRaised = false;
            viewModel.RequestSnippetEditor += (snippet) =>
            {
                eventRaised = true;
                Assert.NotNull(snippet);
            };

            // Act
            viewModel.AddSnippetCommand.Execute(null);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void EditSnippetCommand_InvokesRequestSnippetEditorEvent()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();
            var snippetToEdit = new Snippet { Id = Guid.NewGuid(), Name = "Test" };
            bool eventRaised = false;
            Snippet raisedSnippet = null;
            viewModel.RequestSnippetEditor += (snippet) =>
            {
                eventRaised = true;
                raisedSnippet = snippet;
            };

            // Act
            viewModel.EditSnippetCommand.Execute(snippetToEdit);

            // Assert
            Assert.True(eventRaised);
            Assert.Same(snippetToEdit, raisedSnippet);
        }

        [Fact]
        public void SetPinCommand_InvokesRequestPinSetEvent()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();
            bool eventRaised = false;
            viewModel.RequestPinSet += () => eventRaised = true;

            // Act
            viewModel.SetPinCommand.Execute(null);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void SetAutoLockCommand_SetsAutoLockMinutes()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();

            // Act
            viewModel.SetAutoLockCommand.Execute("15");

            // Assert
            Assert.Equal(15, viewModel.AutoLockMinutes);
        }

        [Fact]
        public void ShowHelpCommand_InvokesRequestShowHelpEvent()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();
            bool eventRaised = false;
            viewModel.RequestShowHelp += () => eventRaised = true;

            // Act
            viewModel.ShowHelpCommand.Execute(null);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public void RestoreFromTrayCommand_WhenLocked_InvokesRequestUnlock()
        {
            // Arrange
            var (viewModel, _, _, _, _, _, _, _) = CreateViewModel();
            viewModel.LockApp(); // IsLocked = true
            bool eventRaised = false;
            viewModel.RequestUnlock += () => eventRaised = true;

            // Act
            viewModel.RestoreFromTrayCommand.Execute(null);

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task SearchText_Change_UpdatesFilteredSnippets()
        {
            // Arrange
            var (viewModel, fakeSnippetManager, _, _, _, _, _, _) = CreateViewModel();
            fakeSnippetManager.AddSnippet(new Snippet { Name = "Apple", Content = "Red" });
            fakeSnippetManager.AddSnippet(new Snippet { Name = "Banana", Content = "Yellow" });
            fakeSnippetManager.AddSnippet(new Snippet { Name = "Cherry", Content = "Red" });

            // To trigger initialization of FilteredSnippets as usually done when loading
            viewModel.SearchText = string.Empty;

            // Act
            viewModel.SearchText = "Banana";

            // Allow task continuation for debounce (300ms)
            await Task.Delay(400);

            // Assert
            Assert.Equal("Banana", viewModel.SearchText);
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("Banana", viewModel.FilteredSnippets[0].Name);
        }
    }
}
