using NUnit.Framework;
using Oiram.Battle;
using Oiram.Core;

namespace Oiram.Tests
{
    public class TimedHitTests
    {
        const double Perfect = 0.06, Good = 0.15;

        static TimedHitWindow Window() => new TimedHitWindow(armedAt: 0.0, impactAt: 1.0, Perfect, Good);

        [TestCase(1.00, TimedHitResult.Perfect)]
        [TestCase(0.95, TimedHitResult.Perfect)]
        [TestCase(1.05, TimedHitResult.Perfect)]
        [TestCase(0.90, TimedHitResult.Good)]
        [TestCase(1.14, TimedHitResult.Good)]
        [TestCase(1.20, TimedHitResult.Miss)]
        [TestCase(0.50, TimedHitResult.Miss)]
        public void SinglePress_Windows(double pressAt, TimedHitResult expected)
        {
            var w = Window();
            w.RegisterPress(pressAt);
            Assert.AreEqual(expected, w.Result);
        }

        [Test]
        public void OnlyFirstPressCounts_AntiSpam()
        {
            var w = Window();
            w.RegisterPress(0.5);  // cedo demais → gasta a tentativa
            w.RegisterPress(1.0);
            Assert.AreEqual(TimedHitResult.Miss, w.Result);
            Assert.IsTrue(w.IsClosed(0.6));
        }

        [Test]
        public void PressBeforeArming_IsIgnored()
        {
            var w = new TimedHitWindow(armedAt: 0.5, impactAt: 1.0, Perfect, Good);
            w.RegisterPress(0.2); // ex.: o mesmo aperto que confirmou o comando
            w.RegisterPress(1.0);
            Assert.AreEqual(TimedHitResult.Perfect, w.Result);
        }

        [Test]
        public void NoPress_ClosesAfterGoodWindow()
        {
            var w = Window();
            Assert.IsFalse(w.IsClosed(1.1));
            Assert.IsTrue(w.IsClosed(1.0 + Good));
            Assert.AreEqual(TimedHitResult.Miss, w.Result);
        }

        [Test]
        public void TimingAffix_WidensWindows()
        {
            var balance = BalanceConfig.CreateDefault();
            var (p0, g0) = TimedHitEvaluator.Windows(balance, 0);
            var (p1, g1) = TimedHitEvaluator.Windows(balance, 40);
            Assert.Greater(p1, p0);
            Assert.AreEqual(g0 + 0.04, g1, 1e-6);
        }

        [Test]
        public void HoldRelease_ReleaseAtFullCharge()
        {
            var w = new HoldReleaseWindow(armedAt: 0, chargeDuration: 1.0, Perfect, Good);
            w.RegisterPress(0.2);
            Assert.AreEqual(0.5f, w.Charge01(0.7), 1e-4);
            w.RegisterRelease(1.22);
            Assert.IsTrue(w.IsDone);
            Assert.AreEqual(TimedHitResult.Perfect, w.Result);
        }

        [Test]
        public void HoldRelease_EarlyReleaseOrOvercharge_Misses()
        {
            var early = new HoldReleaseWindow(0, 1.0, Perfect, Good);
            early.RegisterPress(0);
            early.RegisterRelease(0.5);
            Assert.AreEqual(TimedHitResult.Miss, early.Result);

            var over = new HoldReleaseWindow(0, 1.0, Perfect, Good);
            over.RegisterPress(0);
            over.Update(1.0 + Good + 0.01);
            Assert.IsTrue(over.IsDone);
            Assert.AreEqual(TimedHitResult.Miss, over.Result);
        }
    }
}
