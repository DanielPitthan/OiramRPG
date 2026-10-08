using System;
using System.Collections.Generic;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using Object = UnityEngine.Object;

namespace Oiram.World
{
    /// <summary>Monta os encontros (já escalados) e o tesouro do chefe de uma dungeon.</summary>
    public static class DungeonPopulation
    {
        public static EncounterDefinition RollEncounter(DungeonDefinition dungeon, int level, DifficultyTier tier,
            BalanceConfig balance, IRandom rng, string id)
        {
            float roll = rng.Value();
            int count = roll < 0.25f ? 1 : roll < 0.65f ? 2 : 3;
            count = Math.Min(4, count + balance.TierExtraEnemies(tier));

            var enemies = new List<EnemyDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                var pick = rng.PickWeighted(dungeon.enemies, e => e.enemy != null ? e.weight : 0f).enemy;
                if (pick != null) enemies.Add(EnemyScaling.Scaled(pick, level, tier, balance));
            }
            string name = enemies.Count == 1 ? enemies[0].displayName : $"{enemies[0].displayName} e companhia";
            return EnemyScaling.Encounter(id, name, enemies);
        }

        /// <summary>Chefe um nível acima, com os dois capangas nos lados (o chefe fica no centro da arena).</summary>
        public static EncounterDefinition BossEncounter(DungeonDefinition dungeon, int level, DifficultyTier tier, BalanceConfig balance, string id)
        {
            var enemies = new List<EnemyDefinition>();
            if (dungeon.bossMinions.Count > 0) enemies.Add(EnemyScaling.Scaled(dungeon.bossMinions[0], level, tier, balance));
            enemies.Add(EnemyScaling.Scaled(dungeon.boss, level + 1, tier, balance));
            if (dungeon.bossMinions.Count > 1) enemies.Add(EnemyScaling.Scaled(dungeon.bossMinions[1], level, tier, balance));
            return EnemyScaling.Encounter(id, dungeon.boss.displayName, enemies, canFlee: false, isBoss: true);
        }

        /// <summary>Mímico escondido entre os baús, no nível da dungeon.</summary>
        public static EncounterDefinition MimicEncounter(GameDatabase db, int level, DifficultyTier tier, BalanceConfig balance, string id)
        {
            var mimic = db.Find<EnemyDefinition>("mimico");
            return mimic == null ? null : EnemyScaling.Encounter(id, "Mímico!", new[] { EnemyScaling.Scaled(mimic, level, tier, balance) }, canFlee: false);
        }

        /// <summary>Tesouro do chefe com o piso de raridade da dificuldade (Pesadelo garante Épico).</summary>
        public static LootTable BossChestTable(DungeonDefinition dungeon, DifficultyTier tier, BalanceConfig balance)
        {
            var table = Object.Instantiate(dungeon.bossChestLoot);
            table.guaranteedDrops.Add(balance.TierBossChestFloor(tier));
            return table;
        }
    }
}
