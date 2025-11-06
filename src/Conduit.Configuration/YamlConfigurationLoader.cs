using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Conduit.Configuration
{
    /// <summary>
    /// YAML configuration loader for Conduit framework.
    /// </summary>
    public class YamlConfigurationLoader
    {
        private readonly ILogger<YamlConfigurationLoader>? _logger;
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// Initializes a new instance of the YamlConfigurationLoader class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        public YamlConfigurationLoader(ILogger<YamlConfigurationLoader>? logger = null)
        {
            _logger = logger;

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        /// <summary>
        /// Loads configuration from a YAML file.
        /// </summary>
        /// <param name="filePath">Path to the YAML configuration file</param>
        /// <returns>Loaded Conduit configuration</returns>
        /// <exception cref="ConfigurationLoadException">Thrown when configuration loading fails</exception>
        public async Task<ConduitConfiguration> LoadFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Configuration file not found: {filePath}");

            try
            {
                _logger?.LogInformation("Loading configuration from file: {FilePath}", filePath);

                var yamlContent = await File.ReadAllTextAsync(filePath);
                return LoadFromYamlString(yamlContent);
            }
            catch (Exception ex) when (!(ex is ConfigurationLoadException))
            {
                var message = $"Failed to load configuration from file: {filePath}";
                _logger?.LogError(ex, message);
                throw new ConfigurationLoadException(message, ex);
            }
        }

        /// <summary>
        /// Loads configuration from a YAML string.
        /// </summary>
        /// <param name="yamlContent">YAML content as string</param>
        /// <returns>Loaded Conduit configuration</returns>
        /// <exception cref="ConfigurationLoadException">Thrown when configuration loading fails</exception>
        public ConduitConfiguration LoadFromYamlString(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
                throw new ArgumentException("YAML content cannot be null or empty", nameof(yamlContent));

            try
            {
                _logger?.LogDebug("Deserializing YAML configuration");

                var config = _deserializer.Deserialize<ConduitConfiguration>(yamlContent) ?? new ConduitConfiguration();

                _logger?.LogInformation("Successfully loaded configuration for application: {ApplicationName} v{Version}",
                    config.ApplicationName, config.Version);

                // Validate the configuration
                config.Validate();

                return config;
            }
            catch (Exception ex) when (!(ex is ConfigurationLoadException))
            {
                var message = "Failed to deserialize YAML configuration";
                _logger?.LogError(ex, message);
                throw new ConfigurationLoadException(message, ex);
            }
        }

        /// <summary>
        /// Loads configuration from multiple YAML files and merges them.
        /// </summary>
        /// <param name="filePaths">Array of YAML file paths</param>
        /// <returns>Merged Conduit configuration</returns>
        /// <exception cref="ConfigurationLoadException">Thrown when configuration loading fails</exception>
        public Task<ConduitConfiguration> LoadFromMultipleFilesAsync(params string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
                throw new ArgumentException("At least one file path must be provided", nameof(filePaths));

            try
            {
                _logger?.LogInformation("Loading configuration from {FileCount} files", filePaths.Length);

                var builder = new ConfigurationBuilder();

                // Add each YAML file to the configuration builder
                foreach (var filePath in filePaths)
                {
                    if (File.Exists(filePath))
                    {
                        builder.AddYamlFile(filePath, optional: false, reloadOnChange: false);
                        _logger?.LogDebug("Added configuration file: {FilePath}", filePath);
                    }
                    else
                    {
                        _logger?.LogWarning("Configuration file not found, skipping: {FilePath}", filePath);
                    }
                }

                var configuration = builder.Build();

                // Bind to our configuration object
                var conduitConfig = new ConduitConfiguration();
                configuration.Bind(conduitConfig);

                // Validate the merged configuration
                conduitConfig.Validate();

                _logger?.LogInformation("Successfully merged configuration from {FileCount} files", filePaths.Length);

                return Task.FromResult(conduitConfig);
            }
            catch (Exception ex) when (!(ex is ConfigurationLoadException))
            {
                var message = "Failed to load configuration from multiple files";
                _logger?.LogError(ex, message);
                return Task.FromException<ConduitConfiguration>(new ConfigurationLoadException(message, ex));
            }
        }

        /// <summary>
        /// Loads configuration with environment-specific overrides.
        /// </summary>
        /// <param name="baseConfigPath">Path to the base configuration file</param>
        /// <param name="environment">Environment name (Development, Staging, Production)</param>
        /// <param name="environmentConfigPath">Optional path to environment-specific config file</param>
        /// <returns>Loaded configuration with environment overrides</returns>
        public async Task<ConduitConfiguration> LoadWithEnvironmentAsync(
            string baseConfigPath,
            string environment,
            string? environmentConfigPath = null)
        {
            if (string.IsNullOrWhiteSpace(baseConfigPath))
                throw new ArgumentException("Base config path cannot be null or empty", nameof(baseConfigPath));

            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment cannot be null or empty", nameof(environment));

            try
            {
                _logger?.LogInformation("Loading configuration for environment: {Environment}", environment);

                var filePaths = new List<string> { baseConfigPath };

                // Add environment-specific config if provided
                if (!string.IsNullOrWhiteSpace(environmentConfigPath) && File.Exists(environmentConfigPath))
                {
                    filePaths.Add(environmentConfigPath);
                }
                else
                {
                    // Try to find environment-specific config file by convention
                    var baseDir = Path.GetDirectoryName(baseConfigPath) ?? ".";
                    var baseName = Path.GetFileNameWithoutExtension(baseConfigPath);
                    var extension = Path.GetExtension(baseConfigPath);
                    var envConfigPath = Path.Combine(baseDir, $"{baseName}.{environment.ToLowerInvariant()}{extension}");

                    if (File.Exists(envConfigPath))
                    {
                        filePaths.Add(envConfigPath);
                        _logger?.LogDebug("Found environment-specific config: {EnvConfigPath}", envConfigPath);
                    }
                }

                var config = await LoadFromMultipleFilesAsync(filePaths.ToArray());

                // Ensure the environment is set correctly
                config.Environment = environment;

                return config;
            }
            catch (Exception ex) when (!(ex is ConfigurationLoadException))
            {
                var message = $"Failed to load configuration for environment: {environment}";
                _logger?.LogError(ex, message);
                throw new ConfigurationLoadException(message, ex);
            }
        }

        /// <summary>
        /// Saves configuration to a YAML file.
        /// </summary>
        /// <param name="configuration">Configuration to save</param>
        /// <param name="filePath">Path where to save the configuration</param>
        /// <returns>Task representing the save operation</returns>
        public async Task SaveToFileAsync(ConduitConfiguration configuration, string filePath)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            try
            {
                _logger?.LogInformation("Saving configuration to file: {FilePath}", filePath);

                // Validate before saving
                configuration.Validate();

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .Build();

                var yamlContent = serializer.Serialize(configuration);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(filePath, yamlContent);

                _logger?.LogInformation("Successfully saved configuration to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                var message = $"Failed to save configuration to file: {filePath}";
                _logger?.LogError(ex, message);
                throw new ConfigurationLoadException(message, ex);
            }
        }

        /// <summary>
        /// Creates a default configuration and saves it to a file.
        /// </summary>
        /// <param name="filePath">Path where to save the default configuration</param>
        /// <param name="applicationName">Application name for the configuration</param>
        /// <returns>The created default configuration</returns>
        public async Task<ConduitConfiguration> CreateDefaultConfigurationAsync(
            string filePath,
            string applicationName = "Conduit Application")
        {
            _logger?.LogInformation("Creating default configuration for: {ApplicationName}", applicationName);

            var config = CreateDefaultConfiguration(applicationName);
            await SaveToFileAsync(config, filePath);

            return config;
        }

        /// <summary>
        /// Creates a default configuration object.
        /// </summary>
        /// <param name="applicationName">Application name</param>
        /// <returns>Default configuration</returns>
        public static ConduitConfiguration CreateDefaultConfiguration(string applicationName = "Conduit Application")
        {
            return new ConduitConfiguration
            {
                ApplicationName = applicationName,
                Version = "1.0.0",
                Environment = "Development",
                Gateway = new Conduit.Gateway.GatewayConfiguration(),
                Logging = new LoggingConfiguration
                {
                    LogLevel = "Information",
                    EnableConsoleLogging = true,
                    EnableFileLogging = true,
                    Sinks = new List<LogSinkConfiguration>
                    {
                        new()
                        {
                            Type = "Console",
                            Enabled = true,
                            MinimumLevel = "Information"
                        },
                        new()
                        {
                            Type = "File",
                            Enabled = true,
                            MinimumLevel = "Information",
                            Settings = new Dictionary<string, object>
                            {
                                ["Path"] = "logs/conduit-{Date}.log",
                                ["RollingInterval"] = "Day"
                            }
                        }
                    }
                }
            };
        }
    }

    /// <summary>
    /// Exception thrown when configuration loading fails.
    /// </summary>
    public class ConfigurationLoadException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the ConfigurationLoadException class.
        /// </summary>
        /// <param name="message">Exception message</param>
        public ConfigurationLoadException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConfigurationLoadException class.
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="innerException">Inner exception</param>
        public ConfigurationLoadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}