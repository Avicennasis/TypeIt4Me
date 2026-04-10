using System.ComponentModel;
using System.Windows.Input;
using TypeIt4Me.Models;
using Xunit;

namespace TypeIt4Me.Tests;

public class SnippetModelTests
{
    [Fact]
    public void NewSnippet_HasUniqueGuid()
    {
        var a = new Snippet();
        var b = new Snippet();
        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void NewSnippet_HasEmptyDefaults()
    {
        var s = new Snippet();
        Assert.Equal(string.Empty, s.Name);
        Assert.Equal(string.Empty, s.Content);
        Assert.Equal(string.Empty, s.Category);
        Assert.Equal(Key.None, s.TriggerKey);
        Assert.Equal(ModifierKeys.None, s.TriggerModifiers);
    }

    [Fact]
    public void SetName_RaisesPropertyChanged()
    {
        var s = new Snippet();
        var raised = false;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Snippet.Name)) raised = true;
        };
        s.Name = "test";
        Assert.True(raised);
    }

    [Fact]
    public void SetContent_RaisesPropertyChanged()
    {
        var s = new Snippet();
        var raised = false;
        ((INotifyPropertyChanged)s).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Snippet.Content)) raised = true;
        };
        s.Content = "hello world";
        Assert.True(raised);
    }
}
