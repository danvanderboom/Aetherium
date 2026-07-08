using System.Collections.Generic;

namespace Aetherium.Server.Factions
{
    /// <summary>
    /// Data-driven standing-change rules for one faction (engine gap-analysis §4.6): "a pacifist
    /// faction ranks you up for peaceful resolutions." Maps an action tag (e.g.
    /// <c>"peaceful_resolution"</c>, <c>"violence"</c>) to the standing delta that faction applies
    /// for it — two factions differ in disposition entirely through this data, not through engine code.
    /// </summary>
    public class FactionDoctrine
    {
        private readonly Dictionary<string, double> _standingDeltaByActionTag = new();

        public void SetDelta(string actionTag, double delta) => _standingDeltaByActionTag[actionTag] = delta;

        /// <summary>The standing delta for <paramref name="actionTag"/>, or 0 if this doctrine has no rule for it.</summary>
        public double DeltaFor(string actionTag)
            => _standingDeltaByActionTag.TryGetValue(actionTag, out var delta) ? delta : 0;
    }
}
