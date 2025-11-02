using System;
using System.Collections.Generic;
using System.Linq;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Core.Discovery
{
    /// <summary>
    /// Resolves component dependencies and creates initialization order.
    /// Performs topological sorting for proper component startup sequence.
    /// </summary>
    public class DependencyResolver
    {
        private readonly ILogger<DependencyResolver>? _logger;
        private readonly bool _strictVersioning;

        /// <summary>
        /// Initializes a new instance of the DependencyResolver class.
        /// </summary>
        /// <param name="strictVersioning">If true, enforces strict semantic versioning</param>
        /// <param name="logger">Optional logger</param>
        public DependencyResolver(bool strictVersioning = false, ILogger<DependencyResolver>? logger = null)
        {
            _strictVersioning = strictVersioning;
            _logger = logger;
        }

        /// <summary>
        /// Resolves dependencies for a collection of component descriptors.
        /// </summary>
        /// <param name="descriptors">The component descriptors to resolve</param>
        /// <returns>Resolution result with ordered components</returns>
        public DependencyResolutionResult Resolve(IEnumerable<ComponentDescriptor> descriptors)
        {
            Guard.NotNull(descriptors, nameof(descriptors));

            var descriptorList = descriptors.ToList();
            var descriptorMap = new Dictionary<string, ComponentDescriptor>();
            var warnings = new List<string>();

            // Build descriptor map and check for duplicates
            foreach (var descriptor in descriptorList)
            {
                if (descriptorMap.ContainsKey(descriptor.Id))
                {
                    return new DependencyResolutionResult(
                        false,
                        new List<ComponentDescriptor>(),
                        warnings,
                        $"Duplicate component ID found: {descriptor.Id}");
                }

                descriptorMap[descriptor.Id] = descriptor;
            }

            // Build dependency graph
            var graph = BuildDependencyGraph(descriptorList, descriptorMap, warnings);

            // Check for circular dependencies
            var circularDependency = DetectCircularDependencies(graph, descriptorMap);
            if (circularDependency != null)
            {
                return new DependencyResolutionResult(
                    false,
                    new List<ComponentDescriptor>(),
                    warnings,
                    $"Circular dependency detected: {circularDependency}");
            }

            // Validate version compatibility
            var versionErrors = ValidateVersionCompatibility(descriptorList, descriptorMap);
            if (versionErrors.Count > 0)
            {
                if (_strictVersioning)
                {
                    return new DependencyResolutionResult(
                        false,
                        new List<ComponentDescriptor>(),
                        warnings,
                        $"Version compatibility errors: {string.Join(", ", versionErrors)}");
                }
                else
                {
                    warnings.AddRange(versionErrors);
                }
            }

            // Perform topological sort
            var sortedComponents = TopologicalSort(graph, descriptorMap);

            if (sortedComponents == null)
            {
                return new DependencyResolutionResult(
                    false,
                    new List<ComponentDescriptor>(),
                    warnings,
                    "Failed to perform topological sort - possible circular dependency");
            }

            _logger?.LogInformation("Successfully resolved dependencies for {Count} components", sortedComponents.Count);

            return new DependencyResolutionResult(
                true,
                sortedComponents,
                warnings,
                null);
        }

        /// <summary>
        /// Builds a dependency graph from component descriptors.
        /// </summary>
        private DependencyGraph BuildDependencyGraph(
            List<ComponentDescriptor> descriptors,
            Dictionary<string, ComponentDescriptor> descriptorMap,
            List<string> warnings)
        {
            var graph = new DependencyGraph();

            // Add all components as nodes
            foreach (var descriptor in descriptors)
            {
                graph.AddComponent(descriptor.Id);
            }

            // Add dependencies as edges
            foreach (var descriptor in descriptors)
            {
                if (descriptor.Dependencies == null)
                {
                    continue;
                }

                foreach (var dependency in descriptor.Dependencies)
                {
                    // Check if dependency exists
                    if (!descriptorMap.ContainsKey(dependency))
                    {
                        warnings.Add($"Required dependency '{dependency}' not found for component '{descriptor.Id}'");
                        continue;
                    }

                    graph.AddDependency(descriptor.Id, dependency, true);
                }

                // Add implicit dependencies from required services
                if (descriptor.RequiredServices != null)
                {
                    foreach (var service in descriptor.RequiredServices)
                    {
                        // Find components that provide this service
                        var providers = descriptors.Where(d =>
                            d.ProvidedServices != null && d.ProvidedServices.Contains(service));

                        if (!providers.Any())
                        {
                            warnings.Add($"No provider found for required service '{service}' in component '{descriptor.Id}'");
                        }
                        else
                        {
                            foreach (var provider in providers)
                            {
                                if (provider.Id != descriptor.Id)
                                {
                                    graph.AddDependency(descriptor.Id, provider.Id, true);
                                }
                            }
                        }
                    }
                }
            }

            return graph;
        }

        /// <summary>
        /// Detects circular dependencies in the graph using DFS.
        /// </summary>
        private string? DetectCircularDependencies(DependencyGraph graph, Dictionary<string, ComponentDescriptor> descriptorMap)
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var path = new List<string>();

            foreach (var componentId in graph.GetAllComponents())
            {
                if (!visited.Contains(componentId))
                {
                    if (HasCircularDependencyDFS(componentId, graph, visited, recursionStack, path))
                    {
                        return string.Join(" -> ", path);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// DFS helper for circular dependency detection.
        /// </summary>
        private bool HasCircularDependencyDFS(
            string componentId,
            DependencyGraph graph,
            HashSet<string> visited,
            HashSet<string> recursionStack,
            List<string> path)
        {
            visited.Add(componentId);
            recursionStack.Add(componentId);
            path.Add(componentId);

            foreach (var dependency in graph.GetDependencies(componentId))
            {
                if (recursionStack.Contains(dependency))
                {
                    path.Add(dependency);
                    return true;
                }

                if (!visited.Contains(dependency))
                {
                    if (HasCircularDependencyDFS(dependency, graph, visited, recursionStack, path))
                    {
                        return true;
                    }
                }
            }

            recursionStack.Remove(componentId);
            if (path.Count > 0 && path[path.Count - 1] == componentId)
            {
                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        /// <summary>
        /// Validates version compatibility between components and their dependencies.
        /// </summary>
        private List<string> ValidateVersionCompatibility(
            List<ComponentDescriptor> descriptors,
            Dictionary<string, ComponentDescriptor> descriptorMap)
        {
            var errors = new List<string>();

            foreach (var descriptor in descriptors)
            {
                if (descriptor.Dependencies == null)
                {
                    continue;
                }

                foreach (var dependency in descriptor.Dependencies)
                {
                    if (!descriptorMap.TryGetValue(dependency, out var dependencyDescriptor))
                    {
                        continue;
                    }

                    // For simple string dependencies, we just check existence
                    // Version checking would require a more complex dependency model
                }
            }

            return errors;
        }

        /// <summary>
        /// Compares semantic versions.
        /// </summary>
        private bool IsVersionCompatible(string actualVersion, string requiredVersion, string operation)
        {
            try
            {
                var actual = ParseVersion(actualVersion);
                var required = ParseVersion(requiredVersion);

                return operation switch
                {
                    ">=" => actual.Major > required.Major ||
                            (actual.Major == required.Major && actual.Minor > required.Minor) ||
                            (actual.Major == required.Major && actual.Minor == required.Minor && actual.Patch >= required.Patch),
                    "<=" => actual.Major < required.Major ||
                            (actual.Major == required.Major && actual.Minor < required.Minor) ||
                            (actual.Major == required.Major && actual.Minor == required.Minor && actual.Patch <= required.Patch),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to compare versions: {Actual} vs {Required}", actualVersion, requiredVersion);
                return !_strictVersioning; // If not strict, allow it
            }
        }

        /// <summary>
        /// Parses a semantic version string.
        /// </summary>
        private (int Major, int Minor, int Patch) ParseVersion(string version)
        {
            var parts = version.Split('.');
            if (parts.Length < 3)
            {
                throw new FormatException($"Invalid version format: {version}");
            }

            // Handle pre-release versions (e.g., 1.0.0-alpha)
            var patchParts = parts[2].Split('-');

            return (
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(patchParts[0])
            );
        }

        /// <summary>
        /// Performs topological sort on the dependency graph.
        /// </summary>
        private List<ComponentDescriptor>? TopologicalSort(DependencyGraph graph, Dictionary<string, ComponentDescriptor> descriptorMap)
        {
            var result = new List<ComponentDescriptor>();
            var visited = new HashSet<string>();
            var stack = new Stack<string>();

            // Perform DFS from each unvisited node
            foreach (var componentId in graph.GetAllComponents())
            {
                if (!visited.Contains(componentId))
                {
                    if (!TopologicalSortDFS(componentId, graph, visited, stack))
                    {
                        return null; // Cycle detected
                    }
                }
            }

            // Build result from stack
            while (stack.Count > 0)
            {
                var componentId = stack.Pop();
                if (descriptorMap.TryGetValue(componentId, out var descriptor))
                {
                    result.Add(descriptor);
                }
            }

            return result;
        }

        /// <summary>
        /// DFS helper for topological sort.
        /// </summary>
        private bool TopologicalSortDFS(string componentId, DependencyGraph graph, HashSet<string> visited, Stack<string> stack)
        {
            visited.Add(componentId);

            foreach (var dependency in graph.GetDependencies(componentId))
            {
                if (!visited.Contains(dependency))
                {
                    if (!TopologicalSortDFS(dependency, graph, visited, stack))
                    {
                        return false;
                    }
                }
            }

            stack.Push(componentId);
            return true;
        }

        /// <summary>
        /// Result of dependency resolution.
        /// </summary>
        public class DependencyResolutionResult
        {
            /// <summary>
            /// Gets a value indicating whether resolution was successful.
            /// </summary>
            public bool Success { get; }

            /// <summary>
            /// Gets the ordered list of components (in initialization order).
            /// </summary>
            public IReadOnlyList<ComponentDescriptor> OrderedComponents { get; }

            /// <summary>
            /// Gets any warnings generated during resolution.
            /// </summary>
            public IReadOnlyList<string> Warnings { get; }

            /// <summary>
            /// Gets the error message if resolution failed.
            /// </summary>
            public string? ErrorMessage { get; }

            public DependencyResolutionResult(
                bool success,
                List<ComponentDescriptor> orderedComponents,
                List<string> warnings,
                string? errorMessage)
            {
                Success = success;
                OrderedComponents = orderedComponents.AsReadOnly();
                Warnings = warnings.AsReadOnly();
                ErrorMessage = errorMessage;
            }
        }
    }
}