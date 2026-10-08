using System;
using System.Collections.Generic;

namespace Oiram.Core
{
    /// <summary>Fonte de aleatoriedade injetável, para que as regras sejam determinísticas nos testes.</summary>
    public interface IRandom
    {
        /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Float em [0, 1).</summary>
        float Value();
    }

    public sealed class SeededRandom : IRandom
    {
        readonly System.Random random;

        public SeededRandom(int seed) => random = new System.Random(seed);
        public SeededRandom() => random = new System.Random();

        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
        public float Value() => (float)random.NextDouble();
    }

    public static class RandomExtensions
    {
        public static float Range(this IRandom rng, float min, float max) => min + (max - min) * rng.Value();

        /// <summary>Inteiro em [min, max] (ambos inclusivos).</summary>
        public static int RangeInclusive(this IRandom rng, int min, int max) => rng.Range(min, max + 1);

        public static bool Chance(this IRandom rng, float probability01) => rng.Value() < probability01;

        public static T Pick<T>(this IRandom rng, IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Lista vazia.", nameof(items));
            return items[rng.Range(0, items.Count)];
        }

        /// <summary>Escolhe um item proporcionalmente ao peso. Retorna default se nenhum peso for positivo.</summary>
        public static T PickWeighted<T>(this IRandom rng, IReadOnlyList<T> items, Func<T, float> weight)
        {
            float total = 0f;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0f, weight(items[i]));
            if (total <= 0f) return default;

            float roll = rng.Value() * total;
            for (int i = 0; i < items.Count; i++)
            {
                float w = Math.Max(0f, weight(items[i]));
                if (w <= 0f) continue;
                if (roll < w) return items[i];
                roll -= w;
            }
            // Erro de arredondamento: devolve o último com peso positivo.
            for (int i = items.Count - 1; i >= 0; i--)
                if (weight(items[i]) > 0f) return items[i];
            return default;
        }

        public static void Shuffle<T>(this IRandom rng, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
