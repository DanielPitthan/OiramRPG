using System;
using System.Collections.Generic;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Battle
{
    [CreateAssetMenu(menuName = "OiramRPG/Encounter", fileName = "Encounter")]
    public sealed class EncounterDefinition : Definition
    {
        public List<EnemyDefinition> enemies = new();
        public bool canFlee = true;
        public bool isBoss;
        [Tooltip("Loot extra ao vencer o encontro inteiro (além do loot de cada inimigo).")]
        public LootTable bonusLoot;

        public int Level
        {
            get
            {
                int level = 1;
                foreach (var enemy in enemies)
                    if (enemy != null) level = Math.Max(level, enemy.level);
                return level;
            }
        }
    }
}
