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
        public async System.Threading.Tasks.Task SearchText_NullOrWhiteSpace_ReturnsAllSnippets()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Alpha", Category = "CatA" });
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Beta", Category = "CatB" });

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger()
            );

            // Filter first to narrow list
            viewModel.SearchText = "Alpha";
            await System.Threading.Tasks.Task.Delay(500);
            Assert.Single(viewModel.FilteredSnippets);

            // Act - Set to empty string
            viewModel.SearchText = "";
            await System.Threading.Tasks.Task.Delay(500);
            Assert.Equal(2, viewModel.FilteredSnippets.Count);

            // Act - Set to whitespace
            viewModel.SearchText = "   ";
            await System.Threading.Tasks.Task.Delay(500);
            Assert.Equal(2, viewModel.FilteredSnippets.Count);

            // Act - Set to null
#pragma warning disable CS8625
            viewModel.SearchText = null;
#pragma warning restore CS8625
            await System.Threading.Tasks.Task.Delay(500);
            Assert.Equal(2, viewModel.FilteredSnippets.Count);
        }

        [Fact]
        public async System.Threading.Tasks.Task PerformFiltering_HandlesNullSnippetsInCollection()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Alpha", Category = "CatA" });
#pragma warning disable CS8625
            fakeSnippetManager.Snippets.Add(null);
#pragma warning restore CS8625
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Beta", Category = "CatB" });

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger()
            );

            // Act
            viewModel.SearchText = "Alpha";
            await System.Threading.Tasks.Task.Delay(500);

            // Assert
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("Alpha", viewModel.FilteredSnippets[0].Name);
        }

        [Fact]
        public async System.Threading.Tasks.Task PerformFiltering_HandlesNullOrEmptyNameAndCategory()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
#pragma warning disable CS8625
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = null, Category = "CatA" });
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Beta", Category = null });
#pragma warning restore CS8625
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "", Category = "" });

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger()
            );

            // Act - Search matching Category of item with null Name
            viewModel.SearchText = "CatA";
            await System.Threading.Tasks.Task.Delay(500);

            // Assert
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("CatA", viewModel.FilteredSnippets[0].Category);

            // Act - Search matching Name of item with null Category
            viewModel.SearchText = "Beta";
            await System.Threading.Tasks.Task.Delay(500);

            // Assert
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("Beta", viewModel.FilteredSnippets[0].Name);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchText_DebounceCancellation_OnlyAppliesLatestFilter()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Alpha", Category = "CatA" });
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Beta", Category = "CatB" });

            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger()
            );

            // Act - Trigger rapid updates within debounce window (300ms)
            viewModel.SearchText = "Alpha";
            await System.Threading.Tasks.Task.Delay(100); // Less than debounce duration
            viewModel.SearchText = "Beta";
            await System.Threading.Tasks.Task.Delay(500); // Allow second search to finish

            // Assert
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("Beta", viewModel.FilteredSnippets[0].Name);
        }

        [Fact]
        public void Snippets_CollectionChanged_RefreshesFilteredSnippets()
        {
            // Arrange
            var fakeSnippetManager = new FakeSnippetManager();
            var viewModel = new MainViewModel(
                fakeSnippetManager,
                new FakeHotkeyManager(),
                new FakeInputInjector(),
                new FakeFocusTracker(),
                new FakeSettingsManager(),
                new FakeAutoLockService(),
                new FakeThemeService(),
                new FakeLogger()
            );

            Assert.Empty(viewModel.FilteredSnippets);

            // Act - Add snippet directly to manager collection
            fakeSnippetManager.Snippets.Add(new Models.Snippet { Name = "Gamma", Category = "Greek" });

            // Assert - Synchronous CollectionChanged trigger should refresh FilteredSnippets
            Assert.Single(viewModel.FilteredSnippets);
            Assert.Equal("Gamma", viewModel.FilteredSnippets[0].Name);
        }
    }
}
