using System.Windows.Input;
using TypeIt4Me.Models;
using TypeIt4Me.ViewModels;
using Xunit;

namespace TypeIt4Me.Tests;

public class SnippetEditorViewModelTests
{
    [Fact]
    public void Constructor_WithNull_InitializesNewSnippet()
    {
        // Arrange & Act
        var viewModel = new SnippetEditorViewModel(null);

        // Assert
        Assert.NotNull(viewModel.CurrentSnippet);
        Assert.Equal("New Snippet", viewModel.Name);
    }

    [Fact]
    public void Constructor_WithSnippet_InitializesProperties()
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
        Assert.Equal(snippet, viewModel.CurrentSnippet);
        Assert.Equal("Test Snippet", viewModel.Name);
        Assert.Equal("Test Content", viewModel.Content);
        Assert.Equal("Test Category", viewModel.Category);
        Assert.Equal(Key.A, viewModel.TriggerKey);
        Assert.Equal(ModifierKeys.Control, viewModel.TriggerModifiers);
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("Name", "", false)]
    [InlineData("", "Content", false)]
    [InlineData(" ", "Content", false)]
    [InlineData("Name", "Content", true)]
    public void SaveCommand_CanExecute_RequiresNameAndContent(string name, string content, bool expectedCanExecute)
    {
        // Arrange
        var viewModel = new SnippetEditorViewModel();
        viewModel.Name = name;
        viewModel.Content = content;

        // Act
        var canExecute = viewModel.SaveCommand.CanExecute(null);

        // Assert
        Assert.Equal(expectedCanExecute, canExecute);
    }

    [Fact]
    public void SaveCommand_Execute_UpdatesSnippetAndRequestsCloseTrue()
    {
        // Arrange
        var snippet = new Snippet();
        var viewModel = new SnippetEditorViewModel(snippet);
        viewModel.Name = "New Name";
        viewModel.Content = "New Content";
        viewModel.Category = "New Category";
        viewModel.TriggerKey = Key.B;
        viewModel.TriggerModifiers = ModifierKeys.Alt;

        bool? requestCloseResult = null;
        viewModel.RequestClose = (result) => requestCloseResult = result;

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        Assert.Equal("New Name", snippet.Name);
        Assert.Equal("New Content", snippet.Content);
        Assert.Equal("New Category", snippet.Category);
        Assert.Equal(Key.B, snippet.TriggerKey);
        Assert.Equal(ModifierKeys.Alt, snippet.TriggerModifiers);

        Assert.True(requestCloseResult);
    }

    [Fact]
    public void CancelCommand_Execute_RequestsCloseFalse()
    {
        // Arrange
        var viewModel = new SnippetEditorViewModel();
        bool? requestCloseResult = null;
        viewModel.RequestClose = (result) => requestCloseResult = result;

        // Act
        viewModel.CancelCommand.Execute(null);

        // Assert
        Assert.False(requestCloseResult);
    }

    [Fact]
    public void ClearHotkeyCommand_Execute_ResetsHotkey()
    {
        // Arrange
        var viewModel = new SnippetEditorViewModel();
        viewModel.TriggerKey = Key.C;
        viewModel.TriggerModifiers = ModifierKeys.Shift;

        // Act
        viewModel.ClearHotkeyCommand.Execute(null);

        // Assert
        Assert.Equal(Key.None, viewModel.TriggerKey);
        Assert.Equal(ModifierKeys.None, viewModel.TriggerModifiers);
    }

    [Fact]
    public void UpdateHotkey_UpdatesProperties()
    {
        // Arrange
        var viewModel = new SnippetEditorViewModel();

        // Act
        viewModel.UpdateHotkey(Key.D, ModifierKeys.Control | ModifierKeys.Shift);

        // Assert
        Assert.Equal(Key.D, viewModel.TriggerKey);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, viewModel.TriggerModifiers);
    }
}
