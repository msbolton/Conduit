using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience.ErrorHandling;

/// <summary>
/// Policy that executes compensating actions when operations fail to maintain system consistency
/// </summary>
public class CompensatingActionPolicy : IResiliencePolicy
{
    private readonly ILogger<CompensatingActionPolicy>? _logger;
    private readonly CompensatingActionConfiguration _config;
    private readonly PolicyMetricsTracker _metrics;
    private readonly Stack<CompensatingAction> _executedActions;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public ResiliencePattern Pattern => ResiliencePattern.CompensatingAction;

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the CompensatingActionPolicy class
    /// </summary>
    public CompensatingActionPolicy(string name, CompensatingActionConfiguration config, ILogger<CompensatingActionPolicy>? logger = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _metrics = new PolicyMetricsTracker(name, Pattern);
        _executedActions = new Stack<CompensatingAction>();
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            await action(cancellationToken);
            return;
        }

        var startTime = DateTimeOffset.UtcNow;
        _metrics.IncrementExecutions();
        _executedActions.Clear();

        try
        {
            // Execute the main action
            await action(cancellationToken);
            _metrics.IncrementSuccesses();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            // If successful and we have commit actions, execute them
            if (_config.CommitActions.Count > 0)
            {
                await ExecuteCommitActions(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _metrics.IncrementFailures();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            var errorContext = new ErrorContext(ex)
                .WithOperation("CompensatingActionPolicy")
                .WithComponent(Name);

            if (ShouldCompensate(errorContext))
            {
                _logger?.LogWarning(ex, "Executing compensating actions for policy '{PolicyName}' due to error: {ErrorMessage}",
                    Name, ex.Message);

                try
                {
                    await ExecuteCompensatingActions(errorContext, cancellationToken);
                    _metrics.IncrementCompensations();
                }
                catch (Exception compensationEx)
                {
                    _logger?.LogError(compensationEx, "Compensating actions failed for policy '{PolicyName}'", Name);
                    _metrics.IncrementCompensationFailures();

                    if (_config.ThrowOnCompensationFailure)
                    {
                        throw new AggregateException("Both primary action and compensation failed", ex, compensationEx);
                    }
                }
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> func, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return await func(cancellationToken);
        }

        var startTime = DateTimeOffset.UtcNow;
        _metrics.IncrementExecutions();
        _executedActions.Clear();

        try
        {
            // Execute the main function
            var result = await func(cancellationToken);
            _metrics.IncrementSuccesses();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            // If successful and we have commit actions, execute them
            if (_config.CommitActions.Count > 0)
            {
                await ExecuteCommitActions(cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _metrics.IncrementFailures();
            _metrics.RecordExecutionTime(DateTimeOffset.UtcNow - startTime);

            var errorContext = new ErrorContext(ex)
                .WithOperation("CompensatingActionPolicy")
                .WithComponent(Name);

            if (ShouldCompensate(errorContext))
            {
                _logger?.LogWarning(ex, "Executing compensating actions for policy '{PolicyName}' due to error: {ErrorMessage}",
                    Name, ex.Message);

                try
                {
                    await ExecuteCompensatingActions(errorContext, cancellationToken);
                    _metrics.IncrementCompensations();
                }
                catch (Exception compensationEx)
                {
                    _logger?.LogError(compensationEx, "Compensating actions failed for policy '{PolicyName}'", Name);
                    _metrics.IncrementCompensationFailures();

                    if (_config.ThrowOnCompensationFailure)
                    {
                        throw new AggregateException("Both primary function and compensation failed", ex, compensationEx);
                    }
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Registers a compensating action that will be executed if the main operation fails
    /// </summary>
    public CompensatingActionPolicy RegisterAction(string actionName, Func<ErrorContext, CancellationToken, Task> action, int priority = 0)
    {
        var compensatingAction = new CompensatingAction
        {
            Name = actionName,
            Action = action,
            Priority = priority,
            ActionType = CompensatingActionType.Compensate
        };

        _config.CompensatingActions.Add(compensatingAction);
        _config.CompensatingActions.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Sort by priority descending

        _logger?.LogDebug("Registered compensating action '{ActionName}' with priority {Priority}", actionName, priority);
        return this;
    }

    /// <summary>
    /// Registers a commit action that will be executed if the main operation succeeds
    /// </summary>
    public CompensatingActionPolicy RegisterCommitAction(string actionName, Func<CancellationToken, Task> action, int priority = 0)
    {
        var commitAction = new CompensatingAction
        {
            Name = actionName,
            CommitAction = action,
            Priority = priority,
            ActionType = CompensatingActionType.Commit
        };

        _config.CommitActions.Add(commitAction);
        _config.CommitActions.Sort((a, b) => a.Priority.CompareTo(b.Priority)); // Sort by priority ascending for commits

        _logger?.LogDebug("Registered commit action '{ActionName}' with priority {Priority}", actionName, priority);
        return this;
    }

    /// <inheritdoc />
    public PolicyMetrics GetMetrics()
    {
        return _metrics.ToMetrics();
    }

    /// <inheritdoc />
    public void Reset()
    {
        _metrics.Reset();
        _executedActions.Clear();
    }

    private bool ShouldCompensate(ErrorContext errorContext)
    {
        // Check if error type is in the compensation exception types
        if (_config.CompensationExceptionTypes.Count > 0)
        {
            var exceptionType = errorContext.Exception.GetType();
            return _config.CompensationExceptionTypes.Exists(type => type.IsAssignableFrom(exceptionType));
        }

        // Check custom predicate
        if (_config.ShouldCompensatePredicate != null)
        {
            return _config.ShouldCompensatePredicate(errorContext);
        }

        // Default: compensate for all errors except validation errors
        return errorContext.Category != ErrorCategory.ValidationError;
    }

    private async Task ExecuteCompensatingActions(ErrorContext errorContext, CancellationToken cancellationToken)
    {
        var actionsToExecute = new List<CompensatingAction>(_config.CompensatingActions);

        // Execute compensating actions in reverse order of priority
        foreach (var action in actionsToExecute)
        {
            try
            {
                _logger?.LogDebug("Executing compensating action '{ActionName}' for policy '{PolicyName}'",
                    action.Name, Name);

                await action.Action!(errorContext, cancellationToken);
                _executedActions.Push(action);

                _logger?.LogDebug("Successfully executed compensating action '{ActionName}'", action.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute compensating action '{ActionName}' for policy '{PolicyName}'",
                    action.Name, Name);

                if (_config.StopOnFirstCompensationFailure)
                {
                    throw;
                }
            }
        }
    }

    private async Task ExecuteCommitActions(CancellationToken cancellationToken)
    {
        foreach (var action in _config.CommitActions)
        {
            try
            {
                _logger?.LogDebug("Executing commit action '{ActionName}' for policy '{PolicyName}'",
                    action.Name, Name);

                await action.CommitAction!(cancellationToken);

                _logger?.LogDebug("Successfully executed commit action '{ActionName}'", action.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to execute commit action '{ActionName}' for policy '{PolicyName}'",
                    action.Name, Name);

                if (_config.StopOnFirstCommitFailure)
                {
                    throw;
                }
            }
        }
    }
}

/// <summary>
/// Configuration for compensating action policy
/// </summary>
public class CompensatingActionConfiguration
{
    /// <summary>
    /// Whether the compensating action policy is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// List of compensating actions to execute on failure
    /// </summary>
    public List<CompensatingAction> CompensatingActions { get; set; } = new();

    /// <summary>
    /// List of commit actions to execute on success
    /// </summary>
    public List<CompensatingAction> CommitActions { get; set; } = new();

    /// <summary>
    /// List of exception types that should trigger compensation
    /// </summary>
    public List<Type> CompensationExceptionTypes { get; set; } = new();

    /// <summary>
    /// Custom predicate to determine if compensation should be applied
    /// </summary>
    public Func<ErrorContext, bool>? ShouldCompensatePredicate { get; set; }

    /// <summary>
    /// Whether to throw an exception if compensation itself fails
    /// </summary>
    public bool ThrowOnCompensationFailure { get; set; } = true;

    /// <summary>
    /// Whether to stop executing remaining compensating actions if one fails
    /// </summary>
    public bool StopOnFirstCompensationFailure { get; set; } = false;

    /// <summary>
    /// Whether to stop executing remaining commit actions if one fails
    /// </summary>
    public bool StopOnFirstCommitFailure { get; set; } = true;

    /// <summary>
    /// Adds an exception type that should trigger compensation
    /// </summary>
    public CompensatingActionConfiguration AddCompensationException<T>() where T : Exception
    {
        CompensationExceptionTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Sets a custom predicate for determining when to compensate
    /// </summary>
    public CompensatingActionConfiguration WithCompensationPredicate(Func<ErrorContext, bool> predicate)
    {
        ShouldCompensatePredicate = predicate;
        return this;
    }
}

/// <summary>
/// Represents a compensating action
/// </summary>
public class CompensatingAction
{
    /// <summary>
    /// Name of the action
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Action to execute for compensation
    /// </summary>
    public Func<ErrorContext, CancellationToken, Task>? Action { get; set; }

    /// <summary>
    /// Action to execute for commit
    /// </summary>
    public Func<CancellationToken, Task>? CommitAction { get; set; }

    /// <summary>
    /// Priority of the action (higher priority executes first for compensation, lower for commit)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Type of compensating action
    /// </summary>
    public CompensatingActionType ActionType { get; set; }
}

/// <summary>
/// Types of compensating actions
/// </summary>
public enum CompensatingActionType
{
    /// <summary>
    /// Action executed on failure to compensate
    /// </summary>
    Compensate,

    /// <summary>
    /// Action executed on success to commit
    /// </summary>
    Commit
}