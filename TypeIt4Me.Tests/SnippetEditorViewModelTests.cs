using System;
using System.Windows.Input;
using TypeIt4Me.Models;
using TypeIt4Me.ViewModels;
using Xunit;

namespace TypeIt4Me.Tests
{
    public class SnippetEditorViewModelTests
    {
        [Fact]
        public void Constructor_WithNull_InitializesNewSnippet()
        {
            // Arrange & Act
            var viewModel = new SnippetEditorViewModel();

            // Assert
            Assert.NotNull(viewModel.CurrentSnippet);
            Assert.Equal("New Snippet", viewModel.Name);
            Assert.Null(viewModel.Content);
            Assert.Null(viewModel.Category);
            Assert.Equal(Key.None, viewModel.TriggerKey);
            Assert.Equal(ModifierKeys.None, viewModel.TriggerModifiers);
        }

        [Fact]
        public void Constructor_WithSnippet_PopulatesProperties()
        {
            // Arrange
            var snippet = new Snippet
            {
                Name = "Test Snippet",
                Content = "Test Content",
                Category = "Test Category",
                TriggerKey = Key.A,
                TriggerModifiers = ModifierKeys.Control
            };

            // Act
            var viewModel = new SnippetEditorViewModel(snippet);

            // Assert
            Assert.Same(snippet, viewModel.CurrentSnippet);
            Assert.Equal("Test Snippet", viewModel.Name);
            Assert.Equal("Test Content", viewModel.Content);
            Assert.Equal("Test Category", viewModel.Category);
            Assert.Equal(Key.A, viewModel.TriggerKey);
            Assert.Equal(ModifierKeys.Control, viewModel.TriggerModifiers);
        }

        [Theory]
        [InlineData(null, "Content", false)]
        [InlineData("", "Content", false)]
        [InlineData("   ", "Content", false)]
        [InlineData("Name", null, false)]
        [InlineData("Name", "", false)]
        [InlineData("Name", "Content", true)]
        public void SaveCommand_CanExecute_DependsOnNameAndContent(string name, string content, bool expectedCanExecute)
        {
            // Arrange
            var viewModel = new SnippetEditorViewModel
            {
                Name = name,
                Content = content
            };

            // Act
            bool canExecute = viewModel.SaveCommand.CanExecute(null);

            // Assert
            Assert.Equal(expectedCanExecute, canExecute);
        }

        [Fact]
        public void SaveCommand_Execute_UpdatesCurrentSnippetAndRequestsCloseTrue()
        {
            // Arrange
            var viewModel = new SnippetEditorViewModel();
            viewModel.Name = "Updated Name";
            viewModel.Content = "Updated Content";
            viewModel.Category = "Updated Category";
            viewModel.TriggerKey = Key.B;
            viewModel.TriggerModifiers = ModifierKeys.Shift;

            bool? closeResult = null;
            viewModel.RequestClose = (result) => closeResult = result;

            // Act
            viewModel.SaveCommand.Execute(null);

            // Assert
            Assert.Equal("Updated Name", viewModel.CurrentSnippet.Name);
            Assert.Equal("Updated Content", viewModel.CurrentSnippet.Content);
            Assert.Equal("Updated Category", viewModel.CurrentSnippet.Category);
            Assert.Equal(Key.B, viewModel.CurrentSnippet.TriggerKey);
            Assert.Equal(ModifierKeys.Shift, viewModel.CurrentSnippet.TriggerModifiers);
            Assert.True(closeResult);
        }

        [Fact]
        public void CancelCommand_Execute_RequestsCloseFalse()
        {
            // Arrange
            var viewModel = new SnippetEditorViewModel();
            bool? closeResult = null;
            viewModel.RequestClose = (result) => closeResult = result;

            // Act
            viewModel.CancelCommand.Execute(null);

            // Assert
            Assert.False(closeResult);
        }

        [Fact]
        public void ClearHotkeyCommand_Execute_ClearsTriggerKeyAndModifiers()
        {
            // Arrange
            var viewModel = new SnippetEditorViewModel
            {
                TriggerKey = Key.A,
                TriggerModifiers = ModifierKeys.Control
            };

            // Act
            viewModel.ClearHotkeyCommand.Execute(null);

            // Assert
            Assert.Equal(Key.None, viewModel.TriggerKey);
            Assert.Equal(ModifierKeys.None, viewModel.TriggerModifiers);
        }

        [Fact]
        public void UpdateHotkey_UpdatesTriggerKeyAndModifiers()
        {
            // Arrange
            var viewModel = new SnippetEditorViewModel();

            // Act
            viewModel.UpdateHotkey(Key.Z, ModifierKeys.Alt);

            // Assert
            Assert.Equal(Key.Z, viewModel.TriggerKey);
            Assert.Equal(ModifierKeys.Alt, viewModel.TriggerModifiers);
        }
    }
}