using System;
using System.Collections.Generic;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Stats;

namespace Oiram.Battle
{
    public enum Side
    {
        Party,
        Enemies,
    }

    public sealed class StatusEffect
    {
        public StatusType Type;
        public int TurnsLeft;

        public StatusEffect(StatusType type, int turns)
        {
            Type = type;
            TurnsLeft = turns;
        }
    }

    /// <summary>Participante de uma batalha. Para a party, o PV é o mesmo do <see cref="PartyMember"/> (persiste entre batalhas).</summary>
    public sealed class BattleUnit
    {
        readonly StatBlock stats;
        readonly List<StatusEffect> statuses = new();
        readonly float buffPercent;
        int enemyHp;

        public string Name { get; }
        public Side Side { get; }
        public PartyMember Member { get; }
        public EnemyDefinition Enemy { get; }
        public int Level { get; }
        public int MaxHp { get; }
        public int Slot { get; set; }
        public bool HasBeenStolenFrom { get; set; }

        public IReadOnlyList<StatusEffect> Statuses => statuses;

        public BattleUnit(PartyMember member, float buffPercent)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            Side = Side.Party;
            Name = member.Name;
            Level = member.Level;
            stats = member.ComputeStats();
            MaxHp = member.MaxHp;
            this.buffPercent = buffPercent;
        }

        public BattleUnit(EnemyDefinition enemy, float buffPercent, string nameOverride = null)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Side = Side.Enemies;
            Name = nameOverride ?? enemy.displayName;
            Level = enemy.level;
            stats = enemy.stats.ToBlock();
            MaxHp = Math.Max(1, (int)Math.Round(enemy.stats.hp));
            enemyHp = MaxHp;
            this.buffPercent = buffPercent;
        }

        public int Hp
        {
            get => Member != null ? Member.CurrentHp : enemyHp;
            set
            {
                int clamped = Math.Max(0, Math.Min(MaxHp, value));
                if (Member != null) Member.CurrentHp = clamped;
                else enemyHp = clamped;
                if (clamped == 0) statuses.Clear();
            }
        }

        public bool IsAlive => Hp > 0;
        public float HpPercent => MaxHp > 0 ? (float)Hp / MaxHp : 0f;

        public float Stat(StatType stat)
        {
            float value = stats[stat];
            if (stat == StatType.Attack && Has(StatusType.AttackUp)) value *= 1f + buffPercent / 100f;
            if (stat == StatType.Defense && Has(StatusType.DefenseUp)) value *= 1f + buffPercent / 100f;
            if (stat == StatType.Resistance && Has(StatusType.DefenseUp)) value *= 1f + buffPercent / 100f;
            return value;
        }

        public bool Has(StatusType type)
        {
            foreach (var s in statuses)
                if (s.Type == type) return true;
            return false;
        }

        /// <summary>Aplica ou renova um status (mantém a maior duração).</summary>
        public void AddStatus(StatusType type, int turns)
        {
            if (!IsAlive) return;
            foreach (var s in statuses)
            {
                if (s.Type != type) continue;
                s.TurnsLeft = Math.Max(s.TurnsLeft, turns);
                return;
            }
            statuses.Add(new StatusEffect(type, turns));
        }

        public void RemoveStatus(StatusType type) => statuses.RemoveAll(s => s.Type == type);

        /// <summary>Decrementa durações no fim do turno da própria unidade.</summary>
        public void TickStatuses()
        {
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (statuses[i].Type == StatusType.Defending) continue;
                if (--statuses[i].TurnsLeft <= 0) statuses.RemoveAt(i);
            }
        }

        public override string ToString() => $"{Name} ({Hp}/{MaxHp})";
    }

    public static class StatusText
    {
        public static string Name(StatusType type) => type switch
        {
            StatusType.Burn => "Queimadura",
            StatusType.Stun => "Atordoado",
            StatusType.Taunt => "Provocando",
            StatusType.AttackUp => "ATQ+",
            StatusType.DefenseUp => "DEF+",
            StatusType.Defending => "Defendendo",
            _ => type.ToString(),
        };
    }
}
