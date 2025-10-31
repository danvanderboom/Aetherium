using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Server.Events;

namespace Aetherium.WorldGen.Passes
{
    /// <summary>
    /// Generation pass that seeds procedural events into the world:
    /// merchant caravans, monster invasions, and other time/location-based events.
    /// </summary>
    public sealed class EventSeedPass : IWorldGenerationPass
    {
        public string Name => "event-seed";
        public GenerationPhase Phase => GenerationPhase.Interactions;

        public bool SupportsTemplate(WorldGenerationTemplate template) => true; // Works with all templates

        public void Execute(WorldGenerationContext context)
        {
            if (context.World == null)
            {
                context.AddError("Event seed pass requires world instance");
                return;
            }

            var world = context.World;
            var rng = context.GeneratorContext.GetRandom("event-seed");

            // Seed events into shared data for later scheduling
            var seededEvents = new List<ScheduledEventSeed>();

            // Seed merchant caravans at specific locations
            if (context.StartLocation != null)
            {
                var caravanEvent = new ScheduledEventSeed
                {
                    EventType = "MerchantCaravan",
                    Location = context.StartLocation,
                    DelayHours = 24.0 + (rng.NextDouble() * 24.0), // 24-48 hours
                    EventData = new Dictionary<string, object>
                    {
                        ["merchantType"] = "traveling",
                        ["inventory"] = "general"
                    }
                };
                seededEvents.Add(caravanEvent);
            }

            // Seed monster invasions at remote locations
            if (context.PrimaryPath != null && context.PrimaryPath.Count > 0)
            {
                var invasionLocations = new List<WorldLocation>();
                // Pick 1-3 random locations along the primary path
                var numInvasions = 1 + rng.Next(3);
                for (int i = 0; i < numInvasions && i < context.PrimaryPath.Count; i++)
                {
                    var pathIndex = rng.Next(context.PrimaryPath.Count);
                    invasionLocations.Add(context.PrimaryPath[pathIndex]);
                }

                foreach (var location in invasionLocations)
                {
                    var invasionEvent = new ScheduledEventSeed
                    {
                        EventType = "MonsterInvasion",
                        Location = location,
                        DelayHours = 48.0 + (rng.NextDouble() * 72.0), // 48-120 hours
                        EventData = new Dictionary<string, object>
                        {
                            ["monsterType"] = GetRandomMonsterType(rng),
                            ["intensity"] = rng.Next(1, 4)
                        }
                    };
                    seededEvents.Add(invasionEvent);
                }
            }

            // Store seeded events in shared data for world initialization
            context.SharedData["events:seeded"] = seededEvents;
        }

        private string GetRandomMonsterType(Random rng)
        {
            var types = new[] { "goblin", "orc", "skeleton", "spider", "wolf" };
            return types[rng.Next(types.Length)];
        }
    }

    /// <summary>
    /// Represents an event that should be scheduled when the world initializes.
    /// </summary>
    public class ScheduledEventSeed
    {
        public string EventType { get; set; } = string.Empty;
        public WorldLocation? Location { get; set; }
        public double DelayHours { get; set; }
        public Dictionary<string, object> EventData { get; set; } = new Dictionary<string, object>();
    }
}

