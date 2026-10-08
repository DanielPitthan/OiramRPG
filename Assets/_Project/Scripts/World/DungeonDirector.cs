using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.Loot;
using Oiram.UI;
using UnityEngine;

namespace Oiram.World
{
    /// <summary>
    /// Monta o andar atual da dungeon em tempo de execução (layout procedural + inimigos escalados) e cuida
    /// da descida, do chefe, da derrota (volta ao mapa-múndi) e da saída.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DungeonDirector : MonoBehaviour
    {
        [SerializeField] FieldDirector field;
        [SerializeField] GameObject fieldRoot;
        [SerializeField] Camera fieldCamera;
        [SerializeField] Light sun;

        GameSession session;
        DungeonRun run;
        Transform actors;
        Vector3 bossPosition;

        public static DungeonDirector Instance { get; private set; }
        public DungeonRun Run => run;
        public DungeonFloorLayout Layout { get; private set; }

        public void Configure(FieldDirector fieldDirector, GameObject root, Camera camera, Light light)
        {
            field = fieldDirector;
            fieldRoot = root;
            fieldCamera = camera;
            sun = light;
        }

        void Awake()
        {
            Instance = this;
            session = GameSession.Current;
            run = session.ActiveRun ?? DebugRun();
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (field != null) field.BattleEnded -= OnBattleEnded;
        }

        /// <summary>Play direto na cena Dungeon (sem passar pelo mapa-múndi): primeira dungeon, Normal.</summary>
        DungeonRun DebugRun()
        {
            var location = session.Db.locations.First(l => l.IsDungeon);
            var tier = DifficultyTier.Normal;
            int level = EnemyScaling.DungeonLevel(session.PartyLevel, location.dungeon.baseLevel, tier, session.Balance);
            var debug = new DungeonRun(location, tier, level, new System.Random().Next(1, int.MaxValue), session.Balance.TierFloors(tier));
            session.ActiveRun = debug;
            session.CurrentLocationId = location.id;
            return debug;
        }

        void Build()
        {
            var dungeon = run.Dungeon;
            var balance = session.Balance;
            Layout = DungeonGenerator.Generate(run.FloorSeed, dungeon.minRooms, dungeon.maxRooms, run.IsLastFloor, run.Floor == 0);

            var level = BlockTerrain.Group(fieldRoot.transform, "Level");
            BlockTerrain.Build(Layout.Map, level, TerrainTheme.FromDungeon(dungeon));
            actors = BlockTerrain.Group(level, "Actors");

            var rng = new SeededRandom(unchecked(run.FloorSeed ^ 0x5bd1e995));
            FieldPlayerController player = null;
            foreach (var (row, col, c) in Layout.Map.Objects())
            {
                var pos = Layout.Map.TileTop(row, col);
                string id = $"{run.ObjectPrefix}_{row}_{col}";
                switch (c)
                {
                    case 'P':
                        player = WorldProps.Player(actors, pos, session.Db);
                        WorldProps.Spawn(actors, pos, "entrada");
                        break;
                    case 'E': WorldProps.Portal(actors, pos, dungeon.accentColor, completesRun: false); break;
                    case 'D': WorldProps.StairsDown(actors, pos, dungeon.accentColor); break;
                    case 'F': WorldProps.Campfire(actors, pos); break;
                    case 'T': WorldProps.Torch(actors, pos, dungeon.accentColor); break;
                    case 'K': WorldProps.Crate(actors, pos, id, session.Db.Find<LootTable>("lt_caixa"), run.Level); break;
                    case 'e':
                        WorldProps.Enemy(actors, pos, id, DungeonPopulation.RollEncounter(dungeon, run.Level, run.Tier, balance, rng, id), boss: false);
                        break;
                    case 'O':
                        bossPosition = pos;
                        WorldProps.Enemy(actors, pos, id, DungeonPopulation.BossEncounter(dungeon, run.Level, run.Tier, balance, id), boss: true);
                        break;
                    case 'C':
                        var mimic = rng.Chance(0.1f) ? DungeonPopulation.MimicEncounter(session.Db, run.Level, run.Tier, balance, id) : null;
                        WorldProps.Chest(actors, pos, id, dungeon.chestLoot, run.Level, mimic);
                        break;
                }
            }

            // Clima do tema.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = dungeon.ambientColor;
            if (fieldCamera != null)
            {
                fieldCamera.backgroundColor = dungeon.skyColor;
                var iso = fieldCamera.GetComponent<IsoCamera>();
                if (iso != null && player != null)
                {
                    iso.target = player.transform;
                    iso.Snap();
                }
            }
            if (sun != null)
            {
                sun.intensity = 0.55f;
                sun.color = Color.Lerp(Color.white, dungeon.accentColor, 0.25f);
            }

            field.Configure(fieldRoot, player, run.Location.id);
            field.Theme = new BattleTheme
            {
                Ground = dungeon.floorColor,
                Cliff = dungeon.sideColor,
                Sky = dungeon.skyColor,
                Ambient = dungeon.ambientColor,
                Indoor = true,
            };
            field.DefeatHandler = OnDefeat;
            field.BattleEnded += OnBattleEnded;
        }

        void Start()
        {
            field.ShowToast($"{run.Dungeon.displayName} — andar {run.Floor + 1}/{run.Floors}", "gold-text");
            field.ShowToast($"{BalanceConfig.TierName(run.Tier)} · inimigos Nv {run.Level}" + (run.IsLastFloor ? " · o chefe espera no fim!" : ""), "muted");
        }

        void OnBattleEnded(EncounterDefinition encounter, BattleOutcome outcome)
        {
            if (!encounter.isBoss || outcome.Result != BattleResult.Victory) return;
            run.BossDefeated = true;
            var chestPos = bossPosition + new Vector3(-1f, 0f, 1f);
            WorldProps.Chest(actors, chestPos, $"{run.ObjectPrefix}_tesouro", DungeonPopulation.BossChestTable(run.Dungeon, run.Tier, session.Balance), run.Level + 1);
            WorldProps.Portal(actors, bossPosition + new Vector3(1f, 0f, -1f), run.Dungeon.accentColor, completesRun: true);
            field.ShowToast("Chefe derrotado! Abra o tesouro e saia pelo portal.", "gold-text");
        }

        bool OnDefeat(EncounterDefinition encounter)
        {
            session.RestoreAll();
            session.PendingMessage = $"A party desmaiou na {run.Dungeon.displayName} e acordou no mapa-múndi.";
            EndRun();
            SceneFlow.ToWorldMap();
            return true;
        }

        public void Descend()
        {
            if (run.IsLastFloor || SceneFlow.IsTransitioning) return;
            run.Floor++;
            PlaytestLog.Note("dungeon_andar", $"{run.Location.id} andar {run.Floor + 1}/{run.Floors}");
            SceneFlow.LoadScene(SceneFlow.DungeonScene);
        }

        public void Leave(bool completed)
        {
            if (SceneFlow.IsTransitioning) return;
            if (completed && run.BossDefeated)
            {
                session.RecordDungeonClear(run.Location, run.Tier);
                session.PendingMessage = $"{run.Dungeon.displayName} concluída no {BalanceConfig.TierName(run.Tier)}! " +
                                         $"{run.BattlesWon} batalhas, {run.ItemsFound} itens do mapa. O ferreiro da vila renovou o estoque.";
            }
            else session.PendingMessage = $"Você saiu da {run.Dungeon.displayName}.";
            EndRun();
            SceneFlow.ToWorldMap();
        }

        void EndRun()
        {
            PlaytestLog.Write(new PlaytestEvent
            {
                type = "dungeon_fim",
                encounter = run.Location.id,
                result = run.BossDefeated ? "Victory" : "Left",
                detail = $"{BalanceConfig.TierName(run.Tier)} · Nv {run.Level} · andar {run.Floor + 1}/{run.Floors} · {run.BattlesWon} batalhas",
                levels = string.Join("/", session.Party.Select(m => m.Level)),
            });
            session.ShopRefreshCount++;
            session.ActiveRun = null;
        }
    }
}
