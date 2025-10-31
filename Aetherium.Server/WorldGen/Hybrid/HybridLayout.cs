using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.WorldGen.Hybrid
{
    /// <summary>
    /// Collection of hybrid anchors for mixed authored/procedural content.
    /// </summary>
    public sealed class HybridLayout
    {
        public List<HybridAnchor> Anchors { get; set; } = new List<HybridAnchor>();

        /// <summary>
        /// Checks if a location is blocked by any anchor.
        /// </summary>
        public bool IsBlocked(WorldLocation location, int zLevel)
        {
            return Anchors
                .Where(a => a.ZLevel == zLevel && a.IsBlocking)
                .Any(a => a.Contains(location, zLevel));
        }

        /// <summary>
        /// Gets all blocked locations for a given z-level.
        /// </summary>
        public HashSet<WorldLocation> GetBlockedLocations(int zLevel)
        {
            var blocked = new HashSet<WorldLocation>();
            foreach (var anchor in Anchors.Where(a => a.ZLevel == zLevel && a.IsBlocking))
            {
                foreach (var loc in anchor.GetLocations(zLevel))
                {
                    blocked.Add(loc);
                }
            }
            return blocked;
        }

        /// <summary>
        /// Gets anchors with a specific tag.
        /// </summary>
        public IEnumerable<HybridAnchor> GetAnchorsByTag(string tag, int zLevel)
        {
            return Anchors
                .Where(a => a.ZLevel == zLevel && a.Tags.Contains(tag));
        }

        /// <summary>
        /// Gets all anchors sorted by priority (highest first).
        /// </summary>
        public IEnumerable<HybridAnchor> GetAnchorsByPriority(int zLevel)
        {
            return Anchors
                .Where(a => a.ZLevel == zLevel)
                .OrderByDescending(a => a.Priority);
        }
    }
}

