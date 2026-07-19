using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class Memory : Component
    {
        public ConcurrentDictionary<WorldLocation, List<SpaceTimeMemory>> SpaceTimeMemories;

        public List<SpaceTimeMemory> AllSpaceTimeMemories =>
            SpaceTimeMemories.SelectMany(m => m.Value).ToList();

        public int SpaceTimeMemoriesTracked => AllSpaceTimeMemories.Count();

        public int SpaceTimeMemoryImpressions => AllSpaceTimeMemories.Sum(m => m.Impressions);

        public int ContentTypeImpressions(string contentType) => 
            AllSpaceTimeMemories.Where(m => m.ContentType == contentType).Sum(m => m.Impressions);

        public int LocationsTracked => SpaceTimeMemories.Count;

        public Memory() : base()
        {
            SpaceTimeMemories = new ConcurrentDictionary<WorldLocation, List<SpaceTimeMemory>>();
        }

        public void AddMemory(SpaceTimeMemory newMemory) =>
            AddMemory(newMemory, default, newMemory.LastEventTime);

        /// <summary>
        /// Records a perceived memory, applying memory dynamics (add-memory-dynamics) when
        /// <paramref name="dynamics"/> is enabled: a re-encounter of existing content spaced at least
        /// <c>MinReinforcementIntervalSeconds</c> after it was last seen grows the memory's stability
        /// and refreshes its strength, latching permanence past the threshold. A massed re-encounter
        /// (within the interval) bumps impressions and last-seen only — never compounding durability.
        /// A default (<c>Enabled = false</c>) <paramref name="dynamics"/> reproduces the exact legacy
        /// path: impressions and last-seen update, nothing else. <paramref name="now"/> is the time
        /// used for the spacing comparison and last-seen stamp (passed in for deterministic tests).
        /// </summary>
        public void AddMemory(SpaceTimeMemory newMemory, MemoryDynamics dynamics, DateTime now)
        {
            SpaceTimeMemories.TryAdd(newMemory.Location, new List<SpaceTimeMemory>());

            // Operate on the stored list directly. A previous `.ToList()` here copied the
            // list, so the Add below wrote to a throwaway copy and new memories were silently
            // lost (existing-memory updates happened to work because they mutate shared
            // references). RemoveMemory already uses the stored list; this keeps them consistent.
            var locationMemories = SpaceTimeMemories[newMemory.Location];

            var matchingMemory = locationMemories.FirstOrDefault(m =>
                m.Location == newMemory.Location
                && m.ContentType == newMemory.ContentType
                && m.Content == newMemory.Content);

            if (matchingMemory != null)
            {
                // Reinforcement is decided from the PRIOR last-seen time, before it is overwritten
                // below — a spaced revisit compounds stability; massed re-exposure does not.
                if (dynamics.Enabled && !matchingMemory.Permanent)
                {
                    var elapsed = now - matchingMemory.LastEventTime;
                    if (elapsed.TotalSeconds >= dynamics.MinReinforcementIntervalSeconds)
                    {
                        matchingMemory.StabilitySeconds = MemoryPolicy.ReinforceStability(
                            matchingMemory.StabilitySeconds,
                            dynamics.BaseHalfLifeSeconds,
                            dynamics.StabilityGrowthFactor);
                        matchingMemory.Strength = 1.0;
                        if (matchingMemory.StabilitySeconds >= dynamics.PermanenceThresholdSeconds)
                            matchingMemory.Permanent = true;
                    }
                }

                matchingMemory.LastEventTime = now;
                matchingMemory.Impressions++;
            }
            else
            {
                locationMemories.Add(newMemory);
            }
        }

        /// <summary>
        /// Removes non-permanent memories at <paramref name="location"/> whose effective strength has
        /// decayed below <paramref name="forgetThreshold"/> (add-memory-dynamics). Called at write time
        /// only, so reads stay pure. A non-positive threshold disables culling. Returns the count
        /// removed; drops the location entirely when it empties.
        /// </summary>
        public int CullForgotten(WorldLocation location, double forgetThreshold,
            double fallbackHalfLifeSeconds, DateTime now)
        {
            if (forgetThreshold <= 0 || !SpaceTimeMemories.TryGetValue(location, out var locationMemories))
                return 0;

            var removed = locationMemories.RemoveAll(m => !m.Permanent
                && MemoryPolicy.EffectiveStrength(m.Strength, now - m.LastEventTime,
                    m.StabilitySeconds, m.Permanent, fallbackHalfLifeSeconds) < forgetThreshold);

            if (locationMemories.Count == 0)
                SpaceTimeMemories.TryRemove(location, out _);

            return removed;
        }

        public void RemoveMemory(WorldLocation location, string contentType)
        {
            if (!SpaceTimeMemories.ContainsKey(location))
                return;

            var locationMemories = SpaceTimeMemories[location];

            // find the existing memory object in the list
            var memory = locationMemories
                .FirstOrDefault(m => m.Location == location && m.ContentType == contentType);
            if (memory != null)
                locationMemories.Remove(memory);

            // any more memories remaining in that location?
            if (locationMemories.Count == 0)
                SpaceTimeMemories.TryRemove(location, out var _);
        }

        public List<SpaceTimeMemory> Knowledge(WorldLocation location) =>
            SpaceTimeMemories.AtLocation(location)
            .OrderByDescending(m => m.LastEventTime)
            .ToList();

        public bool Knows(WorldLocation location) => Knowledge(location).Count > 0;

        public void Remember(WorldLocation location, string contentType, string content,
            double strength = 1, double bias = 0) =>
            Remember(location, contentType, content, default, strength, bias);

        /// <summary>
        /// Records a perceived memory under the given <paramref name="dynamics"/> (add-memory-dynamics).
        /// See <see cref="AddMemory(SpaceTimeMemory, MemoryDynamics, DateTime)"/> for the reinforcement
        /// semantics; a default <paramref name="dynamics"/> is the legacy path.
        /// </summary>
        public void Remember(WorldLocation location, string contentType, string content,
            MemoryDynamics dynamics, double strength = 1, double bias = 0)
        {
            var now = DateTime.Now;
            AddMemory(new SpaceTimeMemory
            {
                Location = location,
                LastEventTime = now,
                ContentType = contentType,
                Content = content,
                Strength = strength,
                Bias = bias
            }, dynamics, now);
        }
    }
}

