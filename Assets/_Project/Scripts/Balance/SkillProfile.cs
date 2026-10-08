using Oiram.Battle;
using Oiram.Core;

namespace Oiram.Balance
{
    /// <summary>
    /// Habilidade de um jogador simulado com os timed hits: probabilidades de Perfeito/Bom
    /// em ataques (aperto único), cargas (segurar e soltar) e defesas.
    /// </summary>
    public sealed class SkillProfile
    {
        public string Name;
        public float AttackPerfect, AttackGood;
        public float HoldPerfect, HoldGood;
        public float BlockPerfect, BlockGood;
        /// <summary>Chance de começar a luta com ataque preventivo (pisando no inimigo).</summary>
        public float FirstStrikeChance;

        public TimedHitResult RollAttack(IRandom rng) => Roll(rng, AttackPerfect, AttackGood);
        public TimedHitResult RollHold(IRandom rng) => Roll(rng, HoldPerfect, HoldGood);
        public TimedHitResult RollBlock(IRandom rng) => Roll(rng, BlockPerfect, BlockGood);

        static TimedHitResult Roll(IRandom rng, float perfect, float good)
        {
            float v = rng.Value();
            if (v < perfect) return TimedHitResult.Perfect;
            if (v < perfect + good) return TimedHitResult.Good;
            return TimedHitResult.Miss;
        }

        public static readonly SkillProfile Novice = new()
        {
            Name = "Iniciante",
            AttackPerfect = 0.05f, AttackGood = 0.25f,
            HoldPerfect = 0.03f, HoldGood = 0.20f,
            BlockPerfect = 0.03f, BlockGood = 0.15f,
            FirstStrikeChance = 0.05f,
        };

        public static readonly SkillProfile Average = new()
        {
            Name = "Médio",
            AttackPerfect = 0.20f, AttackGood = 0.45f,
            HoldPerfect = 0.12f, HoldGood = 0.40f,
            BlockPerfect = 0.10f, BlockGood = 0.35f,
            FirstStrikeChance = 0.25f,
        };

        public static readonly SkillProfile Expert = new()
        {
            Name = "Experiente",
            AttackPerfect = 0.60f, AttackGood = 0.30f,
            HoldPerfect = 0.45f, HoldGood = 0.40f,
            BlockPerfect = 0.40f, BlockGood = 0.40f,
            FirstStrikeChance = 0.50f,
        };

        /// <summary>Referências extremas para medir o peso dos timed hits.</summary>
        public static readonly SkillProfile NeverHits = new() { Name = "Nunca acerta" };

        public static readonly SkillProfile AlwaysPerfect = new()
        {
            Name = "Sempre perfeito",
            AttackPerfect = 1f, HoldPerfect = 1f, BlockPerfect = 1f,
        };

        public static readonly SkillProfile[] Players = { Novice, Average, Expert };
    }
}
