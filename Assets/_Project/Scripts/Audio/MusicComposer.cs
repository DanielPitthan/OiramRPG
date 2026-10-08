using System;
using System.Collections.Generic;
using static Oiram.Audio.Synth;

namespace Oiram.Audio
{
    public enum MusicTrack
    {
        None,
        Title,
        Field,
        WorldMap,
        Town,
        Dungeon,
        Battle,
        Boss,
    }

    /// <summary>
    /// Compõe os loops de música (chiptune): progressão de acordes escrita à mão + melodia gerada com seed fixa
    /// em frases (A A' B A''), baixo, arpejo e bateria. Mesma seed = mesma música em todas as execuções.
    /// </summary>
    public static class MusicComposer
    {
        enum BassStyle { Roots, Bounce, Octaves, Waltz, Drone }
        enum ArpStyle { None, Up, Stabs }
        enum DrumStyle { None, Soft, Groove, Driving, Waltz, Sparse }

        sealed class Style
        {
            public int Bpm;
            public int Root;
            public bool Minor;
            public int[] Chords;
            public int StepsPerBar = 8;
            public Wave Lead = Wave.Square;
            public float LeadDuty = 0.5f;
            public float LeadVolume = 0.11f;
            public float RestChance = 0.08f;
            public BassStyle Bass;
            public float BassVolume = 0.22f;
            public ArpStyle Arp;
            public float ArpVolume = 0.045f;
            public DrumStyle Drums;
            public float Echo;
            public int Seed;
        }

        struct Note
        {
            public int Step;
            public int Length;
            public int? Degree;
        }

        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };

        static readonly int[][] Rhythms8 =
        {
            new[] { 2, 2, 2, 2 }, new[] { 3, 1, 2, 2 }, new[] { 2, 1, 1, 2, 2 }, new[] { 1, 1, 2, 4 },
            new[] { 4, 2, 2 }, new[] { 2, 2, 4 }, new[] { 3, 3, 2 }, new[] { 2, 2, 1, 1, 2 },
        };

        static readonly int[][] Cadence8 = { new[] { 2, 2, 4 }, new[] { 1, 1, 2, 4 }, new[] { 3, 1, 4 } };
        static readonly int[][] Cadence6 = { new[] { 2, 4 }, new[] { 1, 1, 4 } };

        static readonly int[][] Rhythms6 =
        {
            new[] { 2, 2, 2 }, new[] { 3, 1, 2 }, new[] { 4, 2 }, new[] { 2, 4 }, new[] { 2, 1, 1, 2 },
        };

        static Style StyleFor(MusicTrack track) => track switch
        {
            MusicTrack.Title => new Style
            {
                Bpm = 96, Root = 72, Chords = new[] { 0, 5, 3, 4, 0, 5, 1, 4 },
                Bass = BassStyle.Roots, Arp = ArpStyle.Up, Drums = DrumStyle.Soft, Echo = 0.25f, Seed = 11,
            },
            MusicTrack.Field => new Style
            {
                Bpm = 138, Root = 67, LeadDuty = 0.25f, Chords = new[] { 0, 3, 4, 0, 5, 3, 1, 4, 0, 3, 4, 0, 5, 1, 4, 4 },
                Bass = BassStyle.Bounce, Drums = DrumStyle.Groove, Seed = 23,
            },
            MusicTrack.WorldMap => new Style
            {
                Bpm = 116, Root = 65, Lead = Wave.Triangle, LeadVolume = 0.17f, Chords = new[] { 0, 4, 5, 3, 0, 4, 3, 4 },
                Bass = BassStyle.Roots, Arp = ArpStyle.Up, Drums = DrumStyle.Soft, Echo = 0.15f, Seed = 31,
            },
            MusicTrack.Town => new Style
            {
                Bpm = 168, StepsPerBar = 6, Root = 74, LeadVolume = 0.09f, Chords = new[] { 0, 3, 0, 4, 0, 3, 4, 0, 3, 0, 4, 0, 3, 1, 4, 0 },
                Bass = BassStyle.Waltz, Arp = ArpStyle.Stabs, ArpVolume = 0.04f, Drums = DrumStyle.Waltz, Seed = 47,
            },
            MusicTrack.Dungeon => new Style
            {
                Bpm = 76, Root = 69, Minor = true, Lead = Wave.Triangle, LeadVolume = 0.17f, RestChance = 0.3f,
                Chords = new[] { 0, 0, 5, 4, 0, 3, 5, 4 }, Bass = BassStyle.Drone, Arp = ArpStyle.Up, ArpVolume = 0.03f,
                Drums = DrumStyle.Sparse, Echo = 0.4f, Seed = 59,
            },
            MusicTrack.Battle => new Style
            {
                Bpm = 150, Root = 64, Minor = true, LeadDuty = 0.25f, LeadVolume = 0.1f,
                Chords = new[] { 0, 0, 5, 6, 0, 0, 3, 4, 5, 6, 0, 0, 3, 3, 4, 4 },
                Bass = BassStyle.Octaves, Drums = DrumStyle.Driving, Seed = 71,
            },
            MusicTrack.Boss => new Style
            {
                Bpm = 162, Root = 60, Minor = true, Lead = Wave.Saw, LeadVolume = 0.08f,
                Chords = new[] { 0, 1, 0, 4, 5, 1, 4, 4, 0, 1, 0, 4, 5, 6, 4, 4 },
                Bass = BassStyle.Octaves, Arp = ArpStyle.Up, ArpVolume = 0.04f, Drums = DrumStyle.Driving, Seed = 83,
            },
            _ => null,
        };

        public static float StepSeconds(MusicTrack track)
        {
            var style = StyleFor(track);
            return style == null ? 0f : 60f / style.Bpm / 2f;
        }

        /// <summary>Gera o loop completo (mono, <see cref="Synth.Rate"/> Hz). <see cref="MusicTrack.None"/> devolve vazio.</summary>
        public static float[] Compose(MusicTrack track)
        {
            var style = StyleFor(track);
            if (style == null) return Array.Empty<float>();

            float step = 60f / style.Bpm / 2f;
            int stepSamples = (int)(step * Rate);
            int bars = style.Chords.Length;
            int totalSteps = bars * style.StepsPerBar;
            var mix = new float[totalSteps * stepSamples];
            var rng = new Random(style.Seed);

            foreach (var note in Melody(style, rng))
            {
                if (note.Degree is not int degree) continue;
                int chord = style.Chords[note.Step / style.StepsPerBar];
                float f = Freq(Pitch(style, degree, chord));
                var tone = Tone(style.Lead, f, f, note.Length * step * 0.92f, style.LeadVolume, style.LeadDuty, decay: 0.6f, attack: 0.006f);
                AddWrapped(mix, tone, note.Step * stepSamples);
            }

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = style.Chords[bar];
                int barStart = bar * style.StepsPerBar * stepSamples;
                Bass(style, mix, chord, barStart, stepSamples, step);
                Arp(style, mix, chord, barStart, stepSamples, step);
                Drums(style, mix, bar, barStart, stepSamples);
            }

            if (style.Echo > 0f) Echo(mix, (int)(step * 3 * Rate), style.Echo);
            Normalize(mix, 0.85f);
            return Limit(mix);
        }

        // ---------- harmonia ----------

        static int Pitch(Style style, int degree, int chord)
        {
            var scale = style.Minor ? Minor : Major;
            int octave = (int)Math.Floor(degree / 7.0);
            int index = degree - octave * 7;
            int midi = style.Root + octave * 12 + scale[index];
            // Menor: a sensível sobe meio tom no acorde de dominante (V maior).
            if (style.Minor && chord == 4 && index == 6) midi += 1;
            return midi;
        }

        static int NearestChordTone(int degree, int chord, Random rng)
        {
            int best = degree, bestDistance = int.MaxValue;
            for (int candidate = degree - 4; candidate <= degree + 4; candidate++)
            {
                int rel = ((candidate - chord) % 7 + 7) % 7;
                if (rel != 0 && rel != 2 && rel != 4) continue;
                int distance = Math.Abs(candidate - degree) * 2 + rng.Next(2);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>A fundamental do acorde (em qualquer oitava) mais perto da nota atual: fim de frase.</summary>
        static int NearestRoot(int degree, int chord)
        {
            int k = (int)Math.Round((degree - chord) / 7.0);
            return chord + 7 * k;
        }

        // ---------- melodia em frases ----------

        static List<Note> Melody(Style style, Random rng)
        {
            int bars = style.Chords.Length;
            var perBar = new List<Note>[bars];
            int current = 2;
            for (int bar = 0; bar < bars; bar++)
            {
                int group = bar / 4, inGroup = bar % 4;
                int source = SourceBar(group, inGroup);
                bool last = bar == bars - 1;
                if (source >= 0 && source < bar && !last)
                {
                    int shift = style.Chords[bar] - style.Chords[source];
                    perBar[bar] = Transpose(perBar[source], bar - source, style.StepsPerBar, shift);
                    current = LastDegree(perBar[bar], current);
                    continue;
                }
                perBar[bar] = FreshBar(style, rng, bar, ref current, last || inGroup == 3);
            }
            var notes = new List<Note>();
            foreach (var bar in perBar) notes.AddRange(bar);
            return notes;
        }

        /// <summary>Forma A A' B A'': o grupo 1 repete o começo do 0, o 2 repete o 0 inteiro, o 3 repete o começo do 1.</summary>
        static int SourceBar(int group, int inGroup) => group switch
        {
            1 when inGroup < 2 => inGroup,
            2 => inGroup,
            3 when inGroup < 2 => 4 + inGroup,
            _ => -1,
        };

        static List<Note> Transpose(List<Note> source, int barOffset, int stepsPerBar, int shift)
        {
            var result = new List<Note>();
            foreach (var n in source)
                result.Add(new Note { Step = n.Step + barOffset * stepsPerBar, Length = n.Length, Degree = n.Degree + shift });
            return result;
        }

        static int LastDegree(List<Note> notes, int fallback)
        {
            for (int i = notes.Count - 1; i >= 0; i--)
                if (notes[i].Degree is int d) return d;
            return fallback;
        }

        static List<Note> FreshBar(Style style, Random rng, int bar, ref int current, bool cadence)
        {
            int chord = style.Chords[bar];
            var rhythms = style.StepsPerBar == 6 ? Rhythms6 : Rhythms8;
            var pool = cadence ? (style.StepsPerBar == 6 ? Cadence6 : Cadence8) : rhythms;
            var rhythm = pool[rng.Next(pool.Length)];

            var notes = new List<Note>();
            int step = bar * style.StepsPerBar;
            for (int i = 0; i < rhythm.Length; i++)
            {
                bool first = i == 0;
                bool final = i == rhythm.Length - 1;
                bool strong = (step % style.StepsPerBar) % (style.StepsPerBar == 6 ? 2 : 4) == 0;
                int next;
                if (cadence && final) next = NearestRoot(current, chord);
                else if (first) next = NearestChordTone(current + rng.Next(-2, 3), chord, rng);
                else
                {
                    int[] moves = { -2, -1, -1, 1, 1, 2, 0, 3, -3 };
                    next = current + moves[rng.Next(moves.Length)];
                    if (strong) next = NearestChordTone(next, chord, rng);
                }
                if (next > 9) next -= 7;
                if (next < -3) next += 7;

                bool rest = !first && !final && rng.NextDouble() < style.RestChance;
                notes.Add(new Note { Step = step, Length = rhythm[i], Degree = rest ? null : next });
                if (!rest) current = next;
                step += rhythm[i];
            }
            return notes;
        }

        // ---------- acompanhamento ----------

        static int BassRoot(Style style, int chord)
        {
            int midi = Pitch(style, chord, chord) - 24;
            while (midi > 55) midi -= 12;
            while (midi < 36) midi += 12;
            return midi;
        }

        static void Bass(Style style, float[] mix, int chord, int barStart, int stepSamples, float step)
        {
            int root = BassRoot(style, chord);
            (int step, int length, int interval)[] pattern = style.Bass switch
            {
                BassStyle.Bounce => new[] { (0, 1, 0), (2, 1, 7), (4, 1, 12), (6, 1, 7) },
                BassStyle.Octaves => new[] { (0, 1, 0), (1, 1, 12), (2, 1, 0), (3, 1, 12), (4, 1, 0), (5, 1, 12), (6, 1, 0), (7, 1, 12) },
                BassStyle.Waltz => new[] { (0, 2, 0) },
                BassStyle.Drone => new[] { (0, 8, 0) },
                _ => new[] { (0, 3, 0), (4, 2, 7), (6, 2, 0) },
            };
            foreach (var (s, length, interval) in pattern)
            {
                if (s >= style.StepsPerBar) continue;
                float f = Freq(root + interval);
                float seconds = length * step * (style.Bass == BassStyle.Drone ? 1f : 0.85f);
                var tone = Tone(Wave.Triangle, f, f, seconds, style.BassVolume, decay: style.Bass == BassStyle.Drone ? 0.4f : 0.9f);
                AddWrapped(mix, tone, barStart + s * stepSamples);
            }
        }

        static void Arp(Style style, float[] mix, int chord, int barStart, int stepSamples, float step)
        {
            if (style.Arp == ArpStyle.None) return;
            int[] tones = { 0, 2, 4, 7 };
            if (style.Arp == ArpStyle.Stabs)
            {
                for (int s = 2; s < style.StepsPerBar; s += 2)
                    foreach (int t in new[] { 0, 2, 4 })
                    {
                        float f = Freq(Pitch(style, chord + t, chord) - 12);
                        AddWrapped(mix, Tone(Wave.Square, f, f, step * 0.8f, style.ArpVolume, 0.125f, decay: 1.6f), barStart + s * stepSamples);
                    }
                return;
            }
            for (int s = 0; s < style.StepsPerBar; s++)
            {
                float f = Freq(Pitch(style, chord + tones[s % tones.Length], chord) - 12);
                AddWrapped(mix, Tone(Wave.Square, f, f, step * 0.85f, style.ArpVolume, 0.125f, decay: 2f), barStart + s * stepSamples);
            }
        }

        static float[] kick, snare, hat;

        static void Drums(Style style, float[] mix, int bar, int barStart, int stepSamples)
        {
            kick ??= Tone(Wave.Sine, 150f, 45f, 0.12f, 0.45f, decay: 2f);
            snare ??= Mix(Tone(Wave.Noise, 6000f, 3000f, 0.12f, 0.17f, decay: 3f), Tone(Wave.Triangle, 230f, 180f, 0.08f, 0.12f, decay: 2f));
            hat ??= Tone(Wave.Noise, 11000f, 11000f, 0.03f, 0.05f, decay: 4f);

            int[] kicks, snares, hats;
            switch (style.Drums)
            {
                case DrumStyle.Soft: kicks = new[] { 0 }; snares = Array.Empty<int>(); hats = new[] { 2, 6 }; break;
                case DrumStyle.Groove: kicks = new[] { 0, 3, 4 }; snares = new[] { 2, 6 }; hats = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }; break;
                case DrumStyle.Driving: kicks = new[] { 0, 3, 4, 7 }; snares = new[] { 2, 6 }; hats = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }; break;
                case DrumStyle.Waltz: kicks = new[] { 0 }; snares = Array.Empty<int>(); hats = new[] { 2, 4 }; break;
                case DrumStyle.Sparse: kicks = bar % 2 == 0 ? new[] { 0 } : Array.Empty<int>(); snares = Array.Empty<int>(); hats = new[] { 4 }; break;
                default: return;
            }
            foreach (int s in kicks) AddWrapped(mix, kick, barStart + s * stepSamples);
            foreach (int s in snares) AddWrapped(mix, snare, barStart + s * stepSamples);
            foreach (int s in hats) AddWrapped(mix, hat, barStart + s * stepSamples);
        }

        // ---------- mixagem ----------

        /// <summary>Soma com volta ao início: notas que passam do fim do loop continuam no começo (loop sem emenda).</summary>
        static void AddWrapped(float[] target, float[] source, int at)
        {
            int n = target.Length;
            if (n == 0) return;
            for (int i = 0; i < source.Length; i++) target[(at + i) % n] += source[i];
        }

        static void Echo(float[] data, int delay, float feedback)
        {
            int n = data.Length;
            if (delay <= 0 || delay >= n) return;
            var wet = new float[n];
            // Duas voltas para a cauda do eco atravessar o ponto do loop.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                    wet[i] = (data[(i - delay + n) % n] + wet[(i - delay + n) % n]) * feedback;
            for (int i = 0; i < n; i++) data[i] += wet[i];
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            foreach (float x in data) max = Math.Max(max, Math.Abs(x));
            if (max <= 0f) return;
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
