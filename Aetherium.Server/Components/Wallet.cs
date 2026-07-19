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
        public double Currency { get; set; }

        public Wallet() : base() { }
    }
}
