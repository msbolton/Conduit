using System;

namespace Conduit.Pipeline.Behaviors
{
    /// <summary>
    /// Specifies the execution phase of a pipeline behavior.
    /// </summary>
    public enum BehaviorPhase
    {
        /// <summary>
        /// Executed before the main processing phase.
        /// </summary>
        PreProcessing = 0,

        /// <summary>
        /// The main processing phase.
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Executed after the main processing phase.
        /// </summary>
        PostProcessing = 2
    }

    /// <summary>
    /// Attribute to specify the execution phase of a pipeline behavior.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class BehaviorPhaseAttribute : Attribute
    {
        /// <summary>
        /// Gets the behavior phase.
        /// </summary>
        public BehaviorPhase Phase { get; }

        /// <summary>
        /// Initializes a new instance of the BehaviorPhaseAttribute class.
        /// </summary>
        /// <param name="phase">The execution phase</param>
        public BehaviorPhaseAttribute(BehaviorPhase phase = BehaviorPhase.Processing)
        {
            Phase = phase;
        }
    }




}