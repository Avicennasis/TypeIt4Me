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
        public void SearchText_FiltersSnippetsByNameAndCategory()
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

            // Assert
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Alpha");
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Gamma");
            Assert.DoesNotContain(viewModel.FilteredSnippets, s => s.Name == "Beta");

            // Act - filter by name "beta" (case-insensitive)
            viewModel.SearchText = "beta";

            // Assert
            Assert.Contains(viewModel.FilteredSnippets, s => s.Name == "Beta");
            Assert.DoesNotContain(viewModel.FilteredSnippets, s => s.Name == "Alpha");

            // Act - filter longer than any string
            viewModel.SearchText = "NonExistentLongSearchTerm";

            // Assert
            Assert.Empty(viewModel.FilteredSnippets);
        }
    }
}
