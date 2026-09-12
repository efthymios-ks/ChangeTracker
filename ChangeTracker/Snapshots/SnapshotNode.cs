namespace ChangeTracker.Snapshots;

/// <summary>A captured value tree. Detached from the object it came from, so later edits cannot reach it.</summary>
internal abstract record SnapshotNode;

/// <summary>A leaf: anything compared by value rather than walked into.</summary>
internal sealed record ValueNode(object? Value) : SnapshotNode;

internal sealed record ObjectNode(IReadOnlyDictionary<string, SnapshotNode> Properties) : SnapshotNode;

/// <summary>
/// <paramref name="Keys"/> holds each item's key from a matcher registered through
/// <see cref="ChangeTracker.MatchBy(Type, Func{object, string})"/>, or null when none gives it one
/// and it can only be matched by position.
/// </summary>
internal sealed record CollectionNode(
    IReadOnlyList<SnapshotNode> Items,
    IReadOnlyList<string?> Keys
) : SnapshotNode
{
    /// <summary>Any one key is enough; the unkeyed items then keep a slot by position.</summary>
    public bool IsKeyed
        => Keys.Any(key => key is not null);
}

/// <summary>
/// Stands in for an object already captured higher up the same branch. Recursing into it would
/// never terminate, and a cycle is not a change.
/// </summary>
internal sealed record CycleNode : SnapshotNode
{
    public static readonly CycleNode Instance = new();
}
