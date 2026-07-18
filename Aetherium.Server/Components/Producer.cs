using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// A place that <em>makes</em> goods each economy step. Rates are units per step, keyed by good
    /// name — a forest town produces timber, a plains town grain, a coastal town adds fish. Production
    /// feeds the co-located <see cref="LocalMarket"/>'s stock; the surplus is what flows out along
    /// <see cref="TradeLinks"/> to places that lack it. Rates scale with population at seed time.
    /// </summary>
    public class Producer : Component
    {
        public Dictionary<string, double> RatesPerStep { get; set; } = new();

        public Producer() : base() { }
    }
}
