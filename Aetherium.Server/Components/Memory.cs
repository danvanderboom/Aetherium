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

        public void AddMemory(SpaceTimeMemory newMemory)
        {
            SpaceTimeMemories.TryAdd(newMemory.Location, new List<SpaceTimeMemory>());

            // NOTE: must mutate the stored list — a .ToList() copy here silently dropped
            // every new memory (the component was dormant partly because of this bug).
            var locationMemories = SpaceTimeMemories[newMemory.Location];

            var matchingMemory = locationMemories.FirstOrDefault(m => 
                m.Location == newMemory.Location 
                && m.ContentType == newMemory.ContentType
                && m.Content == newMemory.Content);

            if (matchingMemory != null)
            {
                matchingMemory.LastEventTime = newMemory.LastEventTime;
                matchingMemory.Impressions++;
            }
            else
            {
                locationMemories.Add(newMemory);
            }
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
            double strength = 1, double bias = 0)
        {
            AddMemory(new SpaceTimeMemory
            {
                Location = location,
                LastEventTime = DateTime.Now,
                ContentType = contentType,
                Content = content,
                Strength = strength,
                Bias = bias
            });
        }
    }
}

