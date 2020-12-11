using System;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class Mind : Component
    {
        public ConcurrentDictionary<Position, List<SpaceTimeMemory>> SpaceTimeMemories;

        public List<SpaceTimeMemory> AllSpaceTimeMemories =>
            SpaceTimeMemories.SelectMany(m => m.Value).ToList();

        public int SpaceTimeMemoriesTracked => AllSpaceTimeMemories.Count();

        public int SpaceTimeMemoryImpressions => AllSpaceTimeMemories.Sum(m => m.Impressions);

        public int ContentTypeImpressions(string contentType) => 
            AllSpaceTimeMemories.Where(m => m.ContentType == contentType).Sum(m => m.Impressions);

        public int LocationsTracked => SpaceTimeMemories.Count;

        public Mind()
        {
            SpaceTimeMemories = new ConcurrentDictionary<Position, List<SpaceTimeMemory>>();
        }

        public void AddMemory(SpaceTimeMemory newMemory)
        {
            SpaceTimeMemories.TryAdd(newMemory.Location, new List<SpaceTimeMemory>());

            var locationMemories = SpaceTimeMemories[newMemory.Location].ToList();

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

        public void RemoveMemory(Position location, string contentType)
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

        public List<SpaceTimeMemory> Knowledge(Position location) =>
            SpaceTimeMemories.AtLocation(location)
            .OrderByDescending(m => m.LastEventTime)
            .ToList();

        public bool Knows(Position location) => Knowledge(location).Count > 0;

        public void Remember(Position location, string contentType, string content, 
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
