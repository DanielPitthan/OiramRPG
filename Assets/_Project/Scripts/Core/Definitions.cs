using System;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Base de todo conteúdo em ScriptableObject: id estável (para save/lookup) + nome exibido.</summary>
    public abstract class Definition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        public override string ToString() => string.IsNullOrEmpty(displayName) ? id : displayName;
    }

    public enum TargetType
    {
        SingleEnemy,
        AllEnemies,
        SingleAlly,
        AllAllies,
        Self,
        SingleFallenAlly,
    }

    public enum TimedHitType
    {
        None,
        SinglePress,  // apertar no impacto
        HoldRelease,  // segurar até carregar e soltar no ponto
        MultiPress,   // uma janela por golpe
    }

    public enum StatusType
    {
        Burn,
        Stun,
        Taunt,
        AttackUp,
        DefenseUp,
        Defending,
    }

    /// <summary>Os seis atributos principais (base, crescimento por nível, multiplicadores de job...).</summary>
    [Serializable]
    public struct PrimaryStats
    {
        public float hp;
        public float attack;
        public float defense;
        public float magic;
        public float resistance;
        public float speed;

        public PrimaryStats(float hp, float attack, float defense, float magic, float resistance, float speed)
        {
            this.hp = hp;
            this.attack = attack;
            this.defense = defense;
            this.magic = magic;
            this.resistance = resistance;
            this.speed = speed;
        }

        public static PrimaryStats One => new PrimaryStats(1, 1, 1, 1, 1, 1);

        public float Get(StatType stat) => stat switch
        {
            StatType.MaxHP => hp,
            StatType.Attack => attack,
            StatType.Defense => defense,
            StatType.Magic => magic,
            StatType.Resistance => resistance,
            StatType.Speed => speed,
            _ => 0f,
        };

        public StatBlock ToBlock()
        {
            var block = new StatBlock();
            block[StatType.MaxHP] = hp;
            block[StatType.Attack] = attack;
            block[StatType.Defense] = defense;
            block[StatType.Magic] = magic;
            block[StatType.Resistance] = resistance;
            block[StatType.Speed] = speed;
            return block;
        }
    }
}
