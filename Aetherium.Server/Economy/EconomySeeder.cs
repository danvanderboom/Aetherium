using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Server.Economy
{
    /// <summary>
    /// Turns a placed settlement into an economic actor: attaches a <see cref="Producer"/>, a
    /// <see cref="Consumer"/>, and a <see cref="LocalMarket"/> derived from the settlement's founding
    /// biome, coastal flag, and population, and wires <see cref="TradeLinks"/> from the road graph. After
    /// seeding, <see cref="EconomySystem"/> alone drives the numbers. Generic on purpose — any generator
    /// (square or sphere) can seed the same economy onto its settlements.
    /// </summary>
    public static class EconomySeeder
    {
        /// <summary>How many steps of consumption a market wants on hand — the target stock that anchors
        /// price. Below it, prices rise and pull imports; above it, prices fall and push exports.</summary>
        public const double StockHorizon = 100.0;

        /// <summary>Give <paramref name="entity"/> production, consumption, and a market priced at
        /// equilibrium, from the goods/recipe in <paramref name="config"/> (per-world data; null uses the
        /// built-in <see cref="Goods.DefaultConfig"/>). Population scales every rate, so a capital dwarfs a
        /// village.</summary>
        public static void Seed(Entity entity, Settlement s, Aetherium.Model.Economy.EconomyConfig? config = null)
        {
            var cfg = config ?? Goods.DefaultConfig();
            double pop = Math.Max(1, s.Population);

            var producer = new Producer();
            var primary = cfg.Production.Find(p => p.Biome == s.Biome);
            if (primary != null) producer.RatesPerStep[primary.Good] = primary.PerPop * pop;
            if (s.Coastal && !string.IsNullOrEmpty(cfg.CoastalGood))
                producer.RatesPerStep[cfg.CoastalGood] =
                    producer.RatesPerStep.GetValueOrDefault(cfg.CoastalGood) + cfg.CoastalPerPop * pop;

            var consumer = new Consumer();
            foreach (var g in cfg.Goods)
                consumer.RatesPerStep[g.Name] = g.ConsumePerPop * pop;

            // A market for every good the place makes or needs, seeded at its target stock so it opens at
            // the base price and only moves as production/consumption/trade pull it off equilibrium.
            var market = new LocalMarket();
            foreach (var g in cfg.Goods)
            {
                double consume = consumer.RatesPerStep.GetValueOrDefault(g.Name);
                double produce = producer.RatesPerStep.GetValueOrDefault(g.Name);
                // Target tracks consumption; a pure exporter (produces, barely consumes) still gets a
                // sensible target from its own output so its glut price has a floor to fall to.
                double target = Math.Max(consume, produce) * StockHorizon;
                market.Goods[g.Name] = new GoodMarket
                {
                    BasePrice = g.BasePrice,
                    Target = target,
                    Stock = target,
                    Price = g.BasePrice,
                };
            }

            entity.Set(producer);
            entity.Set(consumer);
            entity.Set(market);
        }

        /// <summary>Record a bidirectional road route between two settlements, so goods can arbitrage in
        /// either direction. A highway carries 3× a feeder.</summary>
        public static void Link(Entity a, Entity b, bool highway, int length)
            => LinkMode(a, b, "road", highway ? 3.0 : 1.0, length, highway);

        /// <summary>Record a bidirectional route of an arbitrary transport mode (rail, subway, …) with an
        /// explicit throughput capacity — the substrate for grade-separated transit across bands.</summary>
        public static void LinkMode(Entity a, Entity b, string mode, double capacity, int length, bool highway = false)
        {
            LinkOneWay(a, b.EntityId, mode, capacity, length, highway);
            LinkOneWay(b, a.EntityId, mode, capacity, length, highway);
        }

        private static void LinkOneWay(Entity from, string toId, string mode, double capacity, int length, bool highway)
        {
            if (!from.Has<TradeLinks>()) from.Set(new TradeLinks());
            from.Get<TradeLinks>().Links.Add(new TradeLink
            {
                To = toId, Mode = mode, Capacity = capacity, Length = length, Highway = highway,
            });
        }
    }
}
