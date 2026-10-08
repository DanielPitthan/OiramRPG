using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Characters;
using Oiram.Loot;

namespace Oiram.Inventory
{
    /// <summary>Mochila compartilhada da party: equipamentos, consumíveis e ouro.</summary>
    public sealed class PartyInventory
    {
        readonly List<ItemInstance> items = new();
        readonly List<ConsumableDefinition> consumableOrder = new();
        readonly Dictionary<ConsumableDefinition, int> consumableCounts = new();

        public int Capacity { get; set; }
        public int Gold { get; set; }

        public PartyInventory(int capacity) => Capacity = Math.Max(1, capacity);

        public IReadOnlyList<ItemInstance> Items => items;
        public bool IsFull => items.Count >= Capacity;

        public bool TryAdd(ItemInstance item)
        {
            if (item == null || IsFull || items.Contains(item)) return false;
            items.Add(item);
            return true;
        }

        public bool Remove(ItemInstance item) => items.Remove(item);

        /// <summary>Esvazia itens e consumíveis (carregar jogo salvo).</summary>
        public void Clear()
        {
            items.Clear();
            consumableOrder.Clear();
            consumableCounts.Clear();
        }

        /// <summary>Adiciona tudo do drop; devolve os itens que não couberam.</summary>
        public List<ItemInstance> AddLoot(LootDrop drop)
        {
            var overflow = new List<ItemInstance>();
            if (drop == null) return overflow;
            Gold += drop.Gold;
            foreach (var item in drop.Items)
                if (!TryAdd(item)) overflow.Add(item);
            foreach (var consumable in drop.Consumables)
                AddConsumable(consumable);
            return overflow;
        }

        public int Sell(ItemInstance item)
        {
            if (!items.Remove(item)) return 0;
            Gold += item.SellValue;
            return item.SellValue;
        }

        /// <summary>Ordem padrão da UI: slot, raridade (maior primeiro), nível do item.</summary>
        public List<ItemInstance> Sorted(EquipSlot? filter = null) => items
            .Where(i => filter == null || i.Slot == filter)
            .OrderBy(i => i.Slot)
            .ThenByDescending(i => i.Rarity)
            .ThenByDescending(i => i.ItemLevel)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        // ---------- Equipar ----------

        public bool Equip(PartyMember member, ItemInstance item)
        {
            if (member == null || item == null || !items.Contains(item) || !member.CanEquip(item)) return false;
            items.Remove(item);
            var previous = member.Equip(item);
            if (previous != null) items.Add(previous);
            return true;
        }

        public bool Unequip(PartyMember member, EquipSlot slot)
        {
            if (member?.GetEquipped(slot) == null || IsFull) return false;
            items.Add(member.Unequip(slot));
            return true;
        }

        /// <summary>Troca de job devolvendo à mochila os itens incompatíveis (ignora a capacidade).</summary>
        public bool ChangeJob(PartyMember member, JobDefinition job)
        {
            var removed = new List<ItemInstance>();
            if (!member.SetJob(job, removed)) return false;
            items.AddRange(removed);
            return true;
        }

        // ---------- Consumíveis ----------

        public IEnumerable<(ConsumableDefinition item, int count)> Consumables()
        {
            foreach (var def in consumableOrder)
                if (consumableCounts.TryGetValue(def, out int count) && count > 0)
                    yield return (def, count);
        }

        public int Count(ConsumableDefinition def) => def != null && consumableCounts.TryGetValue(def, out int c) ? c : 0;

        public void AddConsumable(ConsumableDefinition def, int count = 1)
        {
            if (def == null || count <= 0) return;
            if (!consumableCounts.ContainsKey(def))
            {
                consumableCounts[def] = 0;
                consumableOrder.Add(def);
            }
            consumableCounts[def] += count;
        }

        public bool ConsumeOne(ConsumableDefinition def)
        {
            if (Count(def) <= 0) return false;
            consumableCounts[def]--;
            return true;
        }
    }
}
