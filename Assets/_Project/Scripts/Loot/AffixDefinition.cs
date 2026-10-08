using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Loot
{
    [Serializable]
    public struct AffixTier
    {
        public int minItemLevel;
        public float min;
        public float max;

        public AffixTier(int minItemLevel, float min, float max)
        {
            this.minItemLevel = minItemLevel;
            this.min = min;
            this.max = max;
        }
    }

    [CreateAssetMenu(menuName = "OiramRPG/Affix", fileName = "Affix")]
    public sealed class AffixDefinition : Definition
    {
        public AffixPosition position;
        [Tooltip("Prefixo: forma masculina do adjetivo. Sufixo: texto completo (\"do Tigre\").")]
        public string masculineName;
        [Tooltip("Prefixo: forma feminina do adjetivo. Vazio = igual à masculina.")]
        public string feminineName;
        [Tooltip("Afixos do mesmo grupo nunca aparecem juntos no mesmo item.")]
        public string group;
        public List<EquipSlot> allowedSlots = new();
        public float weight = 1f;
        [Tooltip("Afixo especial: Lendários sempre têm pelo menos um.")]
        public bool isSpecial;
        public StatType stat;
        public ModifierKind kind;
        public List<AffixTier> tiers = new();

        public string NameFor(GrammaticalGender gender) =>
            gender == GrammaticalGender.Feminine && !string.IsNullOrEmpty(feminineName) ? feminineName : masculineName;

        public bool AllowsSlot(EquipSlot slot) => allowedSlots.Contains(slot);

        public bool HasTierFor(int itemLevel)
        {
            foreach (var tier in tiers)
                if (tier.minItemLevel <= itemLevel) return true;
            return false;
        }
    }
}
