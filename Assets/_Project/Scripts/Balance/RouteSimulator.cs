using System.Collections.Generic;
using System.Linq;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;

namespace Oiram.Balance
{
    public enum StepKind
    {
        Battle,
        Loot,
        /// <summary>Voltar à fogueira (só se a party estiver abaixo do limite de PV).</summary>
        RestIfBelow,
    }

    public sealed class RouteStep
    {
        public StepKind Kind;
        public string Id;
        public int Level;
        public float Threshold;

        public static RouteStep Battle(string encounterId) => new() { Kind = StepKind.Battle, Id = encounterId };
        public static RouteStep Loot(string tableId, int level) => new() { Kind = StepKind.Loot, Id = tableId, Level = level };
        public static RouteStep Rest(float belowHpPercent) => new() { Kind = StepKind.RestIfBelow, Threshold = belowHpPercent };
    }

    public sealed class RouteResult
    {
        public int Seed;
        public bool Cleared;
        public bool ClearedWithoutDefeat;
        public int Defeats;
        public int Rests;
        public readonly List<BattleStats> Battles = new();
        public readonly List<BattleStats> FirstAttempts = new();
        public readonly Dictionary<Rarity, int> ItemsFound = new();
        public int ItemsTotal;
        public int Gold;
        public int[] LevelsAtBoss = new int[0];
        public float EnergyAtBossPercent;
        public float HpAtBossPercent;
        public int AbilitiesLearned;
    }

    /// <summary>
    /// Joga a rota inteira do Vale com um perfil de habilidade: batalhas em ordem, baús e caixas,
    /// equipa o melhor item, aprende habilidades com JP e tenta de novo após derrota (como no jogo).
    /// </summary>
    public static class RouteSimulator
    {
        public const string BossEncounter = "enc_golem";

        /// <summary>Ordem natural partindo da fogueira (veja Levels/Field_Vale.txt).</summary>
        public static readonly IReadOnlyList<RouteStep> ValeRoute = new[]
        {
            RouteStep.Loot("lt_caixa", 2),          // caixa perto do início
            RouteStep.Battle("enc_slimes2"),
            RouteStep.Battle("enc_morcegos"),
            RouteStep.Rest(0.6f),                   // a fogueira ainda está perto
            RouteStep.Loot("lt_bau", 2),
            RouteStep.Loot("lt_caixa", 2),
            RouteStep.Battle("enc_goblin_slime"),
            RouteStep.Battle("enc_mimico"),
            RouteStep.Battle("enc_slimes3"),
            RouteStep.Loot("lt_bau_raro", 3),
            RouteStep.Battle("enc_goblins"),
            RouteStep.Battle("enc_morcegos"),
            RouteStep.Battle(BossEncounter),
        };

        public const int MaxAttemptsPerBattle = 3;

        public static RouteResult Run(GameDatabase db, SkillProfile skill, int seed, IReadOnlyList<RouteStep> route = null)
        {
            route ??= ValeRoute;
            var session = new GameSession(db, new SeededRandom(seed));
            var simulator = new BattleSimulator(session, skill);
            var result = new RouteResult { Seed = seed, Cleared = true };

            PartyManager.Manage(session);
            foreach (var step in route)
            {
                switch (step.Kind)
                {
                    case StepKind.Loot:
                    {
                        var drop = session.RollLoot(db.Find<LootTable>(step.Id), step.Level);
                        foreach (var item in drop.Items) Count(result, item.Rarity);
                        result.Gold += drop.Gold;
                        session.Inventory.AddLoot(drop);
                        break;
                    }

                    case StepKind.RestIfBelow:
                        if (PartyHpPercent(session) < step.Threshold)
                        {
                            session.RestoreAll();
                            result.Rests++;
                        }
                        break;

                    case StepKind.Battle:
                    {
                        var encounter = db.Find<EncounterDefinition>(step.Id);
                        if (step.Id == BossEncounter)
                        {
                            result.LevelsAtBoss = session.Party.Select(m => m.Level).ToArray();
                            result.EnergyAtBossPercent = session.Energy / (float)session.MaxEnergy;
                            result.HpAtBossPercent = PartyHpPercent(session);
                        }

                        bool won = false;
                        for (int attempt = 0; attempt < MaxAttemptsPerBattle && !won; attempt++)
                        {
                            bool firstStrike = !encounter.isBoss && session.Rng.Chance(skill.FirstStrikeChance);
                            var stats = simulator.Run(encounter, firstStrike);
                            result.Battles.Add(stats);
                            if (attempt == 0) result.FirstAttempts.Add(stats);
                            won = stats.Result == BattleResult.Victory;
                            if (won)
                            {
                                foreach (var item in stats.Victory.Items) Count(result, item.Rarity);
                                result.Gold += stats.Victory.Gold;
                            }
                            else
                            {
                                // Como no jogo: acorda na fogueira com tudo recuperado e tenta de novo.
                                result.Defeats++;
                                session.RestoreAll();
                            }
                        }
                        if (!won) { result.Cleared = false; return Finish(result, session); }
                        PartyManager.Manage(session);
                        break;
                    }
                }
            }
            result.ClearedWithoutDefeat = result.Cleared && result.Defeats == 0;
            return Finish(result, session);
        }

        static RouteResult Finish(RouteResult result, GameSession session)
        {
            result.AbilitiesLearned = session.Party.Sum(m => m.ProgressFor(m.Job).LearnedInOrder().Count());
            return result;
        }

        static void Count(RouteResult result, Rarity rarity)
        {
            result.ItemsTotal++;
            result.ItemsFound[rarity] = result.ItemsFound.TryGetValue(rarity, out int c) ? c + 1 : 1;
        }

        public static float PartyHpPercent(GameSession session) =>
            session.Party.Sum(m => m.CurrentHp) / (float)session.Party.Sum(m => m.MaxHp);
    }

    /// <summary>O que um jogador faz no menu entre batalhas: aprende habilidades, equipa o melhor, põe a passiva.</summary>
    public static class PartyManager
    {
        public static void Manage(GameSession session)
        {
            foreach (var member in session.Party)
            {
                LearnAbilities(member);
                EquipBest(session, member);
            }
        }

        static void LearnAbilities(PartyMember member)
        {
            var progress = member.ProgressFor(member.Job);
            bool learned = true;
            while (learned)
            {
                learned = false;
                foreach (var ability in member.Job.abilities)
                {
                    if (progress.IsLearned(ability) || !progress.CanLearn(ability)) continue;
                    progress.TryLearn(ability);
                    learned = true;
                    break;
                }
            }
            if (member.SupportPassive == null)
            {
                var passive = member.LearnedPassives().FirstOrDefault();
                if (passive != null) member.SetSupportPassive(passive);
            }
        }

        static void EquipBest(GameSession session, PartyMember member)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                float current = Score(member, member.ComputeStats());
                ItemInstance best = null;
                float bestScore = current + 0.01f;
                foreach (var item in session.Inventory.Items)
                {
                    if (!member.CanEquip(item)) continue;
                    float score = Score(member, member.PreviewWith(item));
                    if (score > bestScore)
                    {
                        best = item;
                        bestScore = score;
                    }
                }
                if (best == null) return;
                session.Inventory.Equip(member, best);
            }
        }

        /// <summary>Valor de um conjunto de atributos para o job (multiplicadores do job viram pesos).</summary>
        public static float Score(PartyMember member, StatBlock s)
        {
            var m = member.Job.statMultipliers;
            float offense = m.magic > m.attack ? s[StatType.Magic] * 1.2f * m.magic : s[StatType.Attack] * 1.2f * m.attack;
            return offense
                   + s[StatType.MaxHP] * 0.25f * m.hp
                   + s[StatType.Defense] * 0.8f * m.defense
                   + s[StatType.Resistance] * 0.6f * m.resistance
                   + s[StatType.Speed] * 0.5f * m.speed
                   + s[StatType.CritChance] * 0.4f
                   + s[StatType.LifeSteal] * 0.4f
                   + s[StatType.TimingWindow] * 0.05f
                   + s[StatType.BurnOnPerfect] * 0.04f
                   + s[StatType.ReflectOnBlock] * 0.04f
                   + s[StatType.EnergyCostReduction] * 3f
                   + s[StatType.JPBonus] * 0.05f
                   + s[StatType.GoldFind] * 0.02f
                   + s[StatType.MagicFind] * 0.06f;
        }
    }
}
