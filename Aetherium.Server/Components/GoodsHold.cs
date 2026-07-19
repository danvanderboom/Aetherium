using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// The bulk goods a trader is carrying, keyed by good name (units). Buying from a market moves stock
    /// into here; selling moves it back out. This is what makes arbitrage a player activity — buy grain
    /// cheap at a glutted port, carry it, sell it dear at an inland shortage. Distinct from the item
    /// <see cref="Inventory"/>: these are divisible commodities the <see cref="LocalMarket"/> prices, not
    /// discrete objects.
    /// </summary>
    public class GoodsHold : Component
    {
        public Dictionary<string, double> Units { get; set; } = new();

        public GoodsHold() : base() { }
    }
}
