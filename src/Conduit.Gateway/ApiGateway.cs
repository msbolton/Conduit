using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Messaging;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// API Gateway for routing, load balancing, and rate limiting.
    /// </summary>
    public class ApiGateway : IDisposable
    {
        private readonly GatewayConfiguration _configuration;
        private readonly RouteManager _routeManager;
        private readonly LoadBalancer _loadBalancer;
        private readonly RateLimiter _rateLimiter;
        private readonly ILogger<ApiGateway> _logger;
        private readonly IMessageBus? _messageBus;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly ConcurrentDictionary<string, GatewayMetrics> _metrics;

        private volatile bool _isRunning;
        private volatile bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the ApiGateway class.
        /// </summary>
        /// <param name="configuration">The gateway configuration</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="messageBus">Optional message bus for message-based routing</param>
        public ApiGateway(
            GatewayConfiguration configuration,
            ILogger<ApiGateway> logger,
            IMessageBus? messageBus = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageBus = messageBus;

            _configuration.Validate();

            var routeLogger = logger;
            var lbLogger = logger;
            var rlLogger = logger;

            _routeManager = new RouteManager(routeLogger);
            _loadBalancer = new LoadBalancer(lbLogger);
            _rateLimiter = new RateLimiter(rlLogger, _configuration.DefaultRateLimit);

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(_configuration.RequestTimeout)
            };

            _concurrencySemaphore = new SemaphoreSlim(
                _configuration.MaxConcurrentRequests,
                _configuration.MaxConcurrentRequests);

            _metrics = new ConcurrentDictionary<string, GatewayMetrics>();

            // Load routes from configuration
            foreach (var route in _configuration.Routes)
            {
                _routeManager.AddRoute(route);
            }
        }

        /// <summary>
        /// Starts the API gateway.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
                throw new InvalidOperationException("Gateway is already running");

            _logger.LogInformation("Starting API Gateway on {Host}:{Port}", _configuration.Host, _configuration.Port);

            _isRunning = true;

            _logger.LogInformation("API Gateway started with {RouteCount} routes",
                _routeManager.GetAllRoutes().Count());

            await Task.CompletedTask;
        }

        /// <summary>
        /// Stops the API gateway.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return;

            _logger.LogInformation("Stopping API Gateway");

            _isRunning = false;

            _logger.LogInformation("API Gateway stopped");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Processes an HTTP request through the gateway.
        /// </summary>
        /// <param name="path">The request path</param>
        /// <param name="method">The HTTP method</param>
        /// <param name="clientId">The client identifier</param>
        /// <param name="content">The request content</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The gateway response</returns>
        public async Task<GatewayResponse> ProcessRequestAsync(
            string path,
            string method,
            string clientId,
            HttpContent? content = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Check rate limit
                if (_configuration.EnableRateLimiting)
                {
                    var routeMatch = _routeManager.MatchRoute(path, method);
                    var rateLimit = routeMatch?.Route.RateLimit ?? _configuration.DefaultRateLimit;

                    if (!_rateLimiter.AllowRequest(clientId, rateLimit))
                    {
                        return new GatewayResponse
                        {
                            StatusCode = 429,
                            Message = "Rate limit exceeded",
                            Success = false
                        };
                    }
                }

                // Wait for concurrency slot
                await _concurrencySemaphore.WaitAsync(cancellationToken);

                try
                {
                    // Match route
                    var match = _routeManager.MatchRoute(path, method);

                    if (match == null)
                    {
                        return new GatewayResponse
                        {
                            StatusCode = 404,
                            Message = "No route found",
                            Success = false
                        };
                    }

                    // Select upstream
                    var strategy = match.Route.LoadBalancingStrategy ?? _configuration.DefaultLoadBalancingStrategy;
                    var upstream = _loadBalancer.SelectUpstream(match.Route.Upstreams, strategy, clientId);

                    if (upstream == null)
                    {
                        return new GatewayResponse
                        {
                            StatusCode = 503,
                            Message = "No healthy upstream available",
                            Success = false
                        };
                    }

                    // Forward request
                    _loadBalancer.RecordRequestStart(upstream);

                    try
                    {
                        var response = await ForwardRequestAsync(upstream, path, method, content, match, cancellationToken);

                        _loadBalancer.RecordRequestComplete(upstream, response.Success);

                        // Record metrics
                        RecordMetrics(match.Route.Id, response.Success, stopwatch.ElapsedMilliseconds);

                        return response;
                    }
                    catch
                    {
                        _loadBalancer.RecordRequestComplete(upstream, false);
                        throw;
                    }
                }
                finally
                {
                    _concurrencySemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request {Method} {Path}", method, path);

                return new GatewayResponse
                {
                    StatusCode = 500,
                    Message = "Internal gateway error",
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Forwards a request to an upstream server.
        /// </summary>
        private async Task<GatewayResponse> ForwardRequestAsync(
            string upstream,
            string path,
            string method,
            HttpContent? content,
            RouteMatch match,
            CancellationToken cancellationToken)
        {
            var upstreamUrl = $"{upstream.TrimEnd('/')}{path}";

            _logger.LogDebug("Forwarding {Method} {Path} to {Upstream}", method, path, upstreamUrl);

            try
            {
                var request = new HttpRequestMessage(new HttpMethod(method), upstreamUrl);

                // Add upstream headers
                foreach (var header in match.Route.UpstreamHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (content != null)
                {
                    request.Content = content;
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                return new GatewayResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Message = response.ReasonPhrase ?? "OK",
                    Content = responseContent,
                    Success = response.IsSuccessStatusCode
                };
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogWarning("Request to {Upstream} timed out", upstream);

                return new GatewayResponse
                {
                    StatusCode = 504,
                    Message = "Gateway timeout",
                    Success = false,
                    Error = "Upstream request timed out"
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error forwarding request to {Upstream}", upstream);

                return new GatewayResponse
                {
                    StatusCode = 502,
                    Message = "Bad gateway",
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Records metrics for a request.
        /// </summary>
        private void RecordMetrics(string routeId, bool success, long responseTimeMs)
        {
            if (!_configuration.EnableMetrics)
                return;

            var metrics = _metrics.GetOrAdd(routeId, _ => new GatewayMetrics());
            metrics.RecordRequest(success, responseTimeMs);
        }

        /// <summary>
        /// Gets the metrics for a route.
        /// </summary>
        /// <param name="routeId">The route ID</param>
        /// <returns>The gateway metrics, or null if not found</returns>
        public GatewayMetrics? GetMetrics(string routeId)
        {
            _metrics.TryGetValue(routeId, out var metrics);
            return metrics;
        }

        /// <summary>
        /// Gets all metrics.
        /// </summary>
        /// <returns>All gateway metrics</returns>
        public ConcurrentDictionary<string, GatewayMetrics> GetAllMetrics()
        {
            return _metrics;
        }

        /// <summary>
        /// Disposes the gateway.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            StopAsync().GetAwaiter().GetResult();

            _httpClient.Dispose();
            _concurrencySemaphore.Dispose();

            _logger.LogInformation("API Gateway disposed");
        }
    }

    /// <summary>
    /// Gateway response.
    /// </summary>
    public class GatewayResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets whether the request was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error message (if any).
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Gateway metrics.
    /// </summary>
    public class GatewayMetrics
    {
        private long _totalRequests;
        private long _successfulRequests;
        private long _failedRequests;
        private long _totalResponseTimeMs;

        /// <summary>
        /// Gets the total number of requests.
        /// </summary>
        public long TotalRequests => _totalRequests;

        /// <summary>
        /// Gets the number of successful requests.
        /// </summary>
        public long SuccessfulRequests => _successfulRequests;

        /// <summary>
        /// Gets the number of failed requests.
        /// </summary>
        public long FailedRequests => _failedRequests;

        /// <summary>
        /// Gets the average response time in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs =>
            _totalRequests > 0 ? (double)_totalResponseTimeMs / _totalRequests : 0;

        /// <summary>
        /// Gets the success rate (0.0 to 1.0).
        /// </summary>
        public double SuccessRate =>
            _totalRequests > 0 ? (double)_successfulRequests / _totalRequests : 0;

        /// <summary>
        /// Records a request.
        /// </summary>
        public void RecordRequest(bool success, long responseTimeMs)
        {
            Interlocked.Increment(ref _totalRequests);
            Interlocked.Add(ref _totalResponseTimeMs, responseTimeMs);

            if (success)
            {
                Interlocked.Increment(ref _successfulRequests);
            }
            else
            {
                Interlocked.Increment(ref _failedRequests);
            }
        }
    }
}
