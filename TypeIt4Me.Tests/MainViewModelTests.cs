using System;
using TypeIt4Me.ViewModels;
using TypeIt4Me.Tests.Fakes;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class MainViewModelTests
    {
        [Fact]
        public void LockApp_SetsIsLockedToTrue_AndClearsPin()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            var fakeHotkeyManager = new FakeHotkeyManager();
            var fakeInputInjector = new FakeInputInjector();
            var fakeFocusTracker = new FakeFocusTracker();
            var fakeSettingsManager = new FakeSettingsManager();
            var fakeAutoLockService = new FakeAutoLockService();
            var fakeThemeService = new FakeThemeService();
            var fakeLogger = new FakeLogger();

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                fakeHotkeyManager,
                fakeInputInjector,
                fakeFocusTracker,
                fakeSettingsManager,
                fakeAutoLockService,
                fakeThemeService,
                fakeLogger
            );

            // Ensure initial state
            Assert.False(viewModel.IsLocked);

            // Act
            viewModel.LockApp();

            // Assert
            Assert.True(viewModel.IsLocked);
            Assert.Contains(string.Empty, fakeSnippetManager.SetPinLog);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchText_FiltersSnippetsByNameAndCategory()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            var fakeHotkeyManager = new FakeHotkeyManager();
            var fakeInputInjector = new FakeInputInjector();
            var fakeFocusTracker = new FakeFocusTracker();
            var fakeSettingsManager = new FakeSettingsManager();
            var fakeAutoLockService = new FakeAutoLockService();
            var fakeThemeService = new FakeThemeService();
            var fakeLogger = new FakeLogger();

            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Alpha", Category = "Letters" });
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Beta", Category = "Greek" });
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Gamma", Category = "Letters" });

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                fakeHotkeyManager,
                fakeInputInjector,
                fakeFocusTracker,
                fakeSettingsManager,
                fakeAutoLockService,
                fakeThemeService,
                fakeLogger
            );

            // Act - filter by category "Letters"
            viewModel.SearchText = "Letters";
            await System.Threading.Tasks.Task.Delay(1000);

            // Assert
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Alpha");
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Gamma");
            Assert.DoesNotContain(viewModel.FilteredSnippets, s => s.Name == "Beta");

            // Act - filter by name "beta" (case-insensitive)
            viewModel.SearchText = "beta";
            await System.Threading.Tasks.Task.Delay(1000);

            // Assert
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Beta");
            Assert.DoesNotContain(viewModel.FilteredSnippets, s => s.Name == "Alpha");

            // Act - filter longer than any string
            viewModel.SearchText = "NonExistentLongSearchTerm";
            await System.Threading.Tasks.Task.Delay(1000);

            // Assert
            Assert.Empty(viewModel.FilteredSnippets);
        }

        [Fact]
        public void UnlockApp_SetsIsLockedToFalse_UpdatesLastActivity_AndReturnsTrue()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            var fakeHotkeyManager = new FakeHotkeyManager();
            var fakeInputInjector = new FakeInputInjector();
            var fakeFocusTracker = new FakeFocusTracker();
            var fakeSettingsManager = new FakeSettingsManager();
            var fakeAutoLockService = new FakeAutoLockService();
            var fakeThemeService = new FakeThemeService();
            var fakeLogger = new FakeLogger();

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                fakeHotkeyManager,
                fakeInputInjector,
                fakeFocusTracker,
                fakeSettingsManager,
                fakeAutoLockService,
                fakeThemeService,
                fakeLogger
            );

            viewModel.LockApp();
            Assert.True(viewModel.IsLocked);

            // Act
            bool result = viewModel.UnlockApp();

            // Assert
            Assert.True(result);
            Assert.False(viewModel.IsLocked);
            Assert.True(fakeAutoLockService.UpdateLastActivityCalled);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportSnippets_WhenDialogCancelled_DoesNotImport()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            var fakeDialogService = new FakeDialogService
            {
                OpenFileDialogResult = null
            };

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger(),
                fakeDialogService
            );

            // Act
            await viewModel.ImportSnippetsCommand.ExecuteAsync(null);

            // Assert
            Assert.Null(fakeSnippetManager.LastImportFilePath);
            Assert.Empty(fakeDialogService.InformationMessages);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportSnippets_WhenUnencryptedImportSucceeds_ShowsSuccessMessage()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager
            {
                ImportResult = true
            };
            var fakeDialogService = new FakeDialogService
            {
                OpenFileDialogResult = "snippets.json"
            };

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger(),
                fakeDialogService
            );

            // Act
            await viewModel.ImportSnippetsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("snippets.json", fakeSnippetManager.LastImportFilePath);
            Assert.Null(fakeSnippetManager.LastImportPin);
            Assert.Contains("Import Successful!", fakeDialogService.InformationMessages);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportSnippets_WhenImportFailsAndUserDeclinesPinPrompt_DoesNotPromptPin()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager
            {
                ImportResult = false
            };
            var fakeDialogService = new FakeDialogService
            {
                OpenFileDialogResult = "encrypted.json",
                ShowConfirmationResult = false
            };

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger(),
                fakeDialogService
            );

            bool pinRequested = false;
            viewModel.RequestPinInput += callback => pinRequested = true;

            // Act
            await viewModel.ImportSnippetsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("encrypted.json", fakeSnippetManager.LastImportFilePath);
            Assert.Null(fakeSnippetManager.LastImportPin);
            Assert.False(pinRequested);
            Assert.Contains("Failed to decrypt snippets. Do you want to try entering a PIN?", fakeDialogService.ConfirmationMessages);
            Assert.Empty(fakeDialogService.InformationMessages);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportSnippets_WhenImportFailsAndUserEntersValidPin_SuccessfullyImportsWithPin()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager
            {
                ImportHandler = (path, pin) => pin != null && new string(pin) == "1234"
            };
            var fakeDialogService = new FakeDialogService
            {
                OpenFileDialogResult = "encrypted.json",
                ShowConfirmationResult = true
            };

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger(),
                fakeDialogService
            );

            viewModel.RequestPinInput += callback => callback(new char[] { '1', '2', '3', '4' });

            // Act
            await viewModel.ImportSnippetsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("encrypted.json", fakeSnippetManager.LastImportFilePath);
            Assert.Equal("1234", fakeSnippetManager.LastImportPin);
            Assert.Contains("Import Successful!", fakeDialogService.InformationMessages);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportSnippets_WhenImportFailsAndUserEntersInvalidPinThenCancels_AttemptsImportWithPin()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager
            {
                ImportHandler = (path, pin) => false
            };
            var fakeDialogService = new FakeDialogService
            {
                OpenFileDialogResult = "encrypted.json",
                ShowConfirmationResult = true
            };

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger(),
                fakeDialogService
            );

            int pinPromptCount = 0;
            viewModel.RequestPinInput += callback =>
            {
                pinPromptCount++;
                if (pinPromptCount == 1)
                {
                    callback(new char[] { 'w', 'r', 'o', 'n', 'g' });
                }
                else
                {
                    callback(Array.Empty<char>());
                }
            };

            // Act
            await viewModel.ImportSnippetsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("encrypted.json", fakeSnippetManager.LastImportFilePath);
            Assert.Equal("wrong", fakeSnippetManager.LastImportPin);
            Assert.Empty(fakeDialogService.InformationMessages);
        }
    }
}
