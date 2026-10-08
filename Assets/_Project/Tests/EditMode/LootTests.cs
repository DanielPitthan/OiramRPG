using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Oiram.Core;
using Oiram.Loot;

namespace Oiram.Tests
{
    public class LootTests
    {
        GameDatabase db;

        [SetUp]
        public void SetUp() => db = DefaultContent.Build();

        LootGenerator Gen(int seed) => new LootGenerator(db, new SeededRandom(seed));

        [Test]
        public void RarityDistribution_FollowsBaseWeights()
        {
            var gen = Gen(42);
            var counts = new Dictionary<Rarity, int>();
            const int rolls = 20000;
            for (int i = 0; i < rolls; i++)
            {
                var r = gen.RollRarity();
                counts[r] = counts.TryGetValue(r, out int c) ? c + 1 : 1;
            }

            Assert.That(counts[Rarity.Common] / (float)rolls, Is.InRange(0.57f, 0.63f));
            Assert.That(counts[Rarity.Uncommon] / (float)rolls, Is.InRange(0.22f, 0.28f));
            Assert.That(counts[Rarity.Legendary] / (float)rolls, Is.InRange(0.004f, 0.02f));
        }

        [Test]
        public void MagicFind_ShiftsTowardHigherRarities()
        {
            var gen = Gen(7);
            int RareOrBetter(float mf) => Enumerable.Range(0, 5000).Count(_ => gen.RollRarity(mf) >= Rarity.Rare);
            Assert.Greater(RareOrBetter(100f), RareOrBetter(0f) * 1.5f);
        }

        [Test]
        public void RarityFloor_IsRespected()
        {
            var gen = Gen(3);
            for (int i = 0; i < 2000; i++)
                Assert.GreaterOrEqual(gen.RollRarity(0f, Rarity.Rare), Rarity.Rare);
        }

        [Test]
        public void AffixCount_MatchesRarity_AndGroupsNeverRepeat()
        {
            var gen = Gen(11);
            foreach (var baseDef in db.items)
            {
                foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
                {
                    for (int i = 0; i < 30; i++)
                    {
                        var item = gen.Create(baseDef, 1 + i % 10, rarity);
                        Assert.AreEqual(RarityInfo.AffixCount(rarity), item.Affixes.Count, $"{item} deveria ter {RarityInfo.AffixCount(rarity)} afixos");

                        var groups = item.Affixes.Select(a => a.affix.group).ToList();
                        Assert.AreEqual(groups.Count, groups.Distinct().Count(), $"grupo repetido em {item}");
                        Assert.LessOrEqual(item.Affixes.Count(a => a.affix.position == AffixPosition.Prefix), LootGenerator.PrefixLimit(rarity));
                        Assert.LessOrEqual(item.Affixes.Count(a => a.affix.position == AffixPosition.Suffix), LootGenerator.SuffixLimit(rarity));
                        Assert.IsTrue(item.Affixes.All(a => a.affix.AllowsSlot(baseDef.slot)), $"afixo de slot errado em {item}");
                    }
                }
            }
        }

        [Test]
        public void Legendary_AlwaysHasSpecialAffix()
        {
            var gen = Gen(5);
            foreach (var baseDef in db.items)
                for (int i = 0; i < 20; i++)
                    Assert.IsTrue(gen.Create(baseDef, 1, Rarity.Legendary).Affixes.Any(a => a.affix.isSpecial), baseDef.id);
        }

        [Test]
        public void Relic_HasFiveMaxedAffixes_AndNeverDropsRandomly()
        {
            var gen = Gen(13);
            for (int i = 0; i < 40; i++)
            {
                var relic = gen.CreateRelic(1 + i % 12);
                Assert.AreEqual(Rarity.Relic, relic.Rarity);
                Assert.AreEqual(5, relic.Affixes.Count, relic.ToString());
                Assert.IsTrue(relic.Affixes.Any(a => a.affix.isSpecial));
                foreach (var a in relic.Affixes)
                {
                    int best = a.affix.tiers.FindLastIndex(t => t.minItemLevel <= relic.ItemLevel);
                    Assert.AreEqual(best, a.tier, "tier mais alto liberado");
                    Assert.That(a.value, Is.EqualTo(System.Math.Floor(a.affix.tiers[best].max)), "valor máximo");
                }
            }
            for (int i = 0; i < 20000; i++)
                Assert.Less(gen.RollRarity(500f), Rarity.Relic, "Relíquia não cai de inimigos");
        }

        [Test]
        public void AffixTiers_AreGatedByItemLevel()
        {
            var gen = Gen(99);
            for (int i = 0; i < 500; i++)
            {
                int level = 1 + i % 9;
                var item = gen.CreateRandom(level, 0f, Rarity.Epic);
                foreach (var rolled in item.Affixes)
                {
                    var tier = rolled.affix.tiers[rolled.tier];
                    Assert.LessOrEqual(tier.minItemLevel, level);
                    Assert.That(rolled.value, Is.InRange(tier.min, tier.max));
                }
            }
        }

        [Test]
        public void ItemScaling_IncreasesBaseStatsWithLevel()
        {
            var gen = Gen(1);
            var espada = db.Find<ItemBaseDefinition>("espada");
            float low = gen.Create(espada, 1, Rarity.Common).BaseModifiers[0].value;
            float high = gen.Create(espada, 9, Rarity.Common).BaseModifiers[0].value;
            Assert.AreEqual(5f, low);
            Assert.Greater(high, low);
        }

        [Test]
        public void Names_AgreeWithGrammaticalGender()
        {
            var espada = db.Find<ItemBaseDefinition>("espada");
            var elmo = db.Find<ItemBaseDefinition>("elmo");
            var afiado = db.Find<AffixDefinition>("afiado");
            var robusto = db.Find<AffixDefinition>("robusto");
            var tigre = db.Find<AffixDefinition>("do_tigre");
            var coruja = db.Find<AffixDefinition>("da_coruja");

            Assert.AreEqual("Espada Afiada do Tigre",
                ItemNameBuilder.Build(espada, new List<RolledAffix> { new(tigre, 0, 5), new(afiado, 0, 3) }));
            Assert.AreEqual("Elmo Robusto da Coruja",
                ItemNameBuilder.Build(elmo, new List<RolledAffix> { new(robusto, 0, 2), new(coruja, 0, 5) }));
            Assert.AreEqual("Espada", ItemNameBuilder.Build(espada, new List<RolledAffix>()));
        }

        [Test]
        public void LootTable_GuaranteedDropsAndFloor()
        {
            var gen = Gen(21);
            var golem = db.Find<LootTable>("lt_golem");
            for (int i = 0; i < 50; i++)
            {
                var drop = gen.Roll(golem, 4);
                Assert.GreaterOrEqual(drop.Items.Count, golem.itemsMin + 1);
                Assert.IsTrue(drop.Items.All(it => it.Rarity >= golem.rarityFloor), "nada abaixo do piso da tabela");
                Assert.IsTrue(drop.Items.Any(it => it.Rarity >= Rarity.Epic), "o Épico garantido sempre cai");
                Assert.That(drop.Gold, Is.InRange(golem.goldMin, golem.goldMax));
            }
        }

        [Test]
        public void SameSeed_GivesSameLoot()
        {
            var table = db.Find<LootTable>("lt_mimico");
            var a = Gen(1234).Roll(table, 3);
            var b = Gen(1234).Roll(table, 3);
            CollectionAssert.AreEqual(a.Items.Select(i => i.Name + i.Rarity), b.Items.Select(i => i.Name + i.Rarity));
            Assert.AreEqual(a.Gold, b.Gold);
        }
    }
}
