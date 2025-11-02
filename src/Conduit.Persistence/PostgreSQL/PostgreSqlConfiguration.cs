using System;

namespace Conduit.Persistence.PostgreSQL
{
    /// <summary>
    /// PostgreSQL database configuration.
    /// </summary>
    public class PostgreSqlConfiguration
    {
        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the host.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port.
        /// </summary>
        public int Port { get; set; } = 5432;

        /// <summary>
        /// Gets or sets the database name.
        /// </summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to use SSL.
        /// </summary>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum pool size.
        /// </summary>
        public int MinPoolSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum pool size.
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectionTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets the command timeout in seconds.
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether to enable connection pooling.
        /// </summary>
        public bool Pooling { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable detailed errors.
        /// </summary>
        public bool EnableDetailedErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable sensitive data logging.
        /// </summary>
        public bool EnableSensitiveDataLogging { get; set; } = false;

        /// <summary>
        /// Builds the connection string from configuration.
        /// </summary>
        public string BuildConnectionString()
        {
            if (!string.IsNullOrEmpty(ConnectionString))
                return ConnectionString;

            var builder = new System.Text.StringBuilder();
            builder.Append($"Host={Host};");
            builder.Append($"Port={Port};");
            builder.Append($"Database={Database};");
            builder.Append($"Username={Username};");
            builder.Append($"Password={Password};");

            if (UseSsl)
                builder.Append("SSL Mode=Require;");

            if (Pooling)
            {
                builder.Append($"Minimum Pool Size={MinPoolSize};");
                builder.Append($"Maximum Pool Size={MaxPoolSize};");
            }
            else
            {
                builder.Append("Pooling=false;");
            }

            builder.Append($"Timeout={ConnectionTimeout};");
            builder.Append($"Command Timeout={CommandTimeout};");

            return builder.ToString();
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                if (string.IsNullOrEmpty(Host))
                    throw new InvalidOperationException("Host is required");

                if (string.IsNullOrEmpty(Database))
                    throw new InvalidOperationException("Database is required");

                if (string.IsNullOrEmpty(Username))
                    throw new InvalidOperationException("Username is required");
            }

            if (Port < 1 || Port > 65535)
                throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");

            if (MinPoolSize < 0)
                throw new ArgumentOutOfRangeException(nameof(MinPoolSize), "MinPoolSize must be non-negative");

            if (MaxPoolSize < MinPoolSize)
                throw new ArgumentOutOfRangeException(nameof(MaxPoolSize), "MaxPoolSize must be >= MinPoolSize");

            if (ConnectionTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(ConnectionTimeout), "ConnectionTimeout must be non-negative");

            if (CommandTimeout < 0)
                throw new ArgumentOutOfRangeException(nameof(CommandTimeout), "CommandTimeout must be non-negative");
        }
    }
}
