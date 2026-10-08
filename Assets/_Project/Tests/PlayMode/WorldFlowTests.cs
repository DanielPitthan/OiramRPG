using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oiram.Balance;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.Loot;
using Oiram.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oiram.Tests
{
    /// <summary>Fluxos do mundo: título, mapa-múndi, cidade (baú escondido) e uma dungeon do começo ao fim.</summary>
    public class WorldFlowTests
    {
        const float Timeout = 120f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 3f;
            // Uma transição pendente de um teste anterior não pode vazar para este.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneFlow.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timeout esperando: {what}");
                yield return null;
            }
        }

        static IEnumerator WaitForScene(string name)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == name && !SceneFlow.IsTransitioning, $"cena {name}");
            yield return null;
            yield return null;
        }

        static T Find<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).FirstOrDefault();

        static GameSession StrongParty(int seed, int level)
        {
            var db = GameDatabase.Load();
            var s = new GameSession(db, new SeededRandom(seed));
            foreach (var m in s.Party)
                while (m.Level < level) m.GainXp(m.XpToNext - m.Xp);
            foreach (var slot in new[] { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Accessory })
                for (int i = 0; i < 3; i++)
                    s.Inventory.TryAdd(s.Loot.CreateRandom(level, 200f, Rarity.Epic, db.items.Where(b => b.slot == slot).ToList()));
            PartyManager.Manage(s);
            s.RestoreAll();
            s.ClearedLocations.Add("loc_vale");
            GameSession.StartNew(s);
            return s;
        }

        [UnityTest]
        public IEnumerator TitleAndWorldMap_OpenWithoutErrors()
        {
            GameSession.StartNew(new GameSession(GameDatabase.Load(), new SeededRandom(1)));
            SceneManager.LoadScene(SceneFlow.TitleScene);
            yield return WaitForScene(SceneFlow.TitleScene);
            Assert.IsNotNull(Find<TitleDirector>());

            SceneFlow.ToWorldMap();
            yield return WaitForScene(SceneFlow.WorldMapScene);
            Assert.IsNotNull(Find<WorldMapDirector>());
            yield return new WaitForSecondsRealtime(0.3f);
        }

        [UnityTest]
        public IEnumerator Town_SpawnsAtExit_AndHiddenChestGivesRelic()
        {
            var session = StrongParty(5, 3);
            var town = session.Db.Find<LocationDefinition>("loc_vila");
            SceneFlow.EnterLocation(town, "saida");
            yield return WaitForScene("Town_Vila");

            var director = FieldDirector.Instance;
            Assert.IsNotNull(director);
            Assert.AreEqual("loc_vila", director.LocationId);
            var spawn = Find<SpawnPoint>();
            Assert.IsNotNull(spawn);
            var exit = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpawnPoint>(true)).First(s => s.id == "saida");
            Assert.Less(Vector3.Distance(director.Player.transform.position, exit.transform.position), 1f, "chega pela placa de saída");

            Assert.AreEqual(4, SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ShopKeeper>(true)).Count(), "4 lojas");

            var chest = Find<HiddenChest>();
            Assert.IsNotNull(chest, "baú escondido na cidade");
            int before = session.Inventory.Items.Count;
            chest.Bump(director.Player);
            Assert.AreEqual(before + 1, session.Inventory.Items.Count);
            Assert.AreEqual(Rarity.Relic, session.Inventory.Items.Last().Rarity);
            chest.Bump(director.Player);
            Assert.AreEqual(before + 1, session.Inventory.Items.Count, "só uma vez");
        }

        [UnityTest]
        public IEnumerator Dungeon_FullRun_FromEntranceToVictoryPortal()
        {
            var session = StrongParty(11, 8);
            var mine = session.Db.Find<LocationDefinition>("loc_mina");
            var run = SceneFlow.EnterDungeon(mine, DifficultyTier.Easy);
            Assert.AreEqual(2, run.Floors);
            yield return WaitForScene(SceneFlow.DungeonScene);

            var dungeon = DungeonDirector.Instance;
            Assert.IsNotNull(dungeon);
            Assert.AreEqual(0, dungeon.Run.Floor);
            var director = FieldDirector.Instance;
            director.CommandSourceOverride = new AutoCommandSource();

            var enemy = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FieldEnemy>(true))
                .First(e => !e.encounter.isBoss);
            Assert.AreEqual(run.Level, enemy.encounter.enemies[0].level, "inimigos no nível da descida");
            director.StartBattle(enemy.encounter, enemy, firstStrike: true);
            yield return WaitUntil(() => !director.InBattle, "batalha comum");
            Assert.AreEqual(1, run.BattlesWon);

            dungeon.Descend();
            yield return WaitUntil(() => DungeonDirector.Instance != null && DungeonDirector.Instance != dungeon && !SceneFlow.IsTransitioning, "próximo andar");
            yield return null;
            dungeon = DungeonDirector.Instance;
            director = FieldDirector.Instance;
            director.CommandSourceOverride = new AutoCommandSource();
            Assert.AreEqual(1, dungeon.Run.Floor);
            Assert.IsTrue(dungeon.Run.IsLastFloor);

            var boss = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FieldEnemy>(true))
                .First(e => e.encounter.isBoss);
            Assert.AreEqual(3, boss.encounter.enemies.Count, "chefe com dois capangas");
            // Este teste cobre o fluxo, não o balanceamento (BalanceGuardTests cuida disso): chefe enfraquecido.
            foreach (var e in boss.encounter.enemies) e.stats = new PrimaryStats(5, 1, 1, 1, 1, 1);
            director.StartBattle(boss.encounter, boss, firstStrike: false);
            yield return WaitUntil(() => !director.InBattle, "batalha do chefe");
            Assert.IsTrue(run.BossDefeated, "chefe derrotado");

            var portal = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DungeonPortal>(true))
                .FirstOrDefault(p => p.completesRun);
            Assert.IsNotNull(portal, "portal de vitória aparece após o chefe");
            int refresh = session.ShopRefreshCount;
            portal.Interact(director.Player);
            yield return WaitForScene(SceneFlow.WorldMapScene);

            Assert.IsNull(session.ActiveRun);
            Assert.IsTrue(session.ClearedLocations.Contains("loc_mina"));
            Assert.AreEqual(DifficultyTier.Easy, session.BestTier["loc_mina"]);
            Assert.AreEqual(refresh + 1, session.ShopRefreshCount, "ferreiro renova");
            Assert.IsTrue(session.IsLocationUnlocked(session.Db.Find<LocationDefinition>("loc_cripta")), "próxima dungeon liberada");
        }
    }
}
