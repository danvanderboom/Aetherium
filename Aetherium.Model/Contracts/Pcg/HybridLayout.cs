using System.Collections.Generic;

namespace Aetherium.Model.Pcg
{
    /// <summary>
    /// Collection of hybrid anchors for mixed authored/procedural content.
    /// </summary>
    public sealed class HybridLayout
    {
        public List<HybridAnchor> Anchors { get; set; } = new List<HybridAnchor>();
    }
}

