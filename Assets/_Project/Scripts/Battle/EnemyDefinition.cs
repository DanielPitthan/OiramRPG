using System;
using System.Collections.Generic;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Battle
{
    public enum UnitShape
    {
        Hero,
        Slime,
        Bat,
        Goblin,
        Mimic,
        Golem,
        Spider,
        Skeleton,
        Ghost,
        Elemental,
        KingSlime,
        Knight,
    }

    [Serializable]
    public struct EnemyAbility
    {
        public AbilityDefinition ability;
        public float weight;

        public EnemyAbility(AbilityDefinition ability, float weight)
        {
            this.ability = ability;
            this.weight = weight;
        }
    }

    [CreateAssetMenu(menuName = "OiramRPG/Enemy", fileName = "Enemy")]
    public sealed class EnemyDefinition : Definition
    {
        public int level = 1;
        public PrimaryStats stats;
        public int xp;
        public int jp;
        public LootTable lootTable;
        [Tooltip("Habilidades e pesos da IA. O ataque básico entra automaticamente se a lista estiver vazia.")]
        public List<EnemyAbility> abilities = new();

        [Header("Visual (provisório)")]
        public UnitShape shape = UnitShape.Slime;
        public Color color = Color.green;
        public float scale = 1f;
        public bool flying;
        [Tooltip("Opcional: prefab de arte final. Se vazio, usa primitivas.")]
        public GameObject visualPrefab;
    }
}
