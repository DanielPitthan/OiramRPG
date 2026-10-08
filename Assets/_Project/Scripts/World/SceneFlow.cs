using System;
using Oiram.Battle;
using Oiram.Core;
using Oiram.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oiram.World
{
    /// <summary>Navegação entre cenas: título, mapa-múndi, campos, cidades e dungeons (com fade).</summary>
    public static class SceneFlow
    {
        public const string TitleScene = "Title";
        public const string WorldMapScene = "WorldMap";
        public const string DungeonScene = "Dungeon";

        public static bool IsTransitioning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsTransitioning = false;

        public static void LoadScene(string scene)
        {
            if (IsTransitioning || string.IsNullOrEmpty(scene)) return;
            Run(scene);
        }

        static async void Run(string scene)
        {
            IsTransitioning = true;
            try
            {
                Time.timeScale = 1f;
                await ScreenFader.FadeOut(0.35f);
                var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                if (op == null) throw new InvalidOperationException($"Cena '{scene}' não está no Build Settings.");
                while (!op.isDone) await Awaitable.NextFrameAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ = ScreenFader.FadeIn(0.2f);
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        /// <summary>Entra num campo ou cidade. <paramref name="spawnId"/> escolhe o ponto de chegada na cena.</summary>
        public static void EnterLocation(LocationDefinition location, string spawnId = null)
        {
            var session = GameSession.Current;
            session.CurrentLocationId = location.id;
            session.SpawnPointId = spawnId;
            session.ActiveRun = null;
            PlaytestLog.Note("local", location.id);
            LoadScene(location.sceneName);
        }

        /// <summary>Começa uma descida: o nível dos inimigos é fixado agora (nível da party + dificuldade).</summary>
        public static DungeonRun EnterDungeon(LocationDefinition location, DifficultyTier tier)
        {
            var session = GameSession.Current;
            var balance = session.Balance;
            int level = EnemyScaling.DungeonLevel(session.PartyLevel, location.dungeon.baseLevel, tier, balance);
            var run = new DungeonRun(location, tier, level, session.Rng.Range(1, int.MaxValue), balance.TierFloors(tier));
            session.ActiveRun = run;
            session.CurrentLocationId = location.id;
            PlaytestLog.Write(new PlaytestEvent
            {
                type = "dungeon_inicio",
                encounter = location.id,
                detail = $"{BalanceConfig.TierName(tier)} · inimigos Nv {level} · party Nv {session.PartyLevel} · {run.Floors} andares",
            });
            LoadScene(location.sceneName);
            return run;
        }

        public static void ToWorldMap() => LoadScene(WorldMapScene);

        /// <summary>"Continuar" da tela de título: carrega o save e volta para onde parou. False se não há save válido.</summary>
        public static bool Continue(GameDatabase db)
        {
            var data = SaveSystem.LoadData();
            if (data == null) return false;
            var session = SaveSystem.Restore(data, db);
            GameSession.StartNew(session);
            var resume = SaveSystem.ResumeOf(data, session);
            PlaytestLog.Note("continuar", $"{resume} {session.CurrentLocationId}");
            switch (resume)
            {
                case ResumePoint.Dungeon:
                    session.CurrentLocationId = session.ActiveRun.Location.id;
                    LoadScene(session.ActiveRun.Location.sceneName);
                    break;
                case ResumePoint.WorldMap:
                    session.ActiveRun = null;
                    ToWorldMap();
                    break;
                default:
                    var location = db.Find<LocationDefinition>(session.CurrentLocationId) ?? db.startLocation;
                    EnterLocation(location, session.SpawnPointId ?? "pousada");
                    break;
            }
            return true;
        }
    }
}
