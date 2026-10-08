using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Oiram.Balance;
using Oiram.Core;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>
    /// Lê os logs de playtest (PlaytestLog) e gera Docs/playtest-analise.md: precisão real dos timed hits,
    /// sugestão de calibração de latência, batalhas jogadas e uma previsão do simulador com o perfil medido.
    /// Batch: -executeMethod Oiram.EditorTools.PlaytestAnalyzer.RunBatch
    /// </summary>
    public static class PlaytestAnalyzer
    {
        const string DefaultReportPath = "Docs/playtest-analise.md";
        static string reportPath = DefaultReportPath;
        static readonly string[] AttackKinds = { "ataque", "multi", "magia", "area", "cura", "roubo" };
        const float AimedLimitMs = 250f;

        [MenuItem("OiramRPG/Balanceamento/Analisar logs de playtest")]
        static void RunFromMenu()
        {
            var text = Analyze(out int sessions);
            if (sessions == 0)
            {
                EditorUtility.DisplayDialog("Playtest", $"Nenhum log encontrado em\n{PlaytestLog.Folder}\n\nJogue no editor ou num development build primeiro.", "OK");
                return;
            }
            EditorUtility.RevealInFinder(reportPath);
        }

        /// <summary>Batch. Aceita <c>-playtest-folder &lt;pasta&gt;</c> e <c>-playtest-report &lt;arquivo.md&gt;</c>.</summary>
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            PlaytestLog.FolderOverride = Arg("-playtest-folder");
            reportPath = Arg("-playtest-report") ?? DefaultReportPath;
            try { Analyze(out _); }
            finally
            {
                PlaytestLog.FolderOverride = null;
                reportPath = DefaultReportPath;
            }
        }

        sealed class TimingStats
        {
            public string Name;
            public int Total, Pressed, Perfect, Good;
            public readonly List<float> Offsets = new();

            /// <summary>Apertos que miraram o impacto (descarta "apertar sem parar").</summary>
            public List<float> Aimed => Offsets.Where(o => Math.Abs(o) <= AimedLimitMs).ToList();

            public float Rate(int n) => Total == 0 ? 0 : n / (float)Total;
            public float Median => Percentile(0.5f);
            public float Mean => Offsets.Count == 0 ? 0 : Offsets.Average();
            public float StdDev => Offsets.Count < 2 ? 0 : (float)Math.Sqrt(Offsets.Sum(o => (o - Mean) * (o - Mean)) / (Offsets.Count - 1));

            public float Percentile(float p)
            {
                if (Offsets.Count == 0) return 0;
                var sorted = Offsets.OrderBy(o => o).ToList();
                return sorted[Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
            }
        }

        public static string Analyze(out int sessionCount)
        {
            var files = Directory.Exists(PlaytestLog.Folder)
                ? Directory.GetFiles(PlaytestLog.Folder, "*.jsonl").OrderBy(f => f).ToArray()
                : Array.Empty<string>();
            sessionCount = files.Length;

            var events = new List<PlaytestEvent>();
            foreach (var file in files)
                foreach (var line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { events.Add(JsonUtility.FromJson<PlaytestEvent>(line)); }
                    catch (Exception) { /* linha corrompida (jogo fechado no meio da escrita) */ }
                }

            var sb = new StringBuilder();
            sb.AppendLine("# Análise de playtest");
            sb.AppendLine();
            sb.AppendLine($"Gerado em {DateTime.Now:yyyy-MM-dd HH:mm} a partir de {files.Length} sessão(ões) em `{PlaytestLog.Folder}`.");
            sb.AppendLine();
            if (events.Count == 0)
            {
                sb.AppendLine("Nenhum evento encontrado. Jogue no editor ou num development build e rode de novo.");
                Write(sb);
                return sb.ToString();
            }

            var session = events.LastOrDefault(e => e.type == "sessao");
            if (session != null)
                sb.AppendLine($"Configuração na última sessão: Perfeito ±{session.perfectMs:0} ms, Bom ±{session.goodMs:0} ms, compensação de latência {session.latencyMs:0} ms. {session.detail}");
            sb.AppendLine();

            // ---------------- timed hits ----------------
            var timings = events.Where(e => e.type == "timing").ToList();
            var groups = new List<TimingStats>
            {
                Collect("Ataques (aperto único)", timings.Where(e => AttackKinds.Contains(e.kind))),
                Collect("Defesas", timings.Where(e => e.kind == "defesa")),
                Collect("Cargas (segurar e soltar)", timings.Where(e => e.kind == "carga")),
            };

            sb.AppendLine("## Timed hits");
            sb.AppendLine();
            sb.AppendLine("| Tipo | Janelas | Apertou | Perfeito | Bom | Errou | Desvio mediano | Média | Desvio padrão | P10–P90 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var g in groups.Where(g => g.Total > 0))
                sb.AppendLine($"| {g.Name} | {g.Total} | {Pct(g.Rate(g.Pressed))} | {Pct(g.Rate(g.Perfect))} | {Pct(g.Rate(g.Good))} | " +
                              $"{Pct(1 - g.Rate(g.Perfect) - g.Rate(g.Good))} | {Ms(g.Median)} | {Ms(g.Mean)} | {g.StdDev:0} ms | {Ms(g.Percentile(0.1f))} a {Ms(g.Percentile(0.9f))} |");
            sb.AppendLine();
            sb.AppendLine("Desvio = momento do aperto − centro da janela (negativo = cedo, positivo = tarde). O centro já inclui a compensação de latência.");
            sb.AppendLine();

            // Calibração: o desvio mediano dos ataques diz se a compensação de latência está boa.
            var attacks = groups[0];
            var aimed = attacks.Aimed.OrderBy(o => o).ToList();
            int wild = attacks.Offsets.Count - aimed.Count;
            sb.AppendLine("### Calibração sugerida");
            sb.AppendLine();
            if (wild > 0)
                sb.AppendLine($"{wild} de {attacks.Offsets.Count} apertos de ataque ficaram a mais de {AimedLimitMs:0} ms do impacto " +
                              "(apertando cedo demais ou sem parar); eles não entram na calibração.");
            sb.AppendLine();
            if (aimed.Count < 15)
            {
                sb.AppendLine($"Só {aimed.Count} apertos mirando o impacto; jogue mais algumas batalhas para uma sugestão confiável.");
            }
            else
            {
                float current = session?.latencyMs ?? 35f;
                float median = aimed[aimed.Count / 2];
                if (Mathf.Abs(median) <= 15f)
                    sb.AppendLine($"Desvio mediano de {Ms(median)}: a compensação atual ({current:0} ms) está boa.");
                else
                    sb.AppendLine($"Desvio mediano de {Ms(median)}: experimente `timingLatencyCompensation = " +
                                  ((current + median) / 1000f).ToString("0.000", CultureInfo.InvariantCulture) +
                                  "` (hoje " + (current / 1000f).ToString("0.000", CultureInfo.InvariantCulture) +
                                  ") em `Data/BalanceConfig`, para o centro da janela ficar onde você realmente aperta.");
                float p10 = aimed[Mathf.RoundToInt(0.1f * (aimed.Count - 1))];
                float p90 = aimed[Mathf.RoundToInt(0.9f * (aimed.Count - 1))];
                float spread = (p90 - p10) / 2f;
                float good = session?.goodMs ?? 150f;
                sb.AppendLine();
                sb.AppendLine(spread > good
                    ? $"A dispersão (±{spread:0} ms entre P10 e P90) é maior que a janela Bom (±{good:0} ms): os timed hits devem estar parecendo injustos — considere ampliar a janela Bom."
                    : $"A dispersão (±{spread:0} ms entre P10 e P90) cabe na janela Bom (±{good:0} ms).");
            }
            sb.AppendLine();

            // ---------------- batalhas ----------------
            var battles = events.Where(e => e.type == "batalha").ToList();
            if (battles.Count > 0)
            {
                sb.AppendLine("## Batalhas jogadas");
                sb.AppendLine();
                sb.AppendLine("| Encontro | Resultado | Preventivo | Rodadas | Dano causado | Dano recebido | Nocautes | PV ao fim | Itens (Raro+) | Níveis | Duração |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var b in battles)
                    sb.AppendLine($"| {b.encounter} | {b.result} | {(b.firstStrike ? "sim" : "")} | {b.rounds} | {b.damageDealt} | {b.damageTaken} | {b.knockOuts} | " +
                                  $"{Pct(b.partyHp)} | {b.items} ({b.itemsRarePlus}) | {b.levels} | {b.seconds:0}s |");
                sb.AppendLine();
                var wins = battles.Where(b => b.result == "Victory").ToList();
                sb.AppendLine($"{battles.Count} batalhas, {wins.Count} vitórias, {battles.Count(b => b.result == "Defeat")} derrotas, " +
                              $"{battles.Count(b => b.result == "Fled")} fugas. Tempo médio por batalha: {(battles.Count == 0 ? 0 : battles.Average(b => b.seconds)):0} s.");
                sb.AppendLine();
            }

            // ---------------- previsão com o perfil medido ----------------
            if (groups[0].Total >= 10)
            {
                var profile = new SkillProfile
                {
                    Name = "Você",
                    AttackPerfect = groups[0].Rate(groups[0].Perfect),
                    AttackGood = groups[0].Rate(groups[0].Good),
                    BlockPerfect = groups[1].Rate(groups[1].Perfect),
                    BlockGood = groups[1].Rate(groups[1].Good),
                    HoldPerfect = groups[2].Total > 0 ? groups[2].Rate(groups[2].Perfect) : groups[0].Rate(groups[0].Perfect) * 0.7f,
                    HoldGood = groups[2].Total > 0 ? groups[2].Rate(groups[2].Good) : groups[0].Rate(groups[0].Good),
                    FirstStrikeChance = battles.Count == 0 ? 0.2f : battles.Count(b => b.firstStrike) / (float)battles.Count,
                };
                var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentSeeder.DatabasePath) ?? DefaultContent.Build();
                var summary = BalanceReport.Summarize(db, profile, 300);
                sb.AppendLine("## Previsão do simulador com o seu perfil");
                sb.AppendLine();
                sb.AppendLine($"Perfil medido: ataque {Pct(profile.AttackPerfect)} perfeito / {Pct(profile.AttackGood)} bom; " +
                              $"defesa {Pct(profile.BlockPerfect)} / {Pct(profile.BlockGood)}; carga {Pct(profile.HoldPerfect)} / {Pct(profile.HoldGood)}.");
                sb.AppendLine();
                sb.AppendLine($"- Termina a rota sem derrota: **{Pct(summary.ClearedWithoutDefeatRate)}** (com novas tentativas: {Pct(summary.ClearedRate)})");
                if (summary.Boss != null)
                    sb.AppendLine($"- Golem na 1ª tentativa: **{Pct(summary.Boss.FirstTryWinRate)}**, {summary.Boss.AvgRounds:0.0} rodadas, {summary.Boss.AvgKnockOuts:0.00} nocautes");
                sb.AppendLine($"- Rodadas por batalha comum: {summary.Regular.Average(e => e.AvgRounds):0.0}");
                sb.AppendLine();
                sb.AppendLine("Compare com o que você sentiu: se a previsão diz \"fácil\" e foi difícil (ou o contrário), as decisões de menu pesam mais do que o simulador supõe.");
                sb.AppendLine();
            }

            // ---------------- outros eventos ----------------
            var notes = events.Where(e => e.type is "derrota" or "descanso" or "job" or "secundaria" or "aprendeu" or "equipou" or "vendeu").ToList();
            if (notes.Count > 0)
            {
                sb.AppendLine("## Linha do tempo (menus e mapa)");
                sb.AppendLine();
                foreach (var n in notes) sb.AppendLine($"- `{n.time}` **{n.type}** — {n.detail}");
                sb.AppendLine();
            }

            Write(sb);
            Debug.Log($"[PLAYTEST] {files.Length} sessão(ões), {timings.Count} timed hits, {battles.Count} batalhas -> {reportPath}");
            return sb.ToString();
        }

        static TimingStats Collect(string name, IEnumerable<PlaytestEvent> source)
        {
            var stats = new TimingStats { Name = name };
            foreach (var e in source)
            {
                stats.Total++;
                if (e.result == "Perfect") stats.Perfect++;
                else if (e.result == "Good") stats.Good++;
                if (!e.pressed) continue;
                stats.Pressed++;
                stats.Offsets.Add(e.offsetMs);
            }
            return stats;
        }

        static void Write(StringBuilder sb)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, sb.ToString());
        }

        static string Pct(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Ms(float v) => (v >= 0 ? "+" : "−") + Math.Abs(v).ToString("0", CultureInfo.InvariantCulture) + " ms";
    }
}
