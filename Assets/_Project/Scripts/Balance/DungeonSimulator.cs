using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using Oiram.World;

namespace Oiram.Balance
{
    public sealed class DungeonRunResult
    {
        public bool Cleared;
        public int FloorReached;
        public int Battles;
        public int LevelsGained;
        public int Gold;
        public int Items;
        public int EpicOrBetter;
        public int PotionsUsed;
        public readonly List<BattleStats> Regular = new();
        public BattleStats Boss;
    }

    /// <summary>
    /// Joga uma descida inteira como no jogo: mesmo gerador de andares, mesma população escalada, PV/PE
    /// carregando entre batalhas, fogueira antes do chefe, baús/caixas/mímicos e equipamento entre andares.
    /// </summary>
    public static class DungeonSimulator
    {
        /// <summary>Party "típica" daquele nível: equipamento Incomum/Raro um nível abaixo.</summary>
        public static GameSession TypicalParty(GameDatabase db, int level, int seed)
        {
            var session = new GameSession(db, new SeededRandom(seed));
            foreach (var m in session.Party)
                while (m.Level < level) m.GainXp(m.XpToNext - m.Xp);
            int itemLevel = Math.Max(1, level - 1);
            foreach (var slot in new[] { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Accessory })
            {
                var pool = db.items.Where(b => b.slot == slot && b.minItemLevel <= itemLevel).ToList();
                for (int i = 0; i < 4; i++)
                    session.Inventory.TryAdd(session.Loot.CreateRandom(itemLevel, 40f, Rarity.Uncommon, pool));
            }
            // JP de quem já jogou até aqui: aprende o que der no job inicial.
            foreach (var m in session.Party) m.GainJp(40 * level);
            PartyManager.Manage(session);
            session.RestoreAll();
            return session;
        }

        public static DungeonRunResult Run(GameDatabase db, LocationDefinition location, DifficultyTier tier, SkillProfile skill,
            int partyLevel, int seed)
        {
            var session = TypicalParty(db, partyLevel, seed);
            var balance = db.balance;
            var dungeon = location.dungeon;
            int level = EnemyScaling.DungeonLevel(session.PartyLevel, dungeon.baseLevel, tier, balance);
            var run = new DungeonRun(location, tier, level, seed, balance.TierFloors(tier));
            session.ActiveRun = run;
            var simulator = new BattleSimulator(session, skill);
            var result = new DungeonRunResult();
            int levelsBefore = session.Party.Sum(m => m.Level);
            int potionsBefore = session.Inventory.Consumables().Sum(c => c.count);
            int goldBefore = session.Inventory.Gold;

            void TakeLoot(LootDrop drop)
            {
                result.Items += drop.Items.Count;
                result.EpicOrBetter += drop.Items.Count(i => i.Rarity >= Rarity.Epic);
                session.Inventory.AddLoot(drop);
            }

            bool Fight(EncounterDefinition encounter, bool firstStrike, out BattleStats stats)
            {
                stats = simulator.Run(encounter, firstStrike);
                result.Battles++;
                if (stats.Victory != null)
                {
                    result.Items += stats.Victory.Items.Count;
                    result.EpicOrBetter += stats.Victory.Items.Count(i => i.Rarity >= Rarity.Epic);
                }
                return stats.Result == BattleResult.Victory;
            }

            for (run.Floor = 0; run.Floor < run.Floors; run.Floor++)
            {
                result.FloorReached = run.Floor;
                var layout = DungeonGenerator.Generate(run.FloorSeed, dungeon.minRooms, dungeon.maxRooms, run.IsLastFloor, run.Floor == 0);
                var rng = new SeededRandom(unchecked(run.FloorSeed ^ 0x5bd1e995));
                foreach (var (row, col, c) in layout.Map.Objects())
                {
                    string id = $"{run.ObjectPrefix}_{row}_{col}";
                    switch (c)
                    {
                        case 'e':
                        {
                            var encounter = DungeonPopulation.RollEncounter(dungeon, run.Level, tier, balance, rng, id);
                            if (!Fight(encounter, session.Rng.Chance(skill.FirstStrikeChance), out var stats)) return Finish(result, session, levelsBefore, potionsBefore, goldBefore);
                            result.Regular.Add(stats);
                            break;
                        }
                        case 'C':
                        {
                            var mimic = rng.Chance(0.1f) ? DungeonPopulation.MimicEncounter(db, run.Level, tier, balance, id) : null;
                            if (mimic != null)
                            {
                                if (!Fight(mimic, false, out _)) return Finish(result, session, levelsBefore, potionsBefore, goldBefore);
                            }
                            else TakeLoot(session.RollLoot(dungeon.chestLoot, run.Level));
                            break;
                        }
                        case 'K':
                            TakeLoot(session.RollLoot(db.Find<LootTable>("lt_caixa"), run.Level));
                            break;
                        case 'F':
                            session.RestoreAll();
                            break;
                    }
                }

                if (run.IsLastFloor)
                {
                    var boss = DungeonPopulation.BossEncounter(dungeon, run.Level, tier, balance, $"{run.ObjectPrefix}_boss");
                    bool won = Fight(boss, false, out var bossStats);
                    result.Boss = bossStats;
                    if (!won) return Finish(result, session, levelsBefore, potionsBefore, goldBefore);
                    TakeLoot(session.RollLoot(DungeonPopulation.BossChestTable(dungeon, tier, balance), run.Level + 1));
                    result.Cleared = true;
                }
                PartyManager.Manage(session);
            }
            return Finish(result, session, levelsBefore, potionsBefore, goldBefore);
        }

        static DungeonRunResult Finish(DungeonRunResult result, GameSession session, int levelsBefore, int potionsBefore, int goldBefore)
        {
            result.LevelsGained = session.Party.Sum(m => m.Level) - levelsBefore;
            result.PotionsUsed = Math.Max(0, potionsBefore - session.Inventory.Consumables().Sum(c => c.count));
            result.Gold = session.Inventory.Gold - goldBefore;
            session.ActiveRun = null;
            return result;
        }
    }
}
