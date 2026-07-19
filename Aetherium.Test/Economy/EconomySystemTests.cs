using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Economy;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators.Outdoor;

namespace Aetherium.Test.Economy
{
    /// <summary>
    /// The per-map biome economy (docs/economy-simulation.md T2). Verifies the seeder derives the right
    /// production/consumption from biome + population, that stock-based pricing makes scarcity dear and
    /// gluts cheap, that goods arbitrage from cheap markets to dear ones along trade links and converge,
    /// and that a world without markets is a no-op. A closing integration test drives the economy over a
    /// freshly generated planet.
    /// </summary>
    [TestFixture]
    public class EconomySystemTests
    {
        // ---- seeding ----

        [Test]
        public void SeederDerivesProductionConsumptionAndMarketFromBiome()
        {
            var e = new SettlementEntity();
            e.Set(new WorldLocation(0, 0, 0));
            var s = new Settlement { Biome = "Forest", Coastal = true, Population = 30000, Tier = SettlementTier.Town };
            EconomySeeder.Seed(e, s);

            var producer = e.Get<Producer>();
            Assert.That(producer.RatesPerStep.ContainsKey(Goods.Timber), Is.True, "a forest town produces timber");
            Assert.That(producer.RatesPerStep.ContainsKey(Goods.Fish), Is.True, "a coastal town lands fish");
            Assert.That(producer.RatesPerStep.ContainsKey(Goods.Ore), Is.False, "a forest town doesn't mine ore");

            var consumer = e.Get<Consumer>();
            foreach (var good in Goods.All)
                Assert.That(consumer.RatesPerStep[good], Is.GreaterThan(0), $"everyone consumes {good}");

            var market = e.Get<LocalMarket>();
            foreach (var good in Goods.All)
            {
                Assert.That(market.Goods.ContainsKey(good), Is.True);
                Assert.That(market.Goods[good].Price, Is.EqualTo(Goods.BasePrice[good]).Within(1e-9),
                    "a freshly seeded market opens at the base price");
            }
        }

        [Test]
        public void SeederHonoursACustomPerWorldRecipe()
        {
            // A bundle-supplied economy: a wholly different goods vocabulary and biome map. The seeder must
            // use it verbatim — proving goods/recipes are data, not the hard-coded default.
            var cfg = new Aetherium.Model.Economy.EconomyConfig
            {
                Goods = new()
                {
                    new() { Name = "Spice",   BasePrice = 12.0, ConsumePerPop = 0.002 },
                    new() { Name = "Crystal", BasePrice = 40.0, ConsumePerPop = 0.001 },
                },
                CoastalGood = "Pearl",
                CoastalPerPop = 0.004,
                Production = new()
                {
                    new() { Biome = "Desert", Good = "Spice",   PerPop = 0.02 },
                    new() { Biome = "Hills",  Good = "Crystal", PerPop = 0.01 },
                },
            };

            var e = new SettlementEntity();
            e.Set(new WorldLocation(0, 0, 0));
            EconomySeeder.Seed(e, new Settlement { Biome = "Desert", Coastal = true, Population = 10000 }, cfg);

            var producer = e.Get<Producer>();
            Assert.That(producer.RatesPerStep.ContainsKey("Spice"), Is.True, "a desert town produces the recipe's Spice");
            Assert.That(producer.RatesPerStep.ContainsKey("Pearl"), Is.True, "a coastal town lands the recipe's Pearl");
            Assert.That(producer.RatesPerStep.ContainsKey(Goods.Grain), Is.False, "the default goods must not leak in");

            var market = e.Get<LocalMarket>();
            Assert.That(market.Goods.Keys, Is.EquivalentTo(new[] { "Spice", "Crystal" }),
                "the market carries exactly the recipe's goods");
            Assert.That(market.Goods["Crystal"].Price, Is.EqualTo(40.0).Within(1e-9),
                "it opens at the recipe's base price, not a default");
        }

        [Test]
        public void PopulationScalesTheEconomy()
        {
            var big = Seeded("Plains", false, 1_000_000);
            var small = Seeded("Plains", false, 3_000);
            Assert.That(big.Get<Producer>().RatesPerStep[Goods.Grain],
                Is.GreaterThan(small.Get<Producer>().RatesPerStep[Goods.Grain]),
                "a capital out-produces a village");
        }

        // ---- pricing ----

        [Test]
        public void ScarcityRaisesPriceAndGlutLowersIt()
        {
            var (world, econ) = NewEconWorld();
            // A lone plains town: it produces grain (net surplus) but must import timber/ore/fish (never
            // produced here), so grain gluts and the rest go scarce.
            var town = Seeded("Plains", false, 30000);
            world.AddEntity(town);

            for (int i = 0; i < 20; i++) econ.RunStep(world);

            var m = town.Get<LocalMarket>();
            Assert.That(m.Goods[Goods.Grain].Price, Is.LessThan(Goods.BasePrice[Goods.Grain]),
                "a grain surplus should cheapen grain");
            Assert.That(m.Goods[Goods.Timber].Price, Is.GreaterThan(Goods.BasePrice[Goods.Timber]),
                "unmet timber demand should dear it up");
        }

        // ---- trade ----

        [Test]
        public void GoodsArbitrageFromCheapToDearAndConverge()
        {
            var (world, econ) = NewEconWorld();
            // A: a grain glut (cheap). B: a grain shortage (dear). One trade link between them.
            var a = GrainMarket(world, x: 0, stock: 5000, target: 1000);
            var b = GrainMarket(world, x: 1, stock: 100, target: 1000);
            EconomySeeder.Link(a, b, highway: false, length: 10);

            double GapNow() => Math.Abs(a.Get<LocalMarket>().Goods[Goods.Grain].Price
                                        - b.Get<LocalMarket>().Goods[Goods.Grain].Price);

            double aStock0 = a.Get<LocalMarket>().Goods[Goods.Grain].Stock;
            double bStock0 = b.Get<LocalMarket>().Goods[Goods.Grain].Stock;

            econ.RunStep(world);
            double gap1 = GapNow();
            // Goods moved from the cheap glut to the dear shortage.
            Assert.That(a.Get<LocalMarket>().Goods[Goods.Grain].Stock, Is.LessThan(aStock0), "the exporter ships grain out");
            Assert.That(b.Get<LocalMarket>().Goods[Goods.Grain].Stock, Is.GreaterThan(bStock0), "the importer receives grain");

            for (int i = 0; i < 30; i++) econ.RunStep(world);
            Assert.That(GapNow(), Is.LessThan(gap1), "trade should narrow the price gap over time");
        }

        [Test]
        public void ShorterFatterRoutesEqualizeFaster()
        {
            // Two identical glut→shortage pairs. One joined by a short highway, one by a long feeder.
            var (world, econ) = NewEconWorld();
            var closeA = GrainMarket(world, 0, 5000, 1000);
            var closeB = GrainMarket(world, 1, 100, 1000);
            EconomySeeder.Link(closeA, closeB, highway: true, length: 5);

            var farA = GrainMarket(world, 2, 5000, 1000);
            var farB = GrainMarket(world, 3, 100, 1000);
            EconomySeeder.Link(farA, farB, highway: false, length: 400);

            for (int i = 0; i < 15; i++) econ.RunStep(world);

            double closeGap = Gap(closeA, closeB);
            double farGap = Gap(farA, farB);
            Assert.That(closeGap, Is.LessThan(farGap),
                "a short high-capacity route equalizes prices faster than a long thin one");
        }

        // ---- safety ----

        [Test]
        public void AWorldWithoutMarketsIsANoOp()
        {
            var (world, econ) = NewEconWorld();
            var e = new SettlementEntity();
            e.Set(new WorldLocation(0, 0, 0));
            world.AddEntity(e); // no LocalMarket

            Assert.DoesNotThrow(() => econ.RunStep(world));
            Assert.DoesNotThrow(() => econ.Step(world, TimeSpan.FromSeconds(60)));
        }

        // ---- integration: a generated planet has a live economy ----

        [Test]
        public void AGeneratedPlanetHasMarketsThatMove()
        {
            var ctx = new GeneratorContext(256, 256, 20260718)
            {
                GeneratorParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["h3Resolution"] = "2",
                    ["capitalCount"] = "2", ["cityCount"] = "3", ["townCount"] = "6", ["villageCount"] = "16",
                    ["capitalSpacingCells"] = "12", ["citySpacingCells"] = "8",
                    ["townSpacingCells"] = "5", ["villageSpacingCells"] = "3",
                }
            };
            var world = new H3TerrainGenerator().Generate(ctx);

            var markets = world.Entities.Values.Where(e => e.Has<LocalMarket>()).ToList();
            Assert.That(markets.Count, Is.GreaterThan(10), "settlements should be seeded with markets");
            Assert.That(markets.Any(e => e.Has<TradeLinks>() && e.Get<TradeLinks>().Links.Count > 0), Is.True,
                "the road graph should have become trade links");

            var econ = new EconomySystem();
            for (int i = 0; i < 40; i++) econ.RunStep(world);

            bool anyPriceMoved = markets.Any(e => e.Get<LocalMarket>().Goods
                .Any(g => Math.Abs(g.Value.Price - g.Value.BasePrice) > 1e-6));
            Assert.That(anyPriceMoved, Is.True, "a live economy should push some price off its base");
        }

        // ---- helpers ----

        private static (World World, EconomySystem Econ) NewEconWorld() => (new World(), new EconomySystem());

        private static Entity Seeded(string biome, bool coastal, int population)
        {
            var e = new SettlementEntity();
            e.Set(new WorldLocation(population % 997, 0, 0)); // any distinct location
            EconomySeeder.Seed(e, new Settlement { Biome = biome, Coastal = coastal, Population = population });
            return e;
        }

        private static Entity GrainMarket(World world, int x, double stock, double target)
        {
            var e = new SettlementEntity();
            e.Set(new WorldLocation(x, 0, 0));
            var m = new LocalMarket();
            m.Goods[Goods.Grain] = new GoodMarket
            {
                BasePrice = Goods.BasePrice[Goods.Grain],
                Target = target,
                Stock = stock,
                Price = Goods.BasePrice[Goods.Grain],
            };
            e.Set(m);
            world.AddEntity(e);
            return e;
        }

        private static double Gap(Entity a, Entity b)
            => Math.Abs(a.Get<LocalMarket>().Goods[Goods.Grain].Price - b.Get<LocalMarket>().Goods[Goods.Grain].Price);
    }
}
