using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Server.Economy;

namespace Aetherium.Test.Economy
{
    /// <summary>
    /// The player-facing side of the economy (Item 2b): a trader with a <see cref="Wallet"/> buys goods
    /// from a settlement's <see cref="LocalMarket"/> into a <see cref="GoodsHold"/> and sells them back,
    /// at the good's current price. Verifies the bookkeeping is exact and conserved, that trades are
    /// all-or-nothing, that each trade re-prices against new stock, and that the whole point works:
    /// buying at a glut and selling at a shortage nets a profit.
    /// </summary>
    [TestFixture]
    public class MarketTradeTests
    {
        private static (Entity trader, Wallet wallet) Trader(double currency)
        {
            var e = new Character();
            var w = new Wallet { Currency = currency };
            e.Set(w);
            return (e, w);
        }

        private static LocalMarket Market(string good, double stock, double target, double basePrice)
        {
            var m = new LocalMarket();
            var gm = new GoodMarket { Stock = stock, Target = target, BasePrice = basePrice };
            gm.Reprice();
            m.Goods[good] = gm;
            return m;
        }

        [Test]
        public void BuyMovesCurrencyStockAndHold()
        {
            var (trader, wallet) = Trader(1000);
            var market = Market("Grain", stock: 500, target: 500, basePrice: 4.0); // at target → price 4.0
            double price = market.Goods["Grain"].Price;

            var r = MarketTrade.Buy(trader, market, "Grain", 10);

            Assert.That(r.Success, Is.True, r.Reason);
            Assert.That(r.Total, Is.EqualTo(10 * price).Within(1e-9));
            Assert.That(wallet.Currency, Is.EqualTo(1000 - 10 * price).Within(1e-9));
            Assert.That(market.Goods["Grain"].Stock, Is.EqualTo(490).Within(1e-9));
            Assert.That(trader.Get<GoodsHold>().Units["Grain"], Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void SellIsTheInverseOfBuy()
        {
            var (trader, wallet) = Trader(1000);
            var market = Market("Ore", stock: 500, target: 500, basePrice: 10.0);

            MarketTrade.Buy(trader, market, "Ore", 5);
            double afterBuy = wallet.Currency;
            var r = MarketTrade.Sell(trader, market, "Ore", 5);

            Assert.That(r.Success, Is.True, r.Reason);
            Assert.That(trader.Get<GoodsHold>().Units["Ore"], Is.EqualTo(0).Within(1e-9), "the hold is emptied");
            Assert.That(market.Goods["Ore"].Stock, Is.EqualTo(500).Within(1e-9), "stock returns to where it started");
            Assert.That(wallet.Currency, Is.GreaterThan(afterBuy), "selling credits the wallet");
        }

        [Test]
        public void BuyFailsWithoutFundsAndChangesNothing()
        {
            var (trader, wallet) = Trader(5);
            var market = Market("Ore", stock: 500, target: 500, basePrice: 10.0);

            var r = MarketTrade.Buy(trader, market, "Ore", 10); // costs ~100, wallet has 5

            Assert.That(r.Success, Is.False);
            Assert.That(r.Reason, Does.Contain("funds"));
            Assert.That(wallet.Currency, Is.EqualTo(5), "a failed buy must not spend");
            Assert.That(market.Goods["Ore"].Stock, Is.EqualTo(500), "a failed buy must not move stock");
            Assert.That(trader.Has<GoodsHold>(), Is.False, "a failed buy must not create a hold");
        }

        [Test]
        public void BuyFailsWhenOutOfStock()
        {
            var (trader, _) = Trader(100000);
            var market = Market("Fish", stock: 3, target: 100, basePrice: 5.0);

            var r = MarketTrade.Buy(trader, market, "Fish", 10);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Reason, Does.Contain("stock"));
        }

        [Test]
        public void SellFailsWhenNotCarryingEnough()
        {
            var (trader, _) = Trader(100);
            var market = Market("Timber", stock: 500, target: 500, basePrice: 6.0);

            var r = MarketTrade.Sell(trader, market, "Timber", 1);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Reason, Does.Contain("carrying"));
        }

        [Test]
        public void UntradedGoodAndBadQuantityAreRejected()
        {
            var (trader, _) = Trader(100);
            var market = Market("Grain", stock: 500, target: 500, basePrice: 4.0);

            Assert.That(MarketTrade.Buy(trader, market, "Unobtainium", 1).Success, Is.False);
            Assert.That(MarketTrade.Buy(trader, market, "Grain", 0).Success, Is.False);
            Assert.That(MarketTrade.Buy(trader, market, "Grain", -5).Success, Is.False);
        }

        [Test]
        public void ABigBuyWalksThePriceUp()
        {
            var (trader, _) = Trader(1_000_000);
            var market = Market("Ore", stock: 500, target: 500, basePrice: 10.0); // price 10 at target
            double before = market.Goods["Ore"].Price;

            MarketTrade.Buy(trader, market, "Ore", 400); // stock 500→100, well below target

            Assert.That(market.Goods["Ore"].Price, Is.GreaterThan(before),
                "draining stock below target must raise the price for the next buyer");
        }

        [Test]
        public void ResolveMarketAtFindsTheTownYouAreStandingIn()
        {
            var world = new Aetherium.Core.World();
            var town = new SettlementEntity();
            town.Set(new WorldLocation(10, 10, 0));
            town.Set(new Settlement { Name = "Rivertown", Tier = SettlementTier.Town, CoreRadius = 2 });
            town.Set(Market("Grain", 500, 500, 4.0));
            world.AddEntity(town);

            Assert.That(MarketTrade.ResolveMarketAt(world, new WorldLocation(11, 10, 0)), Is.Not.Null,
                "within the core radius resolves the town's market");
            Assert.That(MarketTrade.ResolveMarketAt(world, new WorldLocation(50, 50, 0)), Is.Null,
                "far from any settlement resolves nothing");
        }

        [Test]
        public void ExecuteAtBuysFromTheColocatedMarket()
        {
            var world = new Aetherium.Core.World();
            var town = new SettlementEntity();
            town.Set(new WorldLocation(10, 10, 0));
            town.Set(new Settlement { Name = "Rivertown", CoreRadius = 2 });
            town.Set(Market("Grain", 500, 500, 4.0));
            world.AddEntity(town);

            var (trader, wallet) = Trader(1000);
            trader.Set(new WorldLocation(10, 10, 0)); // standing on the town
            world.AddEntity(trader);

            var ok = MarketTrade.ExecuteAt(world, trader, "buy", "Grain", 10);
            Assert.That(ok.Success, Is.True, ok.Reason);
            Assert.That(wallet.Currency, Is.LessThan(1000), "buying spent from the wallet");

            var (drifter, _) = Trader(1000);
            drifter.Set(new WorldLocation(80, 80, 0)); // wilderness
            world.AddEntity(drifter);
            var noMarket = MarketTrade.ExecuteAt(world, drifter, "buy", "Grain", 1);
            Assert.That(noMarket.Success, Is.False);
            Assert.That(noMarket.Reason, Does.Contain("no market"));
        }

        [Test]
        public void ArbitrageAcrossMarketsNetsAProfit()
        {
            // The gameplay loop: buy cheap at a glutted market, sell dear at a shortage.
            var (trader, wallet) = Trader(10_000);
            var glut = Market("Grain", stock: 4000, target: 500, basePrice: 4.0);      // oversupplied → cheap
            var shortage = Market("Grain", stock: 50, target: 500, basePrice: 4.0);    // scarce → dear
            Assert.That(glut.Goods["Grain"].Price, Is.LessThan(shortage.Goods["Grain"].Price),
                "precondition: the glut is cheaper than the shortage");

            double start = wallet.Currency;
            var bought = MarketTrade.Buy(trader, glut, "Grain", 40);
            var sold = MarketTrade.Sell(trader, shortage, "Grain", 40);

            Assert.That(bought.Success && sold.Success, Is.True);
            Assert.That(sold.Total, Is.GreaterThan(bought.Total), "sold dearer than bought");
            Assert.That(wallet.Currency, Is.GreaterThan(start), "the round trip turns a profit");
            Assert.That(trader.Get<GoodsHold>().Units["Grain"], Is.EqualTo(0).Within(1e-9), "goods delivered, none left");
        }
    }
}
