using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oiram.Core;
using Oiram.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oiram.Tests
{
    /// <summary>Salvamento automático no começo do andar e no mapa-múndi; "Continuar" volta para o mesmo lugar.</summary>
    public class AutoSaveFlowTests
    {
        const float Timeout = 60f;
        string path;
        bool previousAutoSave;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 3f;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneFlow.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            path = Path.Combine(Path.GetTempPath(), $"oiram-flow-save-{System.Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = path;
            previousAutoSave = SaveSystem.AutoSaveEnabled;
            SaveSystem.AutoSaveEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            SaveSystem.PathOverride = null;
            SaveSystem.AutoSaveEnabled = previousAutoSave;
            if (File.Exists(path)) File.Delete(path);
        }

        static IEnumerator WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (SceneManager.GetActiveScene().name != name || SceneFlow.IsTransitioning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timeout esperando a cena {name}");
                yield return null;
            }
            yield return null;
            yield return null;
        }

        static string Signature(DungeonFloorLayout layout) =>
            $"{layout.Map.FloorCount()} {layout.Rooms.Count} {layout.Entrance} {layout.Goal} {string.Join("", layout.Map.Objects().Select(o => o.c))}";

        [UnityTest]
        public IEnumerator Continue_ResumesAtTheSavedDungeonFloor_ThenAtTheWorldMap()
        {
            var db = GameDatabase.Load();
            var session = new GameSession(db, new SeededRandom(17));
            foreach (var m in session.Party)
                while (m.Level < 5) m.GainXp(m.XpToNext - m.Xp);
            session.ClearedLocations.Add("loc_vale");
            GameSession.StartNew(session);

            var run = SceneFlow.EnterDungeon(db.Find<LocationDefinition>("loc_mina"), DifficultyTier.Normal);
            yield return WaitForScene(SceneFlow.DungeonScene);
            Assert.IsTrue(File.Exists(path), "salvou ao chegar no andar 1");

            DungeonDirector.Instance.Descend();
            yield return WaitForScene(SceneFlow.DungeonScene);
            Assert.AreEqual(1, DungeonDirector.Instance.Run.Floor);
            var layoutBefore = Signature(DungeonDirector.Instance.Layout);

            // "Fecha o jogo": sessão nova em folha, depois Continuar.
            GameSession.StartNew(new GameSession(db, new SeededRandom(99)));
            Assert.IsTrue(SceneFlow.Continue(db));
            yield return WaitForScene(SceneFlow.DungeonScene);

            var resumed = DungeonDirector.Instance;
            Assert.AreEqual(1, resumed.Run.Floor, "voltou no andar 2");
            Assert.AreEqual(run.Seed, resumed.Run.Seed);
            Assert.AreEqual(run.Level, resumed.Run.Level);
            Assert.AreEqual(layoutBefore, Signature(resumed.Layout), "mesmo layout");
            Assert.AreEqual(5, GameSession.Current.Party[0].Level, "party do save");

            resumed.Leave(completed: false);
            yield return WaitForScene(SceneFlow.WorldMapScene);
            Assert.AreEqual(ResumePoint.WorldMap, SaveSystem.LoadData().resume, "salvou no mapa-múndi");

            GameSession.StartNew(new GameSession(db, new SeededRandom(100)));
            Assert.IsTrue(SceneFlow.Continue(db));
            yield return WaitForScene(SceneFlow.WorldMapScene);
            Assert.AreEqual("loc_mina", GameSession.Current.CurrentLocationId, "herói no ponto da mina");
            Assert.IsNull(GameSession.Current.ActiveRun);
        }
    }
}
