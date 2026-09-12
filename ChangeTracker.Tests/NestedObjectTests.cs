using ChangeTracker.Tests.Shared;
using Xunit;

namespace ChangeTracker.Tests;

public class NestedObjectTests
{
    private readonly ChangeTracker _tracker = new();

    [Fact]
    public void GetChanges_WhenANestedValueChanged_ShouldReportADottedPath()
    {
        // Arrange
        var order = new Order { Customer = new Customer { Address = new Address { City = "Athens" } } };
        _tracker.Track(order);

        // Act
        order.Customer!.Address!.City = "Patras";
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Customer.Address.City", change.Path);
        Assert.Equal("Athens", change.OldValue);
        Assert.Equal("Patras", change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenANestedObjectIsReplaced_ShouldReportEachChangedLeaf()
    {
        // Arrange
        var order = new Order { Customer = new Customer { Name = "Ann", Address = new Address { City = "Athens" } } };
        _tracker.Track(order);

        // Act
        order.Customer = new Customer { Name = "Bob", Address = new Address { City = "Athens" } };
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Customer.Name", change.Path);
    }

    [Fact]
    public void GetChanges_WhenANestedObjectBecomesNull_ShouldReportItRemoved()
    {
        // Arrange
        var order = new Order { Customer = new Customer { Name = "Ann" } };
        _tracker.Track(order);

        // Act
        order.Customer = null;
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Customer", change.Path);
        Assert.Equal(ChangeKind.Modified, change.Kind);
    }

    [Fact]
    public void GetChanges_WhenANestedObjectIsCreated_ShouldReportItAdded()
    {
        // Arrange
        var order = new Order();
        _tracker.Track(order);

        // Act
        order.Customer = new Customer { Name = "Ann" };
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Customer", change.Path);
    }

    [Fact]
    public void GetChanges_WhenANullableValueIsSet_ShouldReportItAdded()
    {
        // Arrange
        var order = new Order();
        _tracker.Track(order);

        // Act
        order.Note = "rush";
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Note", change.Path);
        Assert.Equal(ChangeKind.Added, change.Kind);
        Assert.Null(change.OldValue);
    }

    [Fact]
    public void GetChanges_WhenANullableValueIsCleared_ShouldReportItRemoved()
    {
        // Arrange
        var order = new Order { Note = "rush" };
        _tracker.Track(order);

        // Act
        order.Note = null;
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal(ChangeKind.Removed, change.Kind);
        Assert.Equal("rush", change.OldValue);
    }

    [Fact]
    public void GetChanges_WhenAnEnumChanged_ShouldReportTheEnumValues()
    {
        // Arrange
        var order = new Order { Status = OrderStatus.Draft };
        _tracker.Track(order);

        // Act
        order.Status = OrderStatus.Shipped;
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal(OrderStatus.Draft, change.OldValue);
        Assert.Equal(OrderStatus.Shipped, change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenTheGraphReferencesItself_ShouldNotRecurseForever()
    {
        // Arrange
        var node = new SelfReferencing { Name = "root" };
        node.Next = node;

        // Act
        _tracker.Track(node);
        node.Name = "renamed";

        // Assert
        var change = Assert.Single(_tracker.GetChanges(node));

        Assert.Equal("Name", change.Path);
    }

    [Fact]
    public void GetChanges_WhenTheSameObjectAppearsTwiceSideBySide_ShouldStillCompareBoth()
    {
        // Arrange
        var shared = new Customer { Name = "Ann" };
        var pair = new { First = shared, Second = shared };

        // Act
        var changes = _tracker.Compare(pair, new { First = shared, Second = new Customer { Name = "Bob" } });
        var change = Assert.Single(changes);

        // Assert
        Assert.Equal("Second.Name", change.Path);
    }

    [Fact]
    public void GetChanges_WhenAPropertyThrowsOnRead_ShouldIgnoreItRatherThanFail()
    {
        // Arrange
        var target = new Throwing { Safe = "before" };
        _tracker.Track(target);

        // Act
        target.Safe = "after";
        var change = Assert.Single(_tracker.GetChanges(target));

        // Assert
        Assert.Equal("Safe", change.Path);
    }
}
