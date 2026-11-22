using System;
using System.Collections.Generic;
using Hollowcrawl.Items;
using Hollowcrawl.Rng;

namespace Hollowcrawl.Economy
{
    public enum ShopOfferKind { Item, Pickup }

    public sealed class ShopOffer
    {
        public ShopOfferKind Kind;
        public ItemDefinition Item;    // for Item offers
        public PickupKind Pickup;      // for Pickup offers
        public int Quantity = 1;
        public int BasePrice;
        public int Price;
        public bool Sold;
        public override string ToString() => (Kind == ShopOfferKind.Item ? Item.Id : Pickup + " x" + Quantity) + " $" + Price + (Sold ? " (sold)" : "");
    }

    /// <summary>Price table for pickups sold in shops. Data-tweakable.</summary>
    public sealed class ShopPricing
    {
        public int VigorHalf = 3, VigorFull = 5, Ward = 5, Bomb = 5, Key = 5, Consumable = 4, Trinket = 8;
        public int ItemQualityBase = 10, ItemQualityStep = 5;   // quality 0 -> 10, 4 -> 30 unless the item sets ShopPrice
        public float DeepFloorInflationPerFloor = 0.05f;
    }

    /// <summary>Generates a seeded shop inventory for one shop room and handles purchases.</summary>
    public sealed class ShopInventory
    {
        private readonly List<ShopOffer> _offers = new List<ShopOffer>();
        public IReadOnlyList<ShopOffer> Offers => _offers;

        public static ShopInventory Generate(int slots, ItemPools pools, InteractionEngine engine, SeededRandom rng, int floorIndex, ShopPricing pricing = null)
        {
            pricing = pricing ?? new ShopPricing();
            var shop = new ShopInventory();
            slots = Math.Max(2, Math.Min(8, slots));
            // Roughly half collectibles, half pickups; at least one collectible when available.
            int itemSlots = Math.Max(1, slots / 2 + (rng.Chance(0.3f) ? 1 : 0));
            float luck = engine?.Stats[Stats.StatId.Luck] ?? 0f;
            for (int i = 0; i < itemSlots; i++)
            {
                var item = pools.Draw("shop", engine, luck);
                if (item == null) break;
                int basePrice = item.ShopPrice > 0 ? item.ShopPrice : pricing.ItemQualityBase + pricing.ItemQualityStep * item.Quality;
                shop._offers.Add(new ShopOffer { Kind = ShopOfferKind.Item, Item = item, BasePrice = basePrice });
            }
            var pickupTable = new WeightedPicker<(PickupKind kind, int qty, int price)>();
            pickupTable.Add((PickupKind.VigorFull, 1, pricing.VigorFull), 3f);
            pickupTable.Add((PickupKind.Ward, 1, pricing.Ward), 1.5f);
            pickupTable.Add((PickupKind.Bomb, 1, pricing.Bomb), 2f);
            pickupTable.Add((PickupKind.Key, 1, pricing.Key), 2f);
            pickupTable.Add((PickupKind.Consumable, 1, pricing.Consumable), 2f);
            pickupTable.Add((PickupKind.Trinket, 1, pricing.Trinket), 0.7f);
            while (shop._offers.Count < slots)
            {
                var p = pickupTable.Pick(rng);
                shop._offers.Add(new ShopOffer { Kind = ShopOfferKind.Pickup, Pickup = p.kind, Quantity = p.qty, BasePrice = p.price });
            }
            float inflation = 1f + pricing.DeepFloorInflationPerFloor * Math.Max(0, floorIndex);
            foreach (var o in shop._offers) o.BasePrice = (int)Math.Ceiling(o.BasePrice * inflation);
            shop.Reprice(engine);
            return shop;
        }

        /// <summary>Recomputes prices through the engine (discount items change prices while you stand in the shop).</summary>
        public void Reprice(InteractionEngine engine)
        {
            foreach (var o in _offers)
                o.Price = engine != null ? engine.GetShopPrice(o.Item, o.BasePrice) : o.BasePrice;
        }

        /// <summary>Attempts a purchase. Collectibles are added to the engine; pickups are returned for the caller to grant.</summary>
        public bool TryBuy(int index, PlayerResources wallet, InteractionEngine engine, out ItemDefinition displaced)
        {
            displaced = null;
            if (index < 0 || index >= _offers.Count) return false;
            var o = _offers[index];
            if (o.Sold) return false;
            if (o.Kind == ShopOfferKind.Item && engine != null && !engine.CanAccept(o.Item)) return false;
            if (!wallet.TrySpend(ResourceKind.Coins, o.Price)) return false;
            o.Sold = true;
            if (o.Kind == ShopOfferKind.Item && engine != null) engine.AddItem(o.Item, out displaced);
            return true;
        }
    }
}
