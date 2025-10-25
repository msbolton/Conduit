using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline;

namespace Conduit.Components
{
    /// <summary>
    /// Represents a behavior contributed by a component to the processing pipeline.
    /// </summary>
    public class BehaviorContribution : IBehaviorContribution
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string? Description { get; init; }
        public Func<IPipelineBehavior> Factory { get; init; } = null!;
        public BehaviorPlacement Placement { get; init; } = BehaviorPlacement.Ordered(100);
        public BehaviorConstraints Constraints { get; init; } = BehaviorConstraints.RunAlways();
        public int Priority { get; init; } = 100;
        public string[] Tags { get; init; } = Array.Empty<string>();
        public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

        /// <summary>
        /// Creates a new behavior contribution builder.
        /// </summary>
        public static BehaviorContributionBuilder Builder()
        {
            return new BehaviorContributionBuilder();
        }

        /// <summary>
        /// Checks if this behavior should be enabled in the given context.
        /// </summary>
        public bool ShouldEnable(ComponentContext context)
        {
            return Constraints.ShouldEnable(context);
        }

        /// <summary>
        /// Creates an instance of the behavior.
        /// </summary>
        public IPipelineBehavior CreateBehavior()
        {
            return Factory();
        }
    }

    /// <summary>
    /// Builder for creating behavior contributions.
    /// </summary>
    public class BehaviorContributionBuilder
    {
        private string _id = "";
        private string _name = "";
        private string? _description;
        private Func<IPipelineBehavior>? _factory;
        private BehaviorPlacement _placement = BehaviorPlacement.Ordered(100);
        private BehaviorConstraints _constraints = BehaviorConstraints.RunAlways();
        private int _priority = 100;
        private readonly List<string> _tags = new();
        private readonly Dictionary<string, object> _metadata = new();

        public BehaviorContributionBuilder WithId(string id)
        {
            _id = id;
            return this;
        }

        public BehaviorContributionBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public BehaviorContributionBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public BehaviorContributionBuilder WithFactory(Func<IPipelineBehavior> factory)
        {
            _factory = factory;
            return this;
        }

        public BehaviorContributionBuilder WithBehavior(Func<object, Func<Task<object>>, Task<object>> behavior)
        {
            _factory = () => new DelegatePipelineBehavior(behavior);
            return this;
        }

        public BehaviorContributionBuilder WithPlacement(BehaviorPlacement placement)
        {
            _placement = placement;
            return this;
        }

        public BehaviorContributionBuilder WithConstraints(BehaviorConstraints constraints)
        {
            _constraints = constraints;
            return this;
        }

        public BehaviorContributionBuilder WithPriority(int priority)
        {
            _priority = priority;
            return this;
        }

        public BehaviorContributionBuilder WithTags(params string[] tags)
        {
            _tags.AddRange(tags);
            return this;
        }

        public BehaviorContributionBuilder WithMetadata(string key, object value)
        {
            _metadata[key] = value;
            return this;
        }

        public BehaviorContribution Build()
        {
            Guard.AgainstNullOrEmpty(_id, nameof(_id));
            Guard.AgainstNull(_factory, nameof(_factory));

            return new BehaviorContribution
            {
                Id = _id,
                Name = string.IsNullOrEmpty(_name) ? _id : _name,
                Description = _description,
                Factory = _factory,
                Placement = _placement,
                Constraints = _constraints,
                Priority = _priority,
                Tags = _tags.ToArray(),
                Metadata = new Dictionary<string, object>(_metadata)
            };
        }
    }

    /// <summary>
    /// Defines constraints for when a behavior should be enabled.
    /// </summary>
    public class BehaviorConstraints
    {
        private readonly Func<ComponentContext, bool> _predicate;

        private BehaviorConstraints(Func<ComponentContext, bool> predicate)
        {
            _predicate = predicate;
        }

        /// <summary>
        /// Creates constraints that always enable the behavior.
        /// </summary>
        public static BehaviorConstraints RunAlways()
        {
            return new BehaviorConstraints(_ => true);
        }

        /// <summary>
        /// Creates constraints that enable the behavior when a feature is enabled.
        /// </summary>
        public static BehaviorConstraints WhenFeatureEnabled(string featureName)
        {
            return new BehaviorConstraints(context =>
                context.IsFeatureEnabled(featureName));
        }

        /// <summary>
        /// Creates constraints that enable the behavior when a configuration setting is true.
        /// </summary>
        public static BehaviorConstraints WhenConfigurationEnabled(string settingName)
        {
            return new BehaviorConstraints(context =>
                context.Configuration?.GetSetting<bool>(settingName) ?? false);
        }

        /// <summary>
        /// Creates constraints with a custom predicate.
        /// </summary>
        public static BehaviorConstraints When(Func<ComponentContext, bool> predicate)
        {
            return new BehaviorConstraints(predicate);
        }

        /// <summary>
        /// Checks if the behavior should be enabled in the given context.
        /// </summary>
        public bool ShouldEnable(ComponentContext context)
        {
            return _predicate(context);
        }

        /// <summary>
        /// Combines this constraint with another using AND logic.
        /// </summary>
        public BehaviorConstraints And(BehaviorConstraints other)
        {
            return new BehaviorConstraints(context =>
                _predicate(context) && other._predicate(context));
        }

        /// <summary>
        /// Combines this constraint with another using OR logic.
        /// </summary>
        public BehaviorConstraints Or(BehaviorConstraints other)
        {
            return new BehaviorConstraints(context =>
                _predicate(context) || other._predicate(context));
        }

        /// <summary>
        /// Negates this constraint.
        /// </summary>
        public BehaviorConstraints Not()
        {
            return new BehaviorConstraints(context => !_predicate(context));
        }
    }

    /// <summary>
    /// Wrapper for delegate-based pipeline behaviors.
    /// </summary>
    internal class DelegatePipelineBehavior : IPipelineBehavior
    {
        private readonly Func<object, Func<Task<object>>, Task<object>> _behavior;

        public DelegatePipelineBehavior(Func<object, Func<Task<object>>, Task<object>> behavior)
        {
            _behavior = behavior;
        }

        public async Task<object?> ExecuteAsync(object input, Func<object, Task<object?>> next)
        {
            return await _behavior(input, async () =>
            {
                var result = await next(input);
                return result ?? new object();
            });
        }
    }

    /// <summary>
    /// Collection of behavior contributions with ordering capabilities.
    /// </summary>
    public class BehaviorContributionCollection
    {
        private readonly List<BehaviorContribution> _contributions = new();

        /// <summary>
        /// Adds a behavior contribution to the collection.
        /// </summary>
        public void Add(BehaviorContribution contribution)
        {
            Guard.AgainstNull(contribution, nameof(contribution));
            _contributions.Add(contribution);
        }

        /// <summary>
        /// Adds multiple behavior contributions to the collection.
        /// </summary>
        public void AddRange(IEnumerable<BehaviorContribution> contributions)
        {
            Guard.AgainstNull(contributions, nameof(contributions));
            _contributions.AddRange(contributions);
        }

        /// <summary>
        /// Gets all contributions, ordered according to their placement rules.
        /// </summary>
        public IEnumerable<BehaviorContribution> GetOrdered()
        {
            return OrderContributions(_contributions);
        }

        /// <summary>
        /// Gets contributions filtered by context and ordered.
        /// </summary>
        public IEnumerable<BehaviorContribution> GetOrdered(ComponentContext context)
        {
            var enabled = _contributions.Where(c => c.ShouldEnable(context));
            return OrderContributions(enabled);
        }

        /// <summary>
        /// Gets contributions with a specific tag.
        /// </summary>
        public IEnumerable<BehaviorContribution> GetByTag(string tag)
        {
            return _contributions.Where(c => c.Tags.Contains(tag));
        }

        private static IEnumerable<BehaviorContribution> OrderContributions(
            IEnumerable<BehaviorContribution> contributions)
        {
            var list = contributions.ToList();
            var ordered = new List<BehaviorContribution>();
            var byPlacement = new Dictionary<BehaviorPlacementType, List<BehaviorContribution>>();

            // Group by placement type
            foreach (var contribution in list)
            {
                var placementType = contribution.Placement.Type;
                if (!byPlacement.ContainsKey(placementType))
                {
                    byPlacement[placementType] = new List<BehaviorContribution>();
                }
                byPlacement[placementType].Add(contribution);
            }

            // Add behaviors marked as "first"
            if (byPlacement.TryGetValue(BehaviorPlacementType.First, out var firstBehaviors))
            {
                ordered.AddRange(firstBehaviors.OrderByDescending(b => b.Priority));
            }

            // Add behaviors with specific ordering
            if (byPlacement.TryGetValue(BehaviorPlacementType.Ordered, out var orderedBehaviors))
            {
                ordered.AddRange(orderedBehaviors.OrderBy(b => b.Placement.Order)
                    .ThenByDescending(b => b.Priority));
            }

            // Handle before/after placement
            if (byPlacement.TryGetValue(BehaviorPlacementType.Before, out var beforeBehaviors))
            {
                foreach (var behavior in beforeBehaviors.OrderByDescending(b => b.Priority))
                {
                    var targetId = behavior.Placement.RelativeToId;
                    var targetIndex = ordered.FindIndex(b => b.Id == targetId);
                    if (targetIndex >= 0)
                    {
                        ordered.Insert(targetIndex, behavior);
                    }
                    else
                    {
                        ordered.Add(behavior);
                    }
                }
            }

            if (byPlacement.TryGetValue(BehaviorPlacementType.After, out var afterBehaviors))
            {
                foreach (var behavior in afterBehaviors.OrderByDescending(b => b.Priority))
                {
                    var targetId = behavior.Placement.RelativeToId;
                    var targetIndex = ordered.FindIndex(b => b.Id == targetId);
                    if (targetIndex >= 0)
                    {
                        ordered.Insert(targetIndex + 1, behavior);
                    }
                    else
                    {
                        ordered.Add(behavior);
                    }
                }
            }

            // Add behaviors marked as "last"
            if (byPlacement.TryGetValue(BehaviorPlacementType.Last, out var lastBehaviors))
            {
                ordered.AddRange(lastBehaviors.OrderByDescending(b => b.Priority));
            }

            return ordered;
        }

        /// <summary>
        /// Gets the count of contributions in the collection.
        /// </summary>
        public int Count => _contributions.Count;

        /// <summary>
        /// Clears all contributions from the collection.
        /// </summary>
        public void Clear()
        {
            _contributions.Clear();
        }
    }
}