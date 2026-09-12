namespace ChangeTracker.Snapshots;

/// <summary>Walks two snapshots side by side and reports every leaf that differs.</summary>
internal static class SnapshotComparer
{
    public static IReadOnlyList<Change> Compare(SnapshotNode original, SnapshotNode current)
    {
        var changes = new List<Change>();
        Compare(path: string.Empty, original, current, changes);

        return changes;
    }

    private static void Compare(string path, SnapshotNode original, SnapshotNode current, List<Change> changes)
    {
        switch (original, current)
        {
            case (CycleNode, _) or (_, CycleNode):
                return;

            case (ObjectNode originalObject, ObjectNode currentObject):
                CompareObjects(path, originalObject, currentObject, changes);
                return;

            case (CollectionNode originalCollection, CollectionNode currentCollection):
                CompareCollections(path, originalCollection, currentCollection, changes);
                return;

            case (ValueNode originalValue, ValueNode currentValue):
                CompareValues(path, originalValue.Value, currentValue.Value, changes);
                return;

            // The shapes disagree, so the whole node was replaced by something of another kind.
            default:
                changes.Add(new Change(path, ChangeKind.Modified, Describe(original), Describe(current)));
                return;
        }
    }

    private static void CompareObjects(string path, ObjectNode original, ObjectNode current, List<Change> changes)
    {
        foreach (var (name, originalValue) in original.Properties)
        {
            var childPath = Join(path, name);

            if (current.Properties.TryGetValue(name, out var currentValue))
            {
                Compare(childPath, originalValue, currentValue, changes);
            }
            else
            {
                changes.Add(new Change(childPath, ChangeKind.Removed, Describe(originalValue), null));
            }
        }

        foreach (var (name, currentValue) in current.Properties)
        {
            if (!original.Properties.ContainsKey(name))
            {
                changes.Add(new Change(Join(path, name), ChangeKind.Added, null, Describe(currentValue)));
            }
        }
    }

    private static void CompareCollections(
        string path,
        CollectionNode original,
        CollectionNode current,
        List<Change> changes
    )
    {
        // A key repeated on either side cannot address a single item, so the collection is compared
        // by position.
        if ((original.IsKeyed || current.IsKeyed)
            && ByKey(original) is { } originalByKey
            && ByKey(current) is { } currentByKey)
        {
            CompareKeyedCollections(path, originalByKey, currentByKey, changes);

            return;
        }

        var shared = Math.Min(original.Items.Count, current.Items.Count);

        for (var index = 0; index < shared; index++)
        {
            Compare($"{path}[{index}]", original.Items[index], current.Items[index], changes);
        }

        for (var index = shared; index < original.Items.Count; index++)
        {
            changes.Add(new Change($"{path}[{index}]", ChangeKind.Removed, Describe(original.Items[index]), null));
        }

        for (var index = shared; index < current.Items.Count; index++)
        {
            changes.Add(new Change($"{path}[{index}]", ChangeKind.Added, null, Describe(current.Items[index])));
        }
    }

    /// <summary>
    /// Items given a key by <see cref="ChangeTracker.MatchBy(Type, Func{object, string})"/> are matched
    /// by that key, so reordering or inserting reports only what genuinely changed rather than every
    /// position after it.
    /// </summary>
    private static void CompareKeyedCollections(
        string path,
        Dictionary<string, SnapshotNode> originalByKey,
        Dictionary<string, SnapshotNode> currentByKey,
        List<Change> changes
    )
    {
        foreach (var (key, originalItem) in originalByKey)
        {
            var childPath = $"{path}[{key}]";

            if (currentByKey.TryGetValue(key, out var currentItem))
            {
                Compare(childPath, originalItem, currentItem, changes);
            }
            else
            {
                changes.Add(new Change(childPath, ChangeKind.Removed, Describe(originalItem), null));
            }
        }

        foreach (var (key, currentItem) in currentByKey)
        {
            if (!originalByKey.ContainsKey(key))
            {
                changes.Add(new Change($"{path}[{key}]", ChangeKind.Added, null, Describe(currentItem)));
            }
        }
    }

    private static void CompareValues(string path, object? original, object? current, List<Change> changes)
    {
        if (Equals(original, current))
        {
            return;
        }

        var kind = (original, current) switch
        {
            (null, not null) => ChangeKind.Added,
            (not null, null) => ChangeKind.Removed,
            _ => ChangeKind.Modified
        };

        changes.Add(new Change(path, kind, original, current));
    }

    /// <summary>Each item under its key, or null when a key repeats and the items cannot be told apart.</summary>
    private static Dictionary<string, SnapshotNode>? ByKey(CollectionNode collection)
    {
        var byKey = new Dictionary<string, SnapshotNode>(StringComparer.Ordinal);

        for (var index = 0; index < collection.Items.Count; index++)
        {
            // An unkeyed item needs a slot of its own. The '#' keeps it clear of a real key such as
            // "1", which is exactly what an integer id produces.
            var key = collection.Keys[index] ?? $"#{index}";

            if (!byKey.TryAdd(key, collection.Items[index]))
            {
                return null;
            }
        }

        return byKey;
    }

    /// <summary>A whole added or removed branch is reported by its value, not expanded leaf by leaf.</summary>
    private static object? Describe(SnapshotNode node)
        => node switch
        {
            ValueNode value => value.Value,
            CollectionNode collection => $"[{collection.Items.Count} items]",
            ObjectNode => "{object}",
            _ => null
        };

    private static string Join(string path, string name)
        => path.Length == 0 ? name : $"{path}.{name}";
}
