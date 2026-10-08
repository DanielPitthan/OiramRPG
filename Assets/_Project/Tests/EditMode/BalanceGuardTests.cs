using System.Linq;
using NUnit.Framework;
using Oiram.Balance;
using Oiram.Battle;
using Oiram.Core;

namespace Oiram.Tests
{
    /// <summary>
    /// Protege o balanceamento: se uma mudança de conteúdo ou regra tirar o jogo das metas
    /// (BalanceReport.DefaultTargets), estes testes falham. Sementes fixas → resultado determinístico.
    /// </summary>
    public class BalanceGuardTests
    {
        [Test]
        public void SliceMeetsAllBalanceTargets()
        {
            var db = DefaultContent.Build();
            var report = BalanceReport.Run(db, runsPerProfile: 300);
            var failed = report.Targets.Where(t => !t.Passed).Select(t => $"{t.Description}: {t.ValueText} (meta {t.Range})").ToList();
            Assert.IsEmpty(failed, "Metas de balanceamento quebradas:\n" + string.Join("\n", failed));
        }

        [Test]
        public void Simulator_IsDeterministicForSameSeed()
        {
            var db = DefaultContent.Build();
            var a = RouteSimulator.Run(db, SkillProfile.Average, 77);
            var b = RouteSimulator.Run(db, SkillProfile.Average, 77);
            CollectionAssert.AreEqual(a.Battles.Select(x => x.Rounds), b.Battles.Select(x => x.Rounds));
            Assert.AreEqual(a.Gold, b.Gold);
            Assert.AreEqual(a.ItemsTotal, b.ItemsTotal);
        }

        [Test]
        public void Simulator_BattlesAlwaysEnd_AndGrantRewardsLikeTheGame()
        {
            var db = DefaultContent.Build();
            var session = new GameSession(db, new SeededRandom(5));
            int itemsBefore = session.Inventory.Items.Count;
            var stats = new BattleSimulator(session, SkillProfile.Average).Run(db.Find<EncounterDefinition>("enc_slimes2"));

            Assert.AreEqual(BattleResult.Victory, stats.Result);
            Assert.Less(stats.Rounds, 60);
            Assert.IsNotNull(stats.Victory);
            Assert.GreaterOrEqual(session.Inventory.Items.Count, itemsBefore + 1, "vitória sempre dá pelo menos 1 item");
            Assert.IsTrue(session.Party.All(m => m.Xp > 0 || m.Level > 1));
        }

        [Test]
        public void TimedHits_MakeARealDifference()
        {
            var db = DefaultContent.Build();
            var never = BalanceReport.MeasureTiming(db, "enc_golem", 4, SkillProfile.NeverHits, 100);
            var perfect = BalanceReport.MeasureTiming(db, "enc_golem", 4, SkillProfile.AlwaysPerfect, 100);
            Assert.Less(perfect.AvgDamageTaken, never.AvgDamageTaken * 0.5f, "defesas perfeitas cortam muito o dano");
            Assert.Greater(perfect.WinRate, never.WinRate);
        }
    }
}
