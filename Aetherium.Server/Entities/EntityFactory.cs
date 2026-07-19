using System;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.Server.Entities
{
    /// <summary>
    /// Creates spawnable entities by type name. Mirrors the creature set supported by
    /// <c>GameMapGrain.SpawnEntityAsync</c>; used by <c>SpawnEntityTool</c> for runtime
    /// world-building (see OpenSpec change add-aetherctl-runtime-worldbuilding).
    /// </summary>
    public static class EntityFactory
    {
        public static readonly string[] SupportedTypes = { "monster", "wolf", "bear", "bandit", "snake", "zombie" };

        /// <summary>
        /// Creates a creature of the given type. Returns null for unsupported types.
        /// Ensures the "Monster" tile type is registered in the world (as GameMapGrain does).
        /// </summary>
        public static Character? TryCreate(string entityType, World world)
        {
            if (string.IsNullOrWhiteSpace(entityType))
                return null;

            // Ensure required tile type exists (same registration as GameMapGrain.SpawnEntityAsync)
            if (!world.TileTypes.ContainsKey("Monster"))
            {
                world.TileTypes["Monster"] = new TileType
                {
                    Name = "Monster",
                    Settings = new Dictionary<string, string>
                    {
                        { "MapCharacter", "M" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", ConsoleColor.DarkRed.ToString() }
                    }
                };
            }

            return entityType.ToLowerInvariant() switch
            {
                "monster" => new Aetherium.Monster(world),
                "wolf" => new Aetherium.Monster(world),
                "bear" => new Aetherium.Monster(world),
                "bandit" => new Aetherium.Monster(world),
                "snake" => new Snake(),
                "zombie" => new Zombie(world),
                _ => null
            };
        }
    }
}
