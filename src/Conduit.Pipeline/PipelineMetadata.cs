using System;
using System.Collections.Generic;

namespace Conduit.Pipeline;

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
    /// Gets the pipeline author.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the last modified timestamp.
    /// </summary>
    public DateTimeOffset? ModifiedAt { get; init; }

    /// <summary>
    /// Gets the pipeline tags.
    /// </summary>
    public HashSet<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets custom metadata properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>
    /// Gets the input type name.
    /// </summary>
    public string? InputType { get; init; }

    /// <summary>
    /// Gets the output type name.
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// Gets whether the pipeline is enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets whether the pipeline is deprecated.
    /// </summary>
    public bool IsDeprecated { get; init; } = false;

    /// <summary>
    /// Gets the deprecation message if deprecated.
    /// </summary>
    public string? DeprecationMessage { get; init; }

    /// <summary>
    /// Creates a new builder for PipelineMetadata.
    /// </summary>
    public static PipelineMetadataBuilder Builder() => new();

    /// <summary>
    /// Creates metadata with minimal configuration.
    /// </summary>
    public static PipelineMetadata Create(string name, PipelineType type = PipelineType.Custom)
    {
        return new PipelineMetadata
        {
            Name = name,
            Type = type
        };
    }

    /// <summary>
    /// Returns a string representation of the metadata.
    /// </summary>
    public override string ToString()
    {
        return $"Pipeline[{Name} v{Version}, Type={Type}, Id={PipelineId}]";
    }
}

/// <summary>
/// Types of pipelines.
/// </summary>
public enum PipelineType
{
    /// <summary>
    /// Custom user-defined pipeline.
    /// </summary>
    Custom,

    /// <summary>
    /// Sequential processing pipeline.
    /// </summary>
    Sequential,

    /// <summary>
    /// Parallel processing pipeline.
    /// </summary>
    Parallel,

    /// <summary>
    /// Event-driven pipeline.
    /// </summary>
    EventDriven,

    /// <summary>
    /// Batch processing pipeline.
    /// </summary>
    Batch,

    /// <summary>
    /// Stream processing pipeline.
    /// </summary>
    Stream,

    /// <summary>
    /// Conditional/branching pipeline.
    /// </summary>
    Conditional,

    /// <summary>
    /// Saga/workflow pipeline.
    /// </summary>
    Saga,

    /// <summary>
    /// Validation pipeline.
    /// </summary>
    Validation,

    /// <summary>
    /// Transformation pipeline.
    /// </summary>
    Transformation
}

/// <summary>
/// Builder for creating PipelineMetadata.
/// </summary>
public class PipelineMetadataBuilder
{
    private string _pipelineId = Guid.NewGuid().ToString();
    private string _name = string.Empty;
    private string? _description;
    private PipelineType _type = PipelineType.Custom;
    private string _version = "1.0.0";
    private string? _author;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _modifiedAt;
    private readonly HashSet<string> _tags = new();
    private readonly Dictionary<string, object> _properties = new();
    private string? _inputType;
    private string? _outputType;
    private bool _isEnabled = true;
    private bool _isDeprecated;
    private string? _deprecationMessage;

    /// <summary>
    /// Sets the pipeline ID.
    /// </summary>
    public PipelineMetadataBuilder WithId(string id)
    {
        _pipelineId = id ?? throw new ArgumentNullException(nameof(id));
        return this;
    }

    /// <summary>
    /// Sets the pipeline name.
    /// </summary>
    public PipelineMetadataBuilder WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
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
        _version = version ?? "1.0.0";
        return this;
    }

    /// <summary>
    /// Sets the pipeline author.
    /// </summary>
    public PipelineMetadataBuilder WithAuthor(string author)
    {
        _author = author;
        return this;
    }

    /// <summary>
    /// Sets the creation timestamp.
    /// </summary>
    public PipelineMetadataBuilder CreatedAt(DateTimeOffset timestamp)
    {
        _createdAt = timestamp;
        return this;
    }

    /// <summary>
    /// Sets the modification timestamp.
    /// </summary>
    public PipelineMetadataBuilder ModifiedAt(DateTimeOffset timestamp)
    {
        _modifiedAt = timestamp;
        return this;
    }

    /// <summary>
    /// Adds tags to the pipeline.
    /// </summary>
    public PipelineMetadataBuilder WithTags(params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _tags.Add(tag);
            }
        }
        return this;
    }

    /// <summary>
    /// Adds a custom property.
    /// </summary>
    public PipelineMetadataBuilder WithProperty(string key, object value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the input and output types.
    /// </summary>
    public PipelineMetadataBuilder WithTypes<TInput, TOutput>()
    {
        _inputType = typeof(TInput).FullName;
        _outputType = typeof(TOutput).FullName;
        return this;
    }

    /// <summary>
    /// Sets the input and output type names.
    /// </summary>
    public PipelineMetadataBuilder WithTypes(string inputType, string outputType)
    {
        _inputType = inputType;
        _outputType = outputType;
        return this;
    }

    /// <summary>
    /// Sets whether the pipeline is enabled.
    /// </summary>
    public PipelineMetadataBuilder Enabled(bool enabled = true)
    {
        _isEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Marks the pipeline as deprecated.
    /// </summary>
    public PipelineMetadataBuilder Deprecated(string? message = null)
    {
        _isDeprecated = true;
        _deprecationMessage = message;
        return this;
    }

    /// <summary>
    /// Builds the PipelineMetadata.
    /// </summary>
    public PipelineMetadata Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
            throw new InvalidOperationException("Pipeline name is required");

        return new PipelineMetadata
        {
            PipelineId = _pipelineId,
            Name = _name,
            Description = _description,
            Type = _type,
            Version = _version,
            Author = _author,
            CreatedAt = _createdAt,
            ModifiedAt = _modifiedAt,
            Tags = _tags,
            Properties = _properties,
            InputType = _inputType,
            OutputType = _outputType,
            IsEnabled = _isEnabled,
            IsDeprecated = _isDeprecated,
            DeprecationMessage = _deprecationMessage
        };
    }
}