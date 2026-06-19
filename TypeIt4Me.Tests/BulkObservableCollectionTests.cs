using System.Collections.Specialized;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class BulkObservableCollectionTests
{
    [Fact]
    public void ReplaceAll_ClearsAndAddsNewItems()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "initial1", "initial2" };
        var newItems = new List<string> { "new1", "new2", "new3" };

        // Act
        collection.ReplaceAll(newItems);

        // Assert
        Assert.Equal(3, collection.Count);
        Assert.Equal("new1", collection[0]);
        Assert.Equal("new2", collection[1]);
        Assert.Equal("new3", collection[2]);
    }

    [Fact]
    public void ReplaceAll_TriggersSingleResetNotification()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "initial" };
        var newItems = new List<string> { "new1", "new2" };
        int notificationCount = 0;
        NotifyCollectionChangedAction? lastAction = null;

        collection.CollectionChanged += (s, e) =>
        {
            notificationCount++;
            lastAction = e.Action;
        };

        // Act
        collection.ReplaceAll(newItems);

        // Assert
        Assert.Equal(1, notificationCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
    }

    [Fact]
    public void AddRange_AddsItemsToExistingCollection()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "initial1" };
        var rangeToAdd = new List<string> { "add1", "add2" };

        // Act
        collection.AddRange(rangeToAdd);

        // Assert
        Assert.Equal(3, collection.Count);
        Assert.Equal("initial1", collection[0]);
        Assert.Equal("add1", collection[1]);
        Assert.Equal("add2", collection[2]);
    }

    [Fact]
    public void AddRange_TriggersSingleResetNotification()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "initial" };
        var rangeToAdd = new List<string> { "add1", "add2" };
        int notificationCount = 0;
        NotifyCollectionChangedAction? lastAction = null;

        collection.CollectionChanged += (s, e) =>
        {
            notificationCount++;
            lastAction = e.Action;
        };

        // Act
        collection.AddRange(rangeToAdd);

        // Assert
        Assert.Equal(1, notificationCount);
        Assert.Equal(NotifyCollectionChangedAction.Reset, lastAction);
    }

    [Fact]
    public void ReplaceAll_WithEmptyList_ClearsCollection()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "item1" };

        // Act
        collection.ReplaceAll(new List<string>());

        // Assert
        Assert.Empty(collection);
    }

    [Fact]
    public void AddRange_WithEmptyList_DoesNotChangeCollection()
    {
        // Arrange
        var collection = new BulkObservableCollection<string> { "item1" };

        // Act
        collection.AddRange(new List<string>());

        // Assert
        Assert.Single(collection);
        Assert.Equal("item1", collection[0]);
    }
}
