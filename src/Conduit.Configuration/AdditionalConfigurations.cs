using System;
using System.Collections.Generic;

namespace Conduit.Configuration
{
    /// <summary>
    /// Pipeline configuration for message processing pipelines.
    /// </summary>
    public class PipelineConfiguration
    {
        /// <summary>
        /// Gets or sets whether pipeline processing is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the default pipeline timeout in milliseconds.
        /// </summary>
        public int DefaultTimeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets the maximum pipeline depth.
        /// </summary>
        public int MaxDepth { get; set; } = 10;

        /// <summary>
        /// Gets or sets whether to enable pipeline metrics.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets pipeline stage configurations.
        /// </summary>
        public List<PipelineStageConfiguration> Stages { get; set; } = new();

        /// <summary>
        /// Gets or sets pipeline behavior configurations.
        /// </summary>
        public List<PipelineBehaviorConfiguration> Behaviors { get; set; } = new();

        /// <summary>
        /// Validates the pipeline configuration.
        /// </summary>
        public void Validate()
        {
            if (DefaultTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(DefaultTimeout), "DefaultTimeout must be greater than 0");

            if (MaxDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxDepth), "MaxDepth must be greater than 0");

            foreach (var stage in Stages)
                stage.Validate();

            foreach (var behavior in Behaviors)
                behavior.Validate();
        }
    }

    /// <summary>
    /// Pipeline stage configuration.
    /// </summary>
    public class PipelineStageConfiguration
    {
        /// <summary>
        /// Gets or sets the stage name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stage type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the stage is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the stage order.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets the stage timeout in milliseconds.
        /// </summary>
        public int Timeout { get; set; } = 30000;

        /// <summary>
        /// Gets or sets stage-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Validates the pipeline stage configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Stage Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Stage Type cannot be null or empty");

            if (Timeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be greater than 0");
        }
    }

    /// <summary>
    /// Pipeline behavior configuration.
    /// </summary>
    public class PipelineBehaviorConfiguration
    {
        /// <summary>
        /// Gets or sets the behavior name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the behavior type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the behavior is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the behavior order.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Gets or sets behavior-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Validates the pipeline behavior configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Behavior Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Behavior Type cannot be null or empty");
        }
    }

    /// <summary>
    /// Security configuration.
    /// </summary>
    public class SecurityConfiguration
    {
        /// <summary>
        /// Gets or sets whether security is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets authentication configuration.
        /// </summary>
        public AuthenticationConfiguration Authentication { get; set; } = new();

        /// <summary>
        /// Gets or sets authorization configuration.
        /// </summary>
        public AuthorizationConfiguration Authorization { get; set; } = new();

        /// <summary>
        /// Gets or sets encryption configuration.
        /// </summary>
        public EncryptionConfiguration Encryption { get; set; } = new();

        /// <summary>
        /// Gets or sets certificate configuration.
        /// </summary>
        public CertificateConfiguration Certificates { get; set; } = new();

        /// <summary>
        /// Validates the security configuration.
        /// </summary>
        public void Validate()
        {
            Authentication?.Validate();
            Authorization?.Validate();
            Encryption?.Validate();
            Certificates?.Validate();
        }
    }

    /// <summary>
    /// Authentication configuration.
    /// </summary>
    public class AuthenticationConfiguration
    {
        /// <summary>
        /// Gets or sets whether authentication is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the authentication scheme.
        /// </summary>
        public string Scheme { get; set; } = "Bearer";

        /// <summary>
        /// Gets or sets JWT configuration.
        /// </summary>
        public JwtConfiguration Jwt { get; set; } = new();

        /// <summary>
        /// Gets or sets API key configuration.
        /// </summary>
        public ApiKeyConfiguration ApiKey { get; set; } = new();

        /// <summary>
        /// Gets or sets OAuth configuration.
        /// </summary>
        public OAuthConfiguration OAuth { get; set; } = new();

        /// <summary>
        /// Validates the authentication configuration.
        /// </summary>
        public void Validate()
        {
            var validSchemes = new[] { "Bearer", "Basic", "ApiKey", "OAuth", "Certificate" };
            if (!validSchemes.Contains(Scheme))
                throw new ArgumentException($"Invalid authentication Scheme: {Scheme}");

            Jwt?.Validate();
            ApiKey?.Validate();
            OAuth?.Validate();
        }
    }

    /// <summary>
    /// JWT configuration.
    /// </summary>
    public class JwtConfiguration
    {
        /// <summary>
        /// Gets or sets whether JWT authentication is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the JWT issuer.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT audience.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT signing key.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT expiration time in minutes.
        /// </summary>
        public int ExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets whether to validate the issuer.
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the audience.
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the lifetime.
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Validates the JWT configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(SigningKey))
                    throw new ArgumentException("JWT SigningKey cannot be null or empty when JWT is enabled");

                if (ExpirationMinutes <= 0)
                    throw new ArgumentOutOfRangeException(nameof(ExpirationMinutes), "ExpirationMinutes must be greater than 0");
            }
        }
    }

    /// <summary>
    /// API key configuration.
    /// </summary>
    public class ApiKeyConfiguration
    {
        /// <summary>
        /// Gets or sets whether API key authentication is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the header name for the API key.
        /// </summary>
        public string HeaderName { get; set; } = "X-API-Key";

        /// <summary>
        /// Gets or sets valid API keys.
        /// </summary>
        public List<string> ValidKeys { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to hash API keys.
        /// </summary>
        public bool HashKeys { get; set; } = true;

        /// <summary>
        /// Validates the API key configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled && !ValidKeys.Any())
                throw new ArgumentException("ValidKeys cannot be empty when API key authentication is enabled");

            if (string.IsNullOrWhiteSpace(HeaderName))
                throw new ArgumentException("HeaderName cannot be null or empty");
        }
    }

    /// <summary>
    /// OAuth configuration.
    /// </summary>
    public class OAuthConfiguration
    {
        /// <summary>
        /// Gets or sets whether OAuth is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the OAuth authority URL.
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OAuth audience.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OAuth scopes.
        /// </summary>
        public List<string> Scopes { get; set; } = new();

        /// <summary>
        /// Validates the OAuth configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(Authority))
                    throw new ArgumentException("OAuth Authority cannot be null or empty when OAuth is enabled");
            }
        }
    }

    /// <summary>
    /// Authorization configuration.
    /// </summary>
    public class AuthorizationConfiguration
    {
        /// <summary>
        /// Gets or sets whether authorization is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets role-based access control configuration.
        /// </summary>
        public RbacConfiguration Rbac { get; set; } = new();

        /// <summary>
        /// Gets or sets policy-based authorization policies.
        /// </summary>
        public List<AuthorizationPolicyConfiguration> Policies { get; set; } = new();

        /// <summary>
        /// Validates the authorization configuration.
        /// </summary>
        public void Validate()
        {
            Rbac?.Validate();

            foreach (var policy in Policies)
                policy.Validate();
        }
    }

    /// <summary>
    /// Role-based access control configuration.
    /// </summary>
    public class RbacConfiguration
    {
        /// <summary>
        /// Gets or sets whether RBAC is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets role definitions.
        /// </summary>
        public List<RoleConfiguration> Roles { get; set; } = new();

        /// <summary>
        /// Gets or sets permission definitions.
        /// </summary>
        public List<PermissionConfiguration> Permissions { get; set; } = new();

        /// <summary>
        /// Validates the RBAC configuration.
        /// </summary>
        public void Validate()
        {
            foreach (var role in Roles)
                role.Validate();

            foreach (var permission in Permissions)
                permission.Validate();
        }
    }

    /// <summary>
    /// Role configuration.
    /// </summary>
    public class RoleConfiguration
    {
        /// <summary>
        /// Gets or sets the role name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the role description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permissions for this role.
        /// </summary>
        public List<string> Permissions { get; set; } = new();

        /// <summary>
        /// Validates the role configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Role Name cannot be null or empty");
        }
    }

    /// <summary>
    /// Permission configuration.
    /// </summary>
    public class PermissionConfiguration
    {
        /// <summary>
        /// Gets or sets the permission name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the permission description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resource this permission applies to.
        /// </summary>
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the actions this permission allows.
        /// </summary>
        public List<string> Actions { get; set; } = new();

        /// <summary>
        /// Validates the permission configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Permission Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Resource))
                throw new ArgumentException("Permission Resource cannot be null or empty");
        }
    }

    /// <summary>
    /// Authorization policy configuration.
    /// </summary>
    public class AuthorizationPolicyConfiguration
    {
        /// <summary>
        /// Gets or sets the policy name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the policy requirements.
        /// </summary>
        public List<string> Requirements { get; set; } = new();

        /// <summary>
        /// Gets or sets the resources this policy applies to.
        /// </summary>
        public List<string> Resources { get; set; } = new();

        /// <summary>
        /// Validates the authorization policy configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Policy Name cannot be null or empty");

            if (!Requirements.Any())
                throw new ArgumentException("Policy Requirements cannot be empty");
        }
    }

    /// <summary>
    /// Encryption configuration.
    /// </summary>
    public class EncryptionConfiguration
    {
        /// <summary>
        /// Gets or sets whether encryption is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the encryption algorithm.
        /// </summary>
        public string Algorithm { get; set; } = "AES256";

        /// <summary>
        /// Gets or sets the encryption key.
        /// </summary>
        public string EncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to encrypt message bodies.
        /// </summary>
        public bool EncryptMessageBodies { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to encrypt message headers.
        /// </summary>
        public bool EncryptMessageHeaders { get; set; } = false;

        /// <summary>
        /// Validates the encryption configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(EncryptionKey))
                    throw new ArgumentException("EncryptionKey cannot be null or empty when encryption is enabled");

                var validAlgorithms = new[] { "AES128", "AES192", "AES256", "ChaCha20" };
                if (!validAlgorithms.Contains(Algorithm))
                    throw new ArgumentException($"Invalid encryption Algorithm: {Algorithm}");
            }
        }
    }

    /// <summary>
    /// Certificate configuration.
    /// </summary>
    public class CertificateConfiguration
    {
        /// <summary>
        /// Gets or sets certificate store configurations.
        /// </summary>
        public List<CertificateStoreConfiguration> Stores { get; set; } = new();

        /// <summary>
        /// Gets or sets certificate validation settings.
        /// </summary>
        public CertificateValidationConfiguration Validation { get; set; } = new();

        /// <summary>
        /// Validates the certificate configuration.
        /// </summary>
        public void Validate()
        {
            foreach (var store in Stores)
                store.Validate();

            Validation?.Validate();
        }
    }

    /// <summary>
    /// Certificate store configuration.
    /// </summary>
    public class CertificateStoreConfiguration
    {
        /// <summary>
        /// Gets or sets the store name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the store location.
        /// </summary>
        public string Location { get; set; } = "CurrentUser";

        /// <summary>
        /// Gets or sets the certificate thumbprint.
        /// </summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the certificate subject name.
        /// </summary>
        public string SubjectName { get; set; } = string.Empty;

        /// <summary>
        /// Validates the certificate store configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Certificate store Name cannot be null or empty");

            var validLocations = new[] { "CurrentUser", "LocalMachine" };
            if (!validLocations.Contains(Location))
                throw new ArgumentException($"Invalid certificate store Location: {Location}");
        }
    }

    /// <summary>
    /// Certificate validation configuration.
    /// </summary>
    public class CertificateValidationConfiguration
    {
        /// <summary>
        /// Gets or sets whether to validate certificate chain.
        /// </summary>
        public bool ValidateChain { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate certificate revocation.
        /// </summary>
        public bool ValidateRevocation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to allow self-signed certificates.
        /// </summary>
        public bool AllowSelfSigned { get; set; } = false;

        /// <summary>
        /// Gets or sets trusted certificate authorities.
        /// </summary>
        public List<string> TrustedCAs { get; set; } = new();

        /// <summary>
        /// Validates the certificate validation configuration.
        /// </summary>
        public void Validate()
        {
            // No specific validation needed
        }
    }

    /// <summary>
    /// Serialization configuration.
    /// </summary>
    public class SerializationConfiguration
    {
        /// <summary>
        /// Gets or sets the default serializer.
        /// </summary>
        public string DefaultSerializer { get; set; } = "Json";

        /// <summary>
        /// Gets or sets whether to compress serialized data.
        /// </summary>
        public bool EnableCompression { get; set; } = false;

        /// <summary>
        /// Gets or sets the compression algorithm.
        /// </summary>
        public string CompressionAlgorithm { get; set; } = "GZip";

        /// <summary>
        /// Gets or sets serializer-specific configurations.
        /// </summary>
        public Dictionary<string, object> SerializerSettings { get; set; } = new();

        /// <summary>
        /// Gets or sets type mappings for serialization.
        /// </summary>
        public Dictionary<string, string> TypeMappings { get; set; } = new();

        /// <summary>
        /// Validates the serialization configuration.
        /// </summary>
        public void Validate()
        {
            var validSerializers = new[] { "Json", "Xml", "MessagePack", "Protobuf", "Avro" };
            if (!validSerializers.Contains(DefaultSerializer))
                throw new ArgumentException($"Invalid DefaultSerializer: {DefaultSerializer}");

            var validAlgorithms = new[] { "GZip", "Deflate", "Brotli" };
            if (!validAlgorithms.Contains(CompressionAlgorithm))
                throw new ArgumentException($"Invalid CompressionAlgorithm: {CompressionAlgorithm}");
        }
    }

    /// <summary>
    /// Persistence configuration.
    /// </summary>
    public class PersistenceConfiguration
    {
        /// <summary>
        /// Gets or sets whether persistence is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets database configurations.
        /// </summary>
        public DatabaseConfiguration Database { get; set; } = new();

        /// <summary>
        /// Gets or sets caching configurations.
        /// </summary>
        public CacheConfiguration Cache { get; set; } = new();

        /// <summary>
        /// Gets or sets saga persistence configuration.
        /// </summary>
        public SagaPersistenceConfiguration Saga { get; set; } = new();

        /// <summary>
        /// Validates the persistence configuration.
        /// </summary>
        public void Validate()
        {
            Database?.Validate();
            Cache?.Validate();
            Saga?.Validate();
        }
    }

    /// <summary>
    /// Database configuration.
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Gets or sets the database provider.
        /// </summary>
        public string Provider { get; set; } = "PostgreSQL";

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets connection pool settings.
        /// </summary>
        public ConnectionPoolConfiguration ConnectionPool { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to enable database migrations.
        /// </summary>
        public bool EnableMigrations { get; set; } = true;

        /// <summary>
        /// Validates the database configuration.
        /// </summary>
        public void Validate()
        {
            var validProviders = new[] { "PostgreSQL", "MySQL", "SQLServer", "SQLite", "MongoDB" };
            if (!validProviders.Contains(Provider))
                throw new ArgumentException($"Invalid database Provider: {Provider}");

            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ArgumentException("Database ConnectionString cannot be null or empty");

            ConnectionPool?.Validate();
        }
    }

    /// <summary>
    /// Cache configuration.
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Gets or sets whether caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the cache provider.
        /// </summary>
        public string Provider { get; set; } = "Redis";

        /// <summary>
        /// Gets or sets the cache connection string.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default cache expiration in seconds.
        /// </summary>
        public int DefaultExpiration { get; set; } = 3600; // 1 hour

        /// <summary>
        /// Gets or sets cache-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Validates the cache configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                var validProviders = new[] { "Redis", "MemoryCache", "Memcached" };
                if (!validProviders.Contains(Provider))
                    throw new ArgumentException($"Invalid cache Provider: {Provider}");

                if (string.IsNullOrWhiteSpace(ConnectionString) && Provider != "MemoryCache")
                    throw new ArgumentException("Cache ConnectionString cannot be null or empty for external cache providers");

                if (DefaultExpiration <= 0)
                    throw new ArgumentOutOfRangeException(nameof(DefaultExpiration), "DefaultExpiration must be greater than 0");
            }
        }
    }

    /// <summary>
    /// Saga persistence configuration.
    /// </summary>
    public class SagaPersistenceConfiguration
    {
        /// <summary>
        /// Gets or sets whether saga persistence is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the saga storage provider.
        /// </summary>
        public string Provider { get; set; } = "Database";

        /// <summary>
        /// Gets or sets the saga table/collection name.
        /// </summary>
        public string TableName { get; set; } = "Sagas";

        /// <summary>
        /// Gets or sets the saga timeout in minutes.
        /// </summary>
        public int TimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Validates the saga persistence configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                var validProviders = new[] { "Database", "Cache", "File" };
                if (!validProviders.Contains(Provider))
                    throw new ArgumentException($"Invalid saga Provider: {Provider}");

                if (string.IsNullOrWhiteSpace(TableName))
                    throw new ArgumentException("Saga TableName cannot be null or empty");

                if (TimeoutMinutes <= 0)
                    throw new ArgumentOutOfRangeException(nameof(TimeoutMinutes), "TimeoutMinutes must be greater than 0");
            }
        }
    }

    /// <summary>
    /// Metrics and monitoring configuration.
    /// </summary>
    public class MetricsConfiguration
    {
        /// <summary>
        /// Gets or sets whether metrics collection is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the metrics collection interval in seconds.
        /// </summary>
        public int CollectionInterval { get; set; } = 60;

        /// <summary>
        /// Gets or sets Prometheus metrics configuration.
        /// </summary>
        public PrometheusConfiguration Prometheus { get; set; } = new();

        /// <summary>
        /// Gets or sets application insights configuration.
        /// </summary>
        public ApplicationInsightsConfiguration ApplicationInsights { get; set; } = new();

        /// <summary>
        /// Gets or sets custom metrics exporters.
        /// </summary>
        public List<MetricsExporterConfiguration> Exporters { get; set; } = new();

        /// <summary>
        /// Validates the metrics configuration.
        /// </summary>
        public void Validate()
        {
            if (CollectionInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(CollectionInterval), "CollectionInterval must be greater than 0");

            Prometheus?.Validate();
            ApplicationInsights?.Validate();

            foreach (var exporter in Exporters)
                exporter.Validate();
        }
    }

    /// <summary>
    /// Prometheus metrics configuration.
    /// </summary>
    public class PrometheusConfiguration
    {
        /// <summary>
        /// Gets or sets whether Prometheus metrics are enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the metrics endpoint path.
        /// </summary>
        public string MetricsPath { get; set; } = "/metrics";

        /// <summary>
        /// Gets or sets the metrics port.
        /// </summary>
        public int Port { get; set; } = 9090;

        /// <summary>
        /// Validates the Prometheus configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(MetricsPath))
                    throw new ArgumentException("Prometheus MetricsPath cannot be null or empty");

                if (!MetricsPath.StartsWith("/"))
                    throw new ArgumentException("Prometheus MetricsPath must start with '/'");

                if (Port < 1 || Port > 65535)
                    throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535");
            }
        }
    }

    /// <summary>
    /// Application Insights configuration.
    /// </summary>
    public class ApplicationInsightsConfiguration
    {
        /// <summary>
        /// Gets or sets whether Application Insights is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the instrumentation key.
        /// </summary>
        public string InstrumentationKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Validates the Application Insights configuration.
        /// </summary>
        public void Validate()
        {
            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(InstrumentationKey) && string.IsNullOrWhiteSpace(ConnectionString))
                    throw new ArgumentException("Either InstrumentationKey or ConnectionString must be provided when Application Insights is enabled");
            }
        }
    }

    /// <summary>
    /// Metrics exporter configuration.
    /// </summary>
    public class MetricsExporterConfiguration
    {
        /// <summary>
        /// Gets or sets the exporter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exporter type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the exporter is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets exporter-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Validates the metrics exporter configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Exporter Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Exporter Type cannot be null or empty");
        }
    }

    /// <summary>
    /// Components configuration.
    /// </summary>
    public class ComponentsConfiguration
    {
        /// <summary>
        /// Gets or sets whether components are enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets component discovery paths.
        /// </summary>
        public List<string> DiscoveryPaths { get; set; } = new();

        /// <summary>
        /// Gets or sets component configurations.
        /// </summary>
        public List<ComponentConfiguration> Components { get; set; } = new();

        /// <summary>
        /// Gets or sets component dependency resolution settings.
        /// </summary>
        public DependencyResolutionConfiguration DependencyResolution { get; set; } = new();

        /// <summary>
        /// Validates the components configuration.
        /// </summary>
        public void Validate()
        {
            foreach (var component in Components)
                component.Validate();

            DependencyResolution?.Validate();
        }
    }

    /// <summary>
    /// Component configuration.
    /// </summary>
    public class ComponentConfiguration
    {
        /// <summary>
        /// Gets or sets the component name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the component type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the component is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the component assembly path.
        /// </summary>
        public string? AssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets component-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Gets or sets component dependencies.
        /// </summary>
        public List<string> Dependencies { get; set; } = new();

        /// <summary>
        /// Validates the component configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Component Name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Component Type cannot be null or empty");
        }
    }

    /// <summary>
    /// Dependency resolution configuration.
    /// </summary>
    public class DependencyResolutionConfiguration
    {
        /// <summary>
        /// Gets or sets whether to enable automatic dependency resolution.
        /// </summary>
        public bool EnableAutoResolution { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate dependencies at startup.
        /// </summary>
        public bool ValidateDependencies { get; set; } = true;

        /// <summary>
        /// Gets or sets the dependency resolution timeout in milliseconds.
        /// </summary>
        public int ResolutionTimeout { get; set; } = 30000;

        /// <summary>
        /// Validates the dependency resolution configuration.
        /// </summary>
        public void Validate()
        {
            if (ResolutionTimeout <= 0)
                throw new ArgumentOutOfRangeException(nameof(ResolutionTimeout), "ResolutionTimeout must be greater than 0");
        }
    }
}