using System;
using System.IO;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Uma linha do log de playtest (JSON Lines). Campos não usados ficam no valor padrão.</summary>
    [Serializable]
    public sealed class PlaytestEvent
    {
        public string type;
        public string time;
        public float t;

        // Timed hits
        public string kind;        // ataque, multi, carga, magia, area, cura, roubo, defesa
        public string actor;
        public string ability;
        public string result;      // Perfect, Good, Miss
        public bool pressed;
        public float offsetMs;     // aperto − impacto (negativo = cedo)
        public float perfectMs;
        public float goodMs;
        public float latencyMs;

        // Batalhas
        public string encounter;
        public bool firstStrike;
        public int rounds;
        public int damageDealt;
        public int damageTaken;
        public int knockOuts;
        public float partyHp;
        public int items;
        public int itemsRarePlus;
        public int gold;
        public string levels;
        public float seconds;

        // Mapa / geral
        public string detail;
    }

    /// <summary>
    /// Grava o que acontece numa sessão de playtest em
    /// <c>%USERPROFILE%/AppData/LocalLow/&lt;empresa&gt;/OiramRPG/playtest/sessao-*.jsonl</c>.
    /// Ativo no editor e em development builds; desligado em batchmode (testes automáticos).
    /// </summary>
    public static class PlaytestLog
    {
        static StreamWriter writer;
        static bool? enabledOverride;

        /// <summary>Pasta alternativa (tour automático, análises em lote). Null = pasta padrão.</summary>
        public static string FolderOverride { get; set; }

        public static string Folder => FolderOverride ?? Path.Combine(Application.persistentDataPath, "playtest");
        public static string CurrentFile { get; private set; }

        public static bool Enabled
        {
            get => enabledOverride ?? (Debug.isDebugBuild && !Application.isBatchMode);
            set => enabledOverride = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Close();
            enabledOverride = null;
            FolderOverride = null;
        }

        public static void Write(PlaytestEvent e)
        {
            if (!Enabled || e == null) return;
            try
            {
                EnsureOpen();
                e.time = DateTime.Now.ToString("s");
                e.t = Time.realtimeSinceStartup;
                writer.WriteLine(JsonUtility.ToJson(e));
                writer.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Playtest] Não consegui gravar o log: {ex.Message}");
                enabledOverride = false;
            }
        }

        public static void Note(string type, string detail) => Write(new PlaytestEvent { type = type, detail = detail });

        static void EnsureOpen()
        {
            if (writer != null) return;
            Directory.CreateDirectory(Folder);
            CurrentFile = Path.Combine(Folder, $"sessao-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
            writer = new StreamWriter(CurrentFile, append: true);
            Application.quitting += Close;

            var balance = GameDatabase.Load().balance;
            writer.WriteLine(JsonUtility.ToJson(new PlaytestEvent
            {
                type = "sessao",
                time = DateTime.Now.ToString("s"),
                perfectMs = balance.perfectWindow * 1000f,
                goodMs = balance.goodWindow * 1000f,
                latencyMs = balance.timingLatencyCompensation * 1000f,
                detail = $"{Application.productName} {Application.version} · {Application.platform} · {Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0}Hz",
            }));
        }

        static void Close()
        {
            Application.quitting -= Close;
            writer?.Dispose();
            writer = null;
        }
    }
}
