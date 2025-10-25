using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Conduit.Common;

/// <summary>
/// Provides guard clauses for parameter validation.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Throws if the value is null.
    /// </summary>
    [DebuggerStepThrough]
    public static T NotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the value is null or empty.
    /// </summary>
    [DebuggerStepThrough]
    public static string NotNullOrEmpty(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        NotNull(value, paramName);
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be empty.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the value is null or whitespace.
    /// </summary>
    [DebuggerStepThrough]
    public static string NotNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        NotNull(value, paramName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the collection is null or empty.
    /// </summary>
    [DebuggerStepThrough]
    public static IEnumerable<T> NotNullOrEmpty<T>(
        [NotNull] IEnumerable<T>? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        NotNull(value, paramName);
        if (!value.Any())
        {
            throw new ArgumentException("Collection cannot be empty.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the value is not in the specified range.
    /// </summary>
    [DebuggerStepThrough]
    public static T InRange<T>(
        T value,
        T min,
        T max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value,
                $"Value must be between {min} and {max}.");
        }
        return value;
    }

    /// <summary>
    /// Throws if the value is negative.
    /// </summary>
    [DebuggerStepThrough]
    public static T NotNegative<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : IComparable<T>
    {
        if (Comparer<T>.Default.Compare(value, default!) < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
        }
        return value;
    }

    /// <summary>
    /// Throws if the value is zero.
    /// </summary>
    [DebuggerStepThrough]
    public static T NotZero<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : IComparable<T>
    {
        if (Comparer<T>.Default.Compare(value, default!) == 0)
        {
            throw new ArgumentException("Value cannot be zero.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the condition is false.
    /// </summary>
    [DebuggerStepThrough]
    public static void Requires(
        bool condition,
        string message,
        [CallerArgumentExpression(nameof(condition))] string? paramName = null)
    {
        if (!condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    /// <summary>
    /// Throws if the value doesn't match the pattern.
    /// </summary>
    [DebuggerStepThrough]
    public static string Matches(
        string value,
        string pattern,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        NotNullOrEmpty(value, paramName);
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
        {
            throw new ArgumentException($"Value must match pattern: {pattern}", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the type is not assignable.
    /// </summary>
    [DebuggerStepThrough]
    public static Type IsAssignableTo<T>(
        Type type,
        [CallerArgumentExpression(nameof(type))] string? paramName = null)
    {
        NotNull(type, paramName);
        if (!typeof(T).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type must be assignable to {typeof(T).Name}", paramName);
        }
        return type;
    }

    /// <summary>
    /// Throws if the GUID is empty.
    /// </summary>
    [DebuggerStepThrough]
    public static Guid NotEmpty(
        Guid value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("GUID cannot be empty.", paramName);
        }
        return value;
    }

    /// <summary>
    /// Throws if the collection contains null elements.
    /// </summary>
    [DebuggerStepThrough]
    public static IEnumerable<T> NoNullElements<T>(
        IEnumerable<T?> value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        NotNull(value, paramName);
        var list = value.ToList();
        if (list.Any(x => x is null))
        {
            throw new ArgumentException("Collection cannot contain null elements.", paramName);
        }
        return list!;
    }

    /// <summary>
    /// Throws if the value is the default value for its type.
    /// </summary>
    [DebuggerStepThrough]
    public static T NotDefault<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException("Value cannot be the default value.", paramName);
        }
        return value;
    }
}