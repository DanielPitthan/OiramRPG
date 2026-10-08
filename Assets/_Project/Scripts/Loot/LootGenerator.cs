using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Inventory;

namespace Oiram.Loot
{
    /// <summary>Resultado de uma rolagem de loot (inimigo, baú, caixa...).</summary>
    public sealed class LootDrop
    {
        public int Gold;
        public readonly List<ItemInstance> Items = new();
        public readonly List<ConsumableDefinition> Consumables = new();

        public bool IsEmpty => Gold <= 0 && Items.Count == 0 && Consumables.Count == 0;

        public Rarity? BestRarity
        {
            get
            {
                Rarity? best = null;
                foreach (var item in Items)
                    if (best == null || item.Rarity > best) best = item.Rarity;
                return best;
            }
        }

        public void Merge(LootDrop other)
        {
            if (other == null) return;
            Gold += other.Gold;
            Items.AddRange(other.Items);
            Consumables.AddRange(other.Consumables);
        }
    }

    /// <summary>
    /// Gerador de loot estilo Diablo: raridade ponderada (afetada por Achado Mágico e piso de raridade),
    /// afixos sorteados sem repetir grupo, tiers liberados pelo nível do item.
    /// </summary>
    public sealed class LootGenerator
    {
        public const int MaxPrefixes = 2;
        public const int MaxSuffixes = 2;

        /// <summary>Relíquias têm um prefixo a mais (5 afixos no total).</summary>
        public static int PrefixLimit(Rarity rarity) => rarity >= Rarity.Relic ? MaxPrefixes + 1 : MaxPrefixes;
        public static int SuffixLimit(Rarity rarity) => MaxSuffixes;

        readonly IReadOnlyList<ItemBaseDefinition> bases;
        readonly IReadOnlyList<AffixDefinition> affixes;
        readonly BalanceConfig balance;
        readonly IRandom rng;

        public LootGenerator(IReadOnlyList<ItemBaseDefinition> bases, IReadOnlyList<AffixDefinition> affixes,
            BalanceConfig balance, IRandom rng)
        {
            this.bases = bases ?? throw new ArgumentNullException(nameof(bases));
            this.affixes = affixes ?? throw new ArgumentNullException(nameof(affixes));
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public LootGenerator(GameDatabase db, IRandom rng) : this(db.items, db.affixes, db.balance, rng) { }

        static readonly Rarity[] AllRarities = (Rarity[])Enum.GetValues(typeof(Rarity));

        public float RarityWeight(Rarity rarity, float magicFind, Rarity floor)
        {
            if (rarity < floor) return 0f;
            int index = (int)rarity;
            float weight = index < balance.rarityWeights.Length ? balance.rarityWeights[index] : 0f;
            // Achado mágico favorece mais as raridades altas.
            if (index > 0) weight *= 1f + Math.Max(0f, magicFind) / 100f * index;
            return weight;
        }

        public Rarity RollRarity(float magicFind = 0f, Rarity floor = Rarity.Common)
        {
            var picked = rng.PickWeighted(AllRarities, r => RarityWeight(r, magicFind, floor));
            return picked < floor ? floor : picked;
        }

        public ItemBaseDefinition RollBase(int itemLevel, IReadOnlyList<ItemBaseDefinition> pool = null)
        {
            var source = pool != null && pool.Count > 0 ? pool : bases;
            var picked = rng.PickWeighted(source, b => b != null && b.minItemLevel <= itemLevel ? b.dropWeight : 0f);
            return picked ?? source[0];
        }

        public ItemInstance CreateRandom(int itemLevel, float magicFind = 0f, Rarity floor = Rarity.Common,
            IReadOnlyList<ItemBaseDefinition> pool = null)
        {
            return Create(RollBase(itemLevel, pool), itemLevel, RollRarity(magicFind, floor));
        }

        /// <summary>Relíquia: base sorteada, 5 afixos no tier mais alto liberado e com o valor máximo.</summary>
        public ItemInstance CreateRelic(int itemLevel, IReadOnlyList<ItemBaseDefinition> pool = null) =>
            Create(RollBase(itemLevel, pool), itemLevel, Rarity.Relic, perfectRolls: true);

        public ItemInstance Create(ItemBaseDefinition baseDef, int itemLevel, Rarity rarity, bool perfectRolls = false)
        {
            itemLevel = Math.Max(1, itemLevel);
            int wanted = RarityInfo.AffixCount(rarity);
            var rolled = new List<RolledAffix>(wanted);
            var usedGroups = new HashSet<string>();
            int prefixes = 0, suffixes = 0;
            int maxPrefixes = PrefixLimit(rarity), maxSuffixes = SuffixLimit(rarity);

            bool needSpecial = rarity >= Rarity.Legendary;
            var candidates = new List<AffixDefinition>();

            while (rolled.Count < wanted)
            {
                candidates.Clear();
                foreach (var affix in affixes)
                {
                    if (affix == null || !affix.AllowsSlot(baseDef.slot) || !affix.HasTierFor(itemLevel)) continue;
                    if (usedGroups.Contains(GroupOf(affix))) continue;
                    if (affix.position == AffixPosition.Prefix && prefixes >= maxPrefixes) continue;
                    if (affix.position == AffixPosition.Suffix && suffixes >= maxSuffixes) continue;
                    if (needSpecial && !affix.isSpecial) continue;
                    candidates.Add(affix);
                }

                if (candidates.Count == 0)
                {
                    if (needSpecial) { needSpecial = false; continue; }
                    break; // não há mais afixos válidos para este slot/nível
                }

                var chosen = rng.PickWeighted(candidates, a => a.weight);
                if (chosen == null) break;
                needSpecial = false;

                usedGroups.Add(GroupOf(chosen));
                if (chosen.position == AffixPosition.Prefix) prefixes++; else suffixes++;
                rolled.Add(RollAffix(chosen, itemLevel, perfectRolls));
            }

            return new ItemInstance(baseDef, itemLevel, rarity, rolled, balance.itemScalingPerLevel);
        }

        RolledAffix RollAffix(AffixDefinition affix, int itemLevel, bool perfect = false)
        {
            if (perfect)
            {
                int best = 0;
                for (int i = 0; i < affix.tiers.Count; i++)
                    if (affix.tiers[i].minItemLevel <= itemLevel) best = i;
                return new RolledAffix(affix, best, (float)Math.Floor(affix.tiers[best].max));
            }

            // Tiers mais altos (liberados pelo nível do item) têm mais peso.
            var eligible = new List<int>();
            for (int i = 0; i < affix.tiers.Count; i++)
                if (affix.tiers[i].minItemLevel <= itemLevel) eligible.Add(i);

            int tierIndex = rng.PickWeighted(eligible, i => i + 1f);
            var tier = affix.tiers[tierIndex];
            // Valores de afixo são inteiros (ATQ +5, +12%...).
            int lo = (int)Math.Ceiling(tier.min);
            int hi = (int)Math.Floor(tier.max);
            float value = hi >= lo ? rng.RangeInclusive(lo, hi) : tier.min;
            return new RolledAffix(affix, tierIndex, value);
        }

        static string GroupOf(AffixDefinition affix) => string.IsNullOrEmpty(affix.group) ? affix.id : affix.group;

        public int RollItemLevel(int sourceLevel, int bonus = 0) =>
            Math.Max(1, sourceLevel + bonus + rng.RangeInclusive(-1, 1));

        public LootDrop Roll(LootTable table, int sourceLevel, float magicFind = 0f, float goldFind = 0f)
        {
            var drop = new LootDrop();
            if (table == null) return drop;

            int gold = rng.RangeInclusive(Math.Min(table.goldMin, table.goldMax), Math.Max(table.goldMin, table.goldMax));
            drop.Gold = (int)Math.Round(gold * (1f + Math.Max(0f, goldFind) / 100f));

            int count = rng.RangeInclusive(Math.Min(table.itemsMin, table.itemsMax), Math.Max(table.itemsMin, table.itemsMax));
            for (int i = 0; i < count; i++)
                drop.Items.Add(CreateRandom(RollItemLevel(sourceLevel, table.itemLevelBonus), magicFind, table.rarityFloor, table.itemPool));

            foreach (var guaranteed in table.guaranteedDrops)
            {
                var floor = guaranteed > table.rarityFloor ? guaranteed : table.rarityFloor;
                drop.Items.Add(CreateRandom(RollItemLevel(sourceLevel, table.itemLevelBonus), magicFind, floor, table.itemPool));
            }

            foreach (var entry in table.consumables)
                if (entry.consumable != null && rng.Chance(entry.chance))
                    drop.Consumables.Add(entry.consumable);

            return drop;
        }
    }
}
