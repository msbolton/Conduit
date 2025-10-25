using System;
using System.Collections.Generic;
using System.Linq;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Defines constraints that determine when a behavior should execute.
/// </summary>
public class BehaviorConstraints
{
    private readonly Predicate<PipelineContext> _predicate;

    /// <summary>
    /// Initializes a new instance of the BehaviorConstraints class.
    /// </summary>
    protected BehaviorConstraints(Predicate<PipelineContext> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>
    /// Evaluates whether the constraints are satisfied.
    /// </summary>
    public bool IsSatisfied(PipelineContext context)
    {
        return _predicate(context);
    }

    /// <summary>
    /// Creates a constraint with no restrictions.
    /// </summary>
    public static BehaviorConstraints None()
    {
        return new BehaviorConstraints(_ => true);
    }

    /// <summary>
    /// Creates a constraint with a custom predicate.
    /// </summary>
    public static BehaviorConstraints When(Predicate<PipelineContext> predicate)
    {
        return new BehaviorConstraints(predicate);
    }

    /// <summary>
    /// Creates a constraint that checks for specific message types.
    /// </summary>
    public static BehaviorConstraints ForMessageTypes(params Type[] messageTypes)
    {
        var typeSet = new HashSet<Type>(messageTypes);
        return new BehaviorConstraints(context =>
        {
            if (context.Input == null) return false;
            var inputType = context.Input.GetType();
            return typeSet.Any(t => t.IsAssignableFrom(inputType));
        });
    }

    /// <summary>
    /// Creates a constraint that checks for a feature flag.
    /// </summary>
    public static BehaviorConstraints WhenFeatureEnabled(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name cannot be null or empty", nameof(featureName));

        return new BehaviorConstraints(context =>
        {
            var featureFlagKey = $"Feature.{featureName}";
            return context.HasProperty(featureFlagKey) &&
                   context.GetValueProperty<bool>(featureFlagKey);
        });
    }

    /// <summary>
    /// Creates a constraint that checks if a property exists.
    /// </summary>
    public static BehaviorConstraints WhenPropertyExists(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

        return new BehaviorConstraints(context => context.HasProperty(propertyName));
    }

    /// <summary>
    /// Creates a constraint that checks if a property has a specific value.
    /// </summary>
    public static BehaviorConstraints WhenPropertyEquals(string propertyName, object expectedValue)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Property name cannot be null or empty", nameof(propertyName));

        return new BehaviorConstraints(context =>
        {
            var value = context.GetProperty(propertyName);
            return value != null && value.Equals(expectedValue);
        });
    }

    /// <summary>
    /// Creates a constraint that checks if the pipeline has a specific name.
    /// </summary>
    public static BehaviorConstraints ForPipeline(string pipelineName)
    {
        if (string.IsNullOrWhiteSpace(pipelineName))
            throw new ArgumentException("Pipeline name cannot be null or empty", nameof(pipelineName));

        return new BehaviorConstraints(context =>
            string.Equals(context.PipelineName, pipelineName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a constraint that checks if the pipeline has one of the specified names.
    /// </summary>
    public static BehaviorConstraints ForPipelines(params string[] pipelineNames)
    {
        var names = new HashSet<string>(pipelineNames, StringComparer.OrdinalIgnoreCase);
        return new BehaviorConstraints(context =>
            context.PipelineName != null && names.Contains(context.PipelineName));
    }

    /// <summary>
    /// Creates a constraint based on the current stage.
    /// </summary>
    public static BehaviorConstraints ForStage(string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
            throw new ArgumentException("Stage name cannot be null or empty", nameof(stageName));

        return new BehaviorConstraints(context =>
            string.Equals(context.CurrentStage, stageName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Creates a constraint that checks if the context has an error.
    /// </summary>
    public static BehaviorConstraints OnError()
    {
        return new BehaviorConstraints(context => context.HasError);
    }

    /// <summary>
    /// Creates a constraint that checks if the context has a specific error type.
    /// </summary>
    public static BehaviorConstraints OnErrorType<TException>() where TException : Exception
    {
        return new BehaviorConstraints(context =>
            context.Exception != null && context.Exception is TException);
    }

    /// <summary>
    /// Combines this constraint with another using AND logic.
    /// </summary>
    public BehaviorConstraints And(BehaviorConstraints other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        return new BehaviorConstraints(context =>
            IsSatisfied(context) && other.IsSatisfied(context));
    }

    /// <summary>
    /// Combines this constraint with another using OR logic.
    /// </summary>
    public BehaviorConstraints Or(BehaviorConstraints other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        return new BehaviorConstraints(context =>
            IsSatisfied(context) || other.IsSatisfied(context));
    }

    /// <summary>
    /// Negates this constraint.
    /// </summary>
    public BehaviorConstraints Not()
    {
        return new BehaviorConstraints(context => !IsSatisfied(context));
    }

    /// <summary>
    /// Combines multiple constraints with AND logic.
    /// </summary>
    public static BehaviorConstraints All(params BehaviorConstraints[] constraints)
    {
        if (constraints == null || constraints.Length == 0)
            return None();

        return new BehaviorConstraints(context =>
            constraints.All(c => c.IsSatisfied(context)));
    }

    /// <summary>
    /// Combines multiple constraints with OR logic.
    /// </summary>
    public static BehaviorConstraints Any(params BehaviorConstraints[] constraints)
    {
        if (constraints == null || constraints.Length == 0)
            return None();

        return new BehaviorConstraints(context =>
            constraints.Any(c => c.IsSatisfied(context)));
    }

    /// <summary>
    /// Creates a constraint that always passes.
    /// </summary>
    public static BehaviorConstraints Always()
    {
        return None();
    }

    /// <summary>
    /// Creates a constraint that never passes.
    /// </summary>
    public static BehaviorConstraints Never()
    {
        return new BehaviorConstraints(_ => false);
    }
}

/// <summary>
/// Extension methods for behavior constraints.
/// </summary>
public static class BehaviorConstraintsExtensions
{
    /// <summary>
    /// Filters behaviors based on constraints.
    /// </summary>
    public static IEnumerable<BehaviorContribution> WhereConstraintsSatisfied(
        this IEnumerable<BehaviorContribution> contributions,
        PipelineContext context)
    {
        return contributions.Where(c => c.Constraints.IsSatisfied(context));
    }

    /// <summary>
    /// Creates a composite constraint from multiple contributions.
    /// </summary>
    public static BehaviorConstraints CombineConstraints(
        this IEnumerable<BehaviorContribution> contributions,
        bool requireAll = true)
    {
        var constraints = contributions
            .Select(c => c.Constraints)
            .ToArray();

        return requireAll
            ? BehaviorConstraints.All(constraints)
            : BehaviorConstraints.Any(constraints);
    }
}