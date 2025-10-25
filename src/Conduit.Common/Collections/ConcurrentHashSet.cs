using System.Collections;
using System.Collections.Concurrent;

namespace Conduit.Common.Collections;

/// <summary>
/// Thread-safe HashSet implementation.
/// </summary>
/// <typeparam name="T">The type of elements in the set</typeparam>
public class ConcurrentHashSet<T> : ISet<T>, IReadOnlyCollection<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary;

    /// <summary>
    /// Initializes a new instance of the ConcurrentHashSet class.
    /// </summary>
    public ConcurrentHashSet()
    {
        _dictionary = new ConcurrentDictionary<T, byte>();
    }

    /// <summary>
    /// Initializes a new instance of the ConcurrentHashSet class with the specified comparer.
    /// </summary>
    public ConcurrentHashSet(IEqualityComparer<T> comparer)
    {
        _dictionary = new ConcurrentDictionary<T, byte>(comparer);
    }

    /// <summary>
    /// Initializes a new instance of the ConcurrentHashSet class with the specified collection.
    /// </summary>
    public ConcurrentHashSet(IEnumerable<T> collection)
    {
        Guard.NotNull(collection);
        _dictionary = new ConcurrentDictionary<T, byte>(
            collection.Select(item => new KeyValuePair<T, byte>(item, 0)));
    }

    /// <summary>
    /// Initializes a new instance with the specified collection and comparer.
    /// </summary>
    public ConcurrentHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
    {
        Guard.NotNull(collection);
        _dictionary = new ConcurrentDictionary<T, byte>(
            collection.Select(item => new KeyValuePair<T, byte>(item, 0)),
            comparer);
    }

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public int Count => _dictionary.Count;

    /// <summary>
    /// Gets a value indicating whether the set is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Adds an element to the set.
    /// </summary>
    public bool Add(T item)
    {
        return _dictionary.TryAdd(item, 0);
    }

    /// <summary>
    /// Adds an element to the set.
    /// </summary>
    void ICollection<T>.Add(T item)
    {
        Add(item);
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    public void Clear()
    {
        _dictionary.Clear();
    }

    /// <summary>
    /// Determines whether the set contains a specific value.
    /// </summary>
    public bool Contains(T item)
    {
        return _dictionary.ContainsKey(item);
    }

    /// <summary>
    /// Copies the elements to an array.
    /// </summary>
    public void CopyTo(T[] array, int arrayIndex)
    {
        _dictionary.Keys.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    public bool Remove(T item)
    {
        return _dictionary.TryRemove(item, out _);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        return _dictionary.Keys.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Removes all elements in the specified collection from the set.
    /// </summary>
    public void ExceptWith(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        foreach (var item in other)
        {
            Remove(item);
        }
    }

    /// <summary>
    /// Modifies the set to contain only elements present in both the set and the collection.
    /// </summary>
    public void IntersectWith(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = new HashSet<T>(other);
        var toRemove = _dictionary.Keys.Where(item => !otherSet.Contains(item)).ToList();
        foreach (var item in toRemove)
        {
            Remove(item);
        }
    }

    /// <summary>
    /// Determines whether the set is a proper subset of the collection.
    /// </summary>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        return Count < otherSet.Count && IsSubsetOf(otherSet);
    }

    /// <summary>
    /// Determines whether the set is a proper superset of the collection.
    /// </summary>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        return Count > otherSet.Count && IsSupersetOf(otherSet);
    }

    /// <summary>
    /// Determines whether the set is a subset of the collection.
    /// </summary>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        return _dictionary.Keys.All(item => otherSet.Contains(item));
    }

    /// <summary>
    /// Determines whether the set is a superset of the collection.
    /// </summary>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        return other.All(item => Contains(item));
    }

    /// <summary>
    /// Determines whether the set overlaps with the collection.
    /// </summary>
    public bool Overlaps(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        return other.Any(item => Contains(item));
    }

    /// <summary>
    /// Determines whether the set and the collection contain the same elements.
    /// </summary>
    public bool SetEquals(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = other as ISet<T> ?? new HashSet<T>(other);
        return Count == otherSet.Count && IsSupersetOf(otherSet);
    }

    /// <summary>
    /// Modifies the set to contain only elements present in either the set or the collection, but not both.
    /// </summary>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        var otherSet = new HashSet<T>(other);
        var toAdd = otherSet.Where(item => !Contains(item)).ToList();
        var toRemove = _dictionary.Keys.Where(item => otherSet.Contains(item)).ToList();

        foreach (var item in toRemove)
        {
            Remove(item);
        }
        foreach (var item in toAdd)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Modifies the set to contain all elements present in both the set and the collection.
    /// </summary>
    public void UnionWith(IEnumerable<T> other)
    {
        Guard.NotNull(other);
        foreach (var item in other)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Attempts to add the specified element to the set.
    /// </summary>
    public bool TryAdd(T item)
    {
        return Add(item);
    }

    /// <summary>
    /// Attempts to remove the specified element from the set.
    /// </summary>
    public bool TryRemove(T item)
    {
        return Remove(item);
    }

    /// <summary>
    /// Gets a snapshot of the current items.
    /// </summary>
    public T[] ToArray()
    {
        return _dictionary.Keys.ToArray();
    }

    /// <summary>
    /// Gets a snapshot of the current items as a list.
    /// </summary>
    public List<T> ToList()
    {
        return _dictionary.Keys.ToList();
    }
}