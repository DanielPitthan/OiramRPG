using System.Linq;
using NUnit.Framework;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;

namespace Oiram.Tests
{
    public class CharacterTests
    {
        GameDatabase db;
        LootGenerator gen;

        [SetUp]
        public void SetUp()
        {
            db = DefaultContent.Build();
            gen = new LootGenerator(db, new SeededRandom(1));
        }

        PartyMember Oiram() => new PartyMember(db.Find<CharacterDefinition>("oiram"), db.balance);
        JobDefinition Job(string id) => db.Find<JobDefinition>(id);

        [Test]
        public void StatBlock_ComposesFlatThenPercent()
        {
            var b = new StatBlock { [StatType.Attack] = 10 };
            var result = StatBlock.Compose(b, new[]
            {
                new StatModifier(StatType.Attack, 5),
                new StatModifier(StatType.Attack, 20, ModifierKind.Percent),
                new StatModifier(StatType.Attack, 30, ModifierKind.Percent),
            });
            Assert.AreEqual(22.5f, result[StatType.Attack], 0.001f);
        }

        [Test]
        public void BaseStats_UseLevelGrowthAndJobMultipliers()
        {
            var m = Oiram();
            // Guardião: ATQ base 10 × 1.15
            Assert.AreEqual(11.5f, m.BaseStats()[StatType.Attack], 0.001f);
            m.GainXp(db.balance.XpToNextLevel(1));
            Assert.AreEqual(2, m.Level);
            Assert.AreEqual((10 + 2) * 1.15f, m.BaseStats()[StatType.Attack], 0.001f);
        }

        [Test]
        public void LevelUp_FullyHeals()
        {
            var m = Oiram();
            m.CurrentHp = 1;
            int gained = m.GainXp(1000);
            Assert.Greater(gained, 1);
            Assert.AreEqual(m.MaxHp, m.CurrentHp);
        }

        [Test]
        public void Equipment_AddsModifiers()
        {
            var m = Oiram();
            float before = m.ComputeStats()[StatType.Attack];
            m.Equip(gen.Create(db.Find<ItemBaseDefinition>("machado"), 1, Rarity.Common));
            Assert.AreEqual(before + 7f, m.ComputeStats()[StatType.Attack], 0.001f);
        }

        [Test]
        public void JobRestrictions_BlockEquip()
        {
            var m = Oiram(); // Guardião
            var cajado = gen.Create(db.Find<ItemBaseDefinition>("cajado"), 1, Rarity.Common);
            Assert.IsFalse(m.CanEquip(cajado));
            Assert.Throws<System.InvalidOperationException>(() => m.Equip(cajado));
        }

        [Test]
        public void JobUnlock_RequiresLevelsInOtherJobs()
        {
            var m = Oiram();
            Assert.IsTrue(m.IsJobUnlocked(Job("guardiao")), "job inicial sempre liberado");
            Assert.IsTrue(m.IsJobUnlocked(Job("aprendiz")));
            Assert.IsFalse(m.IsJobUnlocked(Job("arcanista")));
            Assert.IsFalse(m.IsJobUnlocked(Job("ladino")));

            Assert.IsTrue(m.SetJob(Job("aprendiz")));
            m.GainJp(db.balance.jobLevelThresholds[1]);
            Assert.AreEqual(2, m.JobLevel(Job("aprendiz")));
            Assert.IsTrue(m.IsJobUnlocked(Job("arcanista")));
            Assert.IsFalse(m.IsJobUnlocked(Job("ladino")));
        }

        [Test]
        public void LearningAbilities_SpendsJp()
        {
            var m = Oiram();
            var progress = m.ProgressFor(Job("guardiao"));
            var provocar = db.Find<AbilityDefinition>("provocar");

            Assert.IsTrue(progress.IsLearned(db.Find<AbilityDefinition>("investida")), "habilidade inicial");
            Assert.IsFalse(progress.TryLearn(provocar), "sem JP");
            m.GainJp(provocar.jpCost);
            Assert.IsTrue(progress.TryLearn(provocar));
            Assert.AreEqual(0, progress.AvailableJp);
            Assert.Contains(provocar, m.PrimaryAbilities().ToList());
        }

        [Test]
        public void SecondarySkillset_AndSupportPassive()
        {
            var m = Oiram();
            var aprendiz = Job("aprendiz");
            var disciplina = db.Find<AbilityDefinition>("disciplina");

            m.SetJob(aprendiz);
            m.GainJp(500);
            Assert.IsTrue(m.ProgressFor(aprendiz).TryLearn(db.Find<AbilityDefinition>("golpe_duplo")));
            Assert.IsTrue(m.ProgressFor(aprendiz).TryLearn(disciplina));

            m.SetJob(Job("guardiao"));
            Assert.IsTrue(m.SetSecondaryJob(aprendiz));
            Assert.AreEqual("golpe_duplo", m.SecondaryAbilities().Single().id);

            Assert.IsTrue(m.SetSupportPassive(disciplina));
            Assert.AreEqual(10f, m.ComputeStats()[StatType.JPBonus], 0.001f);
            Assert.AreEqual(110, m.GainJp(100));
        }

        [Test]
        public void ChangingJob_ReturnsIncompatibleGearToInventory()
        {
            var inv = new PartyInventory(10);
            var m = Oiram();
            m.SetJob(Job("aprendiz"));
            m.GainJp(100); // Aprendiz Nv 2 libera o Arcanista
            m.SetJob(Job("guardiao"));

            var armor = gen.Create(db.Find<ItemBaseDefinition>("armadura_placas"), 1, Rarity.Common);
            m.Equip(armor);
            Assert.IsTrue(inv.ChangeJob(m, Job("arcanista")));
            Assert.IsNull(m.GetEquipped(EquipSlot.Armor), "arcanista não usa armadura pesada");
            Assert.Contains(armor, inv.Items.ToList());
        }

        [Test]
        public void Inventory_EquipSwapsAndRespectsCapacity()
        {
            var inv = new PartyInventory(2);
            var m = Oiram();
            var a = gen.Create(db.Find<ItemBaseDefinition>("espada"), 1, Rarity.Rare);
            var b = gen.Create(db.Find<ItemBaseDefinition>("machado"), 1, Rarity.Rare);
            var c = gen.Create(db.Find<ItemBaseDefinition>("elmo"), 1, Rarity.Rare);
            Assert.IsTrue(inv.TryAdd(a));
            Assert.IsTrue(inv.TryAdd(b));
            Assert.IsFalse(inv.TryAdd(c), "mochila cheia");

            Assert.IsTrue(inv.Equip(m, a));
            Assert.IsTrue(inv.Equip(m, b));
            Assert.AreSame(b, m.GetEquipped(EquipSlot.Weapon));
            Assert.Contains(a, inv.Items.ToList());

            int gold = inv.Gold;
            Assert.Greater(inv.Sell(a), 0);
            Assert.Greater(inv.Gold, gold);
        }

        [Test]
        public void Consumables_StackAndConsume()
        {
            var inv = new PartyInventory(10);
            var pocao = db.Find<ConsumableDefinition>("pocao");
            inv.AddConsumable(pocao, 2);
            Assert.AreEqual(2, inv.Count(pocao));
            Assert.IsTrue(inv.ConsumeOne(pocao));
            Assert.IsTrue(inv.ConsumeOne(pocao));
            Assert.IsFalse(inv.ConsumeOne(pocao));
        }
    }
}
