namespace Conduit.Api;

/// <summary>
/// Represents configuration for a pluggable component.
/// </summary>
public class ComponentConfiguration
{
    /// <summary>
    /// Gets or sets the configuration settings as key-value pairs.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the component is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the component timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000; // 30 seconds default

    /// <summary>
    /// Gets or sets the maximum retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the retry delay in milliseconds.
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets custom metadata for the component.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <typeparam name="T">The type to convert the value to</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if key not found</param>
    /// <returns>The configuration value or default</returns>
    public T GetValue<T>(string key, T defaultValue = default!)
    {
        if (Settings.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
                return typedValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <param name="key">The configuration key</param>
    /// <param name="value">The configuration value</param>
    public void SetValue(string key, object value)
    {
        Settings[key] = value;
    }
}