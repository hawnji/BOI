using System.Collections.Generic;
using System.Linq;
using Hollowcrawl.Economy;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;
using NUnit.Framework;

namespace Hollowcrawl.Tests
{
    public class EconomyTests
    {
        private static ItemPools Pools(uint seed, System.Func<string, bool> unlocked = null)
            => new ItemPools(TestContent.Database.Items.Values, unlocked ?? (_ => true), new SeededRandom(seed));

        [Test]
        public void PoolsNeverRepeatWithinARunAndExhaust()
        {
            var pools = Pools(1);
            int n = pools.Remaining("treasure");
            var seen = new HashSet<string>();
            for (int i = 0; i < n; i++) Assert.IsTrue(seen.Add(pools.Draw("treasure", null).Id));
            Assert.IsNull(pools.Draw("treasure", null));
            Assert.AreEqual(0, pools.Remaining("treasure"));
        }

        [Test]
        public void DrawsAreSeededAndSkipHeldUniques()
        {
            var a = Pools(9); var b = Pools(9);
            for (int i = 0; i < 5; i++) Assert.AreEqual(a.Draw("treasure", null).Id, b.Draw("treasure", null).Id);

            var e = TestContent.NewEngine(out _);
            var held = TestContent.Item("ember_sigil");
            e.AddItem(held);
            var p = Pools(3);
            for (int i = 0; i < 30; i++) { var d = p.Draw("treasure", e); if (d == null) break; Assert.AreNotEqual(held.Id, d.Id); }
        }

        [Test]
        public void LockedItemsAreExcluded()
        {
            var db = new Content.ContentDatabase();
            db.LoadJson("t", "{\"items\":[{\"id\":\"a\",\"name\":\"A\",\"description\":\"d\",\"pools\":[\"p\"],\"unlockId\":\"secret_unlock\",\"effects\":[{\"effect\":\"stat\",\"stat\":\"Luck\",\"value\":1}]}," +
                              "{\"id\":\"b\",\"name\":\"B\",\"description\":\"d\",\"pools\":[\"p\"],\"effects\":[{\"effect\":\"stat\",\"stat\":\"Luck\",\"value\":1}]}]}");
            var locked = new ItemPools(db.Items.Values, id => false, new SeededRandom(1));
            Assert.AreEqual(1, locked.Remaining("p"));
            var open = new ItemPools(db.Items.Values, id => id == "secret_unlock", new SeededRandom(1));
            Assert.AreEqual(2, open.Remaining("p"));
        }

        [Test]
        public void QualityGatesRelaxWhenNeeded()
        {
            var pools = Pools(5);
            var best = pools.Draw("treasure", null, minQuality: 3);
            Assert.GreaterOrEqual(best.Quality, 3);
            var impossible = pools.Draw("shop", null, minQuality: 4); // nothing quality 4 in the starter shop pool
            Assert.IsNotNull(impossible, "falls back to any quality rather than nothing");
        }

        [Test]
        public void ShopGeneratesPricesAndSells()
        {
            var e = TestContent.NewEngine(out _);
            var shop = ShopInventory.Generate(6, Pools(11), e, new SeededRandom(11), 0);
            Assert.AreEqual(6, shop.Offers.Count);
            Assert.IsTrue(shop.Offers.Any(o => o.Kind == ShopOfferKind.Item));
            Assert.IsTrue(shop.Offers.All(o => o.Price > 0));

            var wallet = new PlayerResources();
            int idx = shop.Offers.ToList().FindIndex(o => o.Kind == ShopOfferKind.Item);
            Assert.IsFalse(shop.TryBuy(idx, wallet, e, out _), "no money");
            wallet.Add(ResourceKind.Coins, 99);
            Assert.IsTrue(shop.TryBuy(idx, wallet, e, out _));
            Assert.IsTrue(e.HasItem(shop.Offers[idx].Item.Id));
            Assert.AreEqual(99 - shop.Offers[idx].Price, wallet.Get(ResourceKind.Coins));
            Assert.IsFalse(shop.TryBuy(idx, wallet, e, out _), "already sold");
        }

        [Test]
        public void DiscountItemsRepriceTheShop()
        {
            var e = TestContent.NewEngine(out _);
            var shop = ShopInventory.Generate(4, Pools(2), e, new SeededRandom(2), 0);
            int before = shop.Offers.Sum(o => o.Price);
            e.AddItem(TestContent.Item("brass_tally"));
            shop.Reprice(e);
            Assert.Less(shop.Offers.Sum(o => o.Price), before);
        }

        [Test]
        public void DropRatesRespondToLuckAndItems()
        {
            int Count(InteractionEngine engine, uint seed)
            {
                var t = new DropTables(); var rng = new SeededRandom(seed); int n = 0;
                for (int i = 0; i < 4000; i++) if (t.RollRoomClear(rng, engine) != null) n++;
                return n;
            }
            var plain = TestContent.NewEngine(out _);
            var lucky = TestContent.NewEngine(out _);
            lucky.Stats.Add(new Stats.StatModifier(Stats.StatId.Luck, Stats.ModifierOp.Flat, 5f, "test"));
            Assert.Greater(Count(lucky, 4), Count(plain, 4) + 200);
        }
    }
}
