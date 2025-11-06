using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace Conduit.Configuration
{
    public static class YamlConfigurationExtensions
    {
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path)
        {
            return AddYamlFile(builder, path, optional: false, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path, bool optional)
        {
            return AddYamlFile(builder, path, optional, reloadOnChange: false);
        }

        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path, bool optional, bool reloadOnChange)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty", nameof(path));

            var source = new YamlConfigurationSource
            {
                Path = path,
                Optional = optional,
                ReloadOnChange = reloadOnChange
            };

            return builder.Add(source);
        }
    }

    public class YamlConfigurationSource : IConfigurationSource
    {
        public string Path { get; set; } = string.Empty;
        public bool Optional { get; set; }
        public bool ReloadOnChange { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new YamlConfigurationProvider(this);
        }
    }

    public class YamlConfigurationProvider : ConfigurationProvider
    {
        private readonly YamlConfigurationSource _source;

        public YamlConfigurationProvider(YamlConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public override void Load()
        {
            if (!File.Exists(_source.Path))
            {
                if (!_source.Optional)
                {
                    throw new FileNotFoundException($"The configuration file '{_source.Path}' was not found and is not optional.");
                }
                return;
            }

            try
            {
                using var stream = File.OpenRead(_source.Path);
                var parser = new YamlConfigurationFileParser();
                Data = parser.Parse(stream);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Could not parse the YAML file '{_source.Path}'.", ex);
            }
        }
    }
}