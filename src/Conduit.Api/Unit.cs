namespace Conduit.Api;

/// <summary>
/// Represents a void or no-value type for use in generic contexts.
/// Used when a command or query doesn't return a meaningful value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the default instance of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// Returns a completed task containing Unit.Value.
    /// </summary>
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>
    /// Determines whether the specified Unit is equal to the current Unit.
    /// </summary>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines whether the specified object is equal to the current Unit.
    /// </summary>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Returns the hash code for this Unit.
    /// </summary>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of Unit.
    /// </summary>
    public override string ToString() => "()";

    /// <summary>
    /// Determines whether two Unit instances are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two Unit instances are not equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}