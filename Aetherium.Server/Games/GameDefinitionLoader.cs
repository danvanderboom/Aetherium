using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aetherium.Model.Games;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aetherium.Server.Games
{
    /// <summary>Result of loading one bundle directory: the definition (if it parsed) plus every
    /// diagnostic. <see cref="Success"/> requires a definition and zero error-severity findings.</summary>
    public class GameDefinitionLoadResult
    {
        public GameDefinition? Definition { get; init; }
        public List<GameDefinitionDiagnostic> Diagnostics { get; } = new();

        public bool Success =>
            Definition != null &&
            !Diagnostics.Any(d => d.Severity == GameDefinitionDiagnosticSeverity.Error);
    }

    /// <summary>
    /// Loads a game definition bundle (openspec/changes/add-game-definition-loader): a directory
    /// containing a <c>game.yaml</c> manifest, with the gameplay-rule sections either inline or in
    /// conventional sibling files (<c>death.yaml</c>/<c>abilities.yaml</c>/<c>progression.yaml</c>/
    /// <c>factions.yaml</c>). YAML keys are the camelCase of the shipped config types — this loader
    /// deliberately has no schema of its own to drift. Parsing is strict: an unknown key is an
    /// error (a typo must never silently deserialize to a default), per the design's
    /// mechanical-enforcement principle.
    /// </summary>
    public class GameDefinitionLoader
    {
        public const string ManifestFileName = "game.yaml";

        /// <summary>Conventional section files: file name → (section label, bind + assign).</summary>
        private static readonly (string FileName, string Section)[] SectionFiles =
        {
            ("death.yaml", "death"),
            ("abilities.yaml", "abilities"),
            ("progression.yaml", "progression"),
            ("factions.yaml", "factions"),
            ("content.yaml", "content"),
            ("rules.yaml", "rules"),
            ("economy.yaml", "economy"),
        };

        private readonly IDeserializer _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithNodeDeserializer(new TypedScalarObjectDeserializer(), s => s.OnTop())
            .Build();

        public GameDefinitionLoadResult LoadBundle(string bundleDirectory)
        {
            var result = new GameDefinitionLoadResult();
            var manifestPath = Path.Combine(bundleDirectory, ManifestFileName);

            if (!File.Exists(manifestPath))
            {
                return new GameDefinitionLoadResult
                {
                    Definition = null,
                }.WithError(bundleDirectory, "manifest", $"No {ManifestFileName} found in bundle directory.");
            }

            GameDefinition definition;
            try
            {
                definition = _deserializer.Deserialize<GameDefinition>(File.ReadAllText(manifestPath))
                             ?? new GameDefinition();
            }
            catch (YamlException ex)
            {
                return new GameDefinitionLoadResult { Definition = null }
                    .WithError(bundleDirectory, "manifest", DescribeYamlError(ex));
            }

            result = new GameDefinitionLoadResult { Definition = definition };

            foreach (var (fileName, section) in SectionFiles)
            {
                var sectionPath = Path.Combine(bundleDirectory, fileName);
                if (!File.Exists(sectionPath))
                    continue;

                if (SectionAlreadyDeclared(definition, section))
                {
                    result.WithError(bundleDirectory, section,
                        $"Section '{section}' is declared both inline in {ManifestFileName} and in {fileName} — a section must have exactly one source.");
                    continue;
                }

                try
                {
                    AssignSection(definition, section, File.ReadAllText(sectionPath));
                }
                catch (YamlException ex)
                {
                    result.WithError(bundleDirectory, section, DescribeYamlError(ex));
                }
            }

            return result;
        }

        private static bool SectionAlreadyDeclared(GameDefinition definition, string section) => section switch
        {
            "death" => definition.Death != null,
            "abilities" => definition.Abilities != null,
            "progression" => definition.Progression != null,
            "factions" => definition.Factions != null,
            "content" => definition.Content != null,
            "rules" => definition.Rules != null,
            "economy" => definition.Economy != null,
            _ => false,
        };

        private void AssignSection(GameDefinition definition, string section, string yaml)
        {
            switch (section)
            {
                case "death":
                    definition.Death = _deserializer.Deserialize<Aetherium.Model.Combat.DeathPolicy>(yaml);
                    break;
                case "abilities":
                    definition.Abilities = _deserializer.Deserialize<Aetherium.Model.Abilities.AbilityConfig>(yaml);
                    break;
                case "progression":
                    definition.Progression = _deserializer.Deserialize<Aetherium.Model.Progression.ProgressionConfig>(yaml);
                    break;
                case "factions":
                    definition.Factions = _deserializer.Deserialize<Aetherium.Model.Factions.FactionConfig>(yaml);
                    break;
                case "content":
                    definition.Content = _deserializer.Deserialize<Aetherium.Model.Content.ContentConfig>(yaml);
                    break;
                case "rules":
                    definition.Rules = _deserializer.Deserialize<Aetherium.Model.Eca.EcaConfig>(yaml);
                    break;
                case "economy":
                    definition.Economy = _deserializer.Deserialize<Aetherium.Model.Economy.EconomyConfig>(yaml);
                    break;
            }
        }

        private static string DescribeYamlError(YamlException ex)
        {
            // The useful detail ("Property 'foo' not found on type ...") is often on the inner exception.
            var detail = ex.InnerException?.Message is { Length: > 0 } inner ? $" {inner}" : string.Empty;
            return $"{ex.Message}{detail} (at {ex.Start})";
        }

        /// <summary>
        /// YamlDotNet deserializes an <c>object</c>-typed scalar to a string; generator parameters
        /// (<c>Dictionary&lt;string, object&gt;</c>) want real scalar types. Plain (unquoted) scalars
        /// are inferred as bool/int/long/double; quoted scalars stay strings, per YAML semantics.
        /// </summary>
        private sealed class TypedScalarObjectDeserializer : INodeDeserializer
        {
            public bool Deserialize(IParser reader, Type expectedType,
                Func<IParser, Type, object?> nestedObjectDeserializer, out object? value,
                ObjectDeserializer rootDeserializer)
            {
                value = null;
                if (expectedType != typeof(object) || reader.Current is not Scalar scalar)
                    return false;

                reader.MoveNext();

                if (!scalar.IsPlainImplicit)
                {
                    value = scalar.Value;
                    return true;
                }

                if (bool.TryParse(scalar.Value, out var b))
                    value = b;
                else if (int.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    value = i;
                else if (long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    value = l;
                else if (double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    value = d;
                else
                    value = scalar.Value;

                return true;
            }
        }
    }

    internal static class GameDefinitionLoadResultExtensions
    {
        public static GameDefinitionLoadResult WithError(this GameDefinitionLoadResult result,
            string bundlePath, string section, string message)
        {
            result.Diagnostics.Add(new GameDefinitionDiagnostic
            {
                BundlePath = bundlePath,
                Section = section,
                Severity = GameDefinitionDiagnosticSeverity.Error,
                Message = message,
            });
            return result;
        }
    }
}
