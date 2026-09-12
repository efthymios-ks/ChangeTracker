using ChangeTracker.Tests.Shared;
using Xunit;

namespace ChangeTracker.Tests;

/// <summary>The tracked object can itself be a collection, not only an object holding one.</summary>
public class RootCollectionTests
{
    private readonly ChangeTracker _tracker = new ChangeTracker().MatchBy<OrderLine>(line => line.Sku);

    [Fact]
    public void GetChanges_WhenAListIsTrackedDirectly_ShouldReportAnAppendedItem()
    {
        // Arrange
        var tags = new List<string> { "a" };
        _tracker.Track(tags);

        // Act
        tags.Add("b");
        var change = Assert.Single(_tracker.GetChanges(tags));

        // Assert
        Assert.Equal("[1]", change.Path);
        Assert.Equal(ChangeKind.Added, change.Kind);
        Assert.Equal("b", change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenAListIsTrackedDirectly_ShouldReportAReplacedItem()
    {
        // Arrange
        var tags = new List<string> { "a", "b" };
        _tracker.Track(tags);

        // Act
        tags[0] = "z";
        var change = Assert.Single(_tracker.GetChanges(tags));

        // Assert
        Assert.Equal("[0]", change.Path);
        Assert.Equal("a", change.OldValue);
        Assert.Equal("z", change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenAListOfObjectsIsTrackedDirectly_ShouldReachIntoTheItems()
    {
        // Arrange
        var lines = new List<OrderLine> { Line("A", 1) };
        _tracker.Track(lines);

        // Act
        lines[0].Quantity = 7;
        var change = Assert.Single(_tracker.GetChanges(lines));

        // Assert
        Assert.Equal("[A].Quantity", change.Path);
        Assert.Equal(1, change.OldValue);
        Assert.Equal(7, change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenAKeyedListIsTrackedDirectlyAndReordered_ShouldReportNothing()
    {
        // Arrange
        var lines = new List<OrderLine> { Line("A", 1), Line("B", 2) };
        _tracker.Track(lines);

        // Act
        lines.Reverse();

        // Assert
        Assert.Empty(_tracker.GetChanges(lines));
    }

    [Fact]
    public void GetChanges_WhenAnArrayIsTrackedDirectly_ShouldReportTheChangedSlot()
    {
        // Arrange
        var numbers = new[] { 1, 2, 3 };
        _tracker.Track(numbers);

        // Act
        numbers[2] = 9;
        var change = Assert.Single(_tracker.GetChanges(numbers));

        // Assert
        Assert.Equal("[2]", change.Path);
        Assert.Equal(9, change.NewValue);
    }

    [Fact]
    public void GetChanges_WhenADictionaryIsTrackedDirectly_ShouldReportTheChangedEntry()
    {
        // Arrange
        var settings = new Dictionary<string, string> { ["mode"] = "fast" };
        _tracker.Track(settings);

        // Act
        settings["mode"] = "slow";
        var changes = _tracker.GetChanges(settings);

        // Assert
        Assert.Contains(changes, change => Equals(change.OldValue, "fast") && Equals(change.NewValue, "slow"));
    }

    [Fact]
    public void GetChanges_WhenADictionaryGainsAnEntry_ShouldReportItAdded()
    {
        // Arrange
        var settings = new Dictionary<string, string> { ["mode"] = "fast" };
        _tracker.Track(settings);

        // Act
        settings["extra"] = "on";

        // Assert
        Assert.NotEmpty(_tracker.GetChanges(settings));
    }

    [Fact]
    public void GetChanges_WhenASetIsTrackedDirectly_ShouldReportAnAddedMember()
    {
        // Arrange
        var codes = new HashSet<string> { "a" };
        _tracker.Track(codes);

        // Act
        codes.Add("b");
        var change = Assert.Single(_tracker.GetChanges(codes));

        // Assert
        Assert.Equal(ChangeKind.Added, change.Kind);
    }

    [Fact]
    public void GetChanges_WhenANestedCollectionOfCollectionsChanges_ShouldReportTheInnerSlot()
    {
        // Arrange
        List<List<int>> rows = [[1, 2], [3]];
        _tracker.Track(rows);

        // Act
        rows[1].Add(4);
        var change = Assert.Single(_tracker.GetChanges(rows));

        // Assert
        Assert.Equal("[1][1]", change.Path);
        Assert.Equal(4, change.NewValue);
    }

    [Fact]
    public void StopTracking_WhenAListIsTrackedDirectly_ShouldForgetIt()
    {
        // Arrange
        var tags = new List<string> { "a" };
        _tracker.Track(tags);

        // Act
        tags.Add("b");

        // Assert
        Assert.Single(_tracker.StopTracking(tags));
        Assert.False(_tracker.IsTracking(tags));
    }

    private static OrderLine Line(string sku, int quantity)
        => new() { Sku = sku, Quantity = quantity };
}
