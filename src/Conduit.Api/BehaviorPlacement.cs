namespace Conduit.Api;

/// <summary>
/// Defines placement rules for behaviors in the pipeline.
/// </summary>
public class BehaviorPlacement
{
    /// <summary>
    /// Gets or sets the placement strategy.
    /// </summary>
    public PlacementStrategy Strategy { get; set; } = PlacementStrategy.Default;

    /// <summary>
    /// Gets or sets the order value for ordered placement.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the reference behavior ID for relative placement.
    /// </summary>
    public string? ReferenceBehaviorId { get; set; }

    /// <summary>
    /// Gets or sets the stage where the behavior should be placed.
    /// </summary>
    public PipelineStage Stage { get; set; } = PipelineStage.Processing;

    /// <summary>
    /// Gets or sets a value indicating whether this behavior can be reordered.
    /// </summary>
    public bool CanReorder { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this behavior is critical and cannot be skipped.
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Creates a placement at the beginning of the pipeline.
    /// </summary>
    public static BehaviorPlacement First()
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.First,
            Order = int.MinValue,
            CanReorder = false
        };
    }

    /// <summary>
    /// Creates a placement at the end of the pipeline.
    /// </summary>
    public static BehaviorPlacement Last()
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Last,
            Order = int.MaxValue,
            CanReorder = false
        };
    }

    /// <summary>
    /// Creates a placement with specific order.
    /// </summary>
    public static BehaviorPlacement Ordered(int order)
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Ordered,
            Order = order
        };
    }

    /// <summary>
    /// Creates a placement before a specific behavior.
    /// </summary>
    public static BehaviorPlacement Before(string behaviorId)
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Before,
            ReferenceBehaviorId = behaviorId
        };
    }

    /// <summary>
    /// Creates a placement after a specific behavior.
    /// </summary>
    public static BehaviorPlacement After(string behaviorId)
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.After,
            ReferenceBehaviorId = behaviorId
        };
    }

    /// <summary>
    /// Creates a placement at a specific stage.
    /// </summary>
    public static BehaviorPlacement AtStage(PipelineStage stage, int order = 0)
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Stage,
            Stage = stage,
            Order = order
        };
    }

    /// <summary>
    /// Creates a default placement.
    /// </summary>
    public static BehaviorPlacement Default()
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Default,
            Order = 0
        };
    }
}

/// <summary>
/// Defines placement strategies for behaviors.
/// </summary>
public enum PlacementStrategy
{
    /// <summary>
    /// Default placement based on priority.
    /// </summary>
    Default,

    /// <summary>
    /// Place at the beginning.
    /// </summary>
    First,

    /// <summary>
    /// Place at the end.
    /// </summary>
    Last,

    /// <summary>
    /// Place before a specific behavior.
    /// </summary>
    Before,

    /// <summary>
    /// Place after a specific behavior.
    /// </summary>
    After,

    /// <summary>
    /// Place at a specific order.
    /// </summary>
    Ordered,

    /// <summary>
    /// Place at a specific pipeline stage.
    /// </summary>
    Stage,

    /// <summary>
    /// Replace an existing behavior.
    /// </summary>
    Replace
}

/// <summary>
/// Defines pipeline stages.
/// </summary>
public enum PipelineStage
{
    /// <summary>
    /// Authentication and authorization stage.
    /// </summary>
    Authentication,

    /// <summary>
    /// Input validation stage.
    /// </summary>
    Validation,

    /// <summary>
    /// Pre-processing stage.
    /// </summary>
    PreProcessing,

    /// <summary>
    /// Main processing stage.
    /// </summary>
    Processing,

    /// <summary>
    /// Post-processing stage.
    /// </summary>
    PostProcessing,

    /// <summary>
    /// Response transformation stage.
    /// </summary>
    Transformation,

    /// <summary>
    /// Logging and metrics stage.
    /// </summary>
    Telemetry,

    /// <summary>
    /// Error handling stage.
    /// </summary>
    ErrorHandling
}