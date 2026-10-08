using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oiram.Tests
{
    /// <summary>
    /// Testes de ponta a ponta. Precisam das cenas no Build Settings
    /// (rode OiramRPG ▸ Construir Fatia Vertical antes).
    /// </summary>
    public class VerticalSliceTests
    {
        const float TimeoutSeconds = 90f;

        [SetUp]
        public void SetUp()
        {
            GameSession.StartNew(new GameSession(GameDatabase.Load(), new SeededRandom(7)));
            Time.timeScale = 3f; // acelera as animações (as janelas de timing acompanham a escala)
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        static IEnumerator WaitWhile(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timeout esperando: {what}");
                yield return null;
            }
        }

        static System.Collections.Generic.IEnumerable<T> FindInScene<T>(string sceneName) where T : Component =>
            SceneManager.GetSceneByName(sceneName).GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true));

        [UnityTest]
        public IEnumerator AutoBattle_WinsAndGivesLootXpAndJp()
        {
            var session = GameSession.Current;
            int itemsBefore = session.Inventory.Items.Count;
            int goldBefore = session.Inventory.Gold;

            var load = SceneManager.LoadSceneAsync("Battle_Arena", LoadSceneMode.Single);
            yield return WaitWhile(() => !load.isDone, "carregar Battle_Arena");

            var manager = FindInScene<BattleManager>("Battle_Arena").FirstOrDefault();
            Assert.IsNotNull(manager, "BattleManager na cena");
            manager.StartedExternally = true;

            var encounter = session.Db.Find<EncounterDefinition>("enc_teste");
            _ = manager.Run(new BattleRequest { Encounter = encounter, Commands = new AutoCommandSource(), AutoAdvance = true });
            yield return null;
            yield return WaitWhile(() => manager.IsRunning, "fim da batalha");

            Assert.AreEqual(BattleResult.Victory, manager.LastOutcome.Result);
            Assert.Greater(session.Inventory.Items.Count, itemsBefore, "toda vitória dá pelo menos 1 item");
            Assert.GreaterOrEqual(session.Inventory.Gold, goldBefore);
            Assert.IsTrue(session.Party.All(m => m.Xp > 0 || m.Level > 1), "XP distribuído");
            Assert.IsTrue(session.Party.All(m => m.ProgressFor(m.Job).TotalJp > 0), "JP distribuído");
        }

        [UnityTest]
        public IEnumerator FieldEncounter_LoadsBattleAdditively_AndReturnsToMap()
        {
            var load = SceneManager.LoadSceneAsync("Field_Vale", LoadSceneMode.Single);
            yield return WaitWhile(() => !load.isDone, "carregar Field_Vale");
            yield return null;

            var director = FieldDirector.Instance;
            Assert.IsNotNull(director, "FieldDirector na cena");
            Assert.IsNotNull(director.Player, "jogador na cena");

            var enemy = FindInScene<FieldEnemy>("Field_Vale")
                .FirstOrDefault(e => e.encounter != null && e.encounter.id == "enc_slimes2");
            Assert.IsNotNull(enemy, "grupo de slimes no mapa");
            string enemyId = enemy.uniqueId;

            director.CommandSourceOverride = new AutoCommandSource();
            director.StartBattle(enemy.encounter, enemy, firstStrike: true);
            Assert.IsTrue(director.InBattle);

            yield return WaitWhile(() => !SceneManager.GetSceneByName("Battle_Arena").isLoaded, "abrir a batalha");
            Assert.IsFalse(director.Player.gameObject.activeInHierarchy, "mapa desativado durante a batalha");

            yield return WaitWhile(() => director.InBattle, "voltar ao mapa");

            Assert.IsFalse(SceneManager.GetSceneByName("Battle_Arena").isLoaded, "cena de batalha descarregada");
            Assert.IsTrue(director.Player.gameObject.activeInHierarchy, "mapa reativado");
            Assert.IsTrue(GameSession.Current.ClearedFieldObjects.Contains(enemyId), "inimigo derrotado registrado");
            yield return null;
            Assert.IsTrue(enemy == null, "inimigo derrotado sai do mapa");
        }
    }
}
