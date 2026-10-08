using System;
using System.Collections.Generic;
using System.Globalization;

namespace Oiram.Stats
{
    /// <summary>
    /// Todos os atributos numéricos do jogo. Os seis primeiros são os atributos principais;
    /// os demais são atributos secundários vindos de afixos, passivas e status.
    /// </summary>
    public enum StatType
    {
        MaxHP,
        Attack,
        Defense,
        Magic,
        Resistance,
        Speed,
        CritChance,          // %
        TimingWindow,        // ms extras na janela de timed hit
        LifeSteal,           // % do dano causado vira cura
        BurnOnPerfect,       // % de chance de Queimadura num acerto Perfeito
        ReflectOnBlock,      // % do dano refletido num bloqueio Perfeito
        EnergyCostReduction, // PE a menos por habilidade
        JPBonus,             // % de JP extra
        GoldFind,            // % de ouro extra
        MagicFind,           // % de achado mágico (raridade)
    }

    public enum ModifierKind
    {
        Flat,
        Percent,
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierKind kind;
        public float value;

        public StatModifier(StatType stat, float value, ModifierKind kind = ModifierKind.Flat)
        {
            this.stat = stat;
            this.value = value;
            this.kind = kind;
        }

        public StatModifier Scaled(float factor) => new StatModifier(stat, value * factor, kind);

        public override string ToString() => StatText.Describe(this);
    }

    /// <summary>Conjunto de valores indexado por <see cref="StatType"/>.</summary>
    public sealed class StatBlock
    {
        public static readonly int Count = Enum.GetValues(typeof(StatType)).Length;

        readonly float[] values = new float[Count];

        public float this[StatType stat]
        {
            get => values[(int)stat];
            set => values[(int)stat] = value;
        }

        public StatBlock Clone()
        {
            var copy = new StatBlock();
            Array.Copy(values, copy.values, Count);
            return copy;
        }

        /// <summary>Final = (base + Σflat) × (1 + Σ%/100).</summary>
        public static StatBlock Compose(StatBlock baseStats, IEnumerable<StatModifier> modifiers)
        {
            var flat = new float[Count];
            var percent = new float[Count];
            if (modifiers != null)
            {
                foreach (var mod in modifiers)
                {
                    if (mod.kind == ModifierKind.Flat) flat[(int)mod.stat] += mod.value;
                    else percent[(int)mod.stat] += mod.value;
                }
            }

            var result = new StatBlock();
            for (int i = 0; i < Count; i++)
            {
                float b = baseStats != null ? baseStats.values[i] : 0f;
                result.values[i] = (b + flat[i]) * (1f + percent[i] / 100f);
            }
            return result;
        }
    }

    public static class StatText
    {
        public static string ShortName(StatType stat) => stat switch
        {
            StatType.MaxHP => "PV",
            StatType.Attack => "ATQ",
            StatType.Defense => "DEF",
            StatType.Magic => "MAG",
            StatType.Resistance => "RES",
            StatType.Speed => "VEL",
            StatType.CritChance => "Crítico",
            StatType.TimingWindow => "Janela de timing",
            StatType.LifeSteal => "Roubo de vida",
            StatType.BurnOnPerfect => "Queimadura no Perfeito",
            StatType.ReflectOnBlock => "Reflexo no bloqueio",
            StatType.EnergyCostReduction => "Custo de PE",
            StatType.JPBonus => "JP",
            StatType.GoldFind => "Ouro encontrado",
            StatType.MagicFind => "Achado mágico",
            _ => stat.ToString(),
        };

        public static bool IsPrimary(StatType stat) => stat <= StatType.Speed;

        /// <summary>Texto amigável de um modificador, ex.: "+5 ATQ", "+12% MAG", "+30 ms Janela de timing".</summary>
        public static string Describe(StatModifier mod)
        {
            string sign = mod.value >= 0 ? "+" : "−";
            string abs = Number(Math.Abs(mod.value));
            if (mod.kind == ModifierKind.Percent) return $"{sign}{abs}% {ShortName(mod.stat)}";

            return mod.stat switch
            {
                StatType.TimingWindow => $"{sign}{abs} ms {ShortName(mod.stat)}",
                StatType.EnergyCostReduction => $"−{abs} {ShortName(mod.stat)}",
                StatType.CritChance or StatType.LifeSteal or StatType.BurnOnPerfect or StatType.ReflectOnBlock
                    or StatType.JPBonus or StatType.GoldFind or StatType.MagicFind => $"{sign}{abs}% {ShortName(mod.stat)}",
                _ => $"{sign}{abs} {ShortName(mod.stat)}",
            };
        }

        public static string Number(float v)
        {
            float rounded = (float)Math.Round(v);
            return Math.Abs(v - rounded) < 0.05f
                ? ((int)rounded).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
