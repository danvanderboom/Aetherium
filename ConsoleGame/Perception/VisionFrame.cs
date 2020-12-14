using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ConsoleGame.Components
{
    public class VisionFrame : PerceptionFrame
    {
        public ConcurrentDictionary<WorldLocation, List<Visual>> Visuals { get; protected set; }

        public IList<Visual> GetVisualsOfType(VisualType type) => Visuals
            .SelectMany(v => v.Value)
            .Where(v => v.ThingsSeen.Keys.Any(t => t == type))
            .ToList();

        public IList<Visual> CharactersSeen(VisualType type) => GetVisualsOfType(type);

        public VisionFrame() : base()
        {
            Visuals = new ConcurrentDictionary<WorldLocation, List<Visual>>();
        }

        public VisionFrame(IList<Visual> visuals) : this()
        {
            foreach (var visual in visuals)
                AddVisual(visual);
        }

        public void AddVisual(Visual visual)
        {
            if (Visuals.TryGetValue(visual.Location, out var existingVisuals))
                existingVisuals.Add(visual);
            else
                Visuals.TryAdd(visual.Location, new List<Visual> { visual });
        }
    }
}
