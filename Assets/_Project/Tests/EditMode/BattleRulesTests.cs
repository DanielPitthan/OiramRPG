using System.Linq;
using NUnit.Framework;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;

namespace Oiram.Tests
{
    public class BattleRulesTests
    {
        GameDatabase db;
        BalanceConfig b;

        [SetUp]
        public void SetUp()
        {
            db = DefaultContent.Build();
            b = db.balance;
            b.varianceMin = b.varianceMax = 1f; // dano determinístico
        }

        BattleUnit Hero(string id = "oiram") => new BattleUnit(new PartyMember(db.Find<CharacterDefinition>(id), b), b.buffPercent);
        BattleUnit Foe(string id = "slime") => new BattleUnit(db.Find<EnemyDefinition>(id), b.buffPercent);
        BattleRules Rules(int seed = 1) => new BattleRules(b, new SeededRandom(seed));

        [Test]
        public void Damage_IsAtLeastOne_AndDefenseMitigates()
        {
            Assert.AreEqual(1, DamageCalculator.Compute(1, 1, 999, 1, 1));
            int soft = DamageCalculator.Compute(20, 1, 0, 1, 1);
            int hard = DamageCalculator.Compute(20, 1, 20, 1, 1);
            Assert.AreEqual(20, soft);
            Assert.AreEqual(10, hard);
        }

        [Test]
        public void TimedMultipliers_OrderMissGoodPerfect()
        {
            Assert.AreEqual(1f, DamageCalculator.AttackMultiplier(TimedHitResult.Miss, b));
            Assert.AreEqual(b.attackGoodMultiplier, DamageCalculator.AttackMultiplier(TimedHitResult.Good, b));
            Assert.AreEqual(b.attackPerfectMultiplier, DamageCalculator.AttackMultiplier(TimedHitResult.Perfect, b));
            Assert.Less(DamageCalculator.BlockMultiplier(TimedHitResult.Perfect, b), DamageCalculator.BlockMultiplier(TimedHitResult.Good, b));
        }

        [Test]
        public void PerfectAttack_DealsMoreThanMiss()
        {
            int miss = Rules().ResolveAttack(Hero(), Foe(), db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss).Damage;
            int perfect = Rules().ResolveAttack(Hero(), Foe(), db.basicAttack, TimedHitResult.Perfect, TimedHitResult.Miss).Damage;
            Assert.Greater(perfect, miss);
        }

        [Test]
        public void Block_ReducesDamageTakenByParty()
        {
            int open = Rules().ResolveAttack(Foe(), Hero("lia"), db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss).Damage;
            int blocked = Rules().ResolveAttack(Foe(), Hero("lia"), db.basicAttack, TimedHitResult.Miss, TimedHitResult.Perfect).Damage;
            Assert.Less(blocked, open);
        }

        [Test]
        public void Killing_ClearsStatuses()
        {
            var foe = Foe();
            foe.AddStatus(StatusType.Burn, 3);
            foe.Hp = 1;
            var outcome = Rules().ResolveAttack(Hero(), foe, db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss);
            Assert.IsTrue(outcome.Killed);
            Assert.IsEmpty(foe.Statuses);
        }

        [Test]
        public void Burn_TicksAtTurnStart_StunSkipsOnce()
        {
            var rules = Rules();
            var foe = Foe();
            foe.AddStatus(StatusType.Burn, 2);
            foe.AddStatus(StatusType.Stun, 1);

            var (burn, skip) = rules.StartTurn(foe);
            Assert.AreEqual(rules.BurnDamage(foe), burn);
            Assert.IsTrue(skip);
            rules.EndTurn(foe);

            (_, skip) = rules.StartTurn(foe);
            Assert.IsFalse(skip);
            rules.EndTurn(foe);
            Assert.IsFalse(foe.Has(StatusType.Burn), "queimadura expira após 2 turnos");
        }

        [Test]
        public void DefendingHalvesDamage_AndEndsAtNextTurn()
        {
            var rules = Rules();
            var hero = Hero("lia");
            int open = Rules().ResolveAttack(Foe(), Hero("lia"), db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss).Damage;
            hero.AddStatus(StatusType.Defending, 1);
            int defended = Rules().ResolveAttack(Foe(), hero, db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss).Damage;
            Assert.Less(defended, open);
            rules.StartTurn(hero);
            Assert.IsFalse(hero.Has(StatusType.Defending));
        }

        [Test]
        public void EnergyCost_ReducedByAffix_ButNeverFree()
        {
            var member = new PartyMember(db.Find<CharacterDefinition>("lia"), b);
            var energia = db.Find<AffixDefinition>("da_energia");
            var amuleto = db.Find<ItemBaseDefinition>("amuleto");
            member.Equip(new ItemInstance(amuleto, 6, Rarity.Uncommon, new() { new RolledAffix(energia, 1, 2) }, 0f));
            var unit = new BattleUnit(member, b.buffPercent);

            var rules = Rules();
            Assert.AreEqual(1, rules.EnergyCost(unit, db.Find<AbilityDefinition>("faisca")));  // 3 − 2
            Assert.AreEqual(5, rules.EnergyCost(unit, db.Find<AbilityDefinition>("trovao")));  // 7 − 2
            Assert.AreEqual(0, rules.EnergyCost(unit, db.basicAttack));
        }

        [Test]
        public void LifeSteal_HealsAttacker()
        {
            var member = new PartyMember(db.Find<CharacterDefinition>("oiram"), b);
            var vamp = db.Find<AffixDefinition>("vampirico");
            member.Equip(new ItemInstance(db.Find<ItemBaseDefinition>("espada"), 1, Rarity.Uncommon, new() { new RolledAffix(vamp, 0, 50) }, 0f));
            member.CurrentHp = 10;
            var hero = new BattleUnit(member, b.buffPercent);

            var outcome = Rules().ResolveAttack(hero, Foe(), db.basicAttack, TimedHitResult.Miss, TimedHitResult.Miss);
            Assert.Greater(outcome.LifeStolen, 0);
            Assert.AreEqual(10 + outcome.LifeStolen, member.CurrentHp);
        }

        [Test]
        public void PerfectBlock_ReflectsWithAffix()
        {
            var member = new PartyMember(db.Find<CharacterDefinition>("oiram"), b);
            var reflexo = db.Find<AffixDefinition>("do_reflexo");
            member.Equip(new ItemInstance(db.Find<ItemBaseDefinition>("elmo"), 1, Rarity.Uncommon, new() { new RolledAffix(reflexo, 0, 50) }, 0f));
            var hero = new BattleUnit(member, b.buffPercent);
            var foe = Foe();

            var outcome = Rules().ResolveAttack(foe, hero, db.basicAttack, TimedHitResult.Miss, TimedHitResult.Perfect);
            Assert.Greater(outcome.Reflected, 0);
            Assert.AreEqual(foe.MaxHp - outcome.Reflected, foe.Hp);
        }

        [Test]
        public void TurnOrder_SortsBySpeed()
        {
            var fast = Foe("morcego");  // VEL 12
            var slow = Foe("golem");    // VEL 3
            var order = TurnOrder.Build(new[] { slow, fast }, new SeededRandom(1));
            Assert.AreSame(fast, order[0]);

            slow.Hp = 0;
            Assert.AreEqual(1, TurnOrder.Build(new[] { slow, fast }, new SeededRandom(1)).Count, "mortos não entram");
        }

        [Test]
        public void EnemyAI_RespectsTaunt()
        {
            var a = Hero("lia");
            var tank = Hero("oiram");
            tank.AddStatus(StatusType.Taunt, 3);
            var foe = Foe();
            var all = new[] { a, tank, foe };
            for (int i = 0; i < 50; i++)
                Assert.AreSame(tank, EnemyAI.Choose(foe, all, db.basicAttack, new SeededRandom(i)).Targets.Single());
        }

        [Test]
        public void HeroHp_PersistsThroughPartyMember()
        {
            var member = new PartyMember(db.Find<CharacterDefinition>("teo"), b);
            var unit = new BattleUnit(member, b.buffPercent);
            unit.Hp -= 5;
            Assert.AreEqual(member.MaxHp - 5, member.CurrentHp);
        }
    }
}
