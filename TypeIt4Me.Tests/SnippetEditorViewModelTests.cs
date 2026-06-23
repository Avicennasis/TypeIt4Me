using System.Windows.Input;
using TypeIt4Me.Models;
using TypeIt4Me.ViewModels;
using Xunit;

namespace TypeIt4Me.Tests;

public class SnippetEditorViewModelTests
{
    [Fact]
    public void Constructor_NullSnippet_InitializesNewSnippet()
    {
        // Arrange & Act
        var vm = new SnippetEditorViewModel(null);

        // Assert
        Assert.NotNull(vm.CurrentSnippet);
        Assert.Equal("New Snippet", vm.Name);
        Assert.Equal(string.Empty, vm.CurrentSnippet.Name);
    }

    [Fact]
    public void Constructor_WithSnippet_CopiesProperties()
    {
        // Arrange
        var snippet = new Snippet
        {
            Name = "Test Snippet",
            Content = "Test Content",
            Category = "Test Category",
            TriggerKey = Key.A,
            TriggerModifiers = ModifierKeys.Control | ModifierKeys.Shift
        };

        // Act
        var vm = new SnippetEditorViewModel(snippet);

        // Assert
        Assert.Same(snippet, vm.CurrentSnippet);
        Assert.Equal("Test Snippet", vm.Name);
        Assert.Equal("Test Content", vm.Content);
        Assert.Equal("Test Category", vm.Category);
        Assert.Equal(Key.A, vm.TriggerKey);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, vm.TriggerModifiers);
    }

    [Fact]
    public void SaveCommand_UpdatesSnippetAndRequestsClose()
    {
        // Arrange
        var snippet = new Snippet();
        var vm = new SnippetEditorViewModel(snippet)
        {
            Name = "Updated Name",
            Content = "Updated Content",
            Category = "Updated Category",
            TriggerKey = Key.B,
            TriggerModifiers = ModifierKeys.Alt
        };

        bool? closeResult = null;
        vm.RequestClose = result => closeResult = result;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.Equal("Updated Name", snippet.Name);
        Assert.Equal("Updated Content", snippet.Content);
        Assert.Equal("Updated Category", snippet.Category);
        Assert.Equal(Key.B, snippet.TriggerKey);
        Assert.Equal(ModifierKeys.Alt, snippet.TriggerModifiers);

        Assert.NotNull(closeResult);
        Assert.True(closeResult.Value);
    }

    [Fact]
    public void CancelCommand_RequestsCloseWithFalseAndDoesNotUpdateSnippet()
    {
        // Arrange
        var snippet = new Snippet
        {
            Name = "Original Name",
            Content = "Original Content"
        };

        var vm = new SnippetEditorViewModel(snippet)
        {
            Name = "Changed Name",
            Content = "Changed Content"
        };

        bool? closeResult = null;
        vm.RequestClose = result => closeResult = result;

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        Assert.Equal("Original Name", snippet.Name);
        Assert.Equal("Original Content", snippet.Content);
        Assert.NotNull(closeResult);
        Assert.False(closeResult.Value);
    }

    [Fact]
    public void ClearHotkeyCommand_ResetsTriggerKeys()
    {
        // Arrange
        var vm = new SnippetEditorViewModel(new Snippet())
        {
            TriggerKey = Key.C,
            TriggerModifiers = ModifierKeys.Control
        };

        // Act
        vm.ClearHotkeyCommand.Execute(null);

        // Assert
        Assert.Equal(Key.None, vm.TriggerKey);
        Assert.Equal(ModifierKeys.None, vm.TriggerModifiers);
    }

    [Theory]
    [InlineData("Name", "Content", true)]
    [InlineData("", "Content", false)]
    [InlineData(" ", "Content", false)]
    [InlineData(null, "Content", false)]
    [InlineData("Name", "", false)]
    [InlineData("Name", null, false)]
    public void SaveCommand_CanExecute_ValidatesNameAndContent(string? name, string? content, bool expectedCanExecute)
    {
        // Arrange
        var vm = new SnippetEditorViewModel(new Snippet())
        {
            Name = name!,
            Content = content!
        };

        // Act
        var canExecute = vm.SaveCommand.CanExecute(null);

        // Assert
        Assert.Equal(expectedCanExecute, canExecute);
    }

    [Fact]
    public void UpdateHotkey_UpdatesProperties()
    {
        // Arrange
        var vm = new SnippetEditorViewModel(new Snippet());

        // Act
        vm.UpdateHotkey(Key.D, ModifierKeys.Control | ModifierKeys.Alt);

        // Assert
        Assert.Equal(Key.D, vm.TriggerKey);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Alt, vm.TriggerModifiers);
    }
}
