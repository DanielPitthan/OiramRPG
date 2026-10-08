using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oiram.Battle
{
    /// <summary>
    /// Cria cópias em tempo de execução de inimigos/encontros ajustadas a um nível e a uma dificuldade.
    /// Usado nas dungeons, cuja força acompanha a party.
    /// </summary>
    public static class EnemyScaling
    {
        /// <summary>Nível dos inimigos de uma dungeon para a party atual.</summary>
        public static int DungeonLevel(int partyLevel, int dungeonBaseLevel, DifficultyTier tier, BalanceConfig b) =>
            Math.Max(Math.Max(1, dungeonBaseLevel), partyLevel + b.TierLevelOffset(tier, partyLevel));

        public static EnemyDefinition Scaled(EnemyDefinition def, int level, DifficultyTier tier, BalanceConfig b)
        {
            var clone = Object.Instantiate(def);
            clone.name = def.name;
            clone.id = def.id;

            int delta = level - def.level;
            float tierMul = b.TierStatMultiplier(tier);
            float Grow(float value, float growth) => Math.Max(1f, value * (1f + growth * delta) * tierMul);

            var s = def.stats;
            clone.stats = new PrimaryStats(
                Grow(s.hp, b.enemyHpGrowth),
                Grow(s.attack, b.enemyAttackGrowth),
                Grow(s.defense, b.enemyDefenseGrowth),
                Grow(s.magic, b.enemyMagicGrowth),
                Grow(s.resistance, b.enemyResistanceGrowth),
                Math.Max(1f, s.speed * (1f + b.enemySpeedGrowth * delta)));
            clone.level = level;

            float reward = b.TierRewardMultiplier(tier);
            // XP acompanha a curva de níveis: o mesmo inimigo "vale" a mesma fração de um nível em qualquer nível.
            clone.xp = Math.Max(1, (int)Math.Round(def.xp / 10f * b.XpToNextLevel(level) / b.dungeonXpDivisor * reward));
            clone.jp = Math.Max(1, (int)Math.Round(def.jp * (1f + b.jpGrowthPerLevel * Math.Max(0, delta)) * reward));
            return clone;
        }

        public static EncounterDefinition Encounter(string id, string displayName, IEnumerable<EnemyDefinition> enemies,
            bool canFlee = true, bool isBoss = false, LootTable bonusLoot = null)
        {
            var enc = ScriptableObject.CreateInstance<EncounterDefinition>();
            enc.name = id;
            enc.id = id;
            enc.displayName = displayName;
            enc.enemies.AddRange(enemies);
            enc.canFlee = canFlee;
            enc.isBoss = isBoss;
            enc.bonusLoot = bonusLoot;
            return enc;
        }
    }
}
