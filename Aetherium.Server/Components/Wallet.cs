using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Spending money a trader (a player, a caravan) carries. The unit is abstract "credits"; a market
    /// buy debits it and a sell credits it, both at the good's current <see cref="GoodMarket.Price"/>.
    /// Kept separate from the item <see cref="Inventory"/> — currency is fungible, items aren't.
    /// </summary>
    public class Wallet : Component
    {
        /// <summary>The engine-default credits a freshly joined trader starts with — enough to make a few
        /// opening trades so the economy is reachable from turn one. This is the fallback only: a world
        /// overrides it as per-world data via <c>player.startingCurrency</c> in its game bundle
        /// (add-starting-currency-data), threaded to <c>GameMapGrain</c> and applied at join time.</summary>
        public const double StartingCurrency = 500.0;

        public double Currency { get; set; }

        public Wallet() : base() { }
    }
}
