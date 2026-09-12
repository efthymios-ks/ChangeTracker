namespace ChangeTracker;

public enum ChangeKind
{
    /// <summary>The property or item did not exist before.</summary>
    Added = 1,

    /// <summary>The property or item is gone.</summary>
    Removed = 2,

    /// <summary>Both values exist and differ.</summary>
    Modified = 3
}
