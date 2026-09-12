# ChangeTracker

Snapshot an object, edit it, get a flat list of what changed. No base class, no interface, no
attributes.

```
ChangeTracker.cs      MatchBy / Track / GetChanges / AcceptChanges / StopTracking / Compare
Change.cs             Path, Kind, OldValue, NewValue
ChangeKind.cs         Added, Removed, Modified
Snapshots/            internal capture and comparison
```

## Track and compare

```csharp
var tracker = new ChangeTracker();
tracker.Track(order);

order.Total = 25m;
order.Customer.Address.City = "Patras";

foreach (var change in tracker.GetChanges(order))
{
    Console.WriteLine(change);
}

// Total: 10 -> 25
// Customer.Address.City: Athens -> Patras
```

Objects are held by reference, so nothing needs an id and nothing needs unregistering to be
collected. Snapshots are deep copies of the values — later edits cannot reach back into them.

| Method | Does |
| --- | --- |
| `Track` | takes a snapshot, replacing any earlier one |
| `GetChanges` | changes since the snapshot; the snapshot stays |
| `AcceptChanges` | changes since the snapshot, then re-snapshots |
| `StopTracking` | changes since the snapshot, then forgets the object |
| `IsTracking` | whether a snapshot exists |
| `HasChanges` | whether `GetChanges` would return anything |

`GetChanges` on an untracked object returns nothing rather than throwing — use `IsTracking` to tell
"unchanged" from "never tracked" apart.

## Compare two objects

No tracking, nothing remembered. The tracker's matchers apply.

```csharp
var changes = tracker.Compare(before, after);
```

## Changes

```csharp
public sealed record Change(string Path, ChangeKind Kind, object? OldValue, object? NewValue);
```

The path addresses the leaf that moved, so a change is one line to log or display.

| Path | Means |
| --- | --- |
| `Total` | a property on the root |
| `Customer.Address.City` | a property on a nested object |
| `Tags[1]` | an item matched by position |
| `Lines[SKU-9].Quantity` | a property of an item matched by its `MatchBy` key |
| `Lines[#2].Quantity` | a property of an item with no key, in a collection matched by key |

## Collection identity

Without an identity, items are matched by position — inserting at the front reads as every later
slot having changed. Register a key on the tracker to match items by key.

```csharp
var tracker = new ChangeTracker()
    .MatchBy<OrderLine>(line => line.Sku);
```

```csharp
order.Lines.Reverse();              // no changes
order.Lines.Insert(0, newLine);     // Lines[SKU-9]: + {object}
```

A registration can name an open generic type, so one line covers every closed form of it:

```csharp
tracker.MatchBy(typeof(Entity<>), item => ((dynamic)item).Id.ToString());
```

Each item's runtime type is looked up once, most specific first:

1. the type itself, then each base class up to but not including `object`
2. the interfaces it implements
3. `object`, as a catch-all

At each step a closed registration such as `Entity<int>` beats an open one such as `Entity<>`.

Interfaces have no order, so a type whose interfaces carry two different matchers throws
`InvalidOperationException` on its first capture. Register the type itself, or one of its base
classes, to choose its key:

```csharp
tracker
    .MatchBy<IHasCode>(item => item.Code)
    .MatchBy<IHasLabel>(item => item.Label)
    .MatchBy<Coupon>(coupon => coupon.Code);   // Coupon implements both
```

Register matchers before the first `Track`. The keys are captured into the snapshot, so a matcher
added later would make every item read as removed and added; `MatchBy` throws once anything has
been tracked. `Compare` remembers nothing, so it does not count.

A missing or repeated key never loses an edit:

- an item whose key is `null` keeps a slot by position, written `[#2]` so it cannot collide with a
  real key such as `2`
- a key that repeats on either side cannot address a single item, so that collection is compared
  by position, written `[2]`

Unit tests only — nothing here touches a database or a network.
