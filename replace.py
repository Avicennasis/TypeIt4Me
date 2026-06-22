with open("TypeIt4Me.Tests/MainViewModelTests.cs", "r") as f:
    content = f.read()

old_code = """        [Fact]
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
        }"""

new_code = """        [Fact]
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
        }"""

if old_code not in content:
    # Try finding the original unmodified string and replacing it
    old_code_orig = """        [Fact]
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

            // Allow task continuation
            Task.Delay(100).Wait(); // The debounce uses Task.Delay, need to wait for it.

            // Assert
            // Depending on debounce implementation, we might need a longer wait or a different way to test.
            // Let's assume FilteredSnippets is eventually updated.
            Assert.Equal("Banana", viewModel.SearchText);
        }"""

    content = content.replace(old_code_orig, new_code)
else:
    content = content.replace(old_code, new_code)

with open("TypeIt4Me.Tests/MainViewModelTests.cs", "w") as f:
    f.write(content)
