using System;
using MongoDB.Driver;

namespace Conduit.Persistence.MongoDB
{
    /// <summary>
    /// MongoDB configuration.
    /// </summary>
    public class MongoDbConfiguration
    {
        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";

        /// <summary>
        /// Gets or sets the database name.
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server address.
        /// </summary>
        public string Server { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        public int Port { get; set; } = 27017;

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Gets or sets the authentication database.
        /// </summary>
        public string? AuthenticationDatabase { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use SSL.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the socket timeout in milliseconds.
        /// </summary>
        public int SocketTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum connection pool size.
        /// </summary>
        public int MaxConnectionPoolSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the minimum connection pool size.
        /// </summary>
        public int MinConnectionPoolSize { get; set; } = 0;

        /// <summary>
        /// Gets or sets the replica set name.
        /// </summary>
        public string? ReplicaSetName { get; set; }

        /// <summary>
        /// Creates a MongoDB client from configuration.
        /// </summary>
        public IMongoClient CreateClient()
        {
            var settings = CreateClientSettings();
            return new MongoClient(settings);
        }

        /// <summary>
        /// Creates MongoDB client settings from configuration.
        /// </summary>
        public MongoClientSettings CreateClientSettings()
        {
            MongoClientSettings settings;

            if (!string.IsNullOrEmpty(ConnectionString))
            {
                settings = MongoClientSettings.FromConnectionString(ConnectionString);
            }
            else
            {
                settings = new MongoClientSettings
                {
                    Server = new MongoServerAddress(Server, Port),
                    ConnectTimeout = TimeSpan.FromMilliseconds(ConnectionTimeout),
                    SocketTimeout = TimeSpan.FromMilliseconds(SocketTimeout),
                    MaxConnectionPoolSize = MaxConnectionPoolSize,
                    MinConnectionPoolSize = MinConnectionPoolSize
                };

                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    var credential = MongoCredential.CreateCredential(
                        AuthenticationDatabase ?? DatabaseName,
                        Username,
                        Password);

                    settings.Credential = credential;
                }

                if (UseSsl)
                {
                    settings.UseTls = true;
                }

                if (!string.IsNullOrEmpty(ReplicaSetName))
                {
                    settings.ReplicaSetName = ReplicaSetName;
                }
            }

            return settings;
        }

        /// <summary>
        /// Gets a database instance.
        /// </summary>
        public IMongoDatabase GetDatabase()
        {
            var client = CreateClient();
            return client.GetDatabase(DatabaseName);
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString) && string.IsNullOrEmpty(Server))
                throw new InvalidOperationException("Either ConnectionString or Server must be specified");

            if (string.IsNullOrEmpty(DatabaseName))
                throw new InvalidOperationException("DatabaseName is required");

            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (ConnectionTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(ConnectionTimeout), "ConnectionTimeout must be non-negative");

            if (SocketTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(SocketTimeout), "SocketTimeout must be non-negative");

            if (MaxConnectionPoolSize < MinConnectionPoolSize)
                throw new ArgumentOutOfRangeException(nameof(MaxConnectionPoolSize), "MaxConnectionPoolSize must be >= MinConnectionPoolSize");
        }
    }
}
