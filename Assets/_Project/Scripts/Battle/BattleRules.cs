using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Stats;

namespace Oiram.Battle
{
    public enum CommandType
    {
        Attack,
        Ability,
        Item,
        Defend,
        Flee,
    }

    public sealed class BattleCommand
    {
        public CommandType Type;
        public BattleUnit Actor;
        public AbilityDefinition Ability;
        public ConsumableDefinition Item;
        public List<BattleUnit> Targets = new();

        public static BattleCommand Defend(BattleUnit actor) => new() { Type = CommandType.Defend, Actor = actor };
        public static BattleCommand Flee(BattleUnit actor) => new() { Type = CommandType.Flee, Actor = actor };
    }

    /// <summary>Resultado de um golpe/cura aplicado a um alvo.</summary>
    public struct HitOutcome
    {
        public BattleUnit Target;
        public int Damage;
        public int Healed;
        public bool Crit;
        public bool Killed;
        public bool Revived;
        public bool StatusApplied;
        public StatusType Status;
        public int Reflected;
        public int LifeStolen;
        public int EnergyRestored;
        public TimedHitResult Timing;
        public TimedHitResult Block;
    }

    public static class DamageCalculator
    {
        /// <summary>Fórmula suave: raw² / (raw + defesa). Defesa nunca zera o dano, só reduz.</summary>
        public static float Mitigate(float raw, float defense) =>
            raw <= 0f ? 0f : raw * raw / (raw + Math.Max(0f, defense));

        public static int Compute(float offense, float power, float defense, float variance, float multiplier) =>
            Math.Max(1, (int)Math.Round(Mitigate(offense * power, defense) * variance * multiplier));

        public static int Heal(float magic, float power, float variance, float multiplier) =>
            Math.Max(1, (int)Math.Round(magic * power * variance * multiplier));

        public static float AttackMultiplier(TimedHitResult result, BalanceConfig b) => result switch
        {
            TimedHitResult.Perfect => b.attackPerfectMultiplier,
            TimedHitResult.Good => b.attackGoodMultiplier,
            _ => 1f,
        };

        public static float BlockMultiplier(TimedHitResult result, BalanceConfig b) => result switch
        {
            TimedHitResult.Perfect => b.blockPerfectMultiplier,
            TimedHitResult.Good => b.blockGoodMultiplier,
            _ => 1f,
        };
    }

    public static class TurnOrder
    {
        /// <summary>Ordem da rodada: Velocidade + pequeno sorteio; empate favorece a party.</summary>
        public static List<BattleUnit> Build(IEnumerable<BattleUnit> units, IRandom rng)
        {
            return units
                .Where(u => u.IsAlive)
                .Select(u => (unit: u, initiative: u.Stat(StatType.Speed) + rng.Range(0f, 2f)))
                .OrderByDescending(x => x.initiative)
                .ThenBy(x => x.unit.Side == Side.Party ? 0 : 1)
                .Select(x => x.unit)
                .ToList();
        }
    }

    public static class Targeting
    {
        /// <summary>Alvos válidos do ponto de vista do ator ("inimigo" = lado oposto).</summary>
        public static List<BattleUnit> Candidates(BattleUnit actor, TargetType type, IEnumerable<BattleUnit> all)
        {
            var allies = all.Where(u => u.Side == actor.Side);
            var foes = all.Where(u => u.Side != actor.Side);
            return type switch
            {
                TargetType.SingleEnemy or TargetType.AllEnemies => foes.Where(u => u.IsAlive).ToList(),
                TargetType.SingleAlly or TargetType.AllAllies => allies.Where(u => u.IsAlive).ToList(),
                TargetType.Self => new List<BattleUnit> { actor },
                TargetType.SingleFallenAlly => allies.Where(u => !u.IsAlive).ToList(),
                _ => new List<BattleUnit>(),
            };
        }

        public static bool IsMulti(TargetType type) => type is TargetType.AllEnemies or TargetType.AllAllies;
    }

    /// <summary>Regras puras de combate (sem Unity), para serem testadas isoladamente.</summary>
    public sealed class BattleRules
    {
        readonly BalanceConfig balance;
        readonly IRandom rng;

        public BattleRules(BalanceConfig balance, IRandom rng)
        {
            this.balance = balance;
            this.rng = rng;
        }

        public BalanceConfig Balance => balance;

        public int EnergyCost(BattleUnit actor, AbilityDefinition ability)
        {
            if (ability == null) return 0;
            int reduction = actor.Side == Side.Party ? (int)actor.Stat(StatType.EnergyCostReduction) : 0;
            return ability.energyCost <= 0 ? 0 : Math.Max(1, ability.energyCost - reduction);
        }

        public HitOutcome ResolveAttack(BattleUnit actor, BattleUnit target, AbilityDefinition ability,
            TimedHitResult timing, TimedHitResult block, float powerScale = 1f)
        {
            var outcome = new HitOutcome { Target = target, Timing = timing, Block = block };
            if (!target.IsAlive) return outcome;

            bool magic = ability.kind == AbilityKind.MagicAttack;
            float offense = actor.Stat(magic ? StatType.Magic : StatType.Attack);
            float defense = target.Stat(magic ? StatType.Resistance : StatType.Defense);

            float multiplier = 1f;
            if (actor.Side == Side.Party) multiplier *= DamageCalculator.AttackMultiplier(timing, balance);
            if (target.Side == Side.Party) multiplier *= DamageCalculator.BlockMultiplier(block, balance);
            if (target.Has(StatusType.Defending)) multiplier *= balance.defendMultiplier;

            float critChance = actor.Stat(StatType.CritChance) / 100f;
            if (critChance > 0f && rng.Chance(critChance))
            {
                outcome.Crit = true;
                multiplier *= balance.critMultiplier;
            }

            float variance = rng.Range(balance.varianceMin, balance.varianceMax);
            int damage = DamageCalculator.Compute(offense, ability.power * powerScale, defense, variance, multiplier);
            target.Hp -= damage;
            outcome.Damage = damage;
            outcome.Killed = !target.IsAlive;

            if (actor.Side == Side.Party && actor.IsAlive)
            {
                float lifeSteal = actor.Stat(StatType.LifeSteal);
                if (lifeSteal > 0f)
                {
                    int before = actor.Hp;
                    actor.Hp += Math.Max(1, (int)Math.Round(damage * lifeSteal / 100f));
                    outcome.LifeStolen = actor.Hp - before;
                }

                float burn = actor.Stat(StatType.BurnOnPerfect);
                if (timing == TimedHitResult.Perfect && burn > 0f && target.IsAlive && rng.Chance(burn / 100f))
                {
                    target.AddStatus(StatusType.Burn, 3);
                    outcome.StatusApplied = true;
                    outcome.Status = StatusType.Burn;
                }
            }

            if (target.Side == Side.Party && block == TimedHitResult.Perfect && actor.IsAlive)
            {
                float reflect = target.Stat(StatType.ReflectOnBlock);
                if (reflect > 0f)
                {
                    int reflected = Math.Max(1, (int)Math.Round(damage / balance.blockPerfectMultiplier * reflect / 100f));
                    actor.Hp -= reflected;
                    outcome.Reflected = reflected;
                }
            }

            if (ability.appliesStatus && target.IsAlive && rng.Chance(ability.statusChance))
            {
                target.AddStatus(ability.status, ability.statusTurns);
                outcome.StatusApplied = true;
                outcome.Status = ability.status;
            }

            return outcome;
        }

        public HitOutcome ResolveHeal(BattleUnit actor, BattleUnit target, AbilityDefinition ability, TimedHitResult timing)
        {
            var outcome = new HitOutcome { Target = target, Timing = timing };
            if (!target.IsAlive) return outcome;
            float variance = rng.Range(balance.varianceMin, balance.varianceMax);
            int amount = DamageCalculator.Heal(actor.Stat(StatType.Magic), ability.power, variance,
                DamageCalculator.AttackMultiplier(timing, balance));
            int before = target.Hp;
            target.Hp += amount;
            outcome.Healed = target.Hp - before;
            return outcome;
        }

        public HitOutcome Revive(BattleUnit target, float hpPercent)
        {
            var outcome = new HitOutcome { Target = target };
            if (target.IsAlive) return outcome;
            target.Hp = Math.Max(1, (int)Math.Round(target.MaxHp * hpPercent));
            outcome.Revived = true;
            outcome.Healed = target.Hp;
            return outcome;
        }

        public HitOutcome ApplyStatus(BattleUnit target, StatusType status, int turns)
        {
            target.AddStatus(status, turns);
            return new HitOutcome { Target = target, StatusApplied = target.IsAlive, Status = status };
        }

        public int BurnDamage(BattleUnit unit) => Math.Max(1, (int)Math.Round(unit.MaxHp * balance.burnPercentOfMaxHp));

        /// <summary>Início do turno: tira "Defendendo", aplica Queimadura e verifica Atordoado.</summary>
        public (int burnDamage, bool skipTurn) StartTurn(BattleUnit unit)
        {
            unit.RemoveStatus(StatusType.Defending);
            int burn = 0;
            if (unit.Has(StatusType.Burn))
            {
                burn = BurnDamage(unit);
                unit.Hp -= burn;
            }
            bool skip = false;
            if (unit.IsAlive && unit.Has(StatusType.Stun))
            {
                unit.RemoveStatus(StatusType.Stun);
                skip = true;
            }
            return (burn, skip || !unit.IsAlive);
        }

        public void EndTurn(BattleUnit unit) => unit.TickStatuses();

        public bool TryFlee() => rng.Chance(balance.fleeChance);
    }

    public static class EnemyAI
    {
        /// <summary>Escolhe habilidade por peso e alvo (respeita Provocar; às vezes foca o mais ferido).</summary>
        public static BattleCommand Choose(BattleUnit self, IReadOnlyList<BattleUnit> all, AbilityDefinition basicAttack, IRandom rng)
        {
            var options = self.Enemy != null && self.Enemy.abilities.Count > 0
                ? self.Enemy.abilities
                : new List<EnemyAbility> { new(basicAttack, 1f) };
            var ability = rng.PickWeighted(options, o => o.ability != null ? o.weight : 0f).ability ?? basicAttack;

            var command = new BattleCommand { Type = CommandType.Ability, Actor = self, Ability = ability };
            var candidates = Targeting.Candidates(self, ability.target, all);
            if (candidates.Count == 0) return command;

            if (Targeting.IsMulti(ability.target))
            {
                command.Targets.AddRange(candidates);
                return command;
            }

            var taunting = candidates.FirstOrDefault(u => u.Has(StatusType.Taunt));
            if (taunting != null && ability.TargetsEnemies) command.Targets.Add(taunting);
            else if (ability.TargetsEnemies && rng.Chance(0.35f)) command.Targets.Add(candidates.OrderBy(u => u.HpPercent).First());
            else command.Targets.Add(rng.Pick(candidates));
            return command;
        }
    }
}
