using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Characters
{
    public enum AbilityKind
    {
        PhysicalAttack,
        MagicAttack,
        Heal,
        Revive,
        Buff,
        Taunt,
        Steal,
        Passive,
    }

    [CreateAssetMenu(menuName = "OiramRPG/Ability", fileName = "Ability")]
    public sealed class AbilityDefinition : Definition
    {
        public AbilityKind kind;
        public TargetType target;
        [Tooltip("Multiplicador de ATQ/MAG (ou da cura).")]
        public float power = 1f;
        [Min(1)] public int hits = 1;
        [Tooltip("Custo no pool de PE compartilhado da party.")]
        public int energyCost;
        [Tooltip("JP para aprender (0 = não aprendível / já vem aprendida).")]
        public int jpCost;
        public TimedHitType timing = TimedHitType.SinglePress;

        [Header("Status")]
        public bool appliesStatus;
        public StatusType status;
        [Range(0f, 1f)] public float statusChance = 1f;
        public int statusTurns = 3;

        [Header("Passiva")]
        public List<StatModifier> passiveModifiers = new();

        public bool IsPassive => kind == AbilityKind.Passive;
        public bool TargetsEnemies => target is TargetType.SingleEnemy or TargetType.AllEnemies;
        public bool TargetsAll => target is TargetType.AllEnemies or TargetType.AllAllies;
    }
}
