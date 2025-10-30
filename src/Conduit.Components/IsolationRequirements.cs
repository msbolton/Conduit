namespace Conduit.Components
{
    /// <summary>
    /// Defines isolation requirements for a component.
    /// </summary>
    public class IsolationRequirements
    {
        /// <summary>
        /// Gets or sets whether the component requires process isolation.
        /// </summary>
        public bool RequiresProcessIsolation { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the component requires assembly isolation.
        /// </summary>
        public bool RequiresAssemblyIsolation { get; set; } = false;

        /// <summary>
        /// Gets or sets whether the component requires memory isolation.
        /// </summary>
        public bool RequiresMemoryIsolation { get; set; } = false;

        /// <summary>
        /// Gets or sets the security isolation level.
        /// </summary>
        public SecurityIsolationLevel SecurityLevel { get; set; } = SecurityIsolationLevel.Normal;

        /// <summary>
        /// Gets or sets whether the component can share resources with other components.
        /// </summary>
        public bool AllowResourceSharing { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum memory allocation for the component.
        /// </summary>
        public long? MaxMemoryAllocation { get; set; }
    }

    /// <summary>
    /// Security isolation levels for components.
    /// </summary>
    public enum SecurityIsolationLevel
    {
        /// <summary>
        /// Normal security level - standard isolation.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Elevated security level - additional isolation measures.
        /// </summary>
        Elevated = 1,

        /// <summary>
        /// Critical security level - maximum isolation.
        /// </summary>
        Critical = 2
    }
}