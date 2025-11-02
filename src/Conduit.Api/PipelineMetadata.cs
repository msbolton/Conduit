using System.Collections.Generic;

namespace Conduit.Api;

/// <summary>
/// Metadata about a pipeline.
/// </summary>
public class PipelineMetadata
{
    /// <summary>
    /// Gets the unique pipeline identifier.
    /// </summary>
    public string PipelineId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the pipeline description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the pipeline type.
    /// </summary>
    public PipelineType Type { get; init; } = PipelineType.Custom;

    /// <summary>
    /// Gets the pipeline version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the list of stages in this pipeline.
    /// </summary>
    public IList<string> Stages { get; init; } = new List<string>();

    /// <summary>
    /// Creates a new builder for constructing pipeline metadata.
    /// </summary>
    /// <returns>A new pipeline metadata builder</returns>
    public static PipelineMetadataBuilder Builder() => new PipelineMetadataBuilder();
}

/// <summary>
/// Builder for constructing pipeline metadata.
/// </summary>
public class PipelineMetadataBuilder
{
    private string _name = string.Empty;
    private string? _description;
    private PipelineType _type = PipelineType.Custom;
    private string _version = "1.0.0";
    private readonly List<string> _stages = new List<string>();

    /// <summary>
    /// Sets the pipeline name.
    /// </summary>
    public PipelineMetadataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the pipeline description.
    /// </summary>
    public PipelineMetadataBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the pipeline type.
    /// </summary>
    public PipelineMetadataBuilder WithType(PipelineType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// Sets the pipeline version.
    /// </summary>
    public PipelineMetadataBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Adds a stage to the pipeline.
    /// </summary>
    public PipelineMetadataBuilder AddStage(string stageName)
    {
        _stages.Add(stageName);
        return this;
    }

    /// <summary>
    /// Sets multiple pipeline types (for compatibility).
    /// </summary>
    public PipelineMetadataBuilder WithTypes(params PipelineType[] types)
    {
        if (types.Length > 0)
        {
            _type = types[0]; // Use the first type
        }
        return this;
    }

    /// <summary>
    /// Adds tags to the pipeline metadata (for compatibility).
    /// </summary>
    public PipelineMetadataBuilder WithTags(params string[] tags)
    {
        // Tags could be stored in Description or a future Tags property
        if (tags.Length > 0)
        {
            _description = (_description ?? "") + " Tags: " + string.Join(", ", tags);
        }
        return this;
    }

    /// <summary>
    /// Sets a property on the pipeline metadata (for compatibility).
    /// </summary>
    public PipelineMetadataBuilder WithProperty(string key, object value)
    {
        // Properties could be stored in Description or a future Properties dictionary
        _description = (_description ?? "") + $" {key}: {value}";
        return this;
    }

    /// <summary>
    /// Builds the pipeline metadata.
    /// </summary>
    public PipelineMetadata Build() => new PipelineMetadata
    {
        Name = _name,
        Description = _description,
        Type = _type,
        Version = _version,
        Stages = _stages
    };
}

/// <summary>
/// Defines the different types of pipelines.
/// </summary>
public enum PipelineType
{
    /// <summary>
    /// Custom pipeline type.
    /// </summary>
    Custom = 0,

    /// <summary>
    /// Sequential pipeline type.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// Parallel pipeline type.
    /// </summary>
    Parallel = 2,

    /// <summary>
    /// Branching pipeline type.
    /// </summary>
    Branch = 3,

    /// <summary>
    /// Filtering pipeline type.
    /// </summary>
    Filter = 4,

    /// <summary>
    /// Mapping pipeline type.
    /// </summary>
    Map = 5,

    /// <summary>
    /// Caching pipeline type.
    /// </summary>
    Cache = 6,

    /// <summary>
    /// Conditional pipeline type.
    /// </summary>
    Conditional = 7,

    /// <summary>
    /// Transformation pipeline type.
    /// </summary>
    Transformation = 8,

    /// <summary>
    /// Saga orchestration pipeline type.
    /// </summary>
    Saga = 9,

    /// <summary>
    /// Event-driven pipeline type.
    /// </summary>
    EventDriven = 10,

    /// <summary>
    /// Stream processing pipeline type.
    /// </summary>
    Stream = 11,

    /// <summary>
    /// Batch processing pipeline type.
    /// </summary>
    Batch = 12,

    /// <summary>
    /// Validation pipeline type.
    /// </summary>
    Validation = 13
}