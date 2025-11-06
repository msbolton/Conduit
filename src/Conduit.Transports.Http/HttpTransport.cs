using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Conduit.Api;
using Conduit.Common;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Http;

/// <summary>
/// HTTP/REST transport implementation for sending and receiving messages over HTTP
/// </summary>
public class HttpTransport : TransportAdapterBase
{
    private readonly HttpConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, HttpSubscription> _subscriptions;
    private readonly object _subscriptionLock = new();
    private readonly ILogger<HttpTransport> _logger;

    public override TransportType Type => TransportType.Http;
    public override string Name => "HTTP Transport";

    /// <summary>
    /// Initializes a new instance of the HttpTransport class
    /// </summary>
    public HttpTransport(HttpConfiguration configuration, ILogger<HttpTransport> logger)
        : base(configuration, logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = CreateHttpClient();
        _jsonOptions = CreateJsonOptions();
        _subscriptions = new Dictionary<string, HttpSubscription>();
    }

    /// <summary>
    /// Establishes HTTP connection (health check)
    /// </summary>
    protected override async Task ConnectCoreAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Connecting HTTP transport to {BaseUrl}", _configuration.BaseUrl);

        try
        {
            var healthCheckUrl = $"{_configuration.BaseUrl.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(healthCheckUrl, cancellationToken);
            _logger?.LogInformation("HTTP transport connectivity test completed with status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "HTTP connectivity test failed, but transport will continue");
        }

        IsConnected = true;
    }

    /// <summary>
    /// Disconnects HTTP transport
    /// </summary>
    protected override async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Disconnecting HTTP transport");

        lock (_subscriptionLock)
        {
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }

        IsConnected = false;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message over HTTP
    /// </summary>
    protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
    {
        var transportMessage = new TransportMessage
        {
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            CausationId = message.CausationId,
            MessageType = message.GetType().AssemblyQualifiedName ?? message.GetType().FullName ?? "Unknown",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _jsonOptions)),
            ContentType = "application/json",
            Timestamp = DateTimeOffset.UtcNow
        };
        var json = JsonSerializer.Serialize(transportMessage, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));

        var endpoint = destination ?? GetEndpointForMessage(message);
        var response = await SendWithRetryAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"HTTP request failed with status {response.StatusCode}: {errorContent}");
        }

        _logger?.LogDebug("Message sent successfully via HTTP to {Endpoint}", endpoint);
    }

    /// <summary>
    /// Subscribes to HTTP endpoints for receiving messages
    /// </summary>
    protected override async Task<ITransportSubscription> SubscribeCoreAsync(
        string? source,
        Func<TransportMessage, Task> messageHandler,
        CancellationToken cancellationToken)
    {
        var topic = source ?? "default";

        lock (_subscriptionLock)
        {
            if (_subscriptions.ContainsKey(topic))
            {
                throw new InvalidOperationException($"Already subscribed to topic: {topic}");
            }

            var subscription = new HttpSubscription(topic, messageHandler, _configuration, _logger);
            _subscriptions[topic] = subscription;

            _logger?.LogInformation("Subscribed to HTTP topic: {Topic}", topic);
            return subscription;
        }
    }

    /// <summary>
    /// Processes incoming HTTP requests (for server-side scenarios)
    /// </summary>
    public async Task<Result> ProcessHttpRequestAsync(
        string method,
        string path,
        string body,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var transportMessage = JsonSerializer.Deserialize<TransportMessage>(body, _jsonOptions);
            if (transportMessage == null)
            {
                return Result.Failure("Invalid message format");
            }

            var topic = ExtractTopicFromPath(path);

            HttpSubscription? subscription = null;
            lock (_subscriptionLock)
            {
                _subscriptions.TryGetValue(topic, out subscription);
            }

            if (subscription != null)
            {
                await subscription.ProcessTransportMessageAsync(transportMessage);
                return Result.Success();
            }

            _logger?.LogWarning("No subscription found for HTTP topic: {Topic}", topic);
            return Result.Failure($"No subscription for topic: {topic}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing HTTP request");
            return Result.Failure($"Request processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the HTTP transport
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();

            lock (_subscriptionLock)
            {
                foreach (var subscription in _subscriptions.Values)
                {
                    subscription.Dispose();
                }
                _subscriptions.Clear();
            }
        }

        base.Dispose(disposing);
    }

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler();
        if (_configuration.EnableCompression)
        {
            handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
        }

        var client = new HttpClient(handler)
        {
            Timeout = _configuration.RequestTimeout,
            BaseAddress = new Uri(_configuration.BaseUrl)
        };

        // Set default headers
        foreach (var header in _configuration.DefaultHeaders)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        // Configure authentication
        ConfigureAuthentication(client);

        return client;
    }

    private void ConfigureAuthentication(HttpClient client)
    {
        if (_configuration.Authentication == null) return;

        var auth = _configuration.Authentication;
        switch (auth.Type.ToLowerInvariant())
        {
            case "bearer":
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
                break;
            case "basic":
                if (!string.IsNullOrEmpty(auth.Username) && !string.IsNullOrEmpty(auth.Password))
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                break;
            case "apikey":
                if (!string.IsNullOrEmpty(auth.ApiKeyHeader))
                {
                    client.DefaultRequestHeaders.Add(auth.ApiKeyHeader, auth.Token);
                }
                break;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    private string GetEndpointForMessage(IMessage message)
    {
        var messageType = message.MessageType.ToLowerInvariant();
        return $"{_configuration.BaseUrl.TrimEnd('/')}/messages/{messageType}";
    }

    private static string ExtractTopicFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 1 ? segments[^1] : "default";
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string endpoint, HttpContent content, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = _configuration.Retry.BaseDelay;

        while (attempt < _configuration.Retry.MaxAttempts)
        {
            try
            {
                var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

                if (response.IsSuccessStatusCode || !_configuration.Retry.RetryableStatusCodes.Contains((int)response.StatusCode))
                {
                    return response;
                }

                _logger?.LogWarning("HTTP request failed with retryable status {StatusCode}, attempt {Attempt}/{MaxAttempts}",
                    response.StatusCode, attempt + 1, _configuration.Retry.MaxAttempts);
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogWarning(ex, "HTTP request exception on attempt {Attempt}/{MaxAttempts}",
                    attempt + 1, _configuration.Retry.MaxAttempts);
            }

            attempt++;
            if (attempt < _configuration.Retry.MaxAttempts)
            {
                await Task.Delay(delay, cancellationToken);

                if (_configuration.Retry.UseExponentialBackoff)
                {
                    delay = TimeSpan.FromMilliseconds(Math.Min(
                        delay.TotalMilliseconds * 2,
                        _configuration.Retry.MaxDelay.TotalMilliseconds));
                }
            }
        }

        throw new HttpRequestException($"HTTP request failed after {_configuration.Retry.MaxAttempts} attempts");
    }
}