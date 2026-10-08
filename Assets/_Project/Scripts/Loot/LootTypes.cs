using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Loot
{
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        /// <summary>Raríssima: só em baús escondidos (um por cidade). 5 afixos com rolagem máxima.</summary>
        Relic,
    }

    public enum EquipSlot
    {
        Weapon,
        Armor,
        Helmet,
        Accessory,
    }

    public enum EquipCategory
    {
        Sword,
        Axe,
        Dagger,
        Staff,
        Mace,
        HeavyArmor,
        LightArmor,
        Robe,
        HeavyHelm,
        Hood,
        Hat,
        Ring,
        Amulet,
    }

    public enum GrammaticalGender
    {
        Masculine,
        Feminine,
    }

    public enum AffixPosition
    {
        Prefix, // adjetivo após o substantivo: "Espada Afiada"
        Suffix, // complemento: "do Tigre"
    }

    public static class RarityInfo
    {
        public static int AffixCount(Rarity rarity) => (int)rarity;

        public static string Name(Rarity rarity) => rarity switch
        {
            Rarity.Common => "Comum",
            Rarity.Uncommon => "Incomum",
            Rarity.Rare => "Raro",
            Rarity.Epic => "Épico",
            Rarity.Legendary => "Lendário",
            Rarity.Relic => "Relíquia",
            _ => rarity.ToString(),
        };

        public static Color Color(Rarity rarity) => rarity switch
        {
            Rarity.Common => new Color(0.86f, 0.86f, 0.86f),
            Rarity.Uncommon => new Color(0.35f, 0.85f, 0.35f),
            Rarity.Rare => new Color(0.30f, 0.58f, 1f),
            Rarity.Epic => new Color(0.72f, 0.40f, 1f),
            Rarity.Legendary => new Color(1f, 0.62f, 0.15f),
            Rarity.Relic => new Color(0.27f, 0.95f, 0.88f),
            _ => UnityEngine.Color.white,
        };

        /// <summary>Classe USS usada pela UI para colorir textos.</summary>
        public static string UssClass(Rarity rarity) => "rarity-" + rarity.ToString().ToLowerInvariant();

        public static string SlotName(EquipSlot slot) => slot switch
        {
            EquipSlot.Weapon => "Arma",
            EquipSlot.Armor => "Armadura",
            EquipSlot.Helmet => "Cabeça",
            EquipSlot.Accessory => "Acessório",
            _ => slot.ToString(),
        };

        public static string CategoryName(EquipCategory category) => category switch
        {
            EquipCategory.Sword => "Espada",
            EquipCategory.Axe => "Machado",
            EquipCategory.Dagger => "Adaga",
            EquipCategory.Staff => "Cajado",
            EquipCategory.Mace => "Maça",
            EquipCategory.HeavyArmor => "Armadura pesada",
            EquipCategory.LightArmor => "Armadura leve",
            EquipCategory.Robe => "Túnica",
            EquipCategory.HeavyHelm => "Elmo",
            EquipCategory.Hood => "Capuz",
            EquipCategory.Hat => "Chapéu",
            EquipCategory.Ring => "Anel",
            EquipCategory.Amulet => "Amuleto",
            _ => category.ToString(),
        };
    }
}
