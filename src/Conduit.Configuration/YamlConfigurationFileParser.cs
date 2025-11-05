using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Conduit.Configuration
{
    internal class YamlConfigurationFileParser
    {
        private readonly Dictionary<string, string?> _data = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _paths = new();

        public Dictionary<string, string?> Parse(Stream input)
        {
            _data.Clear();

            var yaml = new YamlStream();
            yaml.Load(new StreamReader(input));

            if (yaml.Documents.Count == 0)
            {
                return _data;
            }

            var rootNode = yaml.Documents[0].RootNode;

            if (rootNode is YamlMappingNode mapping)
            {
                VisitYamlMappingNode(mapping);
            }

            return _data;
        }

        private void VisitYamlMappingNode(YamlMappingNode node)
        {
            foreach (var yamlNodePair in node.Children)
            {
                var context = ((YamlScalarNode)yamlNodePair.Key).Value!;
                VisitYamlNode(context, yamlNodePair.Value);
            }
        }

        private void VisitYamlNode(string context, YamlNode node)
        {
            switch (node)
            {
                case YamlScalarNode scalarNode:
                    VisitYamlScalarNode(context, scalarNode);
                    break;
                case YamlMappingNode mappingNode:
                    VisitYamlMappingNode(context, mappingNode);
                    break;
                case YamlSequenceNode sequenceNode:
                    VisitYamlSequenceNode(context, sequenceNode);
                    break;
                default:
                    throw new FormatException($"Unsupported YAML node type '{node.GetType().Name}' was found at path '{GetCurrentPath()}'.");
            }
        }

        private void VisitYamlScalarNode(string context, YamlScalarNode yamlValue)
        {
            EnterContext(context);
            var currentKey = GetCurrentPath();

            if (_data.ContainsKey(currentKey))
            {
                throw new FormatException($"A duplicate key '{currentKey}' was found.");
            }

            _data[currentKey] = yamlValue.Value;
            ExitContext();
        }

        private void VisitYamlMappingNode(string context, YamlMappingNode yamlValue)
        {
            EnterContext(context);

            foreach (var yamlNodePair in yamlValue.Children)
            {
                var innerContext = ((YamlScalarNode)yamlNodePair.Key).Value!;
                VisitYamlNode(innerContext, yamlNodePair.Value);
            }

            ExitContext();
        }

        private void VisitYamlSequenceNode(string context, YamlSequenceNode yamlValue)
        {
            EnterContext(context);

            for (int i = 0; i < yamlValue.Children.Count; i++)
            {
                VisitYamlNode(i.ToString(), yamlValue.Children[i]);
            }

            ExitContext();
        }

        private void EnterContext(string context) => _paths.Push(context);

        private void ExitContext() => _paths.Pop();

        private string GetCurrentPath() => string.Join(":", _paths.Reverse());
    }
}