using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>One good's state at a market: how much is in stock, its base (equilibrium) price, the
    /// stock the market wants on hand, and the current price derived from stock vs. that target.</summary>
    public sealed class GoodMarket
    {
        public double Stock { get; set; }
        public double BasePrice { get; set; }
        public double Target { get; set; }
        public double Price { get; set; }

        /// <summary>Price rises as stock falls below target and falls on a glut, clamped so a shortage or
        /// glut can never run the price away.</summary>
        public const double MinPriceMult = 0.25;
        public const double MaxPriceMult = 4.0;

        /// <summary>Recompute <see cref="Price"/> from current <see cref="Stock"/> vs <see cref="Target"/>.
        /// Shared by the economy tick and by a player buy/sell, so a large trade immediately moves the
        /// price it transacts against.</summary>
        public void Reprice()
        {
            double target = Target > 0 ? Target : 1.0;
            double mult = System.Math.Clamp(target / System.Math.Max(Stock, 1.0), MinPriceMult, MaxPriceMult);
            Price = BasePrice * mult;
        }
    }

    /// <summary>
    /// A settlement's market — the local price of every good it makes or needs. Price is the whole point:
    /// it rises when stock falls below the target and falls on a glut, so a shortage here and a surplus
    /// two towns over create the price gap that goods flow down along <see cref="TradeLinks"/>. This is
    /// per-settlement (per-map) state the <see cref="Aetherium.Server.Economy.EconomySystem"/> ticks; it
    /// is deliberately separate from the cluster-level <c>Market</c> in the multiworld macro-economy.
    /// </summary>
    public class LocalMarket : Component
    {
        public Dictionary<string, GoodMarket> Goods { get; set; } = new();

        public LocalMarket() : base() { }
    }
}
