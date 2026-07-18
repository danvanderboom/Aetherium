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
        /// equilibrium. Population scales every rate, so a capital dwarfs a village.</summary>
        public static void Seed(Entity entity, Settlement s)
        {
            double pop = Math.Max(1, s.Population);

            var producer = new Producer();
            var primary = Goods.PrimaryProduction(s.Biome);
            if (primary is { } p) producer.RatesPerStep[p.Good] = p.PerPop * pop;
            if (s.Coastal)
                producer.RatesPerStep[Goods.Fish] =
                    producer.RatesPerStep.GetValueOrDefault(Goods.Fish) + Goods.CoastalFishPerPop * pop;

            var consumer = new Consumer();
            foreach (var (good, perPop) in Goods.ConsumePerPop)
                consumer.RatesPerStep[good] = perPop * pop;

            // A market for every good the place makes or needs, seeded at its target stock so it opens at
            // the base price and only moves as production/consumption/trade pull it off equilibrium.
            var market = new LocalMarket();
            foreach (var good in Goods.All)
            {
                double consume = consumer.RatesPerStep.GetValueOrDefault(good);
                double produce = producer.RatesPerStep.GetValueOrDefault(good);
                // Target tracks consumption; a pure exporter (produces, barely consumes) still gets a
                // sensible target from its own output so its glut price has a floor to fall to.
                double target = Math.Max(consume, produce) * StockHorizon;
                market.Goods[good] = new GoodMarket
                {
                    BasePrice = Goods.BasePrice.GetValueOrDefault(good, 1.0),
                    Target = target,
                    Stock = target,
                    Price = Goods.BasePrice.GetValueOrDefault(good, 1.0),
                };
            }

            entity.Set(producer);
            entity.Set(consumer);
            entity.Set(market);
        }

        /// <summary>Record a bidirectional trade route between two settlements (a road edge), so goods can
        /// arbitrage in either direction along it.</summary>
        public static void Link(Entity a, Entity b, bool highway, int length)
        {
            LinkOneWay(a, b.EntityId, highway, length);
            LinkOneWay(b, a.EntityId, highway, length);
        }

        private static void LinkOneWay(Entity from, string toId, bool highway, int length)
        {
            if (!from.Has<TradeLinks>()) from.Set(new TradeLinks());
            from.Get<TradeLinks>().Links.Add(new TradeLink { To = toId, Highway = highway, Length = length });
        }
    }
}
