using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;

namespace Oiram.World
{
    public enum ShopKind
    {
        Consumables,
        Blacksmith,
        Gambler,
        Inn,
    }

    /// <summary>Regras das lojas da cidade (sem UI): estoque do ferreiro, preços, apostador, pousada e relíquias.</summary>
    public static class ShopService
    {
        static readonly EquipSlot[] StockSlots =
        {
            EquipSlot.Weapon, EquipSlot.Weapon, EquipSlot.Weapon,
            EquipSlot.Armor, EquipSlot.Armor,
            EquipSlot.Helmet, EquipSlot.Helmet,
            EquipSlot.Accessory,
        };

        /// <summary>Hash estável (string.GetHashCode muda entre execuções).</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s ?? "") h = h * 31 + c;
                return h;
            }
        }

        /// <summary>Estoque atual do ferreiro; muda quando o contador de renovação muda (após cada dungeon).</summary>
        public static List<ItemInstance> BlacksmithStock(GameSession s, string townId)
        {
            if (s.ShopStock.TryGetValue(townId, out var stock) &&
                s.ShopStockVersion.TryGetValue(townId, out int version) && version == s.ShopRefreshCount)
                return stock;

            var rng = new SeededRandom(unchecked(s.WorldSeed * 31 + s.ShopRefreshCount * 7919 + StableHash(townId)));
            var gen = new LootGenerator(s.Db, rng);
            int level = s.PartyLevel;
            stock = new List<ItemInstance>();
            int count = Math.Max(1, s.Balance.blacksmithStock);
            for (int i = 0; i < count; i++)
            {
                var slot = StockSlots[i % StockSlots.Length];
                int itemLevel = level + rng.RangeInclusive(0, 1);
                var pool = s.Db.items.Where(b => b.slot == slot && b.minItemLevel <= itemLevel).ToList();
                if (pool.Count == 0) continue;
                stock.Add(gen.Create(gen.RollBase(itemLevel, pool), itemLevel, gen.RollRarity(30f, Rarity.Uncommon)));
            }
            s.ShopStock[townId] = stock;
            s.ShopStockVersion[townId] = s.ShopRefreshCount;
            return stock;
        }

        public static int BuyPrice(ItemInstance item, BalanceConfig b) => (int)Math.Round(item.SellValue * b.shopBuyMultiplier);

        public static bool CanAfford(GameSession s, int price) => s.Inventory.Gold >= price;

        public static bool BuyItem(GameSession s, string townId, ItemInstance item)
        {
            var stock = BlacksmithStock(s, townId);
            int price = BuyPrice(item, s.Balance);
            if (!stock.Contains(item) || !CanAfford(s, price) || s.Inventory.IsFull) return false;
            s.Inventory.Gold -= price;
            s.Inventory.TryAdd(item);
            stock.Remove(item);
            return true;
        }

        public static bool BuyConsumable(GameSession s, ConsumableDefinition item)
        {
            if (item == null || !CanAfford(s, item.price)) return false;
            s.Inventory.Gold -= item.price;
            s.Inventory.AddConsumable(item);
            return true;
        }

        public static int GamblePrice(GameSession s) => s.Balance.GamblePrice(s.PartyLevel);

        /// <summary>Item misterioso: paga sem ver os afixos; nunca vem Comum.</summary>
        public static ItemInstance Gamble(GameSession s, EquipSlot slot, IRandom rng)
        {
            int price = GamblePrice(s);
            if (!CanAfford(s, price) || s.Inventory.IsFull) return null;
            int level = s.PartyLevel;
            var pool = s.Db.items.Where(b => b.slot == slot && b.minItemLevel <= level).ToList();
            if (pool.Count == 0) return null;

            var weights = s.Balance.gambleWeights;
            var rarities = ((Rarity[])Enum.GetValues(typeof(Rarity))).Where(r => (int)r < weights.Length).ToList();
            var rarity = rng.PickWeighted(rarities, r => weights[(int)r]);
            if (rarity == Rarity.Common) rarity = Rarity.Uncommon;

            var gen = new LootGenerator(s.Db, rng);
            var item = gen.Create(gen.RollBase(level, pool), level, rarity);
            s.Inventory.Gold -= price;
            s.Inventory.TryAdd(item);
            return item;
        }

        public static int InnPrice(GameSession s) => s.Balance.InnPrice(s.PartyLevel);

        public static bool Rest(GameSession s)
        {
            int price = InnPrice(s);
            if (!CanAfford(s, price)) return false;
            s.Inventory.Gold -= price;
            s.RestoreAll();
            return true;
        }

        /// <summary>Baú escondido: uma Relíquia acima do nível da party (uma vez por cidade).</summary>
        public static ItemInstance OpenHiddenChest(GameSession s, string chestId)
        {
            if (s.ClearedFieldObjects.Contains(chestId)) return null;
            s.ClearedFieldObjects.Add(chestId);
            var gen = new LootGenerator(s.Db, s.Rng);
            var relic = gen.CreateRelic(s.PartyLevel + s.Balance.relicLevelBonus);
            s.Inventory.Capacity = Math.Max(s.Inventory.Capacity, s.Inventory.Items.Count + 1); // relíquia nunca se perde
            s.Inventory.TryAdd(relic);
            return relic;
        }
    }
}
