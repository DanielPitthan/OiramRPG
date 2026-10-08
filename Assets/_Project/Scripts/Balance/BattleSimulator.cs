using System.Collections.Generic;
using System.Linq;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;

namespace Oiram.Balance
{
    public sealed class BattleStats
    {
        public string EncounterId;
        public BattleResult Result;
        public int Rounds;
        public int DamageDealt;
        public int DamageTaken;
        public int Healing;
        public int KnockOuts;
        public int EnergySpent;
        public int ItemsUsed;
        public int SkillsUsed;
        public float PartyHpPercentEnd;
        public bool FirstStrike;
        public VictorySummary Victory;
    }

    /// <summary>
    /// Batalha sem apresentação: mesma ordem de turnos, mesmas regras (<see cref="BattleRules"/>),
    /// mesma IA inimiga e mesmas recompensas (<see cref="BattleEffects"/>) do <see cref="BattleManager"/>.
    /// Os timed hits vêm de um <see cref="SkillProfile"/> e as escolhas da party de <see cref="PartyPolicy"/>.
    /// </summary>
    public sealed class BattleSimulator
    {
        readonly GameSession session;
        readonly SkillProfile skill;
        readonly BattleRules rules;
        readonly PartyPolicy policy;
        readonly List<BattleUnit> units = new();

        LootDrop loot;
        List<ItemInstance> stolen;
        BattleStats stats;

        public BattleSimulator(GameSession session, SkillProfile skill)
        {
            this.session = session;
            this.skill = skill;
            rules = new BattleRules(session.Balance, session.Rng);
            policy = new PartyPolicy(session, rules);
        }

        IRandom Rng => session.Rng;

        public BattleStats Run(EncounterDefinition encounter, bool firstStrike = false, int maxRounds = 60)
        {
            units.Clear();
            loot = new LootDrop();
            stolen = new List<ItemInstance>();
            stats = new BattleStats { EncounterId = encounter.id, FirstStrike = firstStrike };

            var balance = session.Balance;
            foreach (var member in session.Party)
            {
                var unit = new BattleUnit(member, balance.buffPercent);
                if (unit.Hp <= 0) unit.Hp = 1;
                units.Add(unit);
            }
            foreach (var def in encounter.enemies) units.Add(new BattleUnit(def, balance.buffPercent));
            if (firstStrike)
                foreach (var enemy in Enemies()) enemy.AddStatus(StatusType.Stun, 1);

            BattleResult? result = null;
            while (result == null && stats.Rounds < maxRounds)
            {
                stats.Rounds++;
                foreach (var unit in TurnOrder.Build(units, Rng))
                {
                    if (!unit.IsAlive) continue;
                    TakeTurn(unit);
                    result = CheckEnd();
                    if (result != null) break;
                }
            }

            stats.Result = result ?? BattleResult.Defeat; // estourar o limite de rodadas conta como derrota
            var party = Party().ToList();
            stats.PartyHpPercentEnd = party.Sum(u => u.Hp) / (float)party.Sum(u => u.MaxHp);
            if (stats.Result == BattleResult.Victory)
                stats.Victory = BattleEffects.GrantVictory(session, encounter, loot, party, stolen);
            return stats;
        }

        IEnumerable<BattleUnit> Party() => units.Where(u => u.Side == Side.Party);
        IEnumerable<BattleUnit> Enemies() => units.Where(u => u.Side == Side.Enemies);

        BattleResult? CheckEnd()
        {
            if (!Enemies().Any(u => u.IsAlive)) return BattleResult.Victory;
            if (!Party().Any(u => u.IsAlive)) return BattleResult.Defeat;
            return null;
        }

        void TakeTurn(BattleUnit unit)
        {
            var (burn, skip) = rules.StartTurn(unit);
            if (burn > 0)
            {
                Count(unit, burn);
                if (!unit.IsAlive) OnDeath(unit);
            }
            if (skip)
            {
                rules.EndTurn(unit);
                return;
            }

            var command = unit.Side == Side.Party
                ? policy.Choose(unit, units)
                : EnemyAI.Choose(unit, units, session.Db.basicAttack, Rng);
            Execute(command);
            rules.EndTurn(unit);
        }

        void Count(BattleUnit target, int damage)
        {
            if (target.Side == Side.Party) stats.DamageTaken += damage;
            else stats.DamageDealt += damage;
        }

        void Record(HitOutcome outcome, BattleUnit actor)
        {
            if (outcome.Target == null) return;
            if (outcome.Damage > 0) Count(outcome.Target, outcome.Damage);
            if (outcome.Healed > 0 && outcome.Target.Side == Side.Party) stats.Healing += outcome.Healed;
            if (outcome.Reflected > 0) Count(actor, outcome.Reflected);
            if (outcome.Killed) OnDeath(outcome.Target);
            if (outcome.Reflected > 0 && !actor.IsAlive) OnDeath(actor);
        }

        void OnDeath(BattleUnit unit)
        {
            if (unit.Side == Side.Enemies) loot.Merge(BattleEffects.RollEnemyLoot(session, unit));
            else stats.KnockOuts++;
        }

        // ---------- execução (espelha BattleManager.Execute) ----------

        void Execute(BattleCommand command)
        {
            var actor = command.Actor;
            switch (command.Type)
            {
                case CommandType.Defend:
                    actor.AddStatus(StatusType.Defending, 1);
                    return;
                case CommandType.Flee:
                    return;
                case CommandType.Item:
                    UseItem(command);
                    return;
            }

            var ability = command.Type == CommandType.Attack || command.Ability == null ? session.Db.basicAttack : command.Ability;
            if (actor.Side == Side.Party && ability.energyCost > 0)
            {
                int cost = rules.EnergyCost(actor, ability);
                if (session.SpendEnergy(cost))
                {
                    stats.EnergySpent += cost;
                    stats.SkillsUsed++;
                }
                else ability = session.Db.basicAttack;
            }

            var targets = ResolveTargets(actor, ability, command.Targets);
            if (targets.Count == 0) return;
            bool party = actor.Side == Side.Party;

            switch (ability.kind)
            {
                case AbilityKind.PhysicalAttack when Targeting.IsMulti(ability.target):
                case AbilityKind.MagicAttack:
                {
                    // Uma janela só para todos os alvos (como no BattleManager).
                    var roll = party ? AttackRoll(ability) : (targets.Any(t => t.Side == Side.Party) ? skill.RollBlock(Rng) : TimedHitResult.Miss);
                    foreach (var target in targets)
                        Record(rules.ResolveAttack(actor, target, ability, party ? roll : TimedHitResult.Miss, party ? TimedHitResult.Miss : roll), actor);
                    break;
                }
                case AbilityKind.PhysicalAttack:
                {
                    var target = targets[0];
                    var hold = party && ability.timing == TimedHitType.HoldRelease ? skill.RollHold(Rng) : TimedHitResult.Miss;
                    int hits = System.Math.Max(1, ability.hits);
                    for (int h = 0; h < hits && target.IsAlive && actor.IsAlive; h++)
                    {
                        var timing = party
                            ? (ability.timing == TimedHitType.HoldRelease ? hold : AttackRoll(ability))
                            : TimedHitResult.Miss;
                        var block = !party && target.Side == Side.Party ? skill.RollBlock(Rng) : TimedHitResult.Miss;
                        Record(rules.ResolveAttack(actor, target, ability, timing, block), actor);
                    }
                    break;
                }
                case AbilityKind.Heal:
                {
                    var roll = AttackRoll(ability);
                    foreach (var target in targets) Record(rules.ResolveHeal(actor, target, ability, roll), actor);
                    break;
                }
                case AbilityKind.Revive:
                    foreach (var target in targets) Record(rules.Revive(target, ability.power), actor);
                    break;
                case AbilityKind.Buff:
                case AbilityKind.Taunt:
                    if (ability.appliesStatus)
                        foreach (var target in targets) rules.ApplyStatus(target, ability.status, ability.statusTurns);
                    break;
                case AbilityKind.Steal:
                    BattleEffects.Steal(session, targets[0], AttackRoll(ability), out var item);
                    if (item != null) stolen.Add(item);
                    break;
            }
        }

        TimedHitResult AttackRoll(AbilityDefinition ability) =>
            ability.timing == TimedHitType.None ? TimedHitResult.Miss : skill.RollAttack(Rng);

        List<BattleUnit> ResolveTargets(BattleUnit actor, AbilityDefinition ability, List<BattleUnit> chosen)
        {
            var valid = Targeting.Candidates(actor, ability.target, units);
            if (Targeting.IsMulti(ability.target)) return valid;
            var alive = chosen.Where(valid.Contains).ToList();
            if (alive.Count > 0) return alive;
            return valid.Count > 0 ? new List<BattleUnit> { valid[0] } : new List<BattleUnit>();
        }

        void UseItem(BattleCommand command)
        {
            var item = command.Item;
            if (item == null || !session.Inventory.ConsumeOne(item)) return;
            stats.ItemsUsed++;
            if (item.effect == ConsumableEffect.RestoreEnergy)
            {
                BattleEffects.ApplyItem(session, rules, item, command.Actor);
                return;
            }
            var valid = Targeting.Candidates(command.Actor, item.target, units);
            var targets = command.Targets.Where(valid.Contains).ToList();
            if (targets.Count == 0) targets = valid.Take(1).ToList();
            foreach (var target in targets) Record(BattleEffects.ApplyItem(session, rules, item, target), command.Actor);
        }
    }

    /// <summary>
    /// Escolhas "razoáveis" de um jogador: revive e cura quando precisa, gasta PE em habilidades
    /// guardando reserva para o curandeiro, rouba com o Ladino e ataca o inimigo mais ferido.
    /// </summary>
    public sealed class PartyPolicy
    {
        const float HealThreshold = 0.4f;
        const float GroupHealThreshold = 0.6f;

        readonly GameSession session;
        readonly BattleRules rules;

        public PartyPolicy(GameSession session, BattleRules rules)
        {
            this.session = session;
            this.rules = rules;
        }

        public BattleCommand Choose(BattleUnit actor, IReadOnlyList<BattleUnit> units)
        {
            var member = actor.Member;
            var allies = units.Where(u => u.Side == Side.Party).ToList();
            var foes = units.Where(u => u.Side == Side.Enemies && u.IsAlive).ToList();
            var abilities = member.PrimaryAbilities().Concat(member.SecondaryAbilities()).ToList();

            bool Can(AbilityDefinition a) =>
                rules.EnergyCost(actor, a) <= session.Energy && Targeting.Candidates(actor, a.target, units).Count > 0;
            AbilityDefinition Find(AbilityKind kind, bool multi) =>
                abilities.FirstOrDefault(a => a.kind == kind && Targeting.IsMulti(a.target) == multi && Can(a));
            ConsumableDefinition Owned(ConsumableEffect effect) =>
                session.Inventory.Consumables().Where(c => c.item.effect == effect).Select(c => c.item).FirstOrDefault();

            // 1) Reviver
            var fallen = allies.FirstOrDefault(u => !u.IsAlive);
            if (fallen != null)
            {
                var revive = Find(AbilityKind.Revive, false);
                if (revive != null) return Ability(actor, revive, fallen);
                var feather = Owned(ConsumableEffect.Revive);
                if (feather != null) return Item(actor, feather, fallen);
            }

            // 2) Curar
            var living = allies.Where(u => u.IsAlive).ToList();
            var weakest = living.OrderBy(u => u.HpPercent).First();
            if (weakest.HpPercent < HealThreshold)
            {
                var group = Find(AbilityKind.Heal, true);
                if (group != null && living.Count(u => u.HpPercent < GroupHealThreshold) >= 2) return Ability(actor, group, null);
                var single = Find(AbilityKind.Heal, false);
                if (single != null) return Ability(actor, single, weakest);
                var potion = Owned(ConsumableEffect.HealHP);
                if (potion != null) return Item(actor, potion, weakest);
            }

            // 3) Sem PE e com Éter: o curandeiro recarrega
            bool isHealer = abilities.Any(a => a.kind == AbilityKind.Heal);
            if (isHealer && session.Energy < 3)
            {
                var ether = Owned(ConsumableEffect.RestoreEnergy);
                if (ether != null) return Item(actor, ether, actor);
            }

            // Reserva de PE para as curas, se há um curandeiro de pé.
            bool healerAlive = living.Any(u => u != actor && u.Member.PrimaryAbilities().Concat(u.Member.SecondaryAbilities()).Any(a => a.kind == AbilityKind.Heal));
            int reserve = healerAlive ? 3 : 0;
            bool Affordable(AbilityDefinition a) => Can(a) && session.Energy - rules.EnergyCost(actor, a) >= reserve;

            // 4) Roubar (1× por inimigo)
            var steal = abilities.FirstOrDefault(a => a.kind == AbilityKind.Steal && Affordable(a));
            var mark = foes.Where(f => !f.HasBeenStolenFrom && f.Enemy?.lootTable != null).OrderByDescending(f => f.Level).FirstOrDefault();
            if (steal != null && mark != null) return Ability(actor, steal, mark);

            // 5) Ofensiva
            var target = foes.OrderBy(f => f.Hp).First();
            if (foes.Count >= 2)
            {
                var area = abilities.FirstOrDefault(a => a.TargetsEnemies && Targeting.IsMulti(a.target) && Affordable(a));
                if (area != null) return Ability(actor, area, null);
            }
            var strong = abilities
                .Where(a => a.kind is AbilityKind.PhysicalAttack or AbilityKind.MagicAttack && !Targeting.IsMulti(a.target) && Affordable(a))
                .OrderByDescending(a => a.power * a.hits)
                .FirstOrDefault();
            if (strong != null && strong.power * strong.hits > 1.15f) return Ability(actor, strong, target);

            return new BattleCommand { Type = CommandType.Attack, Actor = actor, Targets = { target } };
        }

        static BattleCommand Ability(BattleUnit actor, AbilityDefinition ability, BattleUnit target)
        {
            var command = new BattleCommand { Type = CommandType.Ability, Actor = actor, Ability = ability };
            if (target != null) command.Targets.Add(target);
            return command;
        }

        static BattleCommand Item(BattleUnit actor, ConsumableDefinition item, BattleUnit target)
        {
            var command = new BattleCommand { Type = CommandType.Item, Actor = actor, Item = item };
            if (target != null) command.Targets.Add(target);
            return command;
        }
    }
}
