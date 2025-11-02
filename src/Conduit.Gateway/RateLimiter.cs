using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Rate limiter using token bucket algorithm.
    /// </summary>
    public class RateLimiter
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, TokenBucket> _buckets;
        private readonly int _defaultCapacity;
        private readonly double _defaultRefillRate;

        /// <summary>
        /// Initializes a new instance of the RateLimiter class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="defaultRateLimit">The default rate limit (requests per second)</param>
        public RateLimiter(ILogger logger, int defaultRateLimit = 100)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buckets = new ConcurrentDictionary<string, TokenBucket>();
            _defaultCapacity = defaultRateLimit;
            _defaultRefillRate = defaultRateLimit; // Refill at the same rate
        }

        /// <summary>
        /// Checks if a request is allowed.
        /// </summary>
        /// <param name="clientId">The client identifier</param>
        /// <param name="rateLimit">The rate limit for this client (requests per second)</param>
        /// <returns>True if the request is allowed, false if rate limit exceeded</returns>
        public bool AllowRequest(string clientId, int? rateLimit = null)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

            var capacity = rateLimit ?? _defaultCapacity;
            var refillRate = rateLimit ?? _defaultRefillRate;

            var bucket = _buckets.GetOrAdd(clientId, _ => new TokenBucket(capacity, refillRate));

            // Update to use the correct rate limit if changed
            if (bucket.Capacity != capacity)
            {
                bucket = new TokenBucket(capacity, refillRate);
                _buckets[clientId] = bucket;
            }

            var allowed = bucket.TryConsume();

            if (!allowed)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
            }

            return allowed;
        }

        /// <summary>
        /// Gets the current state of a client's rate limit.
        /// </summary>
        /// <param name="clientId">The client identifier</param>
        /// <returns>The rate limit state, or null if client not found</returns>
        public RateLimitState? GetState(string clientId)
        {
            if (_buckets.TryGetValue(clientId, out var bucket))
            {
                return new RateLimitState
                {
                    ClientId = clientId,
                    TokensAvailable = bucket.CurrentTokens,
                    Capacity = bucket.Capacity,
                    RefillRate = bucket.RefillRate
                };
            }

            return null;
        }

        /// <summary>
        /// Resets the rate limit for a client.
        /// </summary>
        /// <param name="clientId">The client identifier</param>
        public void Reset(string clientId)
        {
            _buckets.TryRemove(clientId, out _);
            _logger.LogInformation("Reset rate limit for client {ClientId}", clientId);
        }

        /// <summary>
        /// Resets all rate limits.
        /// </summary>
        public void ResetAll()
        {
            _buckets.Clear();
            _logger.LogInformation("Reset all rate limits");
        }
    }

    /// <summary>
    /// Token bucket for rate limiting.
    /// </summary>
    internal class TokenBucket
    {
        private readonly double _capacity;
        private readonly double _refillRate;
        private double _tokens;
        private long _lastRefillTicks;
        private readonly object _lock = new();

        public TokenBucket(int capacity, double refillRate)
        {
            _capacity = capacity;
            _refillRate = refillRate;
            _tokens = capacity;
            _lastRefillTicks = DateTime.UtcNow.Ticks;
        }

        public int Capacity => (int)_capacity;
        public double RefillRate => _refillRate;
        public double CurrentTokens
        {
            get
            {
                lock (_lock)
                {
                    Refill();
                    return _tokens;
                }
            }
        }

        public bool TryConsume(double tokens = 1.0)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= tokens)
                {
                    _tokens -= tokens;
                    return true;
                }

                return false;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow.Ticks;
            var elapsedSeconds = (now - _lastRefillTicks) / (double)TimeSpan.TicksPerSecond;

            if (elapsedSeconds > 0)
            {
                var tokensToAdd = elapsedSeconds * _refillRate;
                _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                _lastRefillTicks = now;
            }
        }
    }

    /// <summary>
    /// Represents the state of a rate limit.
    /// </summary>
    public class RateLimitState
    {
        /// <summary>
        /// Gets or sets the client ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of tokens available.
        /// </summary>
        public double TokensAvailable { get; set; }

        /// <summary>
        /// Gets or sets the bucket capacity.
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// Gets or sets the refill rate (tokens per second).
        /// </summary>
        public double RefillRate { get; set; }

        /// <summary>
        /// Gets the percentage of capacity remaining (0.0 to 1.0).
        /// </summary>
        public double PercentageRemaining =>
            Capacity > 0 ? TokensAvailable / Capacity : 0.0;
    }
}
