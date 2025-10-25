using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Conduit.Common;

namespace Conduit.Core.Discovery
{
    /// <summary>
    /// Thread-safe graph representation of component dependencies.
    /// Supports required and optional dependencies.
    /// </summary>
    public class DependencyGraph
    {
        private readonly ConcurrentDictionary<string, HashSet<DependencyEdge>> _adjacencyList;
        private readonly ConcurrentDictionary<string, HashSet<string>> _reverseAdjacencyList;
        private readonly object _lockObject = new();

        /// <summary>
        /// Initializes a new instance of the DependencyGraph class.
        /// </summary>
        public DependencyGraph()
        {
            _adjacencyList = new ConcurrentDictionary<string, HashSet<DependencyEdge>>(StringComparer.OrdinalIgnoreCase);
            _reverseAdjacencyList = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds a component node to the graph.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        public void AddComponent(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            _adjacencyList.TryAdd(componentId, new HashSet<DependencyEdge>());
            _reverseAdjacencyList.TryAdd(componentId, new HashSet<string>());
        }

        /// <summary>
        /// Adds a dependency edge between two components.
        /// </summary>
        /// <param name="from">The dependent component</param>
        /// <param name="to">The dependency component</param>
        /// <param name="required">Whether the dependency is required</param>
        public void AddDependency(string from, string to, bool required = true)
        {
            Guard.AgainstNullOrEmpty(from, nameof(from));
            Guard.AgainstNullOrEmpty(to, nameof(to));

            lock (_lockObject)
            {
                // Ensure both nodes exist
                AddComponent(from);
                AddComponent(to);

                // Add forward edge
                var edges = _adjacencyList[from];
                edges.Add(new DependencyEdge(to, required));

                // Add reverse edge for dependent tracking
                var reverseEdges = _reverseAdjacencyList[to];
                reverseEdges.Add(from);
            }
        }

        /// <summary>
        /// Removes a dependency edge.
        /// </summary>
        /// <param name="from">The dependent component</param>
        /// <param name="to">The dependency component</param>
        /// <returns>True if the dependency was removed</returns>
        public bool RemoveDependency(string from, string to)
        {
            Guard.AgainstNullOrEmpty(from, nameof(from));
            Guard.AgainstNullOrEmpty(to, nameof(to));

            lock (_lockObject)
            {
                bool removed = false;

                if (_adjacencyList.TryGetValue(from, out var edges))
                {
                    removed = edges.RemoveWhere(e => e.ComponentId.Equals(to, StringComparison.OrdinalIgnoreCase)) > 0;
                }

                if (_reverseAdjacencyList.TryGetValue(to, out var reverseEdges))
                {
                    reverseEdges.Remove(from);
                }

                return removed;
            }
        }

        /// <summary>
        /// Removes a component and all its dependencies.
        /// </summary>
        /// <param name="componentId">The component to remove</param>
        /// <returns>True if the component was removed</returns>
        public bool RemoveComponent(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            lock (_lockObject)
            {
                // Remove all dependencies from this component
                _adjacencyList.TryRemove(componentId, out _);

                // Remove all dependencies to this component
                foreach (var kvp in _adjacencyList)
                {
                    kvp.Value.RemoveWhere(e => e.ComponentId.Equals(componentId, StringComparison.OrdinalIgnoreCase));
                }

                // Remove from reverse list
                _reverseAdjacencyList.TryRemove(componentId, out _);

                foreach (var kvp in _reverseAdjacencyList)
                {
                    kvp.Value.Remove(componentId);
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the direct dependencies of a component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>The component's direct dependencies</returns>
        public IEnumerable<string> GetDependencies(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (_adjacencyList.TryGetValue(componentId, out var edges))
            {
                return edges.Select(e => e.ComponentId).ToList();
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets only the required dependencies of a component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>The component's required dependencies</returns>
        public IEnumerable<string> GetRequiredDependencies(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (_adjacencyList.TryGetValue(componentId, out var edges))
            {
                return edges.Where(e => e.IsRequired).Select(e => e.ComponentId).ToList();
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets only the optional dependencies of a component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>The component's optional dependencies</returns>
        public IEnumerable<string> GetOptionalDependencies(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (_adjacencyList.TryGetValue(componentId, out var edges))
            {
                return edges.Where(e => !e.IsRequired).Select(e => e.ComponentId).ToList();
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the components that depend on the specified component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>Components that depend on this component</returns>
        public IEnumerable<string> GetDependents(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            if (_reverseAdjacencyList.TryGetValue(componentId, out var dependents))
            {
                return dependents.ToList();
            }

            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets all components in the graph.
        /// </summary>
        /// <returns>All component IDs</returns>
        public IEnumerable<string> GetAllComponents()
        {
            return _adjacencyList.Keys.ToList();
        }

        /// <summary>
        /// Checks if a component exists in the graph.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>True if the component exists</returns>
        public bool ContainsComponent(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));
            return _adjacencyList.ContainsKey(componentId);
        }

        /// <summary>
        /// Checks if a dependency exists between two components.
        /// </summary>
        /// <param name="from">The dependent component</param>
        /// <param name="to">The dependency component</param>
        /// <returns>True if the dependency exists</returns>
        public bool HasDependency(string from, string to)
        {
            Guard.AgainstNullOrEmpty(from, nameof(from));
            Guard.AgainstNullOrEmpty(to, nameof(to));

            if (_adjacencyList.TryGetValue(from, out var edges))
            {
                return edges.Any(e => e.ComponentId.Equals(to, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Gets all transitive dependencies of a component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>All transitive dependencies</returns>
        public IEnumerable<string> GetTransitiveDependencies(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(componentId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (visited.Add(current) && _adjacencyList.TryGetValue(current, out var edges))
                {
                    foreach (var edge in edges)
                    {
                        if (!visited.Contains(edge.ComponentId))
                        {
                            stack.Push(edge.ComponentId);
                        }
                    }
                }
            }

            visited.Remove(componentId); // Don't include the starting component
            return visited.ToList();
        }

        /// <summary>
        /// Gets all components that transitively depend on the specified component.
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <returns>All transitive dependents</returns>
        public IEnumerable<string> GetTransitiveDependents(string componentId)
        {
            Guard.AgainstNullOrEmpty(componentId, nameof(componentId));

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(componentId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (visited.Add(current) && _reverseAdjacencyList.TryGetValue(current, out var dependents))
                {
                    foreach (var dependent in dependents)
                    {
                        if (!visited.Contains(dependent))
                        {
                            stack.Push(dependent);
                        }
                    }
                }
            }

            visited.Remove(componentId); // Don't include the starting component
            return visited.ToList();
        }

        /// <summary>
        /// Gets components with no dependencies (root components).
        /// </summary>
        /// <returns>Root component IDs</returns>
        public IEnumerable<string> GetRootComponents()
        {
            return _adjacencyList
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Gets components that no other component depends on (leaf components).
        /// </summary>
        /// <returns>Leaf component IDs</returns>
        public IEnumerable<string> GetLeafComponents()
        {
            return _reverseAdjacencyList
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Creates a deep copy of the graph.
        /// </summary>
        /// <returns>A new graph with the same structure</returns>
        public DependencyGraph Copy()
        {
            var copy = new DependencyGraph();

            foreach (var kvp in _adjacencyList)
            {
                copy.AddComponent(kvp.Key);
                foreach (var edge in kvp.Value)
                {
                    copy.AddDependency(kvp.Key, edge.ComponentId, edge.IsRequired);
                }
            }

            return copy;
        }

        /// <summary>
        /// Clears the graph.
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _adjacencyList.Clear();
                _reverseAdjacencyList.Clear();
            }
        }

        /// <summary>
        /// Gets statistics about the graph.
        /// </summary>
        /// <returns>Graph statistics</returns>
        public GraphStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var totalEdges = _adjacencyList.Values.Sum(edges => edges.Count);
                var requiredEdges = _adjacencyList.Values.Sum(edges => edges.Count(e => e.IsRequired));
                var optionalEdges = totalEdges - requiredEdges;

                return new GraphStatistics
                {
                    ComponentCount = _adjacencyList.Count,
                    TotalDependencies = totalEdges,
                    RequiredDependencies = requiredEdges,
                    OptionalDependencies = optionalEdges,
                    RootComponents = GetRootComponents().Count(),
                    LeafComponents = GetLeafComponents().Count()
                };
            }
        }

        /// <summary>
        /// Represents a dependency edge in the graph.
        /// </summary>
        private class DependencyEdge : IEquatable<DependencyEdge>
        {
            public string ComponentId { get; }
            public bool IsRequired { get; }

            public DependencyEdge(string componentId, bool isRequired)
            {
                ComponentId = componentId;
                IsRequired = isRequired;
            }

            public bool Equals(DependencyEdge? other)
            {
                if (other == null) return false;
                return ComponentId.Equals(other.ComponentId, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as DependencyEdge);
            }

            public override int GetHashCode()
            {
                return ComponentId.GetHashCode(StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Statistics about the dependency graph.
        /// </summary>
        public class GraphStatistics
        {
            public int ComponentCount { get; set; }
            public int TotalDependencies { get; set; }
            public int RequiredDependencies { get; set; }
            public int OptionalDependencies { get; set; }
            public int RootComponents { get; set; }
            public int LeafComponents { get; set; }

            public override string ToString()
            {
                return $"Components: {ComponentCount}, Dependencies: {TotalDependencies} " +
                       $"(Required: {RequiredDependencies}, Optional: {OptionalDependencies}), " +
                       $"Roots: {RootComponents}, Leaves: {LeafComponents}";
            }
        }
    }
}