using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Core
{
    /// <summary>
    /// Validates component descriptors and classes for compliance with the Conduit framework requirements.
    /// </summary>
    public class ComponentValidator
    {
        private static readonly Regex IdPattern = new Regex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)*$", RegexOptions.Compiled);
        private static readonly Regex VersionPattern = new Regex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9-]+)?$", RegexOptions.Compiled);

        private readonly bool _strictMode;
        private readonly bool _validateServiceContracts;

        /// <summary>
        /// Initializes a new instance of the ComponentValidator class.
        /// </summary>
        /// <param name="strictMode">If true, warnings are treated as errors</param>
        /// <param name="validateServiceContracts">If true, validates service contracts</param>
        public ComponentValidator(bool strictMode = false, bool validateServiceContracts = true)
        {
            _strictMode = strictMode;
            _validateServiceContracts = validateServiceContracts;
        }

        /// <summary>
        /// Validates a component descriptor.
        /// </summary>
        public ValidationResult ValidateDescriptor(ComponentDescriptor descriptor)
        {
            Guard.AgainstNull(descriptor, nameof(descriptor));

            var errors = new List<string>();
            var warnings = new List<string>();

            // Validate ID
            if (string.IsNullOrWhiteSpace(descriptor.ComponentId))
            {
                errors.Add("Component ID is required");
            }
            else if (!IdPattern.IsMatch(descriptor.ComponentId))
            {
                errors.Add($"Component ID '{descriptor.ComponentId}' must be lowercase with dots as separators (e.g., 'com.example.component')");
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(descriptor.Name))
            {
                errors.Add("Component name is required");
            }

            // Validate version
            if (string.IsNullOrWhiteSpace(descriptor.Version))
            {
                errors.Add("Component version is required");
            }
            else if (!VersionPattern.IsMatch(descriptor.Version))
            {
                errors.Add($"Version '{descriptor.Version}' must follow semantic versioning (e.g., '1.0.0')");
            }

            // Validate vendor (warning if missing)
            if (string.IsNullOrWhiteSpace(descriptor.Vendor))
            {
                warnings.Add("Component vendor is recommended");
            }

            // Validate component type
            if (descriptor.ComponentType == null)
            {
                errors.Add("Component type is required");
            }

            // Validate dependencies
            if (descriptor.Dependencies != null)
            {
                foreach (var dependency in descriptor.Dependencies)
                {
                    var depErrors = ValidateDependency(dependency);
                    errors.AddRange(depErrors);
                }
            }

            return new ValidationResult(errors, warnings);
        }

        /// <summary>
        /// Validates a component class for compliance with component requirements.
        /// </summary>
        public ValidationResult ValidateClass(Type componentClass)
        {
            Guard.AgainstNull(componentClass, nameof(componentClass));

            var errors = new List<string>();
            var warnings = new List<string>();

            // Check for Component attribute
            var componentAttr = componentClass.GetCustomAttribute<ComponentAttribute>();
            if (componentAttr == null)
            {
                errors.Add($"Class {componentClass.Name} must be decorated with [Component] attribute");
            }

            // Check class accessibility
            if (!componentClass.IsPublic)
            {
                errors.Add($"Component class {componentClass.Name} must be public");
            }

            // Check for abstract/interface
            if (componentClass.IsAbstract || componentClass.IsInterface)
            {
                errors.Add($"Component class {componentClass.Name} cannot be abstract or an interface");
            }

            // Check for parameterless constructor
            var hasParameterlessConstructor = componentClass.GetConstructor(Type.EmptyTypes) != null;
            var hasDIConstructor = componentClass.GetConstructors()
                .Any(c => c.GetParameters().All(p => p.ParameterType.IsInterface));

            if (!hasParameterlessConstructor && !hasDIConstructor)
            {
                errors.Add($"Component class {componentClass.Name} must have a parameterless constructor or a constructor with only interface parameters");
            }

            // Check if implements IPluggableComponent
            if (!typeof(IPluggableComponent).IsAssignableFrom(componentClass))
            {
                errors.Add($"Component class {componentClass.Name} must implement IPluggableComponent");
            }

            // Validate lifecycle methods
            ValidateLifecycleMethods(componentClass, errors, warnings);

            // Validate service contracts if enabled
            if (_validateServiceContracts)
            {
                ValidateServiceContracts(componentClass, errors, warnings);
            }

            // Check for conflicting attributes
            ValidateAttributes(componentClass, errors, warnings);

            return new ValidationResult(errors, warnings, _strictMode);
        }

        /// <summary>
        /// Validates a set of component descriptors for conflicts and circular dependencies.
        /// </summary>
        public ValidationResult ValidateSet(IEnumerable<ComponentDescriptor> descriptors)
        {
            Guard.AgainstNull(descriptors, nameof(descriptors));

            var descriptorList = descriptors.ToList();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Check for duplicate IDs
            var duplicateIds = descriptorList
                .GroupBy(d => d.ComponentId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var id in duplicateIds)
            {
                errors.Add($"Duplicate component ID found: {id}");
            }

            // Check for circular dependencies
            var circularDeps = DetectCircularDependencies(descriptorList);
            errors.AddRange(circularDeps);

            // Validate each descriptor individually
            foreach (var descriptor in descriptorList)
            {
                var result = ValidateDescriptor(descriptor);
                errors.AddRange(result.Errors);
                warnings.AddRange(result.Warnings);
            }

            return new ValidationResult(errors, warnings);
        }

        private List<string> ValidateDependency(ComponentDescriptor.ComponentDependency dependency)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dependency.ComponentId))
            {
                errors.Add("Dependency component ID is required");
            }
            else if (!IdPattern.IsMatch(dependency.ComponentId))
            {
                errors.Add($"Dependency ID '{dependency.ComponentId}' must be lowercase with dots as separators");
            }

            if (!string.IsNullOrWhiteSpace(dependency.MinVersion) && !VersionPattern.IsMatch(dependency.MinVersion))
            {
                errors.Add($"Dependency minimum version '{dependency.MinVersion}' must follow semantic versioning");
            }

            if (!string.IsNullOrWhiteSpace(dependency.MaxVersion) && !VersionPattern.IsMatch(dependency.MaxVersion))
            {
                errors.Add($"Dependency maximum version '{dependency.MaxVersion}' must follow semantic versioning");
            }

            return errors;
        }

        private void ValidateLifecycleMethods(Type componentClass, List<string> errors, List<string> warnings)
        {
            // Check for Initialize method
            var initMethod = componentClass.GetMethod("InitializeAsync", BindingFlags.Public | BindingFlags.Instance);
            if (initMethod == null)
            {
                warnings.Add($"Component {componentClass.Name} should implement InitializeAsync method");
            }
            else if (initMethod.ReturnType != typeof(Task))
            {
                errors.Add($"InitializeAsync method in {componentClass.Name} must return Task");
            }

            // Check for Shutdown method
            var shutdownMethod = componentClass.GetMethod("ShutdownAsync", BindingFlags.Public | BindingFlags.Instance);
            if (shutdownMethod == null)
            {
                warnings.Add($"Component {componentClass.Name} should implement ShutdownAsync method");
            }
            else if (shutdownMethod.ReturnType != typeof(Task))
            {
                errors.Add($"ShutdownAsync method in {componentClass.Name} must return Task");
            }
        }

        private void ValidateServiceContracts(Type componentClass, List<string> errors, List<string> warnings)
        {
            var interfaces = componentClass.GetInterfaces()
                .Where(i => i != typeof(IPluggableComponent) && !i.IsGenericType);

            if (!interfaces.Any())
            {
                warnings.Add($"Component {componentClass.Name} does not expose any service contracts (interfaces)");
            }

            // Check for [ServiceContract] attributes on interfaces
            foreach (var iface in interfaces)
            {
                var attr = iface.GetCustomAttribute<ServiceContractAttribute>();
                if (attr == null)
                {
                    warnings.Add($"Interface {iface.Name} should be decorated with [ServiceContract] attribute");
                }
            }
        }

        private void ValidateAttributes(Type componentClass, List<string> errors, List<string> warnings)
        {
            var attributes = componentClass.GetCustomAttributes(false);

            // Check for obsolete usage
            if (attributes.OfType<ObsoleteAttribute>().Any())
            {
                warnings.Add($"Component {componentClass.Name} is marked as obsolete");
            }

            // Check for multiple Component attributes (shouldn't happen but just in case)
            var componentAttrs = attributes.OfType<ComponentAttribute>().Count();
            if (componentAttrs > 1)
            {
                errors.Add($"Component {componentClass.Name} has multiple [Component] attributes");
            }
        }

        private List<string> DetectCircularDependencies(List<ComponentDescriptor> descriptors)
        {
            var errors = new List<string>();
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();
            var descriptorMap = descriptors.ToDictionary(d => d.ComponentId);

            foreach (var descriptor in descriptors)
            {
                if (!visited.Contains(descriptor.ComponentId))
                {
                    var path = new List<string>();
                    if (HasCircularDependency(descriptor.ComponentId, descriptorMap, visited, recursionStack, path))
                    {
                        errors.Add($"Circular dependency detected: {string.Join(" -> ", path)}");
                    }
                }
            }

            return errors;
        }

        private bool HasCircularDependency(
            string componentId,
            Dictionary<string, ComponentDescriptor> descriptorMap,
            HashSet<string> visited,
            HashSet<string> recursionStack,
            List<string> path)
        {
            visited.Add(componentId);
            recursionStack.Add(componentId);
            path.Add(componentId);

            if (descriptorMap.TryGetValue(componentId, out var descriptor) && descriptor.Dependencies != null)
            {
                foreach (var dependency in descriptor.Dependencies)
                {
                    if (recursionStack.Contains(dependency.ComponentId))
                    {
                        path.Add(dependency.ComponentId);
                        return true;
                    }

                    if (!visited.Contains(dependency.ComponentId))
                    {
                        if (HasCircularDependency(dependency.ComponentId, descriptorMap, visited, recursionStack, path))
                        {
                            return true;
                        }
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
        /// Represents the result of a validation operation.
        /// </summary>
        public class ValidationResult
        {
            public IReadOnlyList<string> Errors { get; }
            public IReadOnlyList<string> Warnings { get; }
            public bool IsValid { get; }
            public bool HasWarnings => Warnings.Count > 0;

            public ValidationResult(List<string> errors, List<string> warnings, bool strictMode = false)
            {
                Errors = errors?.AsReadOnly() ?? new List<string>().AsReadOnly();
                Warnings = warnings?.AsReadOnly() ?? new List<string>().AsReadOnly();
                IsValid = Errors.Count == 0 && (!strictMode || Warnings.Count == 0);
            }

            public override string ToString()
            {
                if (IsValid && !HasWarnings)
                {
                    return "Validation successful";
                }

                var messages = new List<string>();

                if (Errors.Count > 0)
                {
                    messages.Add($"Errors ({Errors.Count}):");
                    messages.AddRange(Errors.Select(e => $"  - {e}"));
                }

                if (Warnings.Count > 0)
                {
                    messages.Add($"Warnings ({Warnings.Count}):");
                    messages.AddRange(Warnings.Select(w => $"  - {w}"));
                }

                return string.Join(Environment.NewLine, messages);
            }
        }
    }
}