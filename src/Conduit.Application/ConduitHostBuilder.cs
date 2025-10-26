using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Conduit.Application
{
    /// <summary>
    /// Builder for creating a Conduit application host.
    /// </summary>
    public class ConduitHostBuilder
    {
        private readonly IHostBuilder _hostBuilder;
        private Action<ConduitConfiguration>? _configureConduit;
        private Action<IServiceCollection>? _configureServices;

        /// <summary>
        /// Initializes a new instance of the ConduitHostBuilder class.
        /// </summary>
        public ConduitHostBuilder()
        {
            _hostBuilder = Host.CreateDefaultBuilder();

            // Configure default logging
            _hostBuilder.ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();

                if (context.HostingEnvironment.IsDevelopment())
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                }
            });
        }

        /// <summary>
        /// Configures Conduit-specific settings.
        /// </summary>
        public ConduitHostBuilder ConfigureConduit(Action<ConduitConfiguration> configure)
        {
            _configureConduit = configure ?? throw new ArgumentNullException(nameof(configure));
            return this;
        }

        /// <summary>
        /// Configures services.
        /// </summary>
        public ConduitHostBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            _configureServices = configure ?? throw new ArgumentNullException(nameof(configure));
            return this;
        }

        /// <summary>
        /// Configures the application configuration.
        /// </summary>
        public ConduitHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configure)
        {
            _hostBuilder.ConfigureAppConfiguration(configure);
            return this;
        }

        /// <summary>
        /// Configures logging.
        /// </summary>
        public ConduitHostBuilder ConfigureLogging(Action<HostBuilderContext, ILoggingBuilder> configure)
        {
            _hostBuilder.ConfigureLogging(configure);
            return this;
        }

        /// <summary>
        /// Sets the environment.
        /// </summary>
        public ConduitHostBuilder UseEnvironment(string environment)
        {
            _hostBuilder.UseEnvironment(environment);
            return this;
        }

        /// <summary>
        /// Sets the content root path.
        /// </summary>
        public ConduitHostBuilder UseContentRoot(string contentRoot)
        {
            _hostBuilder.UseContentRoot(contentRoot);
            return this;
        }

        /// <summary>
        /// Adds JSON configuration file.
        /// </summary>
        public ConduitHostBuilder AddJsonFile(string path, bool optional = true, bool reloadOnChange = true)
        {
            _hostBuilder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile(path, optional, reloadOnChange);
            });
            return this;
        }

        /// <summary>
        /// Adds environment variables configuration.
        /// </summary>
        public ConduitHostBuilder AddEnvironmentVariables(string? prefix = null)
        {
            _hostBuilder.ConfigureAppConfiguration((context, config) =>
            {
                if (string.IsNullOrEmpty(prefix))
                {
                    config.AddEnvironmentVariables();
                }
                else
                {
                    config.AddEnvironmentVariables(prefix);
                }
            });
            return this;
        }

        /// <summary>
        /// Adds command line configuration.
        /// </summary>
        public ConduitHostBuilder AddCommandLine(string[] args)
        {
            _hostBuilder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddCommandLine(args);
            });
            return this;
        }

        /// <summary>
        /// Builds the host.
        /// </summary>
        public IHost Build()
        {
            // Configure Conduit services
            _hostBuilder.ConfigureServices((context, services) =>
            {
                // Register Conduit configuration
                var conduitConfig = new ConduitConfiguration();

                // Bind from IConfiguration
                var configSection = context.Configuration.GetSection("Conduit");
                if (configSection.Exists())
                {
                    configSection.Bind(conduitConfig);
                }

                // Apply custom configuration
                _configureConduit?.Invoke(conduitConfig);

                services.Configure<ConduitConfiguration>(options =>
                {
                    options.ApplicationName = conduitConfig.ApplicationName;
                    options.Version = conduitConfig.Version;
                    options.Environment = conduitConfig.Environment;
                    options.ComponentDiscovery = conduitConfig.ComponentDiscovery;
                    options.Messaging = conduitConfig.Messaging;
                    options.Security = conduitConfig.Security;
                    options.Resilience = conduitConfig.Resilience;
                    options.Features = conduitConfig.Features;
                    options.CustomSettings = conduitConfig.CustomSettings;
                });

                // Add Conduit services
                services.AddConduitServices(conduitConfig);

                // Add custom services
                _configureServices?.Invoke(services);

                // Register the host
                services.AddHostedService<ConduitHost>();
            });

            return _hostBuilder.Build();
        }

        /// <summary>
        /// Creates a new ConduitHostBuilder.
        /// </summary>
        public static ConduitHostBuilder CreateDefaultBuilder()
        {
            return new ConduitHostBuilder();
        }

        /// <summary>
        /// Creates a new ConduitHostBuilder with command line arguments.
        /// </summary>
        public static ConduitHostBuilder CreateDefaultBuilder(string[] args)
        {
            var builder = new ConduitHostBuilder();
            builder.AddCommandLine(args);
            return builder;
        }
    }
}
