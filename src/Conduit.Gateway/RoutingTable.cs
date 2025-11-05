using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Manages routing rules for network connections.
    /// Supports both inbound and outbound routing with priority-based rule evaluation.
    /// </summary>
    public class RoutingTable
    {
        private readonly List<RouteEntry> _routes;
        private readonly ReaderWriterLockSlim _routesLock;
        private readonly ILogger<RoutingTable>? _logger;
        private readonly ConcurrentDictionary<string, RouteEntry> _routesById;

        /// <summary>
        /// Initializes a new instance of the RoutingTable class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        public RoutingTable(ILogger<RoutingTable>? logger = null)
        {
            _routes = new List<RouteEntry>();
            _routesLock = new ReaderWriterLockSlim();
            _logger = logger;
            _routesById = new ConcurrentDictionary<string, RouteEntry>();
        }

        /// <summary>
        /// Gets the total number of routes in the table.
        /// </summary>
        public int Count
        {
            get
            {
                _routesLock.EnterReadLock();
                try
                {
                    return _routes.Count;
                }
                finally
                {
                    _routesLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Adds a route to the routing table.
        /// </summary>
        /// <param name="route">The route to add</param>
        /// <exception cref="ArgumentNullException">Thrown when route is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when a route with the same ID already exists</exception>
        public void AddRoute(RouteEntry route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            _routesLock.EnterWriteLock();
            try
            {
                if (_routesById.ContainsKey(route.Id))
                    throw new InvalidOperationException($"Route with ID '{route.Id}' already exists");

                _routes.Add(route);
                _routesById.TryAdd(route.Id, route);

                // Sort routes by priority (descending)
                _routes.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                _logger?.LogInformation("Added route {RouteId}: {Route}", route.Id, route);
            }
            finally
            {
                _routesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a route from the routing table.
        /// </summary>
        /// <param name="routeId">The ID of the route to remove</param>
        /// <returns>True if the route was removed, false if it wasn't found</returns>
        public bool RemoveRoute(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
                return false;

            _routesLock.EnterWriteLock();
            try
            {
                if (_routesById.TryRemove(routeId, out var route))
                {
                    _routes.Remove(route);
                    _logger?.LogInformation("Removed route {RouteId}: {Route}", routeId, route);
                    return true;
                }

                return false;
            }
            finally
            {
                _routesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets a route by its ID.
        /// </summary>
        /// <param name="routeId">The route ID</param>
        /// <returns>The route if found, null otherwise</returns>
        public RouteEntry? GetRoute(string routeId)
        {
            return _routesById.TryGetValue(routeId, out var route) ? route : null;
        }

        /// <summary>
        /// Updates an existing route.
        /// </summary>
        /// <param name="route">The updated route</param>
        /// <returns>True if the route was updated, false if it wasn't found</returns>
        public bool UpdateRoute(RouteEntry route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            _routesLock.EnterWriteLock();
            try
            {
                if (_routesById.TryGetValue(route.Id, out var existingRoute))
                {
                    var index = _routes.IndexOf(existingRoute);
                    if (index >= 0)
                    {
                        _routes[index] = route;
                        _routesById[route.Id] = route;

                        // Re-sort by priority
                        _routes.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                        _logger?.LogInformation("Updated route {RouteId}: {Route}", route.Id, route);
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _routesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Looks up the best matching route for inbound connections.
        /// </summary>
        /// <param name="connectionInfo">Connection information</param>
        /// <returns>The best matching route, or null if no route matches</returns>
        public RouteEntry? LookupInbound(ConnectionInfo connectionInfo)
        {
            return Lookup(connectionInfo, RouteDirection.Inbound);
        }

        /// <summary>
        /// Looks up the best matching route for outbound connections.
        /// </summary>
        /// <param name="connectionInfo">Connection information</param>
        /// <returns>The best matching route, or null if no route matches</returns>
        public RouteEntry? LookupOutbound(ConnectionInfo connectionInfo)
        {
            return Lookup(connectionInfo, RouteDirection.Outbound);
        }

        /// <summary>
        /// Looks up the best matching route for the given connection and direction.
        /// </summary>
        /// <param name="connectionInfo">Connection information</param>
        /// <param name="direction">The direction to look up (optional, checks all directions if null)</param>
        /// <returns>The best matching route, or null if no route matches</returns>
        public RouteEntry? Lookup(ConnectionInfo connectionInfo, RouteDirection? direction = null)
        {
            if (connectionInfo == null)
                return null;

            _routesLock.EnterReadLock();
            try
            {
                foreach (var route in _routes)
                {
                    // Check direction
                    if (direction.HasValue && route.Direction != direction.Value && route.Direction != RouteDirection.Both)
                        continue;

                    // Check if route matches the connection
                    if (route.Matches(connectionInfo))
                    {
                        route.RecordMatch();
                        _logger?.LogDebug("Route match: {ConnectionInfo} -> {Route}", connectionInfo, route);
                        return route;
                    }
                }

                _logger?.LogDebug("No route found for connection: {ConnectionInfo}", connectionInfo);
                return null;
            }
            finally
            {
                _routesLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all routes, optionally filtered by direction.
        /// </summary>
        /// <param name="direction">Optional direction filter</param>
        /// <returns>List of routes</returns>
        public List<RouteEntry> GetRoutes(RouteDirection? direction = null)
        {
            _routesLock.EnterReadLock();
            try
            {
                var routes = _routes.ToList();

                if (direction.HasValue)
                {
                    routes = routes.Where(r => r.Direction == direction.Value || r.Direction == RouteDirection.Both).ToList();
                }

                return routes;
            }
            finally
            {
                _routesLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all routes from the table.
        /// </summary>
        public void Clear()
        {
            _routesLock.EnterWriteLock();
            try
            {
                var count = _routes.Count;
                _routes.Clear();
                _routesById.Clear();
                _logger?.LogInformation("Cleared {Count} routes from routing table", count);
            }
            finally
            {
                _routesLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets statistics about the routing table.
        /// </summary>
        /// <returns>Routing table statistics</returns>
        public RoutingTableStatistics GetStatistics()
        {
            _routesLock.EnterReadLock();
            try
            {
                var stats = new RoutingTableStatistics
                {
                    TotalRoutes = _routes.Count,
                    InboundRoutes = _routes.Count(r => r.Direction == RouteDirection.Inbound || r.Direction == RouteDirection.Both),
                    OutboundRoutes = _routes.Count(r => r.Direction == RouteDirection.Outbound || r.Direction == RouteDirection.Both),
                    EnabledRoutes = _routes.Count(r => r.Enabled),
                    DisabledRoutes = _routes.Count(r => !r.Enabled),
                    TotalMatches = _routes.Sum(r => r.MatchCount)
                };

                return stats;
            }
            finally
            {
                _routesLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Disposes the routing table.
        /// </summary>
        public void Dispose()
        {
            _routesLock.Dispose();
        }
    }

    /// <summary>
    /// Statistics about the routing table.
    /// </summary>
    public class RoutingTableStatistics
    {
        /// <summary>
        /// Gets or sets the total number of routes.
        /// </summary>
        public int TotalRoutes { get; set; }

        /// <summary>
        /// Gets or sets the number of inbound routes.
        /// </summary>
        public int InboundRoutes { get; set; }

        /// <summary>
        /// Gets or sets the number of outbound routes.
        /// </summary>
        public int OutboundRoutes { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled routes.
        /// </summary>
        public int EnabledRoutes { get; set; }

        /// <summary>
        /// Gets or sets the number of disabled routes.
        /// </summary>
        public int DisabledRoutes { get; set; }

        /// <summary>
        /// Gets or sets the total number of route matches.
        /// </summary>
        public long TotalMatches { get; set; }
    }
}