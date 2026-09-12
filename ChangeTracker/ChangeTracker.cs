using ChangeTracker.Snapshots;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace ChangeTracker;

/// <summary>
/// Remembers what an object looked like, then tells you what changed. Objects are held by reference
/// rather than by an id, so nothing has to implement an interface and nothing has to be unregistered
/// to be collected.
/// </summary>
public sealed class ChangeTracker
{
    private readonly ConditionalWeakTable<object, SnapshotNode> _snapshots = [];
    private readonly Dictionary<Type, Func<object, string?>> _matchers = [];

    /// <summary>The matcher resolved for each runtime type, including the types that have none.</summary>
    private readonly ConcurrentDictionary<Type, Func<object, string?>?> _matcherByType = new();

    private bool _tracking;

    /// <summary>
    /// Matches collection items of <typeparamref name="TItem"/> by what <paramref name="key"/> returns
    /// rather than by position. See <see cref="MatchBy(Type, Func{object, string})"/>.
    /// </summary>
    public ChangeTracker MatchBy<TItem>(Func<TItem, string?> key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return MatchBy(typeof(TItem), item => key((TItem)item));
    }

    /// <summary>
    /// Matches collection items of <paramref name="type"/> by what <paramref name="key"/> returns
    /// rather than by position. The type may be closed or an open generic definition such as
    /// <c>typeof(Entity&lt;&gt;)</c>, and a null key leaves that one item matched by position.
    /// </summary>
    /// <exception cref="InvalidOperationException">Something has already been tracked.</exception>
    public ChangeTracker MatchBy(Type type, Func<object, string?> key)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(key);

        // Keys are captured into the snapshot, so a late matcher would compare a snapshot keyed by
        // position against one keyed by id, and every item would read as removed and added.
        if (_tracking)
        {
            throw new InvalidOperationException("Register matchers before the first Track.");
        }

        _matchers[type] = key;

        // Compare may already have resolved a type without this matcher.
        _matcherByType.Clear();

        return this;
    }

    /// <summary>Takes a snapshot, replacing any earlier one for the same object.</summary>
    public void Track<TTarget>(TTarget target) where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(target);

        // A capture that throws leaves nothing tracked, so matchers can still be registered.
        var snapshot = Capture(target);

        _tracking = true;
        _snapshots.AddOrUpdate(target, snapshot);
    }

    /// <summary>
    /// The changes since the snapshot was taken. Empty when nothing changed, or when the object was
    /// never tracked — use <see cref="IsTracking"/> to tell those apart.
    /// </summary>
    public IReadOnlyList<Change> GetChanges<TTarget>(TTarget target) where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(target);

        return _snapshots.TryGetValue(target, out var snapshot)
            ? SnapshotComparer.Compare(snapshot, Capture(target))
            : [];
    }

    /// <summary>The changes since the snapshot, then a new snapshot so the next call starts from here.</summary>
    public IReadOnlyList<Change> AcceptChanges<TTarget>(TTarget target) where TTarget : class
    {
        var changes = GetChanges(target);

        if (_snapshots.TryGetValue(target, out _))
        {
            Track(target);
        }

        return changes;
    }

    /// <summary>The changes since the snapshot, then forgets the object.</summary>
    public IReadOnlyList<Change> StopTracking<TTarget>(TTarget target) where TTarget : class
    {
        var changes = GetChanges(target);
        _snapshots.Remove(target);

        return changes;
    }

    public bool IsTracking<TTarget>(TTarget target) where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(target);

        return _snapshots.TryGetValue(target, out _);
    }

    public bool HasChanges<TTarget>(TTarget target) where TTarget : class
        => GetChanges(target).Count > 0;

    /// <summary>
    /// Compares two objects directly, with nothing tracked and nothing remembered. The matchers apply,
    /// and since nothing is stored, <see cref="MatchBy(Type, Func{object, string})"/> may follow.
    /// </summary>
    public IReadOnlyList<Change> Compare<TTarget>(TTarget original, TTarget current)
        => SnapshotComparer.Compare(Capture(original), Capture(current));

    private SnapshotNode Capture(object? target)
        => SnapshotFactory.Capture(target, KeyOf);

    /// <summary>The item's key, or null when nothing matches it and it is matched by position.</summary>
    private string? KeyOf(object item)
        => _matcherByType
            .GetOrAdd(item.GetType(), static (type, tracker) => tracker.FindMatcher(type), this)
            ?.Invoke(item);

    /// <summary>
    /// The most specific matcher for a type: the type itself and each base class below
    /// <see cref="object"/>, then its interfaces, then a catch-all registered for <see cref="object"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two interfaces of the type have different matchers.</exception>
    private Func<object, string?>? FindMatcher(Type type)
    {
        for (var level = type; level is not null && level != typeof(object); level = level.BaseType)
        {
            if (Registered(level) is { } matcher)
            {
                return matcher;
            }
        }

        // Interfaces come in no defined order, so two of them with different matchers have no winner.
        Func<object, string?>? found = null;
        Type? foundContract = null;

        foreach (var contract in type.GetInterfaces())
        {
            if (Registered(contract) is not { } matcher || matcher == found)
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException(
                    $"{type.Name} matches both {foundContract?.Name} and {contract.Name}. "
                        + $"Register {type.Name} or one of its base classes to choose its key."
                );
            }

            found = matcher;
            foundContract = contract;
        }

        return found ?? _matchers.GetValueOrDefault(typeof(object));
    }

    /// <summary>A closed registration beats an open generic one at the same level.</summary>
    private Func<object, string?>? Registered(Type type)
        => _matchers.GetValueOrDefault(type)
            ?? (type.IsGenericType ? _matchers.GetValueOrDefault(type.GetGenericTypeDefinition()) : null);
}
