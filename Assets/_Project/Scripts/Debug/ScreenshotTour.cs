using System;
using System.IO;
using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Balance;
using Oiram.Field;
using Oiram.Loot;
using Oiram.UI;
using Oiram.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Oiram.DevTools
{
    /// <summary>
    /// Passeio automático que tira screenshots das telas principais usando um teclado virtual.
    /// Só roda com o argumento de linha de comando <c>-oiram-tour [pasta]</c>; depois fecha o jogo.
    /// </summary>
    public sealed class ScreenshotTour : MonoBehaviour
    {
        const string Flag = "-oiram-tour";

        string folder;
        Keyboard keyboard;
        int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0) return;
            var go = new GameObject("ScreenshotTour");
            DontDestroyOnLoad(go);
            var tour = go.AddComponent<ScreenshotTour>();
            tour.folder = index + 1 < args.Length && !args[index + 1].StartsWith("-")
                ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "tour");
        }

        async void Start()
        {
            try
            {
                Directory.CreateDirectory(folder);
                // O tour não é playtest de verdade; com -tour-log ele grava numa subpasta própria (para testar o analisador).
                bool keepLog = Array.IndexOf(Environment.GetCommandLineArgs(), "-tour-log") >= 0;
                PlaytestLog.Enabled = keepLog;
                if (keepLog) PlaytestLog.FolderOverride = Path.Combine(folder, "playtest");
                // O tour usa um teclado virtual: não pode depender do foco da janela.
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard = InputSystem.AddDevice<Keyboard>("TourKeyboard");
                await Run();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(Path.Combine(folder, "tour-error.txt"), e.ToString());
            }
            Application.Quit();
        }

        /// <summary>Espera em tempo real (o menu de pausa zera o timeScale).</summary>
        static async Awaitable Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
        }

        void SetKey(Key key, bool down)
        {
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                keyboard[key].WriteValueIntoEvent(down ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        async Awaitable Press(Key key)
        {
            SetKey(key, true);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            SetKey(key, false);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        async Awaitable Shot(string name)
        {
            await Awaitable.EndOfFrameAsync();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
        }

        static async Awaitable WaitForScene(string name)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while ((SceneManager.GetActiveScene().name != name || SceneFlow.IsTransitioning) && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            await Wait(1.2f);
        }

        static T Find<T>() where T : Component =>
            SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).FirstOrDefault();

        async Awaitable Run()
        {
            FieldEnemy.AiPaused = true; // fotos sem encontros acidentais

            // ---------------- título
            await WaitForScene(SceneFlow.TitleScene);
            await Shot("titulo");

            var db = GameDatabase.Load();
            var session = new GameSession(db, new SeededRandom(7));
            GameSession.StartNew(session);
            SceneFlow.EnterLocation(db.startLocation);
            await WaitForScene("Field_Vale");
            await Shot("vale");

            for (int i = 0; i < 12; i++) session.Inventory.TryAdd(session.Loot.CreateRandom(3, 150f));
            await Press(Key.Tab);
            await Press(Key.E);
            await Press(Key.DownArrow);
            await Wait(0.3f);
            await Shot("menu_inventario");
            await Press(Key.Tab);
            await Wait(0.3f);

            // ---------------- batalha do Vale com comandos de verdade
            var director = FieldDirector.Instance;
            director.StartBattle(db.Find<EncounterDefinition>("enc_goblin_slime"), null, false);
            while (!SceneManager.GetSceneByName("Battle_Arena").isLoaded) await Awaitable.NextFrameAsync();
            var manager = SceneManager.GetSceneByName("Battle_Arena").GetRootGameObjects().Select(g => g.GetComponentInChildren<BattleManager>()).First(m => m != null);
            bool tookMenu = false, tookVictory = false;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (director.InBattle && Time.realtimeSinceStartup < deadline)
            {
                if (manager.Units.Where(u => u.Side == Side.Enemies).All(u => !u.IsAlive) && !tookVictory)
                {
                    await Wait(3.5f);
                    await Shot("vale_vitoria");
                    tookVictory = true;
                }
                if (!tookMenu)
                {
                    await Wait(3f);
                    await Shot("vale_batalha");
                    tookMenu = true;
                }
                await Press(Key.Enter);
                await Wait(0.35f);
                await Press(Key.Enter);
                await Wait(0.5f);
            }
            await Wait(1f);

            // ---------------- party mais forte para visitar o mundo
            foreach (var m in session.Party)
                while (m.Level < 5) m.GainXp(m.XpToNext - m.Xp);
            PartyManager.Manage(session);
            session.RestoreAll();
            session.Inventory.Gold += 3000;
            session.ClearedLocations.Add("loc_vale");
            session.CurrentLocationId = "loc_vale";

            SceneFlow.ToWorldMap();
            await WaitForScene(SceneFlow.WorldMapScene);
            await Shot("mundo_vale");
            await Press(Key.UpArrow);
            await Wait(1.5f);
            await Shot("mundo_vila");
            await Press(Key.RightArrow);
            await Wait(1.8f);
            await Press(Key.Enter);
            await Wait(0.5f);
            await Shot("mundo_dificuldade");
            await Press(Key.Escape);
            await Wait(0.3f);

            // ---------------- cidade
            SceneFlow.EnterLocation(db.Find<LocationDefinition>("loc_vila"), "saida");
            await WaitForScene("Town_Vila");
            await Shot("vila");
            director = FieldDirector.Instance;
            var smith = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ShopKeeper>(true)).First(k => k.kind == ShopKind.Blacksmith);
            director.Player.Teleport(smith.transform.position + new Vector3(-1.6f, 0.1f, -1.6f));
            Camera.main.GetComponent<IsoCamera>().Snap();
            await Wait(0.6f);
            await Shot("vila_ferreiro_balcao");
            ShopMenu.Open(ShopKind.Blacksmith, "loc_vila", "Ferreiro Bruno");
            await Wait(0.5f);
            await Shot("loja_ferreiro");
            await Press(Key.Escape);
            ShopMenu.Open(ShopKind.Gambler, "loc_vila", "Apostador Lupo");
            await Wait(0.3f);
            await Press(Key.DownArrow);
            await Press(Key.Enter);
            await Wait(0.5f);
            await Shot("loja_apostador");
            await Press(Key.Escape);
            ShopMenu.Open(ShopKind.Inn, "loc_vila", "Pousada do Vento");
            await Wait(0.4f);
            await Shot("loja_pousada");
            await Press(Key.Escape);
            await Wait(0.3f);

            var villager = Find<Villager>();
            if (villager != null)
            {
                director.Player.Teleport(villager.transform.position + new Vector3(-1.2f, 0.1f, -1.2f));
                Camera.main.GetComponent<IsoCamera>().Snap();
                villager.Interact(director.Player);
                await Wait(0.5f);
                await Shot("vila_morador");
                await Press(Key.Escape);
                await Wait(0.3f);
            }

            var chest = Find<HiddenChest>();
            if (chest != null)
            {
                director.Player.Teleport(chest.transform.position - Vector3.up * 2.4f + new Vector3(-1f, 0f, -1f));
                Camera.main.GetComponent<IsoCamera>().Snap();
                await Wait(0.5f);
                chest.Bump(director.Player);
                await Wait(1.2f);
                await Shot("vila_bau_escondido");
            }
            await Press(Key.Tab);
            await Press(Key.E);
            await Wait(0.3f);
            await Shot("menu_reliquia");
            await Press(Key.Tab);
            await Wait(0.3f);

            // ---------------- dungeon
            SceneFlow.EnterDungeon(db.Find<LocationDefinition>("loc_mina"), DifficultyTier.Normal);
            await WaitForScene(SceneFlow.DungeonScene);
            await Shot("dungeon_andar1");

            director = FieldDirector.Instance;
            director.CommandSourceOverride = new AutoCommandSource();
            var enemy = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FieldEnemy>(true)).First(e => !e.encounter.isBoss);
            director.StartBattle(enemy.encounter, enemy, true);
            while (!SceneManager.GetSceneByName("Battle_Arena").isLoaded) await Awaitable.NextFrameAsync();
            await Wait(2.5f);
            await Shot("dungeon_batalha");
            deadline = Time.realtimeSinceStartup + 120f;
            while (director.InBattle && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
            await Wait(1f);

            var run = session.ActiveRun;
            while (run != null && !run.IsLastFloor)
            {
                DungeonDirector.Instance.Descend();
                int floor = run.Floor;
                while ((DungeonDirector.Instance == null || DungeonDirector.Instance.Run.Floor != floor || SceneFlow.IsTransitioning)) await Awaitable.NextFrameAsync();
                await Wait(1.2f);
            }
            var boss = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FieldEnemy>(true)).FirstOrDefault(e => e.encounter.isBoss);
            if (boss != null)
            {
                FieldDirector.Instance.Player.Teleport(boss.transform.position + new Vector3(-3f, 0.1f, -3f));
                Camera.main.GetComponent<IsoCamera>().Snap();
                await Wait(0.6f);
                await Shot("dungeon_chefe");
            }
        }
    }
}
