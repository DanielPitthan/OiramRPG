using System;
using System.Collections.Generic;

namespace Oiram.Audio
{
    /// <summary>
    /// Sintetizador mínimo (estilo chiptune) que gera amostras em memória — o jogo não precisa de arquivos de áudio.
    /// Tudo aqui é C# puro (testável); <see cref="AudioManager"/> converte em AudioClip.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        public enum Wave
        {
            Square,
            Triangle,
            Sine,
            Saw,
            Noise,
        }

        public static float Freq(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

        /// <summary>
        /// Um som com varredura de frequência (f0 → f1), ataque curto e decaimento (decay = expoente da queda).
        /// </summary>
        public static float[] Tone(Wave wave, float f0, float f1, float seconds, float volume, float duty = 0.5f,
            float decay = 1.5f, float attack = 0.004f, int seed = 7)
        {
            int n = Math.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            var rng = new Random(seed);
            double phase = 0;
            float noise = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float t = i / (float)Rate;
                float f = f0 + (f1 - f0) * k;
                double prev = phase;
                phase += f / Rate;
                phase -= Math.Floor(phase);
                float s;
                switch (wave)
                {
                    case Wave.Square: s = phase < duty ? 1f : -1f; break;
                    case Wave.Triangle: s = (float)(4.0 * Math.Abs(phase - 0.5) - 1.0); break;
                    case Wave.Sine: s = (float)Math.Sin(2.0 * Math.PI * phase); break;
                    case Wave.Saw: s = (float)(2.0 * phase - 1.0); break;
                    default:
                        if (phase < prev || i == 0) noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                        s = noise;
                        break;
                }
                float env = t < attack ? t / attack : (float)Math.Pow(1.0 - k, decay);
                data[i] = s * env * volume;
            }
            return data;
        }

        public static float[] Silence(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

        public static float[] Concat(params float[][] parts)
        {
            int total = 0;
            foreach (var p in parts) total += p.Length;
            var data = new float[total];
            int offset = 0;
            foreach (var p in parts)
            {
                Array.Copy(p, 0, data, offset, p.Length);
                offset += p.Length;
            }
            return data;
        }

        /// <summary>Soma camadas alinhadas no início (a mais longa define a duração).</summary>
        public static float[] Mix(params float[][] layers)
        {
            int n = 0;
            foreach (var l in layers) n = Math.Max(n, l.Length);
            var data = new float[n];
            foreach (var l in layers)
                for (int i = 0; i < l.Length; i++)
                    data[i] += l[i];
            return data;
        }

        public static float[] Delay(float[] data, float seconds) => Concat(Silence(seconds), data);

        /// <summary>Sequência de notas MIDI com a mesma duração.</summary>
        public static float[] Notes(Wave wave, float noteSeconds, float volume, params int[] midis)
        {
            var parts = new List<float[]>();
            foreach (int m in midis)
                parts.Add(m <= 0 ? Silence(noteSeconds) : Tone(wave, Freq(m), Freq(m), noteSeconds, volume, 0.5f, 1.2f));
            return Concat(parts.ToArray());
        }

        /// <summary>Garante amostras em [-1, 1] (soft clip suave para mixagens altas).</summary>
        public static float[] Limit(float[] data, float ceiling = 0.95f)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                data[i] = (float)Math.Tanh(x / ceiling) * ceiling;
            }
            return data;
        }

        /// <summary>Adiciona <paramref name="source"/> em <paramref name="target"/> a partir de <paramref name="at"/> amostras.</summary>
        public static void AddAt(float[] target, float[] source, int at)
        {
            for (int i = 0; i < source.Length && at + i < target.Length; i++)
                if (at + i >= 0) target[at + i] += source[i];
        }
    }
}
