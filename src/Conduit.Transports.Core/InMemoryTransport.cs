using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Microsoft.Extensions.Logging;

namespace Conduit.Transports.Core
{
    /// <summary>
    /// In-memory transport implementation for testing and local communication.
    /// Messages are delivered synchronously within the same process.
    /// </summary>
    public class InMemoryTransport : TransportAdapterBase
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<Func<TransportMessage, Task>>> _handlers = new();
        private readonly ConcurrentBag<Func<TransportMessage, Task>> _globalHandlers = new();

        /// <inheritdoc/>
        public override TransportType Type => TransportType.InMemory;

        /// <inheritdoc/>
        public override string Name { get; }

        /// <summary>
        /// Initializes a new instance of the InMemoryTransport class.
        /// </summary>
        /// <param name="name">The transport name</param>
        /// <param name="logger">Optional logger</param>
        public InMemoryTransport(string name = "InMemory", ILogger<InMemoryTransport>? logger = null)
            : base(new TransportConfiguration { Type = TransportType.InMemory, Name = name }, logger)
        {
            Name = name;
        }

        /// <inheritdoc/>
        protected override Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            // In-memory transport is always "connected"
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            _handlers.Clear();
            _globalHandlers.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task SendCoreAsync(IMessage message, string? destination, CancellationToken cancellationToken)
        {
            // Convert to transport message
            var transportMessage = new TransportMessage
            {
                MessageId = message.MessageId,
                CorrelationId = message.CorrelationId,
                MessageType = message.GetType().FullName,
                Destination = destination,
                Timestamp = message.Timestamp
            };

            // Copy headers
            foreach (var header in message.Headers)
            {
                transportMessage.SetHeader(header.Key, header.Value);
            }

            // Deliver to destination-specific handlers
            if (destination != null && _handlers.TryGetValue(destination, out var destinationHandlers))
            {
                foreach (var handler in destinationHandlers)
                {
                    await handler(transportMessage);
                }
            }

            // Deliver to global handlers
            foreach (var handler in _globalHandlers)
            {
                await handler(transportMessage);
            }

            _statistics.BytesSent += transportMessage.Payload?.Length ?? 0;
        }

        /// <inheritdoc/>
        protected override Task<ITransportSubscription> SubscribeCoreAsync(
            string? source,
            Func<TransportMessage, Task> handler,
            CancellationToken cancellationToken)
        {
            var subscriptionId = Guid.NewGuid().ToString();

            if (source != null)
            {
                var handlers = _handlers.GetOrAdd(source, _ => new ConcurrentBag<Func<TransportMessage, Task>>());
                handlers.Add(handler);
            }
            else
            {
                _globalHandlers.Add(handler);
            }

            var subscription = new InMemorySubscription(subscriptionId, source ?? "*", handler, this);
            return Task.FromResult<ITransportSubscription>(subscription);
        }

        private void UnsubscribeHandler(string? source, Func<TransportMessage, Task> handler)
        {
            if (source != null && _handlers.TryGetValue(source, out var handlers))
            {
                // ConcurrentBag doesn't support removal, so we'll need to recreate it
                var newBag = new ConcurrentBag<Func<TransportMessage, Task>>();
                foreach (var h in handlers)
                {
                    if (h != handler)
                    {
                        newBag.Add(h);
                    }
                }
                _handlers[source] = newBag;
            }
        }

        private class InMemorySubscription : ITransportSubscription
        {
            private readonly Func<TransportMessage, Task> _handler;
            private readonly InMemoryTransport _transport;
            private bool _disposed;

            public string SubscriptionId { get; }
            public string Source { get; }
            public bool IsActive { get; private set; } = true;
            public long MessagesReceived { get; private set; }

            public InMemorySubscription(
                string subscriptionId,
                string source,
                Func<TransportMessage, Task> handler,
                InMemoryTransport transport)
            {
                SubscriptionId = subscriptionId;
                Source = source;
                _handler = handler;
                _transport = transport;
            }

            public Task PauseAsync()
            {
                IsActive = false;
                return Task.CompletedTask;
            }

            public Task ResumeAsync()
            {
                IsActive = true;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _transport.UnsubscribeHandler(Source == "*" ? null : Source, _handler);
                _transport.RemoveSubscription(SubscriptionId);

                _disposed = true;
            }
        }
    }
}
