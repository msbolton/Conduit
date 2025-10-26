using System;
using StackExchange.Redis;

namespace Conduit.Persistence.Caching
{
    /// <summary>
    /// Redis configuration.
    /// </summary>
    public class RedisConfiguration
    {
        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; } = "localhost:6379";

        /// <summary>
        /// Gets or sets the host.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        public int Port { get; set; } = 6379;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the database number.
        /// </summary>
        public int Database { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to use SSL.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectTimeout { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the sync timeout in milliseconds.
        /// </summary>
        public int SyncTimeout { get; set; } = 5000;

        /// <summary>
        /// Gets or sets a value indicating whether to abort on connect failure.
        /// </summary>
        public bool AbortOnConnectFail { get; set; } = false;

        /// <summary>
        /// Gets or sets the key prefix.
        /// </summary>
        public string? KeyPrefix { get; set; }

        /// <summary>
        /// Gets or sets the default expiration time.
        /// </summary>
        public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether to allow admin operations.
        /// </summary>
        public bool AllowAdmin { get; set; } = false;

        /// <summary>
        /// Creates a connection multiplexer from configuration.
        /// </summary>
        public IConnectionMultiplexer CreateConnection()
        {
            var options = CreateConfigurationOptions();
            return ConnectionMultiplexer.Connect(options);
        }

        /// <summary>
        /// Creates Redis configuration options.
        /// </summary>
        public ConfigurationOptions CreateConfigurationOptions()
        {
            ConfigurationOptions options;

            if (!string.IsNullOrEmpty(ConnectionString))
            {
                options = ConfigurationOptions.Parse(ConnectionString);
            }
            else
            {
                options = new ConfigurationOptions
                {
                    EndPoints = { { Host, Port } }
                };
            }

            if (!string.IsNullOrEmpty(Password))
                options.Password = Password;

            options.Ssl = UseSsl;
            options.ConnectTimeout = ConnectTimeout;
            options.SyncTimeout = SyncTimeout;
            options.AbortOnConnectFail = AbortOnConnectFail;
            options.AllowAdmin = AllowAdmin;

            return options;
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString) && string.IsNullOrEmpty(Host))
                throw new InvalidOperationException("Either ConnectionString or Host must be specified");

            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (Database < 0 || Database > 15)
                throw new ArgumentOutOfRangeException(nameof(Database), "Database must be between 0 and 15");

            if (ConnectTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "ConnectTimeout must be non-negative");

            if (SyncTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(SyncTimeout), "SyncTimeout must be non-negative");
        }
    }
}
