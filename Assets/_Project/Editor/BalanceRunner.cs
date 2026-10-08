using System;
using System.IO;
using System.Linq;
using Oiram.Balance;
using Oiram.Core;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>
    /// Roda o simulador de balanceamento e grava Docs/balanceamento.md.
    /// Batch: -executeMethod Oiram.EditorTools.BalanceRunner.RunBatch [-balance-runs 300]
    /// (no modo batch, sincroniza antes os assets com DefaultContent.cs).
    /// </summary>
    public static class BalanceRunner
    {
        const string ReportPath = "Docs/balanceamento.md";

        [MenuItem("OiramRPG/Balanceamento/Simular rota e gerar relatório")]
        static void RunFromMenu()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentSeeder.DatabasePath) ?? DefaultContent.Build();
            var report = Generate(db, 300);
            EditorUtility.DisplayDialog("Balanceamento",
                $"{report.Targets.Count(t => t.Passed)}/{report.Targets.Count} metas atingidas.\nRelatório em {ReportPath}.", "OK");
            EditorUtility.RevealInFinder(ReportPath);
        }

        public static void RunBatch()
        {
            int runs = 300;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-balance-runs");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed)) runs = parsed;

            var db = ContentSeeder.Sync();
            Generate(db, runs);
        }

        static BalanceReport.Report Generate(GameDatabase db, int runs)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var report = BalanceReport.Run(db, runs);
            var markdown = BalanceReport.ToMarkdown(report, db);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
            File.WriteAllText(ReportPath, markdown);
            foreach (var t in report.Targets)
                Debug.Log($"[BALANCE] {(t.Passed ? "OK  " : "FAIL")} {t.Description}: {t.ValueText} (meta {t.Range})");
            Debug.Log($"[BALANCE] {report.Targets.Count(t => t.Passed)}/{report.Targets.Count} metas em {watch.Elapsed.TotalSeconds:0.0}s -> {ReportPath}");
            return report;
        }
    }
}
