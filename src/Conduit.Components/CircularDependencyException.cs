using System;

namespace Conduit.Components
{
    /// <summary>
    /// Exception thrown when a circular dependency is detected in component initialization.
    /// </summary>
    public class CircularDependencyException : Exception
    {
        /// <summary>
        /// Gets the dependency chain that forms the circular reference.
        /// </summary>
        public string[] DependencyChain { get; }

        /// <summary>
        /// Initializes a new instance of CircularDependencyException.
        /// </summary>
        public CircularDependencyException(string message) : base(message)
        {
            DependencyChain = Array.Empty<string>();
        }

        /// <summary>
        /// Initializes a new instance of CircularDependencyException.
        /// </summary>
        public CircularDependencyException(string message, string[] dependencyChain) : base(message)
        {
            DependencyChain = dependencyChain;
        }

        /// <summary>
        /// Initializes a new instance of CircularDependencyException.
        /// </summary>
        public CircularDependencyException(string message, Exception innerException) : base(message, innerException)
        {
            DependencyChain = Array.Empty<string>();
        }
    }
}