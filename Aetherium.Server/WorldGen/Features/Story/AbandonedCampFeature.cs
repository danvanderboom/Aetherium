using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldGen;

namespace Aetherium.WorldGen.Features.Story
{
    /// <summary>
    /// Feature that places abandoned camps with clues about what happened.
    /// </summary>
    public class AbandonedCampFeature : IGenerationFeature
    {
        private const int MaxCamps = 2;
        private const int MinCampDistance = 30; // Minimum distance between camps

        public void Apply(World world, GeneratorContext context)
        {
            var rng = context.GetRandom("abandoned-camp");
            var passableLocations = world.EntitiesByLocation.Keys
                .Where(loc => world.PassableTerrain(loc))
                .ToList();

            if (passableLocations.Count == 0)
                return;

            var placedCamps = new List<WorldLocation>();
            int attempts = 0;
            int maxAttempts = MaxCamps * 20;

            while (placedCamps.Count < MaxCamps && attempts < maxAttempts)
            {
                attempts++;

                var location = passableLocations[rng.Next(passableLocations.Count)];

                // Check minimum distance from other camps
                bool tooClose = placedCamps.Any(camp =>
                {
                    var dx = location.X - camp.X;
                    var dy = location.Y - camp.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    return distance < MinCampDistance;
                });

                if (tooClose)
                    continue;

                // Create camp entity
                var camp = new AbandonedCamp();
                camp.Set(location);

                // Add inscription with clue text
                var inscription = new Inscription
                {
                    Title = "Abandoned Camp",
                    Text = GenerateCampClue(location, context),
                    Topic = "journal",
                    Author = "Unknown Traveler",
                    Era = "Recent"
                };

                camp.Set(inscription);

                // Add items that might have been left behind
                if (rng.NextDouble() < 0.5) // 50% chance of items
                {
                    // In a full implementation, we'd add items here
                    // For now, just mark the camp as having clues
                }

                world.AddEntity(camp);

                placedCamps.Add(location);
            }
        }

        /// <summary>
        /// Generates clue text about what happened at the camp.
        /// </summary>
        private string GenerateCampClue(WorldLocation location, GeneratorContext context)
        {
            var rng = context.GetRandom($"camp-{location.X}-{location.Y}");
            var scenarios = new[]
            {
                "This camp was abandoned in haste. Personal belongings are scattered about, " +
                "but there's no sign of a struggle. Perhaps they moved on quickly.",

                "The campfire is cold, but the embers suggest someone was here recently. " +
                "Footprints lead away to the north, but disappear after a few steps.",

                "A torn journal page lies near the tent. It speaks of strange noises in the night " +
                "and a decision to leave before dawn. The writing ends abruptly.",

                "Equipment left behind suggests the campers were well-prepared. " +
                "Why they left without their supplies remains a mystery."
            };

            return scenarios[rng.Next(scenarios.Length)];
        }
    }

    /// <summary>
    /// Entity representing an abandoned camp.
    /// </summary>
    public class AbandonedCamp : Entity
    {
        public AbandonedCamp() : base()
        {
            var tile = Get<Tile>();
            if (tile != null)
            {
                tile.Type = new TileType
                {
                    Name = "AbandonedCamp",
                    Settings = new Dictionary<string, string>
                    {
                        { "MapCharacter", "C" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", ConsoleColor.DarkYellow.ToString() }
                    }
                };
            }

            // Camps are passable and can be examined
        }
    }
}

