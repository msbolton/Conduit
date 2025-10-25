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

    /// <summary>
    /// Represents a behavior contribution with metadata and placement rules.
    /// </summary>
    public class BehaviorContribution
    {
        /// <summary>
        /// Gets or sets the unique identifier for this behavior.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the display name of the behavior.
        /// </summary>
        public string Name { get; set; } = "Behavior";

        /// <summary>
        /// Gets or sets the description of the behavior.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the behavior implementation.
        /// </summary>
        public IPipelineBehavior? Behavior { get; set; }

        /// <summary>
        /// Gets or sets the execution phase.
        /// </summary>
        public BehaviorPhase Phase { get; set; } = BehaviorPhase.Processing;

        /// <summary>
        /// Gets or sets the priority within the phase (lower values execute first).
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether this behavior is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a function to dynamically determine if the behavior is enabled.
        /// </summary>
        public Func<bool>? EnabledWhen { get; set; }

        /// <summary>
        /// Gets or sets the placement strategy for this behavior.
        /// </summary>
        public BehaviorPlacement? Placement { get; set; }

        /// <summary>
        /// Gets or sets tags for categorizing the behavior.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new HashSet<string>();

        /// <summary>
        /// Gets or sets constraints for this behavior.
        /// </summary>
        public BehaviorConstraints? Constraints { get; set; }

        /// <summary>
        /// Determines if this behavior should be executed.
        /// </summary>
        /// <returns>True if the behavior should execute</returns>
        public bool ShouldExecute()
        {
            if (!Enabled)
                return false;

            return EnabledWhen?.Invoke() ?? true;
        }

        /// <summary>
        /// Creates a new behavior contribution builder.
        /// </summary>
        /// <returns>A new builder instance</returns>
        public static BehaviorContributionBuilder Builder()
        {
            return new BehaviorContributionBuilder();
        }
    }

    /// <summary>
    /// Builder for creating behavior contributions.
    /// </summary>
    public class BehaviorContributionBuilder
    {
        private readonly BehaviorContribution _contribution;

        public BehaviorContributionBuilder()
        {
            _contribution = new BehaviorContribution();
        }

        public BehaviorContributionBuilder WithId(string id)
        {
            _contribution.Id = id;
            return this;
        }

        public BehaviorContributionBuilder WithName(string name)
        {
            _contribution.Name = name;
            return this;
        }

        public BehaviorContributionBuilder WithDescription(string description)
        {
            _contribution.Description = description;
            return this;
        }

        public BehaviorContributionBuilder WithBehavior(IPipelineBehavior behavior)
        {
            _contribution.Behavior = behavior;
            return this;
        }

        public BehaviorContributionBuilder WithPhase(BehaviorPhase phase)
        {
            _contribution.Phase = phase;
            return this;
        }

        public BehaviorContributionBuilder WithPriority(int priority)
        {
            _contribution.Priority = priority;
            return this;
        }

        public BehaviorContributionBuilder WithEnabled(bool enabled)
        {
            _contribution.Enabled = enabled;
            return this;
        }

        public BehaviorContributionBuilder WithEnabledWhen(Func<bool> condition)
        {
            _contribution.EnabledWhen = condition;
            return this;
        }

        public BehaviorContributionBuilder WithPlacement(BehaviorPlacement placement)
        {
            _contribution.Placement = placement;
            return this;
        }

        public BehaviorContributionBuilder WithTags(params string[] tags)
        {
            foreach (var tag in tags)
            {
                _contribution.Tags.Add(tag);
            }
            return this;
        }

        public BehaviorContributionBuilder WithConstraints(BehaviorConstraints constraints)
        {
            _contribution.Constraints = constraints;
            return this;
        }

        public BehaviorContribution Build()
        {
            if (_contribution.Behavior == null)
            {
                throw new InvalidOperationException("Behavior implementation is required");
            }

            return _contribution;
        }
    }

    /// <summary>
    /// Defines placement strategy for behaviors in the pipeline.
    /// </summary>
    public class BehaviorPlacement
    {
        /// <summary>
        /// Gets the placement strategy.
        /// </summary>
        public PlacementStrategy Strategy { get; }

        /// <summary>
        /// Gets the reference behavior ID (for Before/After/Replace strategies).
        /// </summary>
        public string? ReferenceBehaviorId { get; }

        /// <summary>
        /// Gets the numeric order (for Ordered strategy).
        /// </summary>
        public int Order { get; }

        private BehaviorPlacement(PlacementStrategy strategy, string? referenceBehaviorId = null, int order = 0)
        {
            Strategy = strategy;
            ReferenceBehaviorId = referenceBehaviorId;
            Order = order;
        }

        /// <summary>
        /// Places this behavior before the specified behavior.
        /// </summary>
        public static BehaviorPlacement Before(string behaviorId) =>
            new BehaviorPlacement(PlacementStrategy.Before, behaviorId);

        /// <summary>
        /// Places this behavior after the specified behavior.
        /// </summary>
        public static BehaviorPlacement After(string behaviorId) =>
            new BehaviorPlacement(PlacementStrategy.After, behaviorId);

        /// <summary>
        /// Replaces the specified behavior.
        /// </summary>
        public static BehaviorPlacement Replace(string behaviorId) =>
            new BehaviorPlacement(PlacementStrategy.Replace, behaviorId);

        /// <summary>
        /// Places this behavior first in the pipeline.
        /// </summary>
        public static BehaviorPlacement First() =>
            new BehaviorPlacement(PlacementStrategy.First);

        /// <summary>
        /// Places this behavior last in the pipeline.
        /// </summary>
        public static BehaviorPlacement Last() =>
            new BehaviorPlacement(PlacementStrategy.Last);

        /// <summary>
        /// Places this behavior at a specific numeric order.
        /// </summary>
        public static BehaviorPlacement Ordered(int order) =>
            new BehaviorPlacement(PlacementStrategy.Ordered, order: order);

        /// <summary>
        /// Placement strategies.
        /// </summary>
        public enum PlacementStrategy
        {
            Before,
            After,
            Replace,
            First,
            Last,
            Ordered
        }
    }

    /// <summary>
    /// Defines constraints for behavior execution.
    /// </summary>
    public class BehaviorConstraints
    {
        /// <summary>
        /// Gets or sets behaviors that must be present for this behavior to execute.
        /// </summary>
        public HashSet<string> RequiredBehaviors { get; set; } = new HashSet<string>();

        /// <summary>
        /// Gets or sets behaviors that must not be present for this behavior to execute.
        /// </summary>
        public HashSet<string> ConflictingBehaviors { get; set; } = new HashSet<string>();

        /// <summary>
        /// Gets or sets tags that must be present in the pipeline.
        /// </summary>
        public HashSet<string> RequiredTags { get; set; } = new HashSet<string>();

        /// <summary>
        /// Gets or sets the minimum pipeline version required.
        /// </summary>
        public string? MinimumPipelineVersion { get; set; }

        /// <summary>
        /// Gets or sets the maximum pipeline version supported.
        /// </summary>
        public string? MaximumPipelineVersion { get; set; }
    }

    /// <summary>
    /// Interface for pipeline behaviors.
    /// </summary>
    public interface IPipelineBehavior
    {
        /// <summary>
        /// Executes the behavior logic.
        /// </summary>
        /// <param name="context">The pipeline context</param>
        /// <param name="next">The next behavior in the chain</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result of the behavior execution</returns>
        Task<object?> ExecuteAsync(PipelineContext context, Func<Task<object?>> next, CancellationToken cancellationToken = default);
    }
}