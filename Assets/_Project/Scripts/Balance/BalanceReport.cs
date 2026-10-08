using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;

namespace Oiram.Balance
{
    /// <summary>Resumo estatístico de muitas rotas jogadas por um perfil.</summary>
    public sealed class ProfileSummary
    {
        public SkillProfile Profile;
        public int Runs;
        public float ClearedRate;
        public float ClearedWithoutDefeatRate;
        public float AvgDefeats;
        public float AvgRests;
        public float AvgGold;
        public float AvgItems;
        public float AvgItemsPerBattle;
        public float AvgEpicOrBetter;
        public readonly Dictionary<Rarity, float> AvgByRarity = new();
        public float[] AvgLevelsAtBoss = Array.Empty<float>();
        public float AvgHpAtBoss;
        public float AvgEnergyAtBoss;
        public float AvgAbilitiesLearned;
        public readonly List<EncounterSummary> Encounters = new();

        public EncounterSummary Boss => Encounters.LastOrDefault(e => e.EncounterId == RouteSimulator.BossEncounter);
        public IEnumerable<EncounterSummary> Regular => Encounters.Where(e => e.EncounterId != RouteSimulator.BossEncounter);
    }

    public sealed class EncounterSummary
    {
        public string Label;
        public string EncounterId;
        public int Samples;
        public float FirstTryWinRate;
        public float AvgRounds;
        public float AvgHpEnd;
        public float AvgKnockOuts;
        public float AvgSkills;
        public float AvgItemsUsed;
        public float AvgDamageTaken;
    }

    public sealed class Target
    {
        public string Description;
        public Func<BalanceReport.Report, float> Measure;
        public float Min = float.MinValue;
        public float Max = float.MaxValue;
        public string Format = "0.##";

        public float Value { get; private set; }
        public bool Passed { get; private set; }

        public bool Evaluate(BalanceReport.Report data)
        {
            Value = Measure(data);
            Passed = Value >= Min && Value <= Max;
            return Passed;
        }

        public string Range =>
            Min > float.MinValue && Max < float.MaxValue ? $"{Fmt(Min)}–{Fmt(Max)}"
            : Min > float.MinValue ? $"≥ {Fmt(Min)}"
            : $"≤ {Fmt(Max)}";

        string Fmt(float v) => v.ToString(Format, CultureInfo.InvariantCulture);
        public string ValueText => Fmt(Value);
    }

    public sealed class TimingImpact
    {
        public string EncounterId;
        public string ProfileName;
        public int PartyLevel;
        public float WinRate;
        public float AvgRounds;
        public float AvgDamageTaken;
    }

    public static class BalanceReport
    {
        public static ProfileSummary Summarize(GameDatabase db, SkillProfile profile, int runs, int seedBase = 1000)
        {
            var results = Enumerable.Range(0, runs).Select(i => RouteSimulator.Run(db, profile, seedBase + i)).ToList();
            var summary = new ProfileSummary
            {
                Profile = profile,
                Runs = runs,
                ClearedRate = results.Count(r => r.Cleared) / (float)runs,
                ClearedWithoutDefeatRate = results.Count(r => r.ClearedWithoutDefeat) / (float)runs,
                AvgDefeats = (float)results.Average(r => r.Defeats),
                AvgRests = (float)results.Average(r => r.Rests),
                AvgGold = (float)results.Average(r => r.Gold),
                AvgItems = (float)results.Average(r => r.ItemsTotal),
                AvgAbilitiesLearned = (float)results.Average(r => r.AbilitiesLearned),
            };
            int battlesWon = results.Sum(r => r.Battles.Count(b => b.Result == BattleResult.Victory));
            summary.AvgItemsPerBattle = battlesWon == 0 ? 0 : results.Sum(r => r.Battles.Where(b => b.Victory != null).Sum(b => b.Victory.Items.Count)) / (float)battlesWon;
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                summary.AvgByRarity[rarity] = (float)results.Average(r => r.ItemsFound.TryGetValue(rarity, out int c) ? c : 0);
            summary.AvgEpicOrBetter = summary.AvgByRarity[Rarity.Epic] + summary.AvgByRarity[Rarity.Legendary];

            var reachedBoss = results.Where(r => r.LevelsAtBoss.Length > 0).ToList();
            if (reachedBoss.Count > 0)
            {
                int members = reachedBoss[0].LevelsAtBoss.Length;
                summary.AvgLevelsAtBoss = Enumerable.Range(0, members).Select(i => (float)reachedBoss.Average(r => r.LevelsAtBoss[i])).ToArray();
                summary.AvgHpAtBoss = (float)reachedBoss.Average(r => r.HpAtBossPercent);
                summary.AvgEnergyAtBoss = (float)reachedBoss.Average(r => r.EnergyAtBossPercent);
            }

            // Uma linha por batalha da rota (morcegos aparecem duas vezes).
            var battleSteps = RouteSimulator.ValeRoute.Where(s => s.Kind == StepKind.Battle).ToList();
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < battleSteps.Count; i++)
            {
                string id = battleSteps[i].Id;
                seen[id] = seen.TryGetValue(id, out int n) ? n + 1 : 1;
                var samples = results.Where(r => r.FirstAttempts.Count > i).Select(r => r.FirstAttempts[i]).ToList();
                if (samples.Count == 0) continue;
                var wins = samples.Where(s => s.Result == BattleResult.Victory).ToList();
                var name = db.Find<EncounterDefinition>(id)?.displayName ?? id;
                summary.Encounters.Add(new EncounterSummary
                {
                    Label = seen[id] > 1 ? $"{name} ({seen[id]}ª)" : name,
                    EncounterId = id,
                    Samples = samples.Count,
                    FirstTryWinRate = wins.Count / (float)samples.Count,
                    AvgRounds = wins.Count == 0 ? 0 : (float)wins.Average(s => s.Rounds),
                    AvgHpEnd = wins.Count == 0 ? 0 : (float)wins.Average(s => s.PartyHpPercentEnd),
                    AvgKnockOuts = (float)samples.Average(s => s.KnockOuts),
                    AvgSkills = (float)samples.Average(s => s.SkillsUsed),
                    AvgItemsUsed = (float)samples.Average(s => s.ItemsUsed),
                    AvgDamageTaken = (float)samples.Average(s => s.DamageTaken),
                });
            }
            return summary;
        }

        /// <summary>Mesmo encontro, mesma party, só muda o acerto dos timed hits.</summary>
        public static TimingImpact MeasureTiming(GameDatabase db, string encounterId, int partyLevel, SkillProfile profile, int battles, int seedBase = 5000)
        {
            int wins = 0;
            float rounds = 0, damage = 0;
            var encounter = db.Find<EncounterDefinition>(encounterId);
            for (int i = 0; i < battles; i++)
            {
                var session = new GameSession(db, new SeededRandom(seedBase + i));
                foreach (var m in session.Party)
                    while (m.Level < partyLevel) m.GainXp(m.XpToNext - m.Xp);
                PartyManager.Manage(session);
                session.RestoreAll();
                var stats = new BattleSimulator(session, profile).Run(encounter);
                if (stats.Result == BattleResult.Victory)
                {
                    wins++;
                    rounds += stats.Rounds;
                }
                damage += stats.DamageTaken;
            }
            return new TimingImpact
            {
                EncounterId = encounterId,
                ProfileName = profile.Name,
                PartyLevel = partyLevel,
                WinRate = wins / (float)battles,
                AvgRounds = wins == 0 ? 0 : rounds / wins,
                AvgDamageTaken = damage / battles,
            };
        }

        /// <summary>Metas de balanceamento da fatia vertical (ajuste aqui se o design mudar).</summary>
        public static List<Target> DefaultTargets() => new()
        {
            // Rota
            new Target { Description = "Médio termina a rota sem nenhuma derrota", Measure = r => r.Profiles["Médio"].ClearedWithoutDefeatRate, Min = 0.90f, Format = "0%" },
            new Target { Description = "Iniciante termina a rota (com novas tentativas)", Measure = r => r.Profiles["Iniciante"].ClearedRate, Min = 0.90f, Format = "0%" },
            new Target { Description = "Iniciante termina sem derrota (o desafio existe)", Measure = r => r.Profiles["Iniciante"].ClearedWithoutDefeatRate, Min = 0.40f, Max = 0.90f, Format = "0%" },
            new Target { Description = "Experiente termina sem derrota", Measure = r => r.Profiles["Experiente"].ClearedWithoutDefeatRate, Min = 0.97f, Format = "0%" },
            // Batalhas comuns
            new Target { Description = "Rodadas por batalha comum (Médio)", Measure = r => r.Profiles["Médio"].Regular.Average(e => e.AvgRounds), Min = 2f, Max = 4f, Format = "0.0" },
            new Target { Description = "PV da party ao fim de batalha comum (Médio)", Measure = r => r.Profiles["Médio"].Regular.Average(e => e.AvgHpEnd), Min = 0.55f, Max = 0.88f, Format = "0%" },
            new Target { Description = "Pior batalha comum na 1ª tentativa (Iniciante)", Measure = r => r.Profiles["Iniciante"].Regular.Min(e => e.FirstTryWinRate), Min = 0.95f, Format = "0%" },
            // Chefe: teste de habilidade
            new Target { Description = "Chefe vencido na 1ª tentativa (Iniciante)", Measure = r => r.Profiles["Iniciante"].Boss?.FirstTryWinRate ?? 0, Min = 0.55f, Max = 0.85f, Format = "0%" },
            new Target { Description = "Chefe vencido na 1ª tentativa (Médio)", Measure = r => r.Profiles["Médio"].Boss?.FirstTryWinRate ?? 0, Min = 0.90f, Format = "0%" },
            new Target { Description = "Nocautes da party no chefe (Médio) — tensão", Measure = r => r.Profiles["Médio"].Boss?.AvgKnockOuts ?? 0, Min = 0.3f, Max = 1.5f, Format = "0.00" },
            new Target { Description = "Rodadas contra o chefe (Médio)", Measure = r => r.Profiles["Médio"].Boss?.AvgRounds ?? 0, Min = 5f, Max = 10f, Format = "0.0" },
            new Target { Description = "Timed hits: chefe com tudo perfeito vs. sem acertos (rodadas)", Measure = r => TimingRatio(r, "enc_golem"), Max = 0.7f, Format = "0.00" },
            // Progressão e loot
            new Target { Description = "Nível médio da party no chefe (Médio)", Measure = r => r.Profiles["Médio"].AvgLevelsAtBoss.DefaultIfEmpty(0).Average(), Min = 3f, Max = 5f, Format = "0.0" },
            new Target { Description = "Itens por vitória (Médio)", Measure = r => r.Profiles["Médio"].AvgItemsPerBattle, Min = 1.5f, Max = 3f, Format = "0.0" },
            new Target { Description = "Épicos/Lendários por rota (Médio)", Measure = r => r.Profiles["Médio"].AvgEpicOrBetter, Min = 1f, Max = 4f, Format = "0.0" },
            new Target { Description = "Habilidades aprendidas na rota (party, Médio)", Measure = r => r.Profiles["Médio"].AvgAbilitiesLearned, Min = 6f, Format = "0.0" },
        };

        /// <summary>Rodadas com "Sempre perfeito" ÷ rodadas com "Nunca acerta" (quanto menor, mais pesam os timed hits).</summary>
        static float TimingRatio(Report r, string encounterId)
        {
            var never = r.Timing.FirstOrDefault(t => t.EncounterId == encounterId && t.ProfileName == SkillProfile.NeverHits.Name);
            var perfect = r.Timing.FirstOrDefault(t => t.EncounterId == encounterId && t.ProfileName == SkillProfile.AlwaysPerfect.Name);
            return never == null || perfect == null || never.AvgRounds <= 0 ? 1f : perfect.AvgRounds / never.AvgRounds;
        }

        public sealed class Report
        {
            public Dictionary<string, ProfileSummary> Profiles = new();
            public List<Target> Targets = new();
            public List<TimingImpact> Timing = new();
            public List<DungeonCell> Dungeons = new();
            public bool AllPassed => Targets.All(t => t.Passed);
        }

        public static Report Run(GameDatabase db, int runsPerProfile = 300, int dungeonRuns = 60)
        {
            var report = new Report();
            foreach (var profile in SkillProfile.Players)
                report.Profiles[profile.Name] = Summarize(db, profile, runsPerProfile);

            foreach (var (encounter, level) in new[] { ("enc_slimes3", 2), ("enc_golem", 4) })
                foreach (var profile in new[] { SkillProfile.NeverHits, SkillProfile.Average, SkillProfile.AlwaysPerfect })
                    report.Timing.Add(MeasureTiming(db, encounter, level, profile, 200));

            if (dungeonRuns > 0) report.Dungeons = DungeonReport.Run(db, dungeonRuns);

            report.Targets = DefaultTargets();
            if (dungeonRuns > 0) report.Targets.AddRange(DungeonReport.Targets());
            foreach (var target in report.Targets) target.Evaluate(report);
            return report;
        }

        static string Pct(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string F(float v, string format = "0.0") => v.ToString(format, CultureInfo.InvariantCulture);

        public static string ToMarkdown(Report report, GameDatabase db, string title = "Relatório de balanceamento")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"Gerado em {DateTime.Now:yyyy-MM-dd HH:mm} pelo simulador (`Scripts/Balance`). " +
                          $"{report.Profiles.Values.First().Runs} rotas do Vale por perfil" +
                          (report.Dungeons.Count > 0 ? $" e {report.Dungeons.First().Runs} descidas por combinação de dungeon" : "") +
                          ", mesmas regras do jogo.");
            sb.AppendLine();
            sb.AppendLine("Perfis de jogador (chance de acertar o tempo): " + string.Join(" · ", SkillProfile.Players.Select(p =>
                $"**{p.Name}** ataque {Pct(p.AttackPerfect)} perfeito/{Pct(p.AttackGood)} bom, defesa {Pct(p.BlockPerfect)}/{Pct(p.BlockGood)}")));
            sb.AppendLine();

            sb.AppendLine($"## Metas — {report.Targets.Count(t => t.Passed)}/{report.Targets.Count} atingidas");
            sb.AppendLine();
            sb.AppendLine("| | Meta | Faixa | Medido |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var t in report.Targets)
                sb.AppendLine($"| {(t.Passed ? "✅" : "❌")} | {t.Description} | {t.Range} | {t.ValueText} |");
            sb.AppendLine();

            sb.AppendLine("## Rota completa");
            sb.AppendLine();
            sb.AppendLine("| Perfil | Termina | Sem derrota | Derrotas | Descansos | Nível no chefe | PV/PE no chefe | Itens | Épico+ | Ouro | Habilidades |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var p in report.Profiles.Values)
                sb.AppendLine($"| {p.Profile.Name} | {Pct(p.ClearedRate)} | {Pct(p.ClearedWithoutDefeatRate)} | {F(p.AvgDefeats, "0.00")} | {F(p.AvgRests, "0.00")} | " +
                              $"{string.Join(" / ", p.AvgLevelsAtBoss.Select(l => F(l)))} | {Pct(p.AvgHpAtBoss)} / {Pct(p.AvgEnergyAtBoss)} | " +
                              $"{F(p.AvgItems)} | {F(p.AvgEpicOrBetter)} | {F(p.AvgGold, "0")} | {F(p.AvgAbilitiesLearned)} |");
            sb.AppendLine();
            sb.AppendLine("Nível no chefe = Oiram / Lia / Teo. PV/PE = quanto a party tem ao chegar no Golem.");
            sb.AppendLine();

            sb.AppendLine("## Loot por rota (média)");
            sb.AppendLine();
            sb.AppendLine("| Perfil | " + string.Join(" | ", Enum.GetValues(typeof(Rarity)).Cast<Rarity>().Select(RarityInfo.Name)) + " | Itens por vitória |");
            sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", Enum.GetValues(typeof(Rarity)).Length + 1)));
            foreach (var p in report.Profiles.Values)
                sb.AppendLine($"| {p.Profile.Name} | " + string.Join(" | ", p.AvgByRarity.OrderBy(kv => kv.Key).Select(kv => F(kv.Value))) + $" | {F(p.AvgItemsPerBattle)} |");
            sb.AppendLine();

            foreach (var p in report.Profiles.Values)
            {
                sb.AppendLine($"## Batalhas — {p.Profile.Name} (1ª tentativa)");
                sb.AppendLine();
                sb.AppendLine("| Encontro | Vitória | Rodadas | PV ao fim | Nocautes | Habilidades | Itens usados | Dano recebido |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|");
                foreach (var e in p.Encounters)
                    sb.AppendLine($"| {e.Label} | {Pct(e.FirstTryWinRate)} | {F(e.AvgRounds)} | {Pct(e.AvgHpEnd)} | {F(e.AvgKnockOuts, "0.00")} | {F(e.AvgSkills)} | {F(e.AvgItemsUsed)} | {F(e.AvgDamageTaken, "0")} |");
                sb.AppendLine();
            }

            DungeonReport.AppendMarkdown(sb, report.Dungeons);

            sb.AppendLine("## Peso dos timed hits");
            sb.AppendLine();
            sb.AppendLine("Mesma party (nível fixo, equipamento inicial), só muda o acerto do tempo.");
            sb.AppendLine();
            sb.AppendLine("| Encontro | Nível | Perfil | Vitória | Rodadas | Dano recebido |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var t in report.Timing)
                sb.AppendLine($"| {db.Find<EncounterDefinition>(t.EncounterId)?.displayName ?? t.EncounterId} | {t.PartyLevel} | {t.ProfileName} | {Pct(t.WinRate)} | {F(t.AvgRounds)} | {F(t.AvgDamageTaken, "0")} |");
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
