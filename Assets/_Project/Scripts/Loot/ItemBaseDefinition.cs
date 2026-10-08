using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Loot
{
    [CreateAssetMenu(menuName = "OiramRPG/Item Base", fileName = "ItemBase")]
    public sealed class ItemBaseDefinition : Definition
    {
        public GrammaticalGender gender;
        public EquipSlot slot;
        public EquipCategory category;
        [Tooltip("Atributos no nível de item 1; escalam com BalanceConfig.itemScalingPerLevel.")]
        public List<StatModifier> baseModifiers = new();
        public int minItemLevel = 1;
        public float dropWeight = 1f;
        public Color color = Color.gray;
    }
}
