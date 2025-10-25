using System;
using System.Collections.Generic;
using System.Linq;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Represents a behavior contribution to the pipeline with metadata and placement rules.
/// </summary>
public class BehaviorContribution
{
    /// <summary>
    /// Gets the unique identifier for this behavior.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the display name of this behavior.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the description of what this behavior does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the behavior implementation.
    /// </summary>
    public IPipelineBehavior Behavior { get; init; } = null!;

    /// <summary>
    /// Gets the placement rules for this behavior.
    /// </summary>
    public BehaviorPlacement Placement { get; init; } = BehaviorPlacement.Last();

    /// <summary>
    /// Gets the tags associated with this behavior.
    /// </summary>
    public HashSet<string> Tags { get; init; } = new();

    /// <summary>
    /// Gets the constraints for when this behavior should execute.
    /// </summary>
    public BehaviorConstraints Constraints { get; init; } = BehaviorConstraints.None();

    /// <summary>
    /// Gets the execution phase for this behavior.
    /// </summary>
    public BehaviorPhase Phase { get; init; } = BehaviorPhase.Processing;

    /// <summary>
    /// Gets the priority within the phase (lower values execute first).
    /// </summary>
    public int Priority { get; init; } = 1000;

    /// <summary>
    /// Gets whether this behavior is enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets a dynamic enablement function.
    /// </summary>
    public Func<bool>? EnabledPredicate { get; init; }

    /// <summary>
    /// Determines if this behavior is currently enabled.
    /// </summary>
    public bool IsCurrentlyEnabled()
    {
        return IsEnabled && (EnabledPredicate?.Invoke() ?? true);
    }

    /// <summary>
    /// Creates a new builder for BehaviorContribution.
    /// </summary>
    public static BehaviorContributionBuilder Builder() => new();

    /// <summary>
    /// Creates a behavior contribution with minimal configuration.
    /// </summary>
    public static BehaviorContribution Create(string id, IPipelineBehavior behavior)
    {
        return new BehaviorContribution
        {
            Id = id,
            Name = id,
            Behavior = behavior
        };
    }
}

/// <summary>
/// Builder for creating BehaviorContribution instances.
/// </summary>
public class BehaviorContributionBuilder
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string? _description;
    private IPipelineBehavior? _behavior;
    private BehaviorPlacement _placement = BehaviorPlacement.Last();
    private readonly HashSet<string> _tags = new();
    private BehaviorConstraints _constraints = BehaviorConstraints.None();
    private BehaviorPhase _phase = BehaviorPhase.Processing;
    private int _priority = 1000;
    private bool _isEnabled = true;
    private Func<bool>? _enabledPredicate;

    /// <summary>
    /// Sets the unique identifier.
    /// </summary>
    public BehaviorContributionBuilder WithId(string id)
    {
        _id = id ?? throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrWhiteSpace(_name))
        {
            _name = id;
        }
        return this;
    }

    /// <summary>
    /// Sets the display name.
    /// </summary>
    public BehaviorContributionBuilder WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <summary>
    /// Sets the description.
    /// </summary>
    public BehaviorContributionBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Sets the behavior implementation.
    /// </summary>
    public BehaviorContributionBuilder WithBehavior(IPipelineBehavior behavior)
    {
        _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        return this;
    }

    /// <summary>
    /// Sets the behavior from a delegate.
    /// </summary>
    public BehaviorContributionBuilder WithBehavior(PipelineBehaviorDelegate behaviorDelegate)
    {
        _behavior = new DelegatingBehavior(behaviorDelegate);
        return this;
    }

    /// <summary>
    /// Sets the placement rules.
    /// </summary>
    public BehaviorContributionBuilder WithPlacement(BehaviorPlacement placement)
    {
        _placement = placement ?? throw new ArgumentNullException(nameof(placement));
        return this;
    }

    /// <summary>
    /// Adds tags to the behavior.
    /// </summary>
    public BehaviorContributionBuilder WithTags(params string[] tags)
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
    /// Sets the constraints.
    /// </summary>
    public BehaviorContributionBuilder WithConstraints(BehaviorConstraints constraints)
    {
        _constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        return this;
    }

    /// <summary>
    /// Sets the execution phase.
    /// </summary>
    public BehaviorContributionBuilder WithPhase(BehaviorPhase phase)
    {
        _phase = phase;
        return this;
    }

    /// <summary>
    /// Sets the priority.
    /// </summary>
    public BehaviorContributionBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>
    /// Sets whether the behavior is enabled.
    /// </summary>
    public BehaviorContributionBuilder Enabled(bool enabled = true)
    {
        _isEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets a dynamic enablement predicate.
    /// </summary>
    public BehaviorContributionBuilder EnabledWhen(Func<bool> predicate)
    {
        _enabledPredicate = predicate;
        return this;
    }

    /// <summary>
    /// Places this behavior first in the pipeline.
    /// </summary>
    public BehaviorContributionBuilder PlaceFirst()
    {
        _placement = BehaviorPlacement.First();
        return this;
    }

    /// <summary>
    /// Places this behavior last in the pipeline.
    /// </summary>
    public BehaviorContributionBuilder PlaceLast()
    {
        _placement = BehaviorPlacement.Last();
        return this;
    }

    /// <summary>
    /// Places this behavior before another behavior.
    /// </summary>
    public BehaviorContributionBuilder PlaceBefore(string behaviorId)
    {
        _placement = BehaviorPlacement.Before(behaviorId);
        return this;
    }

    /// <summary>
    /// Places this behavior after another behavior.
    /// </summary>
    public BehaviorContributionBuilder PlaceAfter(string behaviorId)
    {
        _placement = BehaviorPlacement.After(behaviorId);
        return this;
    }

    /// <summary>
    /// Builds the BehaviorContribution.
    /// </summary>
    public BehaviorContribution Build()
    {
        if (string.IsNullOrWhiteSpace(_id))
            throw new InvalidOperationException("Behavior ID is required");

        if (_behavior == null)
            throw new InvalidOperationException("Behavior implementation is required");

        return new BehaviorContribution
        {
            Id = _id,
            Name = _name,
            Description = _description,
            Behavior = _behavior,
            Placement = _placement,
            Tags = _tags,
            Constraints = _constraints,
            Phase = _phase,
            Priority = _priority,
            IsEnabled = _isEnabled,
            EnabledPredicate = _enabledPredicate
        };
    }
}

/// <summary>
/// Represents the execution phase of a behavior.
/// </summary>
public enum BehaviorPhase
{
    /// <summary>
    /// Pre-processing phase (runs before main processing).
    /// </summary>
    PreProcessing = 0,

    /// <summary>
    /// Main processing phase.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Post-processing phase (runs after main processing).
    /// </summary>
    PostProcessing = 2
}

/// <summary>
/// Extension methods for managing behavior contributions.
/// </summary>
public static class BehaviorContributionExtensions
{
    /// <summary>
    /// Sorts behavior contributions according to their placement rules and priorities.
    /// </summary>
    public static IEnumerable<BehaviorContribution> SortByPlacement(this IEnumerable<BehaviorContribution> contributions)
    {
        var list = contributions.ToList();
        var sorted = new List<BehaviorContribution>();
        var processed = new HashSet<string>();

        // First, add behaviors with FIRST placement
        foreach (var contrib in list.Where(c => c.Placement.Strategy == PlacementStrategy.First)
                                    .OrderBy(c => c.Placement.Order)
                                    .ThenBy(c => c.Priority))
        {
            sorted.Add(contrib);
            processed.Add(contrib.Id);
        }

        // Process behaviors with relative placement (BEFORE, AFTER, REPLACE)
        var remaining = list.Where(c => !processed.Contains(c.Id)).ToList();
        var maxIterations = remaining.Count * 2; // Prevent infinite loops
        var iteration = 0;

        while (remaining.Any() && iteration++ < maxIterations)
        {
            var toRemove = new List<BehaviorContribution>();

            foreach (var contrib in remaining)
            {
                switch (contrib.Placement.Strategy)
                {
                    case PlacementStrategy.Before:
                        var beforeIndex = sorted.FindIndex(c => c.Id == contrib.Placement.TargetBehaviorId);
                        if (beforeIndex >= 0)
                        {
                            sorted.Insert(beforeIndex, contrib);
                            toRemove.Add(contrib);
                            processed.Add(contrib.Id);
                        }
                        break;

                    case PlacementStrategy.After:
                        var afterIndex = sorted.FindIndex(c => c.Id == contrib.Placement.TargetBehaviorId);
                        if (afterIndex >= 0)
                        {
                            sorted.Insert(afterIndex + 1, contrib);
                            toRemove.Add(contrib);
                            processed.Add(contrib.Id);
                        }
                        break;

                    case PlacementStrategy.Replace:
                        var replaceIndex = sorted.FindIndex(c => c.Id == contrib.Placement.TargetBehaviorId);
                        if (replaceIndex >= 0)
                        {
                            sorted[replaceIndex] = contrib;
                            toRemove.Add(contrib);
                            processed.Add(contrib.Id);
                        }
                        break;

                    case PlacementStrategy.Ordered:
                        // These will be added at the end
                        toRemove.Add(contrib);
                        processed.Add(contrib.Id);
                        break;
                }
            }

            foreach (var item in toRemove)
            {
                remaining.Remove(item);
            }
        }

        // Add behaviors with ORDERED placement
        var orderedBehaviors = list.Where(c => c.Placement.Strategy == PlacementStrategy.Ordered && !sorted.Contains(c))
                                   .OrderBy(c => c.Placement.Order)
                                   .ThenBy(c => c.Priority);

        sorted.AddRange(orderedBehaviors);

        // Finally, add behaviors with LAST placement and any remaining behaviors
        var lastBehaviors = list.Where(c => !sorted.Contains(c))
                                .OrderBy(c => c.Priority);

        sorted.AddRange(lastBehaviors);

        return sorted;
    }

    /// <summary>
    /// Groups behaviors by phase.
    /// </summary>
    public static ILookup<BehaviorPhase, BehaviorContribution> GroupByPhase(
        this IEnumerable<BehaviorContribution> contributions)
    {
        return contributions.ToLookup(c => c.Phase);
    }

    /// <summary>
    /// Filters behaviors by tags.
    /// </summary>
    public static IEnumerable<BehaviorContribution> WithTags(
        this IEnumerable<BehaviorContribution> contributions,
        params string[] tags)
    {
        var tagSet = new HashSet<string>(tags);
        return contributions.Where(c => c.Tags.Overlaps(tagSet));
    }

    /// <summary>
    /// Filters behaviors that are currently enabled.
    /// </summary>
    public static IEnumerable<BehaviorContribution> WhereEnabled(
        this IEnumerable<BehaviorContribution> contributions)
    {
        return contributions.Where(c => c.IsCurrentlyEnabled());
    }
}