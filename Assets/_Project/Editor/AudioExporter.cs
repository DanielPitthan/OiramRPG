using System;
using System.IO;
using System.Text;
using Oiram.Audio;
using Oiram.Loot;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>
    /// Exporta todos os efeitos, jingles e músicas gerados por código como WAV (para ouvir fora do jogo)
    /// e escreve um resumo de volume (pico/RMS) de cada um. -executeMethod Oiram.EditorTools.AudioExporter.RunBatch
    /// </summary>
    public static class AudioExporter
    {
        const string Folder = "Builds/Audio";

        [MenuItem("OiramRPG/Áudio/Exportar WAVs (Builds/Audio)")]
        public static void Export()
        {
            Directory.CreateDirectory(Folder);
            var report = new StringBuilder("tipo;nome;segundos;pico;rms_db\n");
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx))) Write(report, "sfx", sfx.ToString(), SoundBank.Build(sfx));
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity))) Write(report, "loot", rarity.ToString(), SoundBank.Loot(rarity));
            foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
                if (track != MusicTrack.None) Write(report, "musica", track.ToString(), MusicComposer.Compose(track));
            File.WriteAllText(Path.Combine(Folder, "volumes.csv"), report.ToString());
            Debug.Log($"[OiramRPG] Áudio exportado em {Path.GetFullPath(Folder)}");
        }

        public static void RunBatch()
        {
            try
            {
                Export();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Write(StringBuilder report, string kind, string name, float[] samples)
        {
            WriteWav(Path.Combine(Folder, $"{kind}_{name}.wav"), samples);
            double sum = 0, peak = 0;
            foreach (float x in samples)
            {
                sum += x * x;
                peak = Math.Max(peak, Math.Abs(x));
            }
            double rms = Math.Sqrt(sum / Math.Max(1, samples.Length));
            report.AppendLine($"{kind};{name};{samples.Length / (double)Synth.Rate:0.00};{peak:0.000};{20 * Math.Log10(Math.Max(1e-6, rms)):0.0}");
        }

        static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Synth.Rate);
            w.Write(Synth.Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (float x in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * 32767f), -32768, 32767));
        }
    }
}
