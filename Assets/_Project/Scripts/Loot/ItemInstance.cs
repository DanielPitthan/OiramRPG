using System;
using System.Collections.Generic;
using System.Text;
using Oiram.Stats;

namespace Oiram.Loot
{
    [Serializable]
    public sealed class RolledAffix
    {
        public AffixDefinition affix;
        public int tier;
        public float value;

        public RolledAffix(AffixDefinition affix, int tier, float value)
        {
            this.affix = affix;
            this.tier = tier;
            this.value = value;
        }

        public StatModifier Modifier => new StatModifier(affix.stat, value, affix.kind);
    }

    /// <summary>Um equipamento concreto gerado pelo loot (base + nível + raridade + afixos rolados).</summary>
    public sealed class ItemInstance
    {
        readonly List<RolledAffix> affixes;
        readonly List<StatModifier> baseModifiers = new();

        public string Id { get; }
        public ItemBaseDefinition Base { get; }
        public int ItemLevel { get; }
        public Rarity Rarity { get; }
        public string Name { get; }
        public IReadOnlyList<RolledAffix> Affixes => affixes;
        public IReadOnlyList<StatModifier> BaseModifiers => baseModifiers;

        public EquipSlot Slot => Base.slot;
        public EquipCategory Category => Base.category;

        public ItemInstance(ItemBaseDefinition baseDef, int itemLevel, Rarity rarity, List<RolledAffix> affixes,
            float scalingPerLevel, string id = null)
        {
            Base = baseDef ?? throw new ArgumentNullException(nameof(baseDef));
            ItemLevel = Math.Max(1, itemLevel);
            Rarity = rarity;
            this.affixes = affixes ?? new List<RolledAffix>();
            Id = id ?? Guid.NewGuid().ToString("N");

            float factor = 1f + scalingPerLevel * (ItemLevel - 1);
            foreach (var mod in baseDef.baseModifiers)
            {
                var scaled = mod.Scaled(factor);
                // Valores flat inteiros ficam mais legíveis; mantém o sinal original (ex.: VEL −1).
                if (scaled.kind == ModifierKind.Flat) scaled.value = (float)Math.Round(scaled.value, MidpointRounding.AwayFromZero);
                baseModifiers.Add(scaled);
            }

            Name = ItemNameBuilder.Build(baseDef, this.affixes);
        }

        public IEnumerable<StatModifier> AllModifiers()
        {
            foreach (var mod in baseModifiers) yield return mod;
            foreach (var affix in affixes) yield return affix.Modifier;
        }

        public int SellValue => (4 + ItemLevel * 3) * (1 + (int)Rarity * (int)Rarity);

        public string DescribeLines()
        {
            var sb = new StringBuilder();
            foreach (var mod in baseModifiers) sb.AppendLine(StatText.Describe(mod));
            foreach (var affix in affixes) sb.AppendLine(StatText.Describe(affix.Modifier));
            return sb.ToString().TrimEnd();
        }

        public override string ToString() => $"{Name} [{RarityInfo.Name(Rarity)} Nv{ItemLevel}]";
    }

    public static class ItemNameBuilder
    {
        /// <summary>
        /// Monta o nome em português com concordância: "{Base} {Adjetivo} {Sufixo}".
        /// Usa só o primeiro prefixo e o primeiro sufixo para o nome não ficar enorme.
        /// </summary>
        public static string Build(ItemBaseDefinition baseDef, IReadOnlyList<RolledAffix> affixes)
        {
            string prefix = null;
            string suffix = null;
            if (affixes != null)
            {
                foreach (var rolled in affixes)
                {
                    if (rolled?.affix == null) continue;
                    if (rolled.affix.position == AffixPosition.Prefix && prefix == null)
                        prefix = rolled.affix.NameFor(baseDef.gender);
                    else if (rolled.affix.position == AffixPosition.Suffix && suffix == null)
                        suffix = rolled.affix.masculineName;
                }
            }

            var sb = new StringBuilder(baseDef.displayName);
            if (!string.IsNullOrEmpty(prefix)) sb.Append(' ').Append(prefix);
            if (!string.IsNullOrEmpty(suffix)) sb.Append(' ').Append(suffix);
            return sb.ToString();
        }
    }
}
