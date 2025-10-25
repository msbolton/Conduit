using System.Reflection;

namespace Conduit.Common.Reflection;

/// <summary>
/// Extension methods for Type inspection and manipulation.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the type implements the specified interface.
    /// </summary>
    public static bool Implements<TInterface>(this Type type)
    {
        return type.Implements(typeof(TInterface));
    }

    /// <summary>
    /// Determines whether the type implements the specified interface.
    /// </summary>
    public static bool Implements(this Type type, Type interfaceType)
    {
        Guard.NotNull(type);
        Guard.NotNull(interfaceType);

        if (!interfaceType.IsInterface)
            throw new ArgumentException($"Type {interfaceType.Name} is not an interface.", nameof(interfaceType));

        return interfaceType.IsAssignableFrom(type) ||
               type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
    }

    /// <summary>
    /// Determines whether the type inherits from the specified base type.
    /// </summary>
    public static bool InheritsFrom<TBase>(this Type type)
    {
        return type.InheritsFrom(typeof(TBase));
    }

    /// <summary>
    /// Determines whether the type inherits from the specified base type.
    /// </summary>
    public static bool InheritsFrom(this Type type, Type baseType)
    {
        Guard.NotNull(type);
        Guard.NotNull(baseType);

        if (type == baseType)
            return false;

        return baseType.IsAssignableFrom(type);
    }

    /// <summary>
    /// Gets all types that can be assigned to the specified type from an assembly.
    /// </summary>
    public static IEnumerable<Type> GetAssignableTypes(this Assembly assembly, Type targetType)
    {
        Guard.NotNull(assembly);
        Guard.NotNull(targetType);

        return assembly.GetTypes()
            .Where(t => targetType.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
    }

    /// <summary>
    /// Gets the default value for the type.
    /// </summary>
    public static object? GetDefaultValue(this Type type)
    {
        Guard.NotNull(type);

        if (type.IsValueType)
            return Activator.CreateInstance(type);

        return null;
    }

    /// <summary>
    /// Determines whether the type is nullable.
    /// </summary>
    public static bool IsNullable(this Type type)
    {
        Guard.NotNull(type);

        if (!type.IsValueType)
            return true;

        return Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// Gets the underlying type of a nullable type.
    /// </summary>
    public static Type GetUnderlyingType(this Type type)
    {
        Guard.NotNull(type);
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    /// <summary>
    /// Determines whether the type is numeric.
    /// </summary>
    public static bool IsNumeric(this Type type)
    {
        Guard.NotNull(type);

        type = type.GetUnderlyingType();

        return type == typeof(byte) ||
               type == typeof(sbyte) ||
               type == typeof(short) ||
               type == typeof(ushort) ||
               type == typeof(int) ||
               type == typeof(uint) ||
               type == typeof(long) ||
               type == typeof(ulong) ||
               type == typeof(float) ||
               type == typeof(double) ||
               type == typeof(decimal);
    }

    /// <summary>
    /// Determines whether the type is a collection.
    /// </summary>
    public static bool IsCollection(this Type type)
    {
        Guard.NotNull(type);

        if (type == typeof(string))
            return false;

        return type.GetInterfaces().Any(i => i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    /// <summary>
    /// Gets the element type of a collection.
    /// </summary>
    public static Type? GetCollectionElementType(this Type type)
    {
        Guard.NotNull(type);

        if (type.IsArray)
            return type.GetElementType();

        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0];
    }

    /// <summary>
    /// Gets all properties with a specific attribute.
    /// </summary>
    public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<TAttribute>(this Type type)
        where TAttribute : Attribute
    {
        Guard.NotNull(type);

        return type.GetProperties()
            .Where(p => p.GetCustomAttribute<TAttribute>() != null);
    }

    /// <summary>
    /// Gets all methods with a specific attribute.
    /// </summary>
    public static IEnumerable<MethodInfo> GetMethodsWithAttribute<TAttribute>(this Type type)
        where TAttribute : Attribute
    {
        Guard.NotNull(type);

        return type.GetMethods()
            .Where(m => m.GetCustomAttribute<TAttribute>() != null);
    }

    /// <summary>
    /// Creates an instance of the type using the parameterless constructor.
    /// </summary>
    public static T CreateInstance<T>(this Type type)
    {
        Guard.NotNull(type);

        if (!typeof(T).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} cannot be assigned to {typeof(T).Name}");

        return (T)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Creates an instance of the type using constructor parameters.
    /// </summary>
    public static T CreateInstance<T>(this Type type, params object[] args)
    {
        Guard.NotNull(type);

        if (!typeof(T).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} cannot be assigned to {typeof(T).Name}");

        return (T)Activator.CreateInstance(type, args)!;
    }

    /// <summary>
    /// Gets a friendly name for the type.
    /// </summary>
    public static string GetFriendlyName(this Type type)
    {
        Guard.NotNull(type);

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name[..type.Name.IndexOf('`')];
        var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyName));
        return $"{name}<{genericArgs}>";
    }

    /// <summary>
    /// Determines whether the type has a parameterless constructor.
    /// </summary>
    public static bool HasParameterlessConstructor(this Type type)
    {
        Guard.NotNull(type);
        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    /// <summary>
    /// Gets all base types and interfaces.
    /// </summary>
    public static IEnumerable<Type> GetBaseTypesAndInterfaces(this Type type)
    {
        Guard.NotNull(type);

        var current = type.BaseType;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }

        foreach (var @interface in type.GetInterfaces())
        {
            yield return @interface;
        }
    }

    /// <summary>
    /// Determines whether the type is a simple type (primitive, string, decimal, DateTime, etc.).
    /// </summary>
    public static bool IsSimpleType(this Type type)
    {
        Guard.NotNull(type);

        type = type.GetUnderlyingType();

        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum;
    }

    /// <summary>
    /// Gets the generic type definition if the type is generic.
    /// </summary>
    public static Type? GetGenericTypeDefinitionSafe(this Type type)
    {
        Guard.NotNull(type);

        try
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines whether the type is an anonymous type.
    /// </summary>
    public static bool IsAnonymousType(this Type type)
    {
        Guard.NotNull(type);

        return type.Namespace == null &&
               type.IsSealed &&
               type.IsClass &&
               type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Any() &&
               type.Name.Contains("AnonymousType");
    }

    /// <summary>
    /// Gets property value using reflection.
    /// </summary>
    public static object? GetPropertyValue(this object obj, string propertyName)
    {
        Guard.NotNull(obj);
        Guard.NotNullOrEmpty(propertyName);

        var property = obj.GetType().GetProperty(propertyName);
        return property?.GetValue(obj);
    }

    /// <summary>
    /// Sets property value using reflection.
    /// </summary>
    public static void SetPropertyValue(this object obj, string propertyName, object? value)
    {
        Guard.NotNull(obj);
        Guard.NotNullOrEmpty(propertyName);

        var property = obj.GetType().GetProperty(propertyName);
        property?.SetValue(obj, value);
    }

    /// <summary>
    /// Invokes a method using reflection.
    /// </summary>
    public static object? InvokeMethod(this object obj, string methodName, params object[] args)
    {
        Guard.NotNull(obj);
        Guard.NotNullOrEmpty(methodName);

        var method = obj.GetType().GetMethod(methodName);
        return method?.Invoke(obj, args);
    }
}