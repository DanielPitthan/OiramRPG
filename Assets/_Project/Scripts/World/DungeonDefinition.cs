using System;
using System.Collections.Generic;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.World
{
    [Serializable]
    public struct DungeonEnemy
    {
        public EnemyDefinition enemy;
        public float weight;

        public DungeonEnemy(EnemyDefinition enemy, float weight)
        {
            this.enemy = enemy;
            this.weight = weight;
        }
    }

    /// <summary>Tema e população de uma dungeon procedural. O layout é gerado a cada entrada.</summary>
    [CreateAssetMenu(menuName = "OiramRPG/Dungeon", fileName = "Dungeon")]
    public sealed class DungeonDefinition : Definition
    {
        [Header("Tema")]
        public Color floorColor = new(0.5f, 0.45f, 0.4f);
        public Color floorHighColor = new(0.6f, 0.55f, 0.5f);
        public Color sideColor = new(0.3f, 0.25f, 0.2f);
        public Color voidColor = new(0.05f, 0.05f, 0.07f);
        public Color accentColor = new(1f, 0.7f, 0.3f);
        public Color skyColor = new(0.1f, 0.1f, 0.12f);
        public Color ambientColor = new(0.45f, 0.42f, 0.4f);

        [Header("População")]
        [Tooltip("Inimigos nunca ficam abaixo deste nível, mesmo no Fácil.")]
        public int baseLevel = 1;
        public List<DungeonEnemy> enemies = new();
        public EnemyDefinition boss;
        public List<EnemyDefinition> bossMinions = new();

        [Header("Loot")]
        public LootTable chestLoot;
        public LootTable bossChestLoot;

        [Header("Layout")]
        public int minRooms = 5;
        public int maxRooms = 7;
    }
}
