using System.Reflection;
using System.Runtime.Loader;

namespace Conduit.Common.Reflection;

/// <summary>
/// Provides assembly scanning and type discovery functionality.
/// </summary>
public class AssemblyScanner
{
    private readonly List<Assembly> _assemblies;
    private readonly HashSet<string> _scannedPaths;
    private readonly AssemblyLoadContext _loadContext;

    /// <summary>
    /// Initializes a new instance of the AssemblyScanner class.
    /// </summary>
    public AssemblyScanner() : this(AssemblyLoadContext.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance with a specific load context.
    /// </summary>
    public AssemblyScanner(AssemblyLoadContext loadContext)
    {
        _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        _assemblies = new List<Assembly>();
        _scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the scanned assemblies.
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies.AsReadOnly();

    /// <summary>
    /// Scans assemblies in the specified directory.
    /// </summary>
    public AssemblyScanner ScanDirectory(string path, string searchPattern = "*.dll", bool recursive = false)
    {
        Guard.NotNullOrEmpty(path);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(path, searchPattern, searchOption);

        foreach (var file in files)
        {
            TryLoadAssembly(file);
        }

        return this;
    }

    /// <summary>
    /// Scans the current application domain.
    /// </summary>
    public AssemblyScanner ScanAppDomain()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }
        }

        return this;
    }

    /// <summary>
    /// Scans the executing assembly.
    /// </summary>
    public AssemblyScanner ScanExecutingAssembly()
    {
        var assembly = Assembly.GetExecutingAssembly();
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Scans the calling assembly.
    /// </summary>
    public AssemblyScanner ScanCallingAssembly()
    {
        var assembly = Assembly.GetCallingAssembly();
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds a specific assembly to scan.
    /// </summary>
    public AssemblyScanner AddAssembly(Assembly assembly)
    {
        Guard.NotNull(assembly);

        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds assemblies by name.
    /// </summary>
    public AssemblyScanner AddAssemblies(params string[] assemblyNames)
    {
        Guard.NotNull(assemblyNames);

        foreach (var name in assemblyNames)
        {
            try
            {
                var assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(name));
                if (!_assemblies.Contains(assembly))
                {
                    _assemblies.Add(assembly);
                }
            }
            catch (Exception ex)
            {
                // Log or handle assembly load failure
                Console.WriteLine($"Failed to load assembly {name}: {ex.Message}");
            }
        }

        return this;
    }

    /// <summary>
    /// Filters assemblies by predicate.
    /// </summary>
    public AssemblyScanner Where(Func<Assembly, bool> predicate)
    {
        Guard.NotNull(predicate);

        _assemblies.RemoveAll(a => !predicate(a));
        return this;
    }

    /// <summary>
    /// Excludes system assemblies.
    /// </summary>
    public AssemblyScanner ExcludeSystemAssemblies()
    {
        return Where(a => !a.FullName?.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ?? true)
            .Where(a => !a.FullName?.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ?? true);
    }

    /// <summary>
    /// Finds types matching a predicate.
    /// </summary>
    public IEnumerable<Type> FindTypes(Func<Type, bool> predicate)
    {
        Guard.NotNull(predicate);

        return _assemblies
            .SelectMany(a => GetTypesFromAssembly(a))
            .Where(predicate);
    }

    /// <summary>
    /// Finds types implementing an interface.
    /// </summary>
    public IEnumerable<Type> FindTypesImplementing<TInterface>()
    {
        return FindTypesImplementing(typeof(TInterface));
    }

    /// <summary>
    /// Finds types implementing an interface.
    /// </summary>
    public IEnumerable<Type> FindTypesImplementing(Type interfaceType)
    {
        Guard.NotNull(interfaceType);

        if (!interfaceType.IsInterface)
            throw new ArgumentException($"Type {interfaceType.Name} is not an interface.");

        return FindTypes(t =>
            t.IsClass &&
            !t.IsAbstract &&
            t.Implements(interfaceType));
    }

    /// <summary>
    /// Finds types inheriting from a base type.
    /// </summary>
    public IEnumerable<Type> FindTypesInheritingFrom<TBase>()
    {
        return FindTypesInheritingFrom(typeof(TBase));
    }

    /// <summary>
    /// Finds types inheriting from a base type.
    /// </summary>
    public IEnumerable<Type> FindTypesInheritingFrom(Type baseType)
    {
        Guard.NotNull(baseType);

        return FindTypes(t =>
            t.IsClass &&
            !t.IsAbstract &&
            t.InheritsFrom(baseType));
    }

    /// <summary>
    /// Finds types with a specific attribute.
    /// </summary>
    public IEnumerable<Type> FindTypesWithAttribute<TAttribute>()
        where TAttribute : Attribute
    {
        return FindTypes(t => t.GetCustomAttribute<TAttribute>() != null);
    }

    /// <summary>
    /// Finds types in a namespace.
    /// </summary>
    public IEnumerable<Type> FindTypesInNamespace(string @namespace, bool includeNested = false)
    {
        Guard.NotNullOrEmpty(@namespace);

        return FindTypes(t =>
        {
            if (t.Namespace == null)
                return false;

            if (includeNested)
                return t.Namespace.StartsWith(@namespace, StringComparison.Ordinal);

            return t.Namespace.Equals(@namespace, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Creates instances of types matching a predicate.
    /// </summary>
    public IEnumerable<T> CreateInstances<T>(Func<Type, bool>? predicate = null)
    {
        var types = FindTypesImplementing<T>();

        if (predicate != null)
            types = types.Where(predicate);

        foreach (var type in types)
        {
            if (type.HasParameterlessConstructor())
            {
                yield return type.CreateInstance<T>();
            }
        }
    }

    /// <summary>
    /// Clears all scanned assemblies.
    /// </summary>
    public void Clear()
    {
        _assemblies.Clear();
        _scannedPaths.Clear();
    }

    private bool TryLoadAssembly(string path)
    {
        try
        {
            if (_scannedPaths.Contains(path))
                return false;

            _scannedPaths.Add(path);

            var assembly = _loadContext.LoadFromAssemblyPath(path);
            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            // Log or handle assembly load failure
            Console.WriteLine($"Failed to load assembly from {path}: {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<Type> GetTypesFromAssembly(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return types that were successfully loaded
            return ex.Types.Where(t => t != null)!;
        }
        catch
        {
            return Enumerable.Empty<Type>();
        }
    }

    /// <summary>
    /// Creates a scanner builder for fluent configuration.
    /// </summary>
    public static AssemblyScannerBuilder CreateBuilder()
    {
        return new AssemblyScannerBuilder();
    }
}

/// <summary>
/// Builder for fluent assembly scanner configuration.
/// </summary>
public class AssemblyScannerBuilder
{
    private readonly AssemblyScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the AssemblyScannerBuilder class.
    /// </summary>
    public AssemblyScannerBuilder()
    {
        _scanner = new AssemblyScanner();
    }

    /// <summary>
    /// Initializes a new instance with a specific load context.
    /// </summary>
    public AssemblyScannerBuilder(AssemblyLoadContext loadContext)
    {
        _scanner = new AssemblyScanner(loadContext);
    }

    /// <summary>
    /// Scans the specified directory.
    /// </summary>
    public AssemblyScannerBuilder FromDirectory(string path, string pattern = "*.dll", bool recursive = false)
    {
        _scanner.ScanDirectory(path, pattern, recursive);
        return this;
    }

    /// <summary>
    /// Scans the application domain.
    /// </summary>
    public AssemblyScannerBuilder FromAppDomain()
    {
        _scanner.ScanAppDomain();
        return this;
    }

    /// <summary>
    /// Scans the executing assembly.
    /// </summary>
    public AssemblyScannerBuilder FromExecutingAssembly()
    {
        _scanner.ScanExecutingAssembly();
        return this;
    }

    /// <summary>
    /// Adds specific assemblies.
    /// </summary>
    public AssemblyScannerBuilder WithAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
        {
            _scanner.AddAssembly(assembly);
        }
        return this;
    }

    /// <summary>
    /// Excludes system assemblies.
    /// </summary>
    public AssemblyScannerBuilder ExcludeSystem()
    {
        _scanner.ExcludeSystemAssemblies();
        return this;
    }

    /// <summary>
    /// Applies a filter to assemblies.
    /// </summary>
    public AssemblyScannerBuilder WhereAssembly(Func<Assembly, bool> predicate)
    {
        _scanner.Where(predicate);
        return this;
    }

    /// <summary>
    /// Builds the configured scanner.
    /// </summary>
    public AssemblyScanner Build()
    {
        return _scanner;
    }
}