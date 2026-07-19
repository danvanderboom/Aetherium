using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Topology;

namespace Aetherium.Server.Economy
{
    /// <summary>The outcome of a buy/sell attempt: whether it filled, why not, and — when it did — how
    /// much moved at what unit price and total value.</summary>
    public readonly record struct TradeResult(bool Success, string? Reason, double Quantity, double UnitPrice, double Total)
    {
        public static TradeResult Fail(string reason) => new(false, reason, 0, 0, 0);
        public static TradeResult Ok(double qty, double price) => new(true, null, qty, price, qty * price);
    }

    /// <summary>
    /// A player's side of the economy: buy goods from a settlement's <see cref="LocalMarket"/> into a
    /// <see cref="GoodsHold"/>, paying from a <see cref="Wallet"/> at the good's current price; sell them
    /// back the same way. Every trade is all-or-nothing (no partial fills) and re-prices the good against
    /// its new stock, so a big buy walks the price up and a big sell walks it down — the same clamp band
    /// the <see cref="EconomySystem"/> tick uses. This is what turns the ticking simulation into a game a
    /// player can profit against by arbitraging shortages and gluts along the road network.
    /// </summary>
    public static class MarketTrade
    {
        /// <summary>Buy <paramref name="qty"/> of <paramref name="good"/> from <paramref name="market"/>
        /// into <paramref name="trader"/>'s hold, paying at the current price. Fails (no state change) on a
        /// bad quantity, an untraded good, a missing wallet, insufficient stock, or insufficient funds.</summary>
        public static TradeResult Buy(Entity trader, LocalMarket market, string good, double qty)
        {
            if (qty <= 0) return TradeResult.Fail("quantity must be positive");
            if (market is null || !market.Goods.TryGetValue(good, out var gm))
                return TradeResult.Fail($"this market does not trade {good}");
            if (!trader.Has<Wallet>()) return TradeResult.Fail("trader has no wallet");

            var wallet = trader.Get<Wallet>();
            double price = gm.Price;
            double cost = qty * price;
            if (gm.Stock < qty) return TradeResult.Fail("market is out of stock");
            if (wallet.Currency < cost) return TradeResult.Fail("insufficient funds");

            wallet.Currency -= cost;
            gm.Stock -= qty;
            gm.Reprice();

            var hold = trader.Has<GoodsHold>() ? trader.Get<GoodsHold>() : Attach(trader);
            hold.Units[good] = hold.Units.GetValueOrDefault(good) + qty;
            return TradeResult.Ok(qty, price);
        }

        /// <summary>Sell <paramref name="qty"/> of <paramref name="good"/> from <paramref name="trader"/>'s
        /// hold into <paramref name="market"/>, crediting the wallet at the current price. Fails (no state
        /// change) on a bad quantity, an untraded good, a missing wallet, or not carrying enough.</summary>
        public static TradeResult Sell(Entity trader, LocalMarket market, string good, double qty)
        {
            if (qty <= 0) return TradeResult.Fail("quantity must be positive");
            if (market is null || !market.Goods.TryGetValue(good, out var gm))
                return TradeResult.Fail($"this market does not trade {good}");
            if (!trader.Has<Wallet>()) return TradeResult.Fail("trader has no wallet");
            if (!trader.Has<GoodsHold>() || trader.Get<GoodsHold>().Units.GetValueOrDefault(good) < qty)
                return TradeResult.Fail($"not carrying {qty} {good}");

            double price = gm.Price;
            var hold = trader.Get<GoodsHold>();
            hold.Units[good] -= qty;
            gm.Stock += qty;
            gm.Reprice();
            trader.Get<Wallet>().Currency += qty * price;
            return TradeResult.Ok(qty, price);
        }

        private static GoodsHold Attach(Entity e)
        {
            var hold = new GoodsHold();
            e.Set(hold);
            return hold;
        }

        /// <summary>The settlement market a trader standing at <paramref name="loc"/> can trade at: the
        /// nearest settlement whose core the trader is within (topology distance ≤ the settlement's
        /// <see cref="Settlement.CoreRadius"/>). Null if the trader isn't in any town. Scans the few
        /// settlement entities, not the whole terrain.</summary>
        public static (LocalMarket Market, Settlement Settlement)? ResolveMarketAt(World world, WorldLocation loc)
        {
            if (world is null || loc is null) return null;
            var topo = world.Topology;
            var here = new GridCoord(loc.X, loc.Y, loc.Z);

            (LocalMarket Market, Settlement Settlement)? best = null;
            int bestDist = int.MaxValue;
            foreach (var e in world.Entities.Values)
            {
                if (!e.Has<LocalMarket>() || !e.Has<Settlement>() || !e.Has<WorldLocation>()) continue;
                var s = e.Get<Settlement>();
                var mloc = e.Get<WorldLocation>();
                int radius = Math.Max(1, s.CoreRadius);
                int d = topo.Distance(here, new GridCoord(mloc.X, mloc.Y, mloc.Z));
                if (d <= radius && d < bestDist)
                {
                    best = (e.Get<LocalMarket>(), s);
                    bestDist = d;
                }
            }
            return best;
        }

        /// <summary>Resolve the market under <paramref name="trader"/> and buy/sell there. Fails clearly
        /// when the trader isn't standing in a town, or on an unknown side.</summary>
        public static TradeResult ExecuteAt(World world, Entity trader, string side, string good, double quantity)
        {
            if (!trader.Has<WorldLocation>()) return TradeResult.Fail("trader has no location");
            var resolved = ResolveMarketAt(world, trader.Get<WorldLocation>());
            if (resolved is not { } r) return TradeResult.Fail("no market here — stand in a settlement to trade");
            return (side ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "buy" => Buy(trader, r.Market, good, quantity),
                "sell" => Sell(trader, r.Market, good, quantity),
                _ => TradeResult.Fail($"unknown side '{side}' (use buy or sell)"),
            };
        }
    }
}
