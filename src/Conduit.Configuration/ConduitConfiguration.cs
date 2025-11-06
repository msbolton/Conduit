using System;
using System.Collections.Generic;
using Conduit.Gateway;
using Conduit.Messaging;
using Conduit.Resilience;
using Conduit.Security;
using Conduit.Serialization;
using Conduit.Transports.Core;

namespace Conduit.Configuration
{
    /// <summary>
    /// Root configuration object for the entire Conduit framework.
    /// </summary>
    public class ConduitConfiguration
    {
        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        public string ApplicationName { get; set; } = "Conduit Application";

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the environment (Development, Staging, Production).
        /// </summary>
        public string Environment { get; set; } = "Development";

        /// <summary>
        /// Gets or sets global logging configuration.
        /// </summary>
        public LoggingConfiguration Logging { get; set; } = new();

        /// <summary>
        /// Gets or sets gateway configuration.
        /// </summary>
        public GatewayConfiguration Gateway { get; set; } = new();

        /// <summary>
        /// Gets or sets messaging configuration.
        /// </summary>
        public MessagingConfiguration Messaging { get; set; } = new();

        /// <summary>
        /// Gets or sets transport configurations.
        /// </summary>
        public TransportsConfiguration Transports { get; set; } = new();

        /// <summary>
        /// Gets or sets pipeline configuration.
        /// </summary>
        public PipelineConfiguration Pipeline { get; set; } = new();

        /// <summary>
        /// Gets or sets resilience configuration.
        /// </summary>
        public ResilienceConfiguration Resilience { get; set; } = new();

        /// <summary>
        /// Gets or sets security configuration.
        /// </summary>
        public SecurityConfiguration Security { get; set; } = new();

        /// <summary>
        /// Gets or sets serialization configuration.
        /// </summary>
        public SerializationConfiguration Serialization { get; set; } = new();

        /// <summary>
        /// Gets or sets persistence configuration.
        /// </summary>
        public PersistenceConfiguration Persistence { get; set; } = new();

        /// <summary>
        /// Gets or sets metrics and monitoring configuration.
        /// </summary>
        public MetricsConfiguration Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets component configuration.
        /// </summary>
        public ComponentsConfiguration Components { get; set; } = new();

        /// <summary>
        /// Gets or sets custom application-specific settings.
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new();

        /// <summary>
        /// Validates the entire configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApplicationName))
                throw new ArgumentException("ApplicationName cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Version))
                throw new ArgumentException("Version cannot be null or empty");

            Gateway?.Validate();
            Messaging?.Validate();
            Transports?.Validate();
            Pipeline?.Validate();
            Security?.Validate();
            Serialization?.Validate();
            Persistence?.Validate();
            Metrics?.Validate();
            Components?.Validate();
            Logging?.Validate();
        }
    }

    /// <summary>
    /// Logging configuration.
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// Gets or sets the minimum log level.
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets whether to enable console logging.
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable file logging.
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets the log file path.
        /// </summary>
        public string LogFilePath { get; set; } = "logs/conduit-{Date}.log";

        /// <summary>
        /// Gets or sets whether to enable structured logging.
        /// </summary>
        public bool EnableStructuredLogging { get; set; } = true;

        /// <summary>
        /// Gets or sets log sinks configuration.
        /// </summary>
        public List<LogSinkConfiguration> Sinks { get; set; } = new();

        /// <summary>
        /// Gets or sets logger-specific configurations.
        /// </summary>
        public Dictionary<string, string> LoggerLevels { get; set; } = new();

        /// <summary>
        /// Validates the logging configuration.
        /// </summary>
        public void Validate()
        {
            var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            if (!validLevels.Contains(LogLevel))
                throw new ArgumentException($"Invalid LogLevel: {LogLevel}");

            foreach (var sink in Sinks)
                sink.Validate();
        }
    }

    /// <summary>
    /// Log sink configuration.
    /// </summary>
    public class LogSinkConfiguration
    {
        /// <summary>
        /// Gets or sets the sink type (Console, File, Elasticsearch, etc.).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the sink is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum log level for this sink.
        /// </summary>
        public string MinimumLevel { get; set; } = "Information";

        /// <summary>
        /// Gets or sets sink-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Validates the sink configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Sink Type cannot be null or empty");

            var validLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            if (!validLevels.Contains(MinimumLevel))
                throw new ArgumentException($"Invalid MinimumLevel for sink: {MinimumLevel}");
        }
    }

    /// <summary>
    /// Messaging configuration.
    /// </summary>
    public class MessagingConfiguration
    {
        /// <summary>
        /// Gets or sets the default message timeout in milliseconds.
        /// </summary>
        public int DefaultMessageTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum message size in bytes.
        /// </summary>
        public int MaxMessageSize { get; set; } = 1048576; // 1MB

        /// <summary>
        /// Gets or sets whether to enable message routing.
        /// </summary>
        public bool EnableMessageRouting { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable dead letter queues.
        /// </summary>
        public bool EnableDeadLetterQueue { get; set; } = true;

        /// <summary>
        /// Gets or sets the dead letter queue name.
        /// </summary>
        public string DeadLetterQueueName { get; set; } = "dlq";

        /// <summary>
        /// Gets or sets retry configuration.
        /// </summary>
        public RetryConfiguration Retry { get; set; } = new();

        /// <summary>
        /// Gets or sets message routing rules.
        /// </summary>
        public List<MessageRouteConfiguration> Routes { get; set; } = new();

        /// <summary>
        /// Validates the messaging configuration.
        /// </summary>
        public void Validate()
        {
            if (DefaultMessageTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(DefaultMessageTimeout), "Must be greater than 0");

            if (MaxMessageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMessageSize), "Must be greater than 0");

            Retry?.Validate();

            foreach (var route in Routes)
                route.Validate();
        }
    }

    /// <summary>
    /// Retry configuration.
    /// </summary>
    public class RetryConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the initial retry delay in milliseconds.
        /// </summary>
        public int InitialDelay { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum retry delay in milliseconds.
        /// </summary>
        public int MaxDelay { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the retry strategy (Linear, Exponential, Fixed).
        /// </summary>
        public string Strategy { get; set; } = "Exponential";

        /// <summary>
        /// Gets or sets the backoff multiplier for exponential strategy.
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Validates the retry configuration.
        /// </summary>
        public void Validate()
        {
            if (MaxAttempts < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxAttempts), "Cannot be negative");

            if (InitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(InitialDelay), "Cannot be negative");

            if (MaxDelay < InitialDelay)
                throw new ArgumentException("MaxDelay cannot be less than InitialDelay");

            var validStrategies = new[] { "Linear", "Exponential", "Fixed" };
            if (!validStrategies.Contains(Strategy))
                throw new ArgumentException($"Invalid Strategy: {Strategy}");

            if (BackoffMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(BackoffMultiplier), "Must be greater than 0");
        }
    }

    /// <summary>
    /// Message route configuration.
    /// </summary>
    public class MessageRouteConfiguration
    {
        /// <summary>
        /// Gets or sets the route name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message type pattern.
        /// </summary>
        public string MessageType { get; set; } = "*";

        /// <summary>
        /// Gets or sets the source endpoint.
        /// </summary>
        public string Source { get; set; } = "*";

        /// <summary>
        /// Gets or sets the destination endpoint.
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the transport to use.
        /// </summary>
        public string Transport { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the route is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the route priority.
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Validates the message route configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Route Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Destination))
                throw new ArgumentException("Route Destination cannot be null or empty");
        }
    }
}