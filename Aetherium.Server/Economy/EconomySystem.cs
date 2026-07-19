using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Economy
{
    /// <summary>
    /// The living economy that ticks on a map (slotted into <c>GameMapGrain.TickAsync</c>). Each step it
    /// (1) runs every settlement's production and consumption into its <see cref="LocalMarket"/>, (2) prices
    /// each good from stock vs. target — scarce goods rise, gluts fall, (3) lets goods <b>arbitrage</b> from
    /// cheap markets to dear ones along <see cref="TradeLinks"/>, throttled by each route's throughput and
    /// length, then (4) reprices. Over many steps a forest's timber spreads toward the plains that lack it,
    /// prices converge across connected markets modulo transport friction, and distance stays legible in the
    /// price map. This is the T2 "real flows" tier of docs/economy-simulation.md, per-map and player-facing —
    /// distinct from the cluster-level macro-economy, which is untouched.
    ///
    /// <para>Instance state (like <c>DeathSystem</c>): it caches the map's market entities keyed by the world
    /// it last saw, so a step is O(markets) not O(all ~288k terrain entities), and rate-limits itself to one
    /// step per <see cref="Interval"/> of game time. A world with no markets (every square game) is a fast
    /// no-op.</para>
    /// </summary>
    public sealed class EconomySystem
    {
        /// <summary>Game time between economy steps — the economy is slow-moving, not per-frame.</summary>
        public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(2);

        // Pricing: price = base × clamp(target/stock, Min, Max). Stock is capped so a runaway glut can't
        // grow without bound (its price is already floored well before the cap).
        private const double MinPriceMult = 0.25;
        private const double MaxPriceMult = 4.0;
        private const double StockCapFactor = 8.0;

        // Trade: throughput = Base × link.Capacity / (1 + length/LengthScale). Capacity is the route's tier
        // (feeder 1, highway 3, rail/subway more). A step never drains more than MaxDrainFraction of the
        // source's stock, so trade is always stable.
        private const double BaseThroughput = 200.0;
        private const double LengthScale = 50.0;
        private const double MaxDrainFraction = 0.25;

        private const int MaxStepsPerTick = 4; // clamp catch-up if a big elapsed arrives

        private World? _cachedWorld;
        private List<Entity> _markets = new();
        private Dictionary<string, Entity> _marketById = new();
        private TimeSpan _accumulator = TimeSpan.Zero;

        /// <summary>Advance the economy by real elapsed game time, running whole steps at <see cref="Interval"/>
        /// cadence. Cheap no-op when the map has no markets.</summary>
        public void Step(World world, TimeSpan elapsed)
        {
            EnsureCache(world);
            if (_markets.Count == 0) return;

            _accumulator += elapsed;
            int steps = 0;
            while (_accumulator >= Interval && steps < MaxStepsPerTick)
            {
                RunStep(world);
                _accumulator -= Interval;
                steps++;
            }
            // Don't let a long stall bank unbounded catch-up.
            if (_accumulator > Interval) _accumulator = Interval;
        }

        /// <summary>One economy step, unconditionally (production → price → trade → price). Public for tests
        /// and deterministic stepping.</summary>
        public void RunStep(World world)
        {
            EnsureCache(world);
            if (_markets.Count == 0) return;

            foreach (var e in _markets) ProduceAndConsume(e);
            foreach (var e in _markets) Reprice(e.Get<LocalMarket>());
            Trade();
            foreach (var e in _markets) Reprice(e.Get<LocalMarket>());
        }

        private void EnsureCache(World world)
        {
            if (ReferenceEquals(_cachedWorld, world)) return;
            _cachedWorld = world;
            _markets = world.Entities.Values.Where(e => e.Has<LocalMarket>()).ToList();
            _marketById = _markets.ToDictionary(e => e.EntityId, e => e);
            _accumulator = TimeSpan.Zero;
        }

        private static void ProduceAndConsume(Entity e)
        {
            var market = e.Get<LocalMarket>();
            if (e.Has<Producer>())
                foreach (var (good, rate) in e.Get<Producer>().RatesPerStep)
                    if (market.Goods.TryGetValue(good, out var gm)) gm.Stock += rate;

            if (e.Has<Consumer>())
                foreach (var (good, rate) in e.Get<Consumer>().RatesPerStep)
                    if (market.Goods.TryGetValue(good, out var gm)) gm.Stock = Math.Max(0, gm.Stock - rate);

            foreach (var gm in market.Goods.Values)
                if (gm.Target > 0) gm.Stock = Math.Min(gm.Stock, gm.Target * StockCapFactor);
        }

        private static void Reprice(LocalMarket market)
        {
            foreach (var gm in market.Goods.Values)
            {
                double target = gm.Target > 0 ? gm.Target : 1.0;
                double mult = Math.Clamp(target / Math.Max(gm.Stock, 1.0), MinPriceMult, MaxPriceMult);
                gm.Price = gm.BasePrice * mult;
            }
        }

        // Arbitrage each undirected link once (canonical by entity-id order): for every good, move some
        // from the cheaper market to the pricier one, sized by the price gap and the route's throughput.
        private void Trade()
        {
            foreach (var a in _markets)
            {
                if (!a.Has<TradeLinks>()) continue;
                var la = a.Get<LocalMarket>();
                foreach (var link in a.Get<TradeLinks>().Links)
                {
                    if (string.CompareOrdinal(a.EntityId, link.To) >= 0) continue; // once per undirected edge
                    if (!_marketById.TryGetValue(link.To, out var b)) continue;
                    var lb = b.Get<LocalMarket>();

                    double throughput = BaseThroughput * Math.Max(0.0, link.Capacity)
                                        / (1.0 + Math.Max(0, link.Length) / LengthScale);

                    // Trade every good the source market carries; the good vocabulary is whatever the
                    // seeded markets hold (per-world data), not a fixed global list.
                    foreach (var good in la.Goods.Keys)
                    {
                        if (!lb.Goods.TryGetValue(good, out var gb) || !la.Goods.TryGetValue(good, out var ga))
                            continue;
                        var (src, dst) = ga.Price <= gb.Price ? (ga, gb) : (gb, ga);
                        double gap = dst.Price - src.Price;
                        if (gap <= 0) continue;

                        double gapFrac = gap / Math.Max(src.BasePrice, 1e-6);
                        double amount = Math.Min(src.Stock * MaxDrainFraction, throughput * gapFrac);
                        if (amount <= 0) continue;

                        src.Stock -= amount;
                        dst.Stock += amount;
                    }
                }
            }
        }
    }
}
