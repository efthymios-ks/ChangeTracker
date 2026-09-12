using ChangeTracker.Tests.Shared;
using Xunit;

namespace ChangeTracker.Tests;

public class CollectionTests
{
    private readonly ChangeTracker _tracker = new ChangeTracker().MatchBy<OrderLine>(line => line.Sku);

    [Fact]
    public void GetChanges_WhenAnItemIsAppended_ShouldReportItAdded()
    {
        // Arrange
        var order = new Order { Tags = ["a"] };
        _tracker.Track(order);

        // Act
        order.Tags.Add("b");
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Tags[1]", change.Path);
        Assert.Equal(ChangeKind.Added, change.Kind);
        Assert.Equal("b", change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenAnItemIsDropped_ShouldReportItRemoved()
    {
        // Arrange
        var order = new Order { Tags = ["a", "b"] };
        _tracker.Track(order);

        // Act
        order.Tags.RemoveAt(1);
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Tags[1]", change.Path);
        Assert.Equal(ChangeKind.Removed, change.Kind);
        Assert.Equal("b", change.OldValue);
    }

    [Fact]
    public void GetChanges_WhenAnItemIsReplaced_ShouldReportItModified()
    {
        // Arrange
        var order = new Order { Tags = ["a", "b"] };
        _tracker.Track(order);

        // Act
        order.Tags[1] = "c";
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Tags[1]", change.Path);
        Assert.Equal("b", change.OldValue);
        Assert.Equal("c", change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenTheCollectionIsUnchanged_ShouldReportNothing()
    {
        // Arrange & Act
        var order = new Order { Tags = ["a", "b"] };
        _tracker.Track(order);

        // Assert
        Assert.Empty(_tracker.GetChanges(order));
    }

    [Fact]
    public void GetChanges_WhenAnUnkeyedItemIsInsertedAtTheFront_ShouldReportEveryPositionAfterIt()
    {
        // Arrange
        var order = new Order { Tags = ["a", "b"] };
        _tracker.Track(order);

        order.Tags.Insert(0, "z");

        // Act
        // Without an identity there is nothing to match on, so a shift reads as a change per slot.
        Assert.Equal(3, _tracker.GetChanges(order).Count);
    }

    [Fact]
    public void GetChanges_WhenAKeyedItemIsInsertedAtTheFront_ShouldReportOnlyTheNewItem()
    {
        // Arrange
        var order = new Order { Lines = [Line("A", 1), Line("B", 2)] };
        _tracker.Track(order);

        // Act
        order.Lines.Insert(0, Line("C", 3));
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Lines[C]", change.Path);
        Assert.Equal(ChangeKind.Added, change.Kind);
    }

    [Fact]
    public void GetChanges_WhenAKeyedItemIsReordered_ShouldReportNothing()
    {
        // Arrange
        var order = new Order { Lines = [Line("A", 1), Line("B", 2)] };
        _tracker.Track(order);

        // Act
        order.Lines.Reverse();

        // Assert
        Assert.Empty(_tracker.GetChanges(order));
    }

    [Fact]
    public void GetChanges_WhenAKeyedItemIsEdited_ShouldReportThePropertyUnderItsKey()
    {
        // Arrange
        var order = new Order { Lines = [Line("A", 1), Line("B", 2)] };
        _tracker.Track(order);

        // Act
        order.Lines[1].Quantity = 9;
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Lines[B].Quantity", change.Path);
        Assert.Equal(2, change.OldValue);
        Assert.Equal(9, change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenAKeyedItemIsRemoved_ShouldReportOnlyThatItem()
    {
        // Arrange
        var order = new Order { Lines = [Line("A", 1), Line("B", 2)] };
        _tracker.Track(order);

        // Act
        order.Lines.RemoveAt(0);
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Lines[A]", change.Path);
        Assert.Equal(ChangeKind.Removed, change.Kind);
    }

    [Fact]
    public void GetChanges_WhenKeyedItemsAreAddedAndRemovedTogether_ShouldReportBoth()
    {
        // Arrange
        var order = new Order { Lines = [Line("A", 1)] };
        _tracker.Track(order);

        order.Lines.Clear();
        order.Lines.Add(Line("B", 2));

        // Act
        var changes = _tracker.GetChanges(order);

        // Assert
        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, change => change.Path == "Lines[A]" && change.Kind == ChangeKind.Removed);
        Assert.Contains(changes, change => change.Path == "Lines[B]" && change.Kind == ChangeKind.Added);
    }

    [Fact]
    public void GetChanges_WhenAnUnkeyedItemIsEdited_ShouldReportThePropertyUnderItsIndex()
    {
        // Arrange & Act
        var original = new { Lines = new List<PlainLine> { new() { Sku = "A", Quantity = 1 } } };
        var current = new { Lines = new List<PlainLine> { new() { Sku = "A", Quantity = 5 } } };

        // Assert
        var change = Assert.Single(_tracker.Compare(original, current));

        Assert.Equal("Lines[0].Quantity", change.Path);
    }

    [Fact]
    public void GetChanges_WhenTheCollectionIsEmptied_ShouldReportEveryItemRemoved()
    {
        // Arrange
        var order = new Order { Tags = ["a", "b"] };
        _tracker.Track(order);

        // Act
        order.Tags.Clear();

        // Assert
        Assert.Equal(2, _tracker.GetChanges(order).Count);
    }

    [Fact]
    public void GetChanges_WhenTheCollectionIsReplacedWithAnotherInstance_ShouldCompareTheContents()
    {
        // Arrange
        var order = new Order { Tags = ["a"] };
        _tracker.Track(order);

        // Act
        order.Tags = ["a"];

        // Assert
        Assert.Empty(_tracker.GetChanges(order));
    }

    private static OrderLine Line(string sku, int quantity)
        => new() { Sku = sku, Quantity = quantity };
}
