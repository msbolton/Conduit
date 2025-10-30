namespace Conduit.Components
{
    /// <summary>
    /// Defines the placement type for behaviors in the pipeline.
    /// </summary>
    public enum BehaviorPlacementType
    {
        /// <summary>
        /// Execute first in the pipeline.
        /// </summary>
        First,

        /// <summary>
        /// Execute at a specific order position.
        /// </summary>
        Ordered,

        /// <summary>
        /// Execute before a specific behavior.
        /// </summary>
        Before,

        /// <summary>
        /// Execute after a specific behavior.
        /// </summary>
        After,

        /// <summary>
        /// Execute last in the pipeline.
        /// </summary>
        Last
    }
}