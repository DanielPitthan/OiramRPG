using System.Collections.Generic;
using System.Linq;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;

namespace Oiram.Battle
{
    public sealed class VictorySummary
    {
        public int Xp;
        public int Jp;
        public int Gold;
        public readonly List<string> Notes = new();
        public readonly List<ItemInstance> Items = new();
        public readonly List<(ConsumableDefinition item, int count)> Consumables = new();
        public int LostItems;
        public int LevelUps;
    }

    public enum StealResult
    {
        Stolen,
        NothingToSteal,
        InventoryFull,
    }

    /// <summary>
    /// Efeitos de batalha sem apresentação (loot de inimigo, roubo, consumíveis, recompensas da vitória).
    /// Usado pelo <see cref="BattleManager"/> e pelo simulador de balanceamento — mesma regra nos dois.
    /// </summary>
    public static class BattleEffects
    {
        public static LootDrop RollEnemyLoot(GameSession session, BattleUnit enemy) =>
            enemy.Side == Side.Enemies && enemy.Enemy?.lootTable != null
                ? session.RollLoot(enemy.Enemy.lootTable, enemy.Level)
                : new LootDrop();

        /// <summary>Roubar: 1× por inimigo; acertar o tempo melhora a raridade mínima.</summary>
        public static StealResult Steal(GameSession session, BattleUnit target, TimedHitResult timing, out ItemInstance item)
        {
            item = null;
            var table = target.Enemy?.lootTable;
            if (target.HasBeenStolenFrom || table == null) return StealResult.NothingToSteal;
            target.HasBeenStolenFrom = true;

            var floor = timing switch
            {
                TimedHitResult.Perfect => Rarity.Rare,
                TimedHitResult.Good => Rarity.Uncommon,
                _ => Rarity.Common,
            };
            if (table.rarityFloor > floor) floor = table.rarityFloor;
            item = session.Loot.CreateRandom(session.Loot.RollItemLevel(target.Level, table.itemLevelBonus), session.MagicFind, floor, table.itemPool);
            if (session.Inventory.TryAdd(item)) return StealResult.Stolen;
            item = null;
            return StealResult.InventoryFull;
        }

        /// <summary>Aplica um consumível num alvo (PE é da party inteira; o alvo é ignorado).</summary>
        public static HitOutcome ApplyItem(GameSession session, BattleRules rules, ConsumableDefinition item, BattleUnit target)
        {
            switch (item.effect)
            {
                case ConsumableEffect.RestoreEnergy:
                    int before = session.Energy;
                    session.RestoreEnergy(item.amount);
                    return new HitOutcome { Target = target, EnergyRestored = session.Energy - before };
                case ConsumableEffect.HealHP:
                {
                    if (target == null || !target.IsAlive) return new HitOutcome { Target = target };
                    int hp = target.Hp;
                    target.Hp += item.amount;
                    return new HitOutcome { Target = target, Healed = target.Hp - hp };
                }
                case ConsumableEffect.Revive:
                    return target == null ? default : rules.Revive(target, item.amount / 100f);
                default:
                    return new HitOutcome { Target = target };
            }
        }

        /// <summary>
        /// Recompensas da vitória: mínimo de 1 item, loot bônus do encontro, XP/JP para quem está de pé
        /// (nocauteados levantam com 1 PV, sem XP), loot na mochila e PE de volta.
        /// </summary>
        public static VictorySummary GrantVictory(GameSession session, EncounterDefinition encounter, LootDrop loot,
            IEnumerable<BattleUnit> party, IEnumerable<ItemInstance> stolen)
        {
            if (loot.Items.Count == 0)
                loot.Items.Add(session.Loot.CreateRandom(session.Loot.RollItemLevel(encounter.Level), session.MagicFind));
            if (encounter.bonusLoot != null)
                loot.Merge(session.RollLoot(encounter.bonusLoot, encounter.Level));

            var summary = new VictorySummary
            {
                Xp = encounter.enemies.Sum(e => e.xp),
                Jp = encounter.enemies.Sum(e => e.jp),
                Gold = loot.Gold,
            };

            foreach (var unit in party)
            {
                var member = unit.Member;
                if (!unit.IsAlive)
                {
                    member.CurrentHp = 1;
                    summary.Notes.Add($"{member.Name} se levanta com 1 PV (sem XP).");
                    continue;
                }
                int jobLevelBefore = member.JobLevel(member.Job);
                int levels = member.GainXp(summary.Xp);
                member.GainJp(summary.Jp);
                summary.LevelUps += levels;
                if (levels > 0) summary.Notes.Add($"{member.Name} subiu para o Nv {member.Level}!");
                int jobLevelAfter = member.JobLevel(member.Job);
                if (jobLevelAfter > jobLevelBefore) summary.Notes.Add($"{member.Name}: {member.Job.displayName} Nv {jobLevelAfter}!");
            }

            summary.Items.AddRange(loot.Items);
            if (stolen != null) summary.Items.AddRange(stolen);
            foreach (var group in loot.Consumables.GroupBy(c => c))
                summary.Consumables.Add((group.Key, group.Count()));
            summary.LostItems = session.Inventory.AddLoot(loot).Count;
            session.RestoreEnergy(session.Balance.energyRestoredAfterBattle);
            return summary;
        }
    }
}
