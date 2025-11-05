# Conduit Framework Configuration

This directory contains sample YAML configuration files for the Conduit framework.

## Configuration Files

### `conduit-minimal.yaml`
A minimal configuration file with only the essential settings needed to get started. Use this as a starting point for simple applications.

### `conduit-full.yaml`
A comprehensive configuration file demonstrating all available configuration options. Use this as a reference for understanding all capabilities of the Conduit framework.

### `conduit-base.yaml` and `conduit-base.production.yaml`
Example of environment-specific configuration using base configuration with environment overrides. The base file contains common settings, while the production file overrides specific values for production deployment.

## Usage

### Basic Usage

```csharp
using Conduit.Configuration;

var loader = new YamlConfigurationLoader();
var config = await loader.LoadFromFileAsync("conduit-minimal.yaml");
```

### Environment-Specific Configuration

```csharp
var loader = new YamlConfigurationLoader();
var config = await loader.LoadWithEnvironmentAsync(
    "conduit-base.yaml",
    "Production",
    "conduit-base.production.yaml"
);
```

### Multiple Configuration Files

```csharp
var loader = new YamlConfigurationLoader();
var config = await loader.LoadFromMultipleFilesAsync(
    "conduit-base.yaml",
    "conduit-secrets.yaml",
    "conduit-overrides.yaml"
);
```

### Creating Default Configuration

```csharp
var loader = new YamlConfigurationLoader();
var defaultConfig = await loader.CreateDefaultConfigurationAsync(
    "conduit-default.yaml",
    "My Application"
);
```

## Configuration Sections

### Application Information
- `ApplicationName`: Name of your application
- `Version`: Application version
- `Environment`: Environment name (Development, Staging, Production)

### Gateway
Network gateway configuration for handling connections and routing:
- Load balancing strategies
- Rate limiting
- Circuit breaker patterns
- TLS/SSL settings
- CORS configuration

### Messaging
Message processing and routing configuration:
- Message timeouts and size limits
- Dead letter queue settings
- Retry policies
- Message routing rules

### Transports
Multiple transport protocols support:
- HTTP/HTTPS
- TCP/UDP
- ActiveMQ
- ZeroMQ
- WebSocket
- gRPC

### Pipeline
Request/response processing pipeline:
- Authentication behaviors
- Validation behaviors
- Logging behaviors
- Rate limiting behaviors

### Security
Authentication and authorization:
- JWT authentication
- API key authentication
- Role-based authorization
- Encryption settings
- CORS policies

### Serialization
Data serialization options:
- System.Text.Json
- Newtonsoft.Json
- MessagePack
- Protocol Buffers

### Persistence
Database and storage configuration:
- SQL Server
- PostgreSQL
- MongoDB
- Redis

### Metrics
Monitoring and metrics collection:
- Prometheus metrics
- Application Insights
- Custom metrics
- Collection intervals

### Components
Dependency injection and component discovery:
- Auto-discovery settings
- Service registrations
- Module configurations

## Environment Variables

You can use environment variables in your YAML configuration:

```yaml
# Use environment variable syntax
CustomSettings:
  DatabaseConnection: "${DATABASE_CONNECTION_STRING}"
  ApiKey: "${EXTERNAL_API_KEY}"
```

## Validation

The configuration system includes comprehensive validation:
- Required fields are checked
- Value ranges are validated
- Enum values are verified
- Cross-field dependencies are validated

Invalid configurations will throw `ConfigurationLoadException` with detailed error messages.

## Best Practices

1. **Start Small**: Begin with `conduit-minimal.yaml` and add sections as needed
2. **Environment Separation**: Use base configuration with environment-specific overrides
3. **Security**: Never commit secrets to version control; use environment variables
4. **Validation**: Always test configuration changes in a development environment
5. **Documentation**: Comment your configuration files to explain custom settings

## Schema Reference

For a complete reference of all available configuration options, see `conduit-full.yaml` which includes:
- All configuration sections
- Default values
- Example values
- Comments explaining each option

## Troubleshooting

### Common Issues

1. **File Not Found**: Ensure configuration file paths are correct and files exist
2. **Validation Errors**: Check the exception message for specific validation failures
3. **Type Mismatches**: Ensure YAML values match expected C# types
4. **Missing Dependencies**: Verify all referenced assemblies are available

### Error Messages

The configuration loader provides detailed error messages:
- File location information
- Specific validation failures
- Suggested corrections
- Line numbers (where applicable)