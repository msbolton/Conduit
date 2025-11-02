namespace Conduit.Components
{
    /// <summary>
    /// Defines the scope/lifetime of a component instance.
    /// </summary>
    public enum ComponentScope
    {
        /// <summary>
        /// Component is created as a singleton (single instance).
        /// </summary>
        Singleton,

        /// <summary>
        /// Component is created as transient (new instance each time).
        /// </summary>
        Transient,

        /// <summary>
        /// Component is scoped to a specific context or request.
        /// </summary>
        Scoped
    }
}