using System;
using System.Linq;
using Oiram.Audio;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using Oiram.UI;
using Oiram.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Oiram.Field
{
    /// <summary>
    /// Orquestra a exploração (Vale, cidades e andares de dungeon): HUD, menu de pausa, loot no mapa,
    /// ponto de chegada e a ida/volta das batalhas. A cena de batalha é carregada de forma aditiva;
    /// o mapa só é desativado, então tudo fica como estava.
    /// </summary>
    public sealed class FieldDirector : MonoBehaviour
    {
        [SerializeField] GameObject fieldRoot;
        [SerializeField] FieldPlayerController player;
        [SerializeField] string battleScene = "Battle_Arena";
        [Tooltip("Id do LocationDefinition desta cena (loc_vale, loc_vila...).")]
        [SerializeField] string locationId;

        FieldHud hud;
        PauseMenu pauseMenu;
        bool pauseWasOpen;
        string lookJobId;
        MusicTrack music = MusicTrack.Field;

        public static FieldDirector Instance { get; private set; }
        public FieldPlayerController Player => player;
        public string LocationId => locationId;
        public FieldHud Hud => hud;
        public bool InBattle { get; private set; }
        bool modalOpen;
        int modalClosedFrame = -1;

        /// <summary>Menus de loja/diálogo abertos travam o movimento também.</summary>
        public bool ModalOpen
        {
            get => modalOpen;
            set
            {
                // O aperto que fecha o diálogo não deve virar pulo/interação no mesmo frame.
                if (modalOpen && !value) modalClosedFrame = Time.frameCount;
                modalOpen = value;
            }
        }

        public bool InputLocked => InBattle || modalOpen || Time.frameCount == modalClosedFrame || (pauseMenu != null && pauseMenu.IsOpen);
        /// <summary>Se definido, as batalhas usam estes comandos e pulam as telas de resultado (testes).</summary>
        public IBattleCommandSource CommandSourceOverride { get; set; }
        /// <summary>Cores da arena de batalha (dungeons). Null = arena padrão.</summary>
        public BattleTheme? Theme { get; set; }
        /// <summary>Chamado ao voltar de cada batalha (a dungeon usa para saber do chefe).</summary>
        public event Action<EncounterDefinition, BattleOutcome> BattleEnded;
        /// <summary>Se retornar true, a derrota foi tratada por quem registrou (dungeon expulsa a party).</summary>
        public Func<EncounterDefinition, bool> DefeatHandler { get; set; }

        public void Configure(GameObject root, FieldPlayerController playerController, string location = null)
        {
            fieldRoot = root;
            player = playerController;
            if (location != null) locationId = location;
        }

        void Awake()
        {
            Instance = this;
            GameInput.EnsureInitialized();
            hud = FieldHud.Create(transform);
            pauseMenu = PauseMenu.Create(transform, hud);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (pauseMenu != null && pauseMenu.IsOpen) Time.timeScale = 1f;
        }

        void Start()
        {
            var session = GameSession.Current;
            if (!string.IsNullOrEmpty(locationId) && session.ActiveRun == null) session.CurrentLocationId = locationId;
            PlaceAtSpawnPoint(session);
            RefreshPlayerLook();

            hud.Refresh();
            var location = session.Db.Find<LocationDefinition>(locationId);
            music = location == null ? MusicTrack.Field : location.kind switch
            {
                LocationKind.Town => MusicTrack.Town,
                LocationKind.Dungeon => MusicTrack.Dungeon,
                _ => MusicTrack.Field,
            };
            AudioManager.PlayMusic(music);
            if (location != null && session.ActiveRun == null)
            {
                bool firstVisitToVale = location == session.Db.startLocation && !session.ClearedLocations.Contains(location.id);
                hud.Toast(firstVisitToVale ? "Explore o vale, derrote o Golem e junte muito loot!" : location.displayName, "gold-text", 5f);
            }
            _ = ScreenFader.FadeIn(0.4f, destroyCancellationToken);
        }

        void PlaceAtSpawnPoint(GameSession session)
        {
            string id = session.SpawnPointId;
            session.SpawnPointId = null;
            if (string.IsNullOrEmpty(id) || player == null) return;
            var spawn = gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<SpawnPoint>(true))
                .FirstOrDefault(s => s.id == id);
            if (spawn == null) return;
            player.Teleport(spawn.transform.position + Vector3.up * 0.05f);
            var cam = Camera.main != null ? Camera.main.GetComponent<IsoCamera>() : null;
            if (cam != null) cam.Snap();
        }

        /// <summary>O herói do mapa veste o chapéu e a arma do job atual do líder.</summary>
        void RefreshPlayerLook()
        {
            var session = GameSession.Current;
            if (player == null || session == null || session.Party.Count == 0) return;
            var leader = session.Party[0];
            if (leader.Job == null || leader.Job.id == lookJobId) return;
            lookJobId = leader.Job.id;
            player.SetVisual(WorldProps.LeaderVisual(player.transform, leader));
        }

        void Update()
        {
            bool pauseOpen = pauseMenu != null && pauseMenu.IsOpen;
            if (pauseWasOpen && !pauseOpen) RefreshPlayerLook();
            pauseWasOpen = pauseOpen;
            if (InBattle || ModalOpen) return;
            if (!pauseMenu.IsOpen && !InputLocked && pauseMenu.ClosedFrame != Time.frameCount && GameInput.OpenMenuDown)
            {
                pauseMenu.Open();
                hud.SetPrompt(null);
                return;
            }
            if (!pauseMenu.IsOpen && player != null)
            {
                var nearest = FieldInteractable.Nearest(player.transform.position);
                hud.SetPrompt(nearest != null ? $"E: {nearest.Prompt}" : null);
            }
        }

        public void ShowToast(string text, string cssClass = null) => hud.Toast(text, cssClass);

        /// <summary>Coloca o loot na mochila e avisa o jogador (baús, caixas...).</summary>
        public void GiveLoot(LootDrop drop, Vector3 at)
        {
            var session = GameSession.Current;
            var overflow = session.Inventory.AddLoot(drop);
            if (session.ActiveRun != null)
            {
                session.ActiveRun.ItemsFound += drop.Items.Count - overflow.Count;
                session.ActiveRun.GoldFound += drop.Gold;
            }
            PlaytestLog.Write(new PlaytestEvent
            {
                type = "loot_mapa",
                gold = drop.Gold,
                items = drop.Items.Count,
                itemsRarePlus = drop.Items.Count(i => i.Rarity >= Rarity.Rare),
                detail = string.Join(", ", drop.Items.Select(i => $"{i.Name} [{i.Rarity}]")),
            });
            if (drop.BestRarity is Rarity loudest) AudioManager.PlayLoot(loudest);
            else if (drop.Gold > 0) AudioManager.Play(Sfx.Coin);
            if (drop.Gold > 0) hud.Toast($"+{drop.Gold} ouro", "gold-text");
            foreach (var item in drop.Items.Where(i => !overflow.Contains(i)).OrderByDescending(i => i.Rarity))
                hud.Toast($"{item.Name}  ({RarityInfo.Name(item.Rarity)}, Nv {item.ItemLevel})", RarityInfo.UssClass(item.Rarity));
            foreach (var consumable in drop.Consumables)
                hud.Toast(consumable.displayName);
            if (overflow.Count > 0) hud.Toast($"Mochila cheia! {overflow.Count} item(ns) perdidos.", "bad");

            if (drop.BestRarity is Rarity best)
                _ = Shapes.LootBeam(fieldRoot != null ? fieldRoot.transform : transform, at, RarityInfo.Color(best), destroyCancellationToken);
            hud.Refresh();
        }

        public void StartBattle(EncounterDefinition encounter, IBattleSource source, bool firstStrike)
        {
            if (InBattle || encounter == null || SceneFlow.IsTransitioning) return;
            RunBattle(encounter, source, firstStrike);
        }

        async void RunBattle(EncounterDefinition encounter, IBattleSource source, bool firstStrike)
        {
            InBattle = true;
            var ct = destroyCancellationToken;
            try
            {
                hud.SetPrompt(null);
                AudioManager.Play(Sfx.Encounter);
                AudioManager.StopMusic(0.3f);
                await ScreenFader.FadeOut(0.35f, ct);

                var fieldScene = gameObject.scene;
                var load = SceneManager.LoadSceneAsync(battleScene, LoadSceneMode.Additive);
                if (load == null) throw new InvalidOperationException($"Cena '{battleScene}' não está no Build Settings.");
                while (!load.isDone) await Awaitable.NextFrameAsync(ct);

                var scene = SceneManager.GetSceneByName(battleScene);
                var manager = scene.GetRootGameObjects()
                    .Select(go => go.GetComponentInChildren<BattleManager>(true))
                    .FirstOrDefault(m => m != null);
                if (manager == null) throw new InvalidOperationException($"BattleManager não encontrado em '{battleScene}'.");

                manager.StartedExternally = true;
                SceneManager.SetActiveScene(scene);
                if (fieldRoot) fieldRoot.SetActive(false);
                hud.SetVisible(false);

                var fadeIn = ScreenFader.FadeIn(0.3f, ct);
                var outcome = await manager.Run(new BattleRequest
                {
                    Encounter = encounter,
                    FirstStrike = firstStrike,
                    Commands = CommandSourceOverride,
                    AutoAdvance = CommandSourceOverride != null,
                    Theme = Theme,
                });
                await fadeIn;

                await ScreenFader.FadeOut(0.3f, ct);
                SceneManager.SetActiveScene(fieldScene);
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) await Awaitable.NextFrameAsync(ct);

                if (fieldRoot) fieldRoot.SetActive(true);
                hud.SetVisible(true);
                if (outcome.Result != BattleResult.Defeat || DefeatHandler == null) AudioManager.PlayMusic(music, 0.6f);

                var session = GameSession.Current;
                bool handled = false;
                if (outcome.Result == BattleResult.Defeat)
                {
                    PlaytestLog.Note("derrota", encounter.id);
                    handled = DefeatHandler != null && DefeatHandler(encounter);
                    if (!handled)
                    {
                        session.RestoreAll();
                        player.RespawnAtStart();
                    }
                }
                if (!handled && (source is Object unityObject ? unityObject != null : source != null))
                    source.OnBattleEnded(outcome.Result);

                if (outcome.Result == BattleResult.Victory)
                {
                    if (session.ActiveRun != null) session.ActiveRun.BattlesWon++;
                    else if (encounter.isBoss)
                    {
                        session.MarkLocationCleared(locationId);
                        hud.Toast($"{encounter.displayName} derrotado! A saída para o mapa-múndi está livre.", "gold-text", 8f);
                    }
                }
                BattleEnded?.Invoke(encounter, outcome);
                hud.Refresh();
                if (!handled) await ScreenFader.FadeIn(0.35f, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (fieldRoot) fieldRoot.SetActive(true);
                hud.SetVisible(true);
                _ = ScreenFader.FadeIn(0.2f);
            }
            finally
            {
                InBattle = false;
            }
        }
    }
}
