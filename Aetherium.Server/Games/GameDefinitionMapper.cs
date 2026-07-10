using System.Collections.Generic;
using Aetherium.Model.Games;
using Aetherium.Server.MultiWorld;

namespace Aetherium.Server.Games
{
    /// <summary>
    /// Maps a <see cref="GameDefinition"/> onto the existing world-creation contract
    /// (openspec/changes/add-game-definition-loader): a game instance is just a world created
    /// through the battle-tested <c>CreateWorldAsync</c> path with every declared config applied
    /// and the creating definition's id/version stamped for instance bookkeeping. Pure function —
    /// no I/O, no grains — so the definition→request mapping is unit-testable field by field.
    /// </summary>
    public static class GameDefinitionMapper
    {
        public static CreateWorldRequest ToCreateWorldRequest(GameDefinition definition, string? instanceName = null)
        {
            return new CreateWorldRequest
            {
                Name = string.IsNullOrWhiteSpace(instanceName) ? definition.Name : instanceName,
                Description = definition.Description,
                GeneratorType = definition.World.GeneratorType,
                GeneratorParameters = new Dictionary<string, object>(definition.World.GeneratorParameters),
                NarrativeId = definition.World.NarrativeId,
                MaxPlayers = definition.World.MaxPlayers,
                Size = definition.World.Size is { } dims
                    ? new WorldSize { Width = dims.Width, Height = dims.Height, Depth = dims.Depth }
                    : null,
                DeathPolicy = definition.Death,
                AbilityConfig = definition.Abilities,
                ProgressionConfig = definition.Progression,
                FactionConfig = definition.Factions,
                ContentConfig = definition.Content,
                EcaConfig = definition.Rules,
                GameDefinitionId = definition.Id,
                GameDefinitionVersion = definition.Version,
            };
        }
    }
}
