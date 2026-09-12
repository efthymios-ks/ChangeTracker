namespace ChangeTracker;

/// <summary>
/// One difference, addressed by a dotted path such as <c>Customer.Address.City</c> or
/// <c>Lines[2].Quantity</c>. A flat list of these is far easier to log, diff or display than a tree.
/// </summary>
public sealed record Change(string Path, ChangeKind Kind, object? OldValue, object? NewValue)
{
    public override string ToString()
        => Kind switch
        {
            ChangeKind.Added => $"{Path}: + {Format(NewValue)}",
            ChangeKind.Removed => $"{Path}: - {Format(OldValue)}",
            _ => $"{Path}: {Format(OldValue)} -> {Format(NewValue)}"
        };

    private static string Format(object? value)
        => value?.ToString() ?? "null";
}
