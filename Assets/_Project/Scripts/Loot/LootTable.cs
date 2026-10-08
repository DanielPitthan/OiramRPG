using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Loot
{
    [Serializable]
    public struct ConsumableDrop
    {
        public ConsumableDefinition consumable;
        [Range(0f, 1f)] public float chance;
    }

    [CreateAssetMenu(menuName = "OiramRPG/Loot Table", fileName = "LootTable")]
    public sealed class LootTable : Definition
    {
        public int goldMin;
        public int goldMax;
        public int itemsMin;
        public int itemsMax = 1;
        [Tooltip("Nenhum item cai abaixo desta raridade.")]
        public Rarity rarityFloor = Rarity.Common;
        [Tooltip("Itens extras garantidos, um por entrada, com no mínimo a raridade indicada.")]
        public List<Rarity> guaranteedDrops = new();
        [Tooltip("Bases permitidas. Vazio = qualquer base do banco de dados.")]
        public List<ItemBaseDefinition> itemPool = new();
        public List<ConsumableDrop> consumables = new();
        public int itemLevelBonus;
    }
}
