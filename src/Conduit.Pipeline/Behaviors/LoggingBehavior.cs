using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Pipeline behavior that logs request/response information and execution metrics
/// </summary>
public class LoggingBehavior : IPipelineBehavior
{
    private readonly ILogger<LoggingBehavior> _logger;
    private readonly LoggingBehaviorOptions _options;

    /// <summary>
    /// Initializes a new instance of the LoggingBehavior class
    /// </summary>
    public LoggingBehavior(ILogger<LoggingBehavior> logger, LoggingBehaviorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new LoggingBehaviorOptions();
    }

    /// <summary>
    /// Executes the logging behavior around the pipeline execution
    /// </summary>
    public async Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        var correlationId = context.GetProperty<string>("CorrelationId") ?? Guid.NewGuid().ToString();
        var requestId = context.GetProperty<string>("RequestId") ?? Guid.NewGuid().ToString();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestId"] = requestId,
            ["MessageType"] = context.Message?.GetType().Name ?? "Unknown"
        });

        if (_options.LogRequests)
        {
            LogRequest(context, correlationId, requestId);
        }

        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;
        object? result = null;

        try
        {
            result = await next.ProceedAsync(context);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (_options.LogResponses)
            {
                LogResponse(context, result, stopwatch.Elapsed, exception, correlationId, requestId);
            }

            if (_options.LogPerformanceMetrics)
            {
                LogPerformanceMetrics(context, stopwatch.Elapsed, correlationId, requestId);
            }
        }
    }

    private void LogRequest(PipelineContext context, string correlationId, string requestId)
    {
        var messageType = context.Message?.GetType().Name ?? "Unknown";
        var messageData = _options.IncludeMessageContent ? GetSafeMessageContent(context.Message) : "[Content Hidden]";

        _logger.LogInformation(
            "Processing {MessageType} - CorrelationId: {CorrelationId}, RequestId: {RequestId}, Message: {MessageData}",
            messageType, correlationId, requestId, messageData);
    }

    private void LogResponse(PipelineContext context, object? result, TimeSpan duration, Exception? exception, string correlationId, string requestId)
    {
        var messageType = context.Message?.GetType().Name ?? "Unknown";

        if (exception != null)
        {
            _logger.LogError(exception,
                "Failed processing {MessageType} in {Duration}ms - CorrelationId: {CorrelationId}, RequestId: {RequestId}",
                messageType, duration.TotalMilliseconds, correlationId, requestId);
        }
        else
        {
            var resultData = _options.IncludeResponseContent ? GetSafeContent(result) : "[Content Hidden]";

            _logger.LogInformation(
                "Completed {MessageType} in {Duration}ms - CorrelationId: {CorrelationId}, RequestId: {RequestId}, Result: {ResultData}",
                messageType, duration.TotalMilliseconds, correlationId, requestId, resultData);
        }
    }

    private void LogPerformanceMetrics(PipelineContext context, TimeSpan duration, string correlationId, string requestId)
    {
        var messageType = context.Message?.GetType().Name ?? "Unknown";

        if (duration > _options.SlowRequestThreshold)
        {
            _logger.LogWarning(
                "Slow request detected: {MessageType} took {Duration}ms (threshold: {Threshold}ms) - CorrelationId: {CorrelationId}, RequestId: {RequestId}",
                messageType, duration.TotalMilliseconds, _options.SlowRequestThreshold.TotalMilliseconds, correlationId, requestId);
        }

        // Add timing to context for other behaviors
        context.SetProperty("ExecutionDuration", duration);
        context.SetProperty("ExecutionDurationMs", duration.TotalMilliseconds);
    }

    private string GetSafeMessageContent(object? message)
    {
        if (message == null) return "null";

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(message, options);
            return json.Length > _options.MaxContentLength
                ? json[.._options.MaxContentLength] + "..."
                : json;
        }
        catch
        {
            return $"[{message.GetType().Name}]";
        }
    }

    private string GetSafeContent(object? content)
    {
        if (content == null) return "null";

        try
        {
            var json = JsonSerializer.Serialize(content);
            return json.Length > _options.MaxContentLength
                ? json[.._options.MaxContentLength] + "..."
                : json;
        }
        catch
        {
            return $"[{content.GetType().Name}]";
        }
    }
}

/// <summary>
/// Configuration options for the logging behavior
/// </summary>
public class LoggingBehaviorOptions
{
    /// <summary>
    /// Whether to log incoming requests
    /// </summary>
    public bool LogRequests { get; set; } = true;

    /// <summary>
    /// Whether to log outgoing responses
    /// </summary>
    public bool LogResponses { get; set; } = true;

    /// <summary>
    /// Whether to log performance metrics
    /// </summary>
    public bool LogPerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Whether to include message content in logs
    /// </summary>
    public bool IncludeMessageContent { get; set; } = false;

    /// <summary>
    /// Whether to include response content in logs
    /// </summary>
    public bool IncludeResponseContent { get; set; } = false;

    /// <summary>
    /// Maximum length of content to include in logs
    /// </summary>
    public int MaxContentLength { get; set; } = 1000;

    /// <summary>
    /// Threshold for considering a request slow
    /// </summary>
    public TimeSpan SlowRequestThreshold { get; set; } = TimeSpan.FromSeconds(5);
}