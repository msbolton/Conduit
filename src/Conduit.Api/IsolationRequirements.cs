namespace Conduit.Api;

/// <summary>
/// Defines isolation requirements for a component.
/// </summary>
public class IsolationRequirements
{
    /// <summary>
    /// Gets or sets the isolation level.
    /// </summary>
    public IsolationLevel Level { get; set; } = IsolationLevel.Standard;

    /// <summary>
    /// Gets or sets a value indicating whether the component requires a separate AppDomain/AssemblyLoadContext.
    /// </summary>
    public bool RequiresSeparateContext { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the component can share resources with other components.
    /// </summary>
    public bool CanShareResources { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum memory limit in MB for this component.
    /// </summary>
    public long? MaxMemoryMB { get; set; }

    /// <summary>
    /// Gets or sets the maximum CPU usage percentage for this component.
    /// </summary>
    public int? MaxCpuPercent { get; set; }

    /// <summary>
    /// Gets or sets the maximum thread count for this component.
    /// </summary>
    public int? MaxThreadCount { get; set; }

    /// <summary>
    /// Gets or sets network access restrictions.
    /// </summary>
    public NetworkRestrictions NetworkRestrictions { get; set; } = NetworkRestrictions.None;

    /// <summary>
    /// Gets or sets file system access restrictions.
    /// </summary>
    public FileSystemRestrictions FileSystemRestrictions { get; set; } = FileSystemRestrictions.ReadOnly;

    /// <summary>
    /// Gets or sets allowed assemblies that can be loaded.
    /// </summary>
    public HashSet<string> AllowedAssemblies { get; set; } = new();

    /// <summary>
    /// Gets or sets blocked assemblies that cannot be loaded.
    /// </summary>
    public HashSet<string> BlockedAssemblies { get; set; } = new();

    /// <summary>
    /// Gets or sets security permissions required.
    /// </summary>
    public HashSet<string> RequiredPermissions { get; set; } = new();

    /// <summary>
    /// Creates standard isolation requirements.
    /// </summary>
    public static IsolationRequirements Standard()
    {
        return new IsolationRequirements
        {
            Level = IsolationLevel.Standard,
            CanShareResources = true,
            NetworkRestrictions = NetworkRestrictions.None,
            FileSystemRestrictions = FileSystemRestrictions.ReadOnly
        };
    }

    /// <summary>
    /// Creates strict isolation requirements.
    /// </summary>
    public static IsolationRequirements Strict()
    {
        return new IsolationRequirements
        {
            Level = IsolationLevel.Strict,
            RequiresSeparateContext = true,
            CanShareResources = false,
            NetworkRestrictions = NetworkRestrictions.LocalOnly,
            FileSystemRestrictions = FileSystemRestrictions.None
        };
    }

    /// <summary>
    /// Creates no isolation requirements.
    /// </summary>
    public static IsolationRequirements None()
    {
        return new IsolationRequirements
        {
            Level = IsolationLevel.None,
            CanShareResources = true,
            NetworkRestrictions = NetworkRestrictions.None,
            FileSystemRestrictions = FileSystemRestrictions.Full
        };
    }
}

/// <summary>
/// Defines isolation levels.
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// No isolation required.
    /// </summary>
    None,

    /// <summary>
    /// Standard isolation with shared resources.
    /// </summary>
    Standard,

    /// <summary>
    /// Strict isolation with separate context.
    /// </summary>
    Strict,

    /// <summary>
    /// Complete sandboxing.
    /// </summary>
    Sandbox
}

/// <summary>
/// Defines network access restrictions.
/// </summary>
public enum NetworkRestrictions
{
    /// <summary>
    /// No network restrictions.
    /// </summary>
    None,

    /// <summary>
    /// Local network only.
    /// </summary>
    LocalOnly,

    /// <summary>
    /// Specific hosts only.
    /// </summary>
    SpecificHosts,

    /// <summary>
    /// No network access.
    /// </summary>
    Blocked
}

/// <summary>
/// Defines file system access restrictions.
/// </summary>
public enum FileSystemRestrictions
{
    /// <summary>
    /// Full file system access.
    /// </summary>
    Full,

    /// <summary>
    /// Read-only access.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Specific paths only.
    /// </summary>
    SpecificPaths,

    /// <summary>
    /// No file system access.
    /// </summary>
    None
}