using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oiram.Audio;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.World;

namespace Oiram.Tests
{
    public class SynthTests
    {
        static void AssertPlayable(float[] samples, string what, float maxSeconds)
        {
            Assert.IsNotNull(samples, what);
            Assert.Greater(samples.Length, 10, $"{what}: vazio");
            Assert.LessOrEqual(samples.Length / (float)Synth.Rate, maxSeconds, $"{what}: longo demais");
            float peak = 0f;
            foreach (float x in samples)
            {
                Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), $"{what}: amostra inválida");
                Assert.LessOrEqual(Math.Abs(x), 1f, $"{what}: estoura (clipping)");
                peak = Math.Max(peak, Math.Abs(x));
            }
            Assert.Greater(peak, 0.05f, $"{what}: mudo");
        }

        [Test]
        public void EverySoundEffect_IsShortFiniteAndAudible()
        {
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
                AssertPlayable(SoundBank.Build(sfx), sfx.ToString(), 3f);
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                AssertPlayable(SoundBank.Loot(rarity), "loot " + rarity, 3f);
        }

        [Test]
        public void RarerLoot_HasLongerJingle()
        {
            Assert.Less(SoundBank.Loot(Rarity.Common).Length, SoundBank.Loot(Rarity.Rare).Length);
            Assert.Less(SoundBank.Loot(Rarity.Rare).Length, SoundBank.Loot(Rarity.Legendary).Length);
            Assert.LessOrEqual(SoundBank.Loot(Rarity.Legendary).Length, SoundBank.Loot(Rarity.Relic).Length);
        }

        [Test]
        public void EveryMusicTrack_ComposesADeterministicLoop()
        {
            Assert.AreEqual(0, MusicComposer.Compose(MusicTrack.None).Length);
            foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
            {
                if (track == MusicTrack.None) continue;
                var a = MusicComposer.Compose(track);
                AssertPlayable(a, track.ToString(), 40f);
                Assert.GreaterOrEqual(a.Length / (float)Synth.Rate, 8f, $"{track}: loop curto demais");
                var b = MusicComposer.Compose(track);
                Assert.AreEqual(a.Length, b.Length);
                for (int i = 0; i < a.Length; i += 997) Assert.AreEqual(a[i], b[i], $"{track}: mesma seed, mesma música");
            }
        }

        [Test]
        public void Tracks_AreDifferentFromEachOther()
        {
            var field = MusicComposer.Compose(MusicTrack.Field);
            var battle = MusicComposer.Compose(MusicTrack.Battle);
            Assert.AreNotEqual(field.Length, battle.Length);
            Assert.Greater(MusicComposer.StepSeconds(MusicTrack.Dungeon), MusicComposer.StepSeconds(MusicTrack.Boss), "dungeon lenta, chefe rápido");
        }
    }

    public class FieldItemsTests
    {
        GameDatabase db;
        GameSession s;
        ConsumableDefinition potion, ether, feather;

        [SetUp]
        public void SetUp()
        {
            db = DefaultContent.Build();
            s = new GameSession(db, new SeededRandom(21));
            s.Inventory.Clear();
            potion = db.Find<ConsumableDefinition>("pocao");
            ether = db.Find<ConsumableDefinition>("eter");
            feather = db.Find<ConsumableDefinition>("pena");
        }

        [Test]
        public void Potion_HealsUpToMax_AndIsConsumed()
        {
            s.Inventory.AddConsumable(potion, 2);
            var m = s.Party[0];
            m.CurrentHp = m.MaxHp - 10;
            var r = FieldItems.Use(s, potion, m);
            Assert.IsTrue(r.Used);
            Assert.AreEqual(10, r.Healed, "não passa do máximo");
            Assert.AreEqual(m.MaxHp, m.CurrentHp);
            Assert.AreEqual(1, s.Inventory.Count(potion));
        }

        [Test]
        public void Potion_OnFullHp_IsNotWasted()
        {
            s.Inventory.AddConsumable(potion);
            var m = s.Party[0];
            m.FullHeal();
            var r = FieldItems.Use(s, potion, m);
            Assert.IsFalse(r.Used);
            StringAssert.Contains("cheios", r.Message);
            Assert.AreEqual(1, s.Inventory.Count(potion));
            Assert.IsFalse(FieldItems.AnyUseful(s, potion));
            s.Party[1].CurrentHp = 1;
            Assert.IsTrue(FieldItems.AnyUseful(s, potion));
        }

        [Test]
        public void Ether_RestoresPartyEnergy_WithoutTarget()
        {
            s.Inventory.AddConsumable(ether);
            Assert.IsFalse(FieldItems.NeedsTarget(ether));
            s.SetEnergy(s.MaxEnergy);
            Assert.IsFalse(FieldItems.Use(s, ether, null).Used, "PE cheio");
            s.SetEnergy(0);
            var r = FieldItems.Use(s, ether, null);
            Assert.IsTrue(r.Used);
            Assert.AreEqual(Math.Min(ether.amount, s.MaxEnergy), s.Energy);
            Assert.AreEqual(0, s.Inventory.Count(ether));
        }

        [Test]
        public void Feather_OnlyRevivesKnockedOutMembers()
        {
            s.Inventory.AddConsumable(feather);
            var m = s.Party[2];
            Assert.IsFalse(FieldItems.Use(s, feather, m).Used, "está de pé");
            m.CurrentHp = 0;
            Assert.IsFalse(FieldItems.CanUse(s, potion, m), "poção não revive");
            var r = FieldItems.Use(s, feather, m);
            Assert.IsTrue(r.Used);
            Assert.IsTrue(r.Revived);
            Assert.AreEqual((int)Math.Round(m.MaxHp * feather.amount / 100f), m.CurrentHp);
        }

        [Test]
        public void Using_WithoutTheItem_Fails()
        {
            var m = s.Party[0];
            m.CurrentHp = 1;
            var r = FieldItems.Use(s, potion, m);
            Assert.IsFalse(r.Used);
            Assert.AreEqual(1, m.CurrentHp);
        }
    }

    public class AutoSaveTests
    {
        GameDatabase db;
        string path;
        bool previousAutoSave;

        [SetUp]
        public void SetUp()
        {
            db = DefaultContent.Build();
            path = Path.Combine(Path.GetTempPath(), $"oiram-autosave-{Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = path;
            previousAutoSave = SaveSystem.AutoSaveEnabled;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.PathOverride = null;
            SaveSystem.AutoSaveEnabled = previousAutoSave;
            if (File.Exists(path)) File.Delete(path);
        }

        DungeonRun StartRun(GameSession s)
        {
            var crypt = db.Find<LocationDefinition>("loc_cripta");
            var run = new DungeonRun(crypt, DifficultyTier.Hard, 7, 123456, 3) { Floor = 1, BattlesWon = 4, ItemsFound = 2, GoldFound = 55 };
            s.ActiveRun = run;
            s.CurrentLocationId = crypt.id;
            return run;
        }

        [Test]
        public void DungeonSave_RestoresTheSameFloor()
        {
            var s = new GameSession(db, new SeededRandom(8));
            var run = StartRun(s);
            s.Party[0].CurrentHp = 5;
            SaveSystem.AutoSaveEnabled = true;
            Assert.IsTrue(SaveSystem.AutoSave(s, ResumePoint.Dungeon));

            var data = SaveSystem.LoadData();
            var loaded = SaveSystem.Restore(data, db);
            Assert.AreEqual(ResumePoint.Dungeon, SaveSystem.ResumeOf(data, loaded));
            var back = loaded.ActiveRun;
            Assert.IsNotNull(back);
            Assert.AreEqual(run.Location, back.Location);
            Assert.AreEqual(run.Tier, back.Tier);
            Assert.AreEqual(run.Level, back.Level);
            Assert.AreEqual(run.Floors, back.Floors);
            Assert.AreEqual(run.Floor, back.Floor);
            Assert.AreEqual(run.FloorSeed, back.FloorSeed, "mesmo layout do andar");
            Assert.AreEqual(run.ObjectPrefix, back.ObjectPrefix);
            Assert.AreEqual(4, back.BattlesWon);
            Assert.AreEqual(55, back.GoldFound);
            Assert.AreEqual(5, loaded.Party[0].CurrentHp);
            Assert.AreEqual(db.balance.TierMagicFind(DifficultyTier.Hard), loaded.RunMagicFind, "bônus da dificuldade continua valendo");
            StringAssert.Contains("andar 2", SaveSystem.Describe(db));
        }

        [Test]
        public void WorldMapAndInnSaves_DoNotRestoreARun()
        {
            var s = new GameSession(db, new SeededRandom(9));
            StartRun(s);
            SaveSystem.Save(s, "pousada");
            var data = SaveSystem.LoadData();
            var loaded = SaveSystem.Restore(data, db);
            Assert.AreEqual(ResumePoint.Location, SaveSystem.ResumeOf(data, loaded));
            Assert.IsNull(loaded.ActiveRun);

            SaveSystem.Save(s, null, ResumePoint.WorldMap);
            data = SaveSystem.LoadData();
            loaded = SaveSystem.Restore(data, db);
            Assert.AreEqual(ResumePoint.WorldMap, SaveSystem.ResumeOf(data, loaded));
            Assert.IsNull(loaded.ActiveRun);
            StringAssert.Contains("mapa-múndi", SaveSystem.Describe(db));
        }

        [Test]
        public void BrokenRun_FallsBackToWorldMap()
        {
            var s = new GameSession(db, new SeededRandom(10));
            StartRun(s);
            var data = SaveSystem.Capture(s, null, ResumePoint.Dungeon);
            data.run.location = "dungeon_que_nao_existe";
            var loaded = SaveSystem.Restore(data, db);
            Assert.IsNull(loaded.ActiveRun);
            Assert.AreEqual(ResumePoint.WorldMap, SaveSystem.ResumeOf(data, loaded));
        }

        [Test]
        public void OldSaves_WithoutResume_StillLoadInTown()
        {
            File.WriteAllText(path, "{\"version\":1,\"location\":\"loc_vila\",\"spawn\":\"pousada\",\"gold\":42,\"party\":[]}");
            var data = SaveSystem.LoadData();
            var loaded = SaveSystem.Restore(data, db);
            Assert.AreEqual(ResumePoint.Location, SaveSystem.ResumeOf(data, loaded));
            Assert.AreEqual(42, loaded.Inventory.Gold);
            Assert.AreEqual("pousada", loaded.SpawnPointId);
        }

        [Test]
        public void AutoSave_Disabled_WritesNothing()
        {
            SaveSystem.AutoSaveEnabled = false;
            Assert.IsFalse(SaveSystem.AutoSave(new GameSession(db, new SeededRandom(11)), ResumePoint.WorldMap));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void VolumeSteps_Clamp()
        {
            Assert.AreEqual(7, GameSettings.ToSteps(0.7f));
            Assert.AreEqual(1f, GameSettings.FromSteps(14));
            Assert.AreEqual(0f, GameSettings.FromSteps(-3));
        }
    }
}
