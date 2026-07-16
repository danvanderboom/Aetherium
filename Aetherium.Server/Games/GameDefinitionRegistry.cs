using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aetherium.Model.Games;

namespace Aetherium.Server.Games
{
    /// <summary>
    /// Holds every valid game definition loaded from bundle directories under a base path
    /// (default <c>Data/Games</c>) — the <c>PrefabLibrary</c>/<c>HubWorldLoader</c> mold
    /// (openspec/changes/add-game-definition-loader). A bundle that fails to load or validate is
    /// skipped with its diagnostics retained; it never blocks other bundles or startup. Duplicate
    /// game ids: first bundle wins, the second is rejected with a diagnostic. Reloading affects
    /// future instance creations only — running instances copied their configs at creation.
    /// </summary>
    public class GameDefinitionRegistry
    {
        private readonly string _basePath;
        private readonly GameDefinitionLoader _loader = new();
        private readonly GameDefinitionValidator _validator = new();
        private readonly Dictionary<string, GameDefinition> _definitions = new(StringComparer.Ordinal);
        private readonly List<GameDefinitionDiagnostic> _diagnostics = new();
        private readonly object _gate = new();
        private bool _loaded;

        public GameDefinitionRegistry(string basePath = "Data/Games")
        {
            _basePath = basePath;
        }

        /// <summary>Loads every bundle directory under the base path (idempotent; see <see cref="Reload"/>).</summary>
        public void LoadAll()
        {
            lock (_gate)
            {
                if (_loaded)
                    return;
                LoadAllCore();
                _loaded = true;
            }
        }

        /// <summary>Clears and re-loads from disk. Affects future instance creations only.</summary>
        public void Reload()
        {
            lock (_gate)
            {
                _definitions.Clear();
                _diagnostics.Clear();
                LoadAllCore();
                _loaded = true;
            }
        }

        private void LoadAllCore()
        {
            if (!Directory.Exists(_basePath))
            {
                Console.WriteLine($"[GameDefinitionRegistry] Game definitions directory not found: {_basePath}");
                return;
            }

            var bundleDirs = Directory.GetDirectories(_basePath)
                .Where(dir => File.Exists(Path.Combine(dir, GameDefinitionLoader.ManifestFileName)))
                .OrderBy(dir => dir, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine($"[GameDefinitionRegistry] Loading {bundleDirs.Count} game definition bundle(s) from {_basePath}");

            foreach (var bundleDir in bundleDirs)
            {
                var result = _loader.LoadBundle(bundleDir);
                _diagnostics.AddRange(result.Diagnostics);

                if (!result.Success || result.Definition is not { } definition)
                {
                    Console.WriteLine($"[GameDefinitionRegistry] Skipping {bundleDir}: {result.Diagnostics.Count} load error(s)");
                    continue;
                }

                var validation = _validator.Validate(definition, bundleDir);
                _diagnostics.AddRange(validation);
                if (validation.Any(d => d.Severity == GameDefinitionDiagnosticSeverity.Error))
                {
                    Console.WriteLine($"[GameDefinitionRegistry] Skipping {bundleDir}: {validation.Count} validation error(s)");
                    continue;
                }

                if (_definitions.ContainsKey(definition.Id))
                {
                    _diagnostics.Add(new GameDefinitionDiagnostic
                    {
                        BundlePath = bundleDir,
                        Section = "manifest",
                        Severity = GameDefinitionDiagnosticSeverity.Error,
                        Message = $"Duplicate game id '{definition.Id}' — already loaded from another bundle; this bundle is ignored.",
                    });
                    continue;
                }

                _definitions[definition.Id] = definition;
                Console.WriteLine($"[GameDefinitionRegistry] Loaded game definition: {definition.Id} v{definition.Version} ({definition.Name})");
            }
        }

        public IReadOnlyCollection<GameDefinition> All
        {
            get { lock (_gate) return _definitions.Values.ToList(); }
        }

        /// <summary>Load/validation findings across all bundles (operator/designer-facing data).</summary>
        public IReadOnlyList<GameDefinitionDiagnostic> Diagnostics
        {
            get { lock (_gate) return _diagnostics.ToList(); }
        }

        public bool TryGet(string gameDefinitionId, out GameDefinition? definition)
        {
            lock (_gate) return _definitions.TryGetValue(gameDefinitionId, out definition);
        }

        public List<GameDefinitionSummaryDto> ListSummaries()
        {
            lock (_gate)
            {
                return _definitions.Values
                    .OrderBy(d => d.Id, StringComparer.Ordinal)
                    .Select(d => new GameDefinitionSummaryDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Version = d.Version,
                        Description = d.Description,
                        Tags = d.Tags.ToList(),
                    })
                    .ToList();
            }
        }
    }
}
