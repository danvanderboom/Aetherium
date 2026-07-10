using System.Collections.Generic;
using Orleans;
using Aetherium.Model.Combat;
using Aetherium.Model.Abilities;
using Aetherium.Model.Progression;
using Aetherium.Model.Factions;

namespace Aetherium.Model.Games
{
    /// <summary>
    /// The world-shape section of a <see cref="GameDefinition"/>: which generator builds the
    /// world, its dimensions, and its player capacity. Pure data — the YAML `world:` section
    /// binds here (openspec/changes/add-game-definition-loader).
    /// </summary>
    [GenerateSerializer]
    public class GameWorldDefinition
    {
        [Id(0)] public string GeneratorType { get; set; } = "rooms-and-corridors";
        [Id(1)] public Dictionary<string, object> GeneratorParameters { get; set; } = new();
        [Id(2)] public Aetherium.Model.Worlds.WorldDimensions? Size { get; set; }
        [Id(3)] public int MaxPlayers { get; set; } = 100;
        [Id(4)] public string? NarrativeId { get; set; }
    }

    /// <summary>
    /// A complete, declaratively-authored game (openspec/changes/add-game-definition-loader): id,
    /// version, world shape, and the per-world gameplay-rule configs the four wire-X-live slices
    /// shipped. Loaded from a YAML bundle directory (Data/Games/&lt;id&gt;/game.yaml + conventional
    /// split section files); each running instance of the game is a world created from this
    /// definition through the existing world-creation path. Sections for subsystems whose config
    /// types haven't shipped yet (economy, party, events, …) are added here as those types land.
    /// </summary>
    [GenerateSerializer]
    public class GameDefinition
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Version { get; set; } = string.Empty;
        [Id(3)] public string Description { get; set; } = string.Empty;
        [Id(4)] public List<string> Tags { get; set; } = new();

        [Id(5)] public GameWorldDefinition World { get; set; } = new();

        [Id(6)] public DeathPolicy? Death { get; set; }
        [Id(7)] public AbilityConfig? Abilities { get; set; }
        [Id(8)] public ProgressionConfig? Progression { get; set; }
        [Id(9)] public FactionConfig? Factions { get; set; }
    }

    [GenerateSerializer]
    public enum GameDefinitionDiagnosticSeverity
    {
        Warning,
        Error,
    }

    /// <summary>
    /// One loader/validator finding, as data (not console text): which bundle, which section,
    /// how bad, and what's wrong. Operator/designer-facing, surfaced by the registry list APIs.
    /// </summary>
    [GenerateSerializer]
    public class GameDefinitionDiagnostic
    {
        [Id(0)] public string BundlePath { get; set; } = string.Empty;
        [Id(1)] public string Section { get; set; } = string.Empty;
        [Id(2)] public GameDefinitionDiagnosticSeverity Severity { get; set; } = GameDefinitionDiagnosticSeverity.Error;
        [Id(3)] public string Message { get; set; } = string.Empty;

        public override string ToString() => $"[{Severity}] {BundlePath} ({Section}): {Message}";
    }

    /// <summary>Registry listing entry for one loaded game definition.</summary>
    [GenerateSerializer]
    public class GameDefinitionSummaryDto
    {
        [Id(0)] public string Id { get; set; } = string.Empty;
        [Id(1)] public string Name { get; set; } = string.Empty;
        [Id(2)] public string Version { get; set; } = string.Empty;
        [Id(3)] public string Description { get; set; } = string.Empty;
        [Id(4)] public List<string> Tags { get; set; } = new();
    }

    /// <summary>Result of creating a game instance from a registered definition.</summary>
    [GenerateSerializer]
    public class GameInstanceResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string? WorldId { get; set; }
        [Id(2)] public string? Error { get; set; }

        public static GameInstanceResult Ok(string worldId) => new() { Success = true, WorldId = worldId };
        public static GameInstanceResult Fail(string error) => new() { Success = false, Error = error };
    }
}
