using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Manages gateway routes and route matching.
    /// </summary>
    public class RouteManager
    {
        private readonly ConcurrentDictionary<string, RouteConfiguration> _routes;
        private readonly List<CompiledRoute> _compiledRoutes;
        private readonly ILogger _logger;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the RouteManager class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public RouteManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _routes = new ConcurrentDictionary<string, RouteConfiguration>();
            _compiledRoutes = new List<CompiledRoute>();
        }

        /// <summary>
        /// Adds a route.
        /// </summary>
        /// <param name="route">The route configuration</param>
        public void AddRoute(RouteConfiguration route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (_routes.TryAdd(route.Id, route))
            {
                lock (_lock)
                {
                    var compiledRoute = CompileRoute(route);
                    _compiledRoutes.Add(compiledRoute);
                    // Sort by specificity (more specific routes first)
                    _compiledRoutes.Sort((a, b) => b.Specificity.CompareTo(a.Specificity));
                }

                _logger.LogInformation("Added route {RouteId}: {Path}", route.Id, route.Path);
            }
            else
            {
                _logger.LogWarning("Route {RouteId} already exists", route.Id);
            }
        }

        /// <summary>
        /// Removes a route.
        /// </summary>
        /// <param name="routeId">The route ID</param>
        public bool RemoveRoute(string routeId)
        {
            if (_routes.TryRemove(routeId, out var route))
            {
                lock (_lock)
                {
                    _compiledRoutes.RemoveAll(r => r.Configuration.Id == routeId);
                }

                _logger.LogInformation("Removed route {RouteId}", routeId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a route by ID.
        /// </summary>
        /// <param name="routeId">The route ID</param>
        /// <returns>The route configuration, or null if not found</returns>
        public RouteConfiguration? GetRoute(string routeId)
        {
            _routes.TryGetValue(routeId, out var route);
            return route;
        }

        /// <summary>
        /// Gets all routes.
        /// </summary>
        /// <returns>All route configurations</returns>
        public IEnumerable<RouteConfiguration> GetAllRoutes()
        {
            return _routes.Values;
        }

        /// <summary>
        /// Matches a request to a route.
        /// </summary>
        /// <param name="path">The request path</param>
        /// <param name="method">The HTTP method</param>
        /// <returns>The matched route, or null if no match found</returns>
        public RouteMatch? MatchRoute(string path, string method)
        {
            lock (_lock)
            {
                foreach (var compiledRoute in _compiledRoutes)
                {
                    if (!compiledRoute.Configuration.Enabled)
                        continue;

                    // Check HTTP method
                    if (!compiledRoute.Configuration.Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
                        continue;

                    // Check path match
                    var match = compiledRoute.Pattern.Match(path);
                    if (match.Success)
                    {
                        var parameters = new Dictionary<string, string>();

                        // Extract path parameters
                        foreach (var paramName in compiledRoute.ParameterNames)
                        {
                            var group = match.Groups[paramName];
                            if (group.Success)
                            {
                                parameters[paramName] = group.Value;
                            }
                        }

                        _logger.LogDebug("Matched route {RouteId} for {Method} {Path}",
                            compiledRoute.Configuration.Id, method, path);

                        return new RouteMatch
                        {
                            Route = compiledRoute.Configuration,
                            Parameters = parameters
                        };
                    }
                }
            }

            _logger.LogDebug("No route matched for {Method} {Path}", method, path);
            return null;
        }

        /// <summary>
        /// Compiles a route into a regex pattern.
        /// </summary>
        private CompiledRoute CompileRoute(RouteConfiguration route)
        {
            var pattern = route.Path;
            var parameterNames = new List<string>();
            var specificity = CalculateSpecificity(pattern);

            // Replace {param} with named regex groups
            var regex = Regex.Replace(pattern, @"\{([a-zA-Z0-9_]+)\}", match =>
            {
                var paramName = match.Groups[1].Value;
                parameterNames.Add(paramName);
                return $"(?<{paramName}>[^/]+)";
            });

            // Escape other regex characters
            regex = "^" + regex + "$";

            return new CompiledRoute
            {
                Configuration = route,
                Pattern = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase),
                ParameterNames = parameterNames,
                Specificity = specificity
            };
        }

        /// <summary>
        /// Calculates the specificity of a route pattern.
        /// Higher specificity means more specific route.
        /// </summary>
        private int CalculateSpecificity(string pattern)
        {
            var specificity = 0;

            // Static segments are more specific
            var segments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (!segment.Contains('{'))
                {
                    specificity += 10; // Static segment
                }
                else
                {
                    specificity += 1; // Parameter segment
                }
            }

            return specificity;
        }

        /// <summary>
        /// Compiled route with regex pattern.
        /// </summary>
        private class CompiledRoute
        {
            public RouteConfiguration Configuration { get; set; } = null!;
            public Regex Pattern { get; set; } = null!;
            public List<string> ParameterNames { get; set; } = new();
            public int Specificity { get; set; }
        }
    }

    /// <summary>
    /// Represents a route match.
    /// </summary>
    public class RouteMatch
    {
        /// <summary>
        /// Gets or sets the matched route.
        /// </summary>
        public RouteConfiguration Route { get; set; } = null!;

        /// <summary>
        /// Gets or sets the extracted path parameters.
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
