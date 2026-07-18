using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// A place that <em>uses up</em> goods each economy step — everyone eats grain (or fish), builds with
    /// timber, works metal from ore. Consumption draws down the co-located <see cref="LocalMarket"/>'s
    /// stock; when a good runs short its price climbs, which pulls imports in along
    /// <see cref="TradeLinks"/>. Rates scale with population at seed time, so a capital is a hungry hub
    /// and a village barely dents supply.
    /// </summary>
    public class Consumer : Component
    {
        public Dictionary<string, double> RatesPerStep { get; set; } = new();

        public Consumer() : base() { }
    }
}
