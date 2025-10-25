using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Conduit.Common.Extensions;

/// <summary>
/// Extension methods for string manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Checks if the string is null or whitespace.
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Checks if the string is null or empty.
    /// </summary>
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Returns the string or a default value if null/empty.
    /// </summary>
    public static string OrDefault(this string? value, string defaultValue = "")
    {
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// Truncates the string to the specified length.
    /// </summary>
    public static string Truncate(this string value, int maxLength, string suffix = "...")
    {
        Guard.NotNull(value);
        Guard.NotNegative(maxLength);

        if (value.Length <= maxLength)
            return value;

        if (maxLength <= suffix.Length)
            return suffix[..maxLength];

        return value[..(maxLength - suffix.Length)] + suffix;
    }

    /// <summary>
    /// Converts string to PascalCase.
    /// </summary>
    public static string ToPascalCase(this string value)
    {
        Guard.NotNull(value);

        if (value.Length == 0)
            return value;

        var words = Regex.Split(value, @"[^a-zA-Z0-9]+|(?<=\p{Ll})(?=\p{Lu})|(?<=\p{Lu})(?=\p{Lu}\p{Ll})");
        var result = new StringBuilder();

        foreach (var word in words.Where(w => !string.IsNullOrEmpty(w)))
        {
            result.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
                result.Append(word[1..].ToLowerInvariant());
        }

        return result.ToString();
    }

    /// <summary>
    /// Converts string to camelCase.
    /// </summary>
    public static string ToCamelCase(this string value)
    {
        var pascalCase = value.ToPascalCase();
        if (pascalCase.Length == 0)
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }

    /// <summary>
    /// Converts string to snake_case.
    /// </summary>
    public static string ToSnakeCase(this string value)
    {
        Guard.NotNull(value);

        if (value.Length == 0)
            return value;

        var result = Regex.Replace(value, @"([a-z0-9])([A-Z])", "$1_$2");
        result = Regex.Replace(result, @"([A-Z])([A-Z][a-z])", "$1_$2");
        result = Regex.Replace(result, @"[^a-zA-Z0-9]+", "_");
        return result.ToLowerInvariant().Trim('_');
    }

    /// <summary>
    /// Converts string to kebab-case.
    /// </summary>
    public static string ToKebabCase(this string value)
    {
        return value.ToSnakeCase().Replace('_', '-');
    }

    /// <summary>
    /// Removes specified characters from the string.
    /// </summary>
    public static string Remove(this string value, params char[] characters)
    {
        Guard.NotNull(value);
        Guard.NotNull(characters);

        if (characters.Length == 0)
            return value;

        var result = new StringBuilder(value.Length);
        var toRemove = new HashSet<char>(characters);

        foreach (var c in value)
        {
            if (!toRemove.Contains(c))
                result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Ensures the string starts with the specified prefix.
    /// </summary>
    public static string EnsureStartsWith(this string value, string prefix)
    {
        Guard.NotNull(value);
        Guard.NotNull(prefix);

        return value.StartsWith(prefix, StringComparison.Ordinal) ? value : prefix + value;
    }

    /// <summary>
    /// Ensures the string ends with the specified suffix.
    /// </summary>
    public static string EnsureEndsWith(this string value, string suffix)
    {
        Guard.NotNull(value);
        Guard.NotNull(suffix);

        return value.EndsWith(suffix, StringComparison.Ordinal) ? value : value + suffix;
    }

    /// <summary>
    /// Converts string to Base64.
    /// </summary>
    public static string ToBase64(this string value, Encoding? encoding = null)
    {
        Guard.NotNull(value);
        encoding ??= Encoding.UTF8;
        return Convert.ToBase64String(encoding.GetBytes(value));
    }

    /// <summary>
    /// Converts Base64 string to plain text.
    /// </summary>
    public static string FromBase64(this string value, Encoding? encoding = null)
    {
        Guard.NotNull(value);
        encoding ??= Encoding.UTF8;
        return encoding.GetString(Convert.FromBase64String(value));
    }

    /// <summary>
    /// Checks if the string contains any of the specified substrings.
    /// </summary>
    public static bool ContainsAny(this string value, params string[] substrings)
    {
        Guard.NotNull(value);
        Guard.NotNull(substrings);

        return substrings.Any(s => value.Contains(s, StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks if the string contains all of the specified substrings.
    /// </summary>
    public static bool ContainsAll(this string value, params string[] substrings)
    {
        Guard.NotNull(value);
        Guard.NotNull(substrings);

        return substrings.All(s => value.Contains(s, StringComparison.Ordinal));
    }

    /// <summary>
    /// Splits the string into lines.
    /// </summary>
    public static string[] ToLines(this string value)
    {
        Guard.NotNull(value);
        return value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    /// <summary>
    /// Joins strings with the specified separator.
    /// </summary>
    public static string JoinWith(this IEnumerable<string> values, string separator)
    {
        Guard.NotNull(values);
        Guard.NotNull(separator);

        return string.Join(separator, values);
    }

    /// <summary>
    /// Repeats the string the specified number of times.
    /// </summary>
    public static string Repeat(this string value, int count)
    {
        Guard.NotNull(value);
        Guard.NotNegative(count);

        if (count == 0 || value.Length == 0)
            return string.Empty;

        if (count == 1)
            return value;

        var result = new StringBuilder(value.Length * count);
        for (int i = 0; i < count; i++)
            result.Append(value);

        return result.ToString();
    }

    /// <summary>
    /// Reverses the string.
    /// </summary>
    public static string Reverse(this string value)
    {
        Guard.NotNull(value);

        if (value.Length <= 1)
            return value;

        var chars = value.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// Extracts substring between two delimiters.
    /// </summary>
    public static string? Between(this string value, string start, string end)
    {
        Guard.NotNull(value);
        Guard.NotNull(start);
        Guard.NotNull(end);

        var startIndex = value.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
            return null;

        startIndex += start.Length;
        var endIndex = value.IndexOf(end, startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
            return null;

        return value[startIndex..endIndex];
    }

    /// <summary>
    /// Normalizes line endings to the current environment.
    /// </summary>
    public static string NormalizeLineEndings(this string value)
    {
        Guard.NotNull(value);
        return value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
    }
}