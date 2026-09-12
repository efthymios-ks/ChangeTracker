using ChangeTracker.Tests.Shared;
using Xunit;

namespace ChangeTracker.Tests;

public class ChangeTrackerTests
{
    private readonly ChangeTracker _tracker = new();

    [Fact]
    public void Track_WhenTheTargetIsNull_ShouldThrowArgumentNull()
        => Assert.Throws<ArgumentNullException>(() => _tracker.Track<Order>(null!));

    [Fact]
    public void GetChanges_WhenTheTargetIsNull_ShouldThrowArgumentNull()
        => Assert.Throws<ArgumentNullException>(() => _tracker.GetChanges<Order>(null!));

    [Fact]
    public void IsTracking_WhenTheTargetIsNull_ShouldThrowArgumentNull()
        => Assert.Throws<ArgumentNullException>(() => _tracker.IsTracking<Order>(null!));

    [Fact]
    public void IsTracking_WhenTheObjectWasNeverTracked_ShouldBeFalse()
        => Assert.False(_tracker.IsTracking(new Order()));

    [Fact]
    public void IsTracking_WhenTheObjectIsTracked_ShouldBeTrue()
    {
        // Arrange & Act
        var order = new Order();
        _tracker.Track(order);

        // Assert
        Assert.True(_tracker.IsTracking(order));
    }

    [Fact]
    public void GetChanges_WhenTheObjectWasNeverTracked_ShouldReturnNothing()
        => Assert.Empty(_tracker.GetChanges(new Order { Number = 1 }));

    [Fact]
    public void GetChanges_WhenNothingChanged_ShouldReturnNothing()
    {
        // Arrange & Act
        var order = new Order { Number = 1, Total = 10m };
        _tracker.Track(order);

        // Assert
        Assert.Empty(_tracker.GetChanges(order));
    }

    [Fact]
    public void GetChanges_WhenAValueChanged_ShouldReportTheOldAndNewValue()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 25m;
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Total", change.Path);
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Equal(10m, change.OldValue);
        Assert.Equal(25m, change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenSeveralValuesChanged_ShouldReportOneChangeEach()
    {
        // Arrange
        var order = new Order { Number = 1, Total = 10m };
        _tracker.Track(order);

        // Act
        order.Number = 2;
        order.Total = 20m;

        // Assert
        Assert.Equal(2, _tracker.GetChanges(order).Count);
    }

    [Fact]
    public void GetChanges_WhenTheSnapshotIsTakenAgain_ShouldMeasureFromTheNewOne()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 20m;
        _tracker.Track(order);

        // Assert
        Assert.Empty(_tracker.GetChanges(order));
    }

    [Fact]
    public void GetChanges_WhenCalledTwice_ShouldStillMeasureFromTheOriginalSnapshot()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 20m;
        _tracker.GetChanges(order);

        // Assert
        Assert.Single(_tracker.GetChanges(order));
    }

    [Fact]
    public void GetChanges_WhenTheSnapshotWasTaken_ShouldNotSeeLaterEditsInTheOldValue()
    {
        // Arrange
        var order = new Order { Customer = new Customer { Name = "Ann" } };
        _tracker.Track(order);

        // Act
        order.Customer!.Name = "Bob";
        var change = Assert.Single(_tracker.GetChanges(order));

        // Assert
        Assert.Equal("Ann", change.OldValue);
    }

    [Fact]
    public void AcceptChanges_WhenChangesExist_ShouldReportThemThenStartFromHere()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 20m;

        // Assert
        Assert.Single(_tracker.AcceptChanges(order));
        Assert.Empty(_tracker.GetChanges(order));
        Assert.True(_tracker.IsTracking(order));
    }

    [Fact]
    public void AcceptChanges_WhenTheObjectWasNeverTracked_ShouldNotStartTrackingIt()
    {
        // Arrange & Act
        var order = new Order();

        // Assert
        Assert.Empty(_tracker.AcceptChanges(order));
        Assert.False(_tracker.IsTracking(order));
    }

    [Fact]
    public void StopTracking_WhenChangesExist_ShouldReportThemThenForgetTheObject()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 20m;

        // Assert
        Assert.Single(_tracker.StopTracking(order));
        Assert.False(_tracker.IsTracking(order));
        Assert.Empty(_tracker.GetChanges(order));
    }

    [Fact]
    public void StopTracking_WhenTheObjectWasNeverTracked_ShouldReturnNothing()
        => Assert.Empty(_tracker.StopTracking(new Order()));

    [Fact]
    public void HasChanges_WhenNothingChanged_ShouldBeFalse()
    {
        // Arrange & Act
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Assert
        Assert.False(_tracker.HasChanges(order));
    }

    [Fact]
    public void HasChanges_WhenSomethingChanged_ShouldBeTrue()
    {
        // Arrange
        var order = new Order { Total = 10m };
        _tracker.Track(order);

        // Act
        order.Total = 20m;

        // Assert
        Assert.True(_tracker.HasChanges(order));
    }

    [Fact]
    public void Track_WhenTwoObjectsAreEqualButDistinct_ShouldTrackThemSeparately()
    {
        // Arrange
        var first = new Order { Total = 10m };
        var second = new Order { Total = 10m };

        // Act
        _tracker.Track(first);

        // Assert
        Assert.True(_tracker.IsTracking(first));
        Assert.False(_tracker.IsTracking(second));
    }

    [Fact]
    public void Compare_WhenGivenTwoObjects_ShouldReportTheDifferenceWithoutTracking()
    {
        // Arrange & Act
        var original = new Order { Total = 10m };
        var current = new Order { Total = 20m };

        // Assert
        var change = Assert.Single(_tracker.Compare(original, current));

        Assert.Equal("Total", change.Path);
        Assert.False(_tracker.IsTracking(original));
    }

    [Fact]
    public void Compare_WhenBothAreNull_ShouldReportNothing()
        => Assert.Empty(_tracker.Compare<Order>(null!, null!));

    [Fact]
    public void Compare_WhenOneSideIsNull_ShouldReportTheWholeObject()
    {
        // Act & Assert
        var change = Assert.Single(_tracker.Compare(null!, new Order()));

        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Null(change.OldValue);
    }
}
