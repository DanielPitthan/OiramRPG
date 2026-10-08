using System.Collections.Generic;
using System.Linq;
using Oiram.Audio;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.World
{
    /// <summary>
    /// Mapa-múndi estilo Mario RPG: pontos (Vale, cidades, dungeons) ligados por caminhos.
    /// Setas movem o herói para o vizinho naquela direção; Confirmar entra (dungeons pedem a dificuldade).
    /// </summary>
    public sealed class WorldMapDirector : MonoBehaviour
    {
        [SerializeField] Camera mapCamera;

        GameSession session;
        LocationDefinition current;
        Transform token;
        IsoCamera iso;
        bool moving;
        readonly Dictionary<LocationDefinition, Transform> nodes = new();

        FieldHud hud;
        PauseMenu pauseMenu;
        VisualElement panel, tierPanel;
        Label nameLabel, kindLabel, description, status, levels;
        MenuList tierMenu;
        VisualElement uiRoot;
        readonly Dictionary<LocationDefinition, Label> nameTags = new();
        bool choosingTier;
        int tierOpenedFrame;

        public void Configure(Camera camera) => mapCamera = camera;

        void Awake()
        {
            GameInput.EnsureInitialized();
            session = GameSession.Current;
            BuildDiorama();
            BuildUi();
        }

        void Start()
        {
            var startId = session.CurrentLocationId;
            current = session.Db.locations.FirstOrDefault(l => l.id == startId) ?? session.Db.startLocation ?? session.Db.locations.First();
            token.position = NodeTop(current);
            if (iso != null) iso.Snap();
            Refresh();
            hud.Refresh();
            AudioManager.PlayMusic(MusicTrack.WorldMap);
            session.ActiveRun = null;
            if (SaveSystem.AutoSave(session, ResumePoint.WorldMap)) hud.Toast("Jogo salvo automaticamente.", "muted", 2.5f);
            if (!string.IsNullOrEmpty(session.PendingMessage))
            {
                hud.Toast(session.PendingMessage, "gold-text", 7f);
                session.PendingMessage = null;
            }
            _ = ScreenFader.FadeIn(0.4f, destroyCancellationToken);
        }

        // ------------------------------------------------------------------ diorama

        static Vector3 World(LocationDefinition l) => new(l.mapPosition.x, 0f, l.mapPosition.y);
        Vector3 NodeTop(LocationDefinition l) => World(l) + Vector3.up * 0.35f;

        void BuildDiorama()
        {
            var root = new GameObject("WorldMap").transform;
            var locations = session.Db.locations;
            var min = new Vector2(locations.Min(l => l.mapPosition.x), locations.Min(l => l.mapPosition.y));
            var max = new Vector2(locations.Max(l => l.mapPosition.x), locations.Max(l => l.mapPosition.y));
            var center = new Vector3((min.x + max.x) / 2f, 0f, (min.y + max.y) / 2f);
            float size = Mathf.Max(max.x - min.x, max.y - min.y) + 10f;

            Shapes.Part(PrimitiveType.Plane, root, center + Vector3.down * 1.1f, new Vector3((size + 80f) / 10f, 1f, (size + 80f) / 10f),
                Palette.Water(new Color(0.33f, 0.66f, 0.93f), new Color(0.2f, 0.45f, 0.82f))).name = "Sea";
            Shapes.Part(PrimitiveType.Cylinder, root, center + Vector3.down * 1.06f, new Vector3(size + 1.4f, 0.005f, size * 0.85f + 1.4f),
                Palette.Glow(new Color(1f, 1f, 1f, 0.38f), GlowShape.Solid, additive: false)).name = "Foam";
            Shapes.Part(PrimitiveType.Cylinder, root, center + Vector3.down * 0.6f, new Vector3(size, 0.55f, size * 0.85f), new Color(0.62f, 0.44f, 0.28f)).name = "Cliff";
            Shapes.Part(PrimitiveType.Cylinder, root, center + Vector3.down * 0.12f, new Vector3(size + 0.15f, 0.08f, size * 0.85f + 0.15f), new Color(0.38f, 0.64f, 0.3f)).name = "GrassLip";
            Shapes.Part(PrimitiveType.Cylinder, root, center + Vector3.down * 0.05f, new Vector3(size - 0.6f, 0.05f, size * 0.85f - 0.6f), new Color(0.47f, 0.75f, 0.37f)).name = "Land";

            // Caminhos
            var drawn = new HashSet<(LocationDefinition, LocationDefinition)>();
            foreach (var a in locations)
                foreach (var b in a.connections)
                {
                    if (drawn.Contains((b, a))) continue;
                    drawn.Add((a, b));
                    var from = World(a);
                    var to = World(b);
                    // Caminho pontilhado (pedrinhas), estilo mapa de RPG.
                    var path = new GameObject($"Path {a.id}-{b.id}").transform;
                    path.SetParent(root, false);
                    float length = Vector3.Distance(from, to);
                    int dots = Mathf.Max(2, Mathf.FloorToInt((length - 2.4f) / 0.55f));
                    for (int i = 0; i <= dots; i++)
                    {
                        var p = Vector3.Lerp(from, to, Mathf.Lerp(1.2f / length, 1f - 1.2f / length, i / (float)dots));
                        Shapes.Part(PrimitiveType.Cylinder, path, p + Vector3.up * 0.02f, new Vector3(0.26f, 0.03f, 0.26f), Palette.Toon(new Color(0.93f, 0.84f, 0.6f), 0.012f));
                    }
                }

            // Decoração determinística
            var rng = new SeededRandom(1234);
            for (int i = 0; i < 26; i++)
            {
                var p = center + new Vector3(rng.Range(-size / 2f + 1f, size / 2f - 1f), 0f, rng.Range(-size * 0.4f + 1f, size * 0.4f - 1f));
                if (locations.Any(l => Vector3.Distance(World(l), p) < 2.2f)) continue;
                if (DistanceToPaths(locations, p) < 1f) continue;
                var holder = new GameObject("Decor").transform;
                holder.SetParent(root, false);
                holder.position = p;
                holder.rotation = Quaternion.Euler(0f, rng.Value() * 360f, 0f);
                float pick = rng.Value();
                if (pick < 0.6f) Shapes.Tree(holder, 0.7f + rng.Value() * 0.4f, i);
                else if (pick < 0.8f) Shapes.Bush(holder, 0.9f, i);
                else Shapes.Rock(holder, 0.8f + rng.Value() * 0.5f, i);
            }
            Shapes.MeadowScatter(root, "WorldMapMeadow", center, size * 0.42f, 0f, 260, 77, new Color(0.36f, 0.62f, 0.3f),
                p => locations.Any(l => Vector3.Distance(World(l), p) < 1.4f) || DistanceToPaths(locations, p) < 0.35f);

            foreach (var location in locations) nodes[location] = BuildNode(root, location);

            token = new GameObject("Token").transform;
            token.SetParent(root, false);
            var hero = session.Db.characters.FirstOrDefault();
            var leader = session.Party.FirstOrDefault();
            var visual = Shapes.Hero(token, hero != null ? hero.color : Color.red, leader?.Job.color ?? Color.blue, leader?.Job.id);
            visual.localScale = Vector3.one * 0.8f;
            visual.localRotation = Quaternion.Euler(0f, WorldProps.FacingCamera, 0f);
            Shapes.BlobShadow(token, 0.3f).localPosition = Vector3.up * 0.02f;

            if (mapCamera != null)
            {
                mapCamera.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
                iso = mapCamera.GetComponent<IsoCamera>();
                if (iso == null) iso = mapCamera.gameObject.AddComponent<IsoCamera>();
                iso.target = token;
                iso.offset = new Vector3(0f, 0.3f, 0f);
            }
        }

        static float DistanceToPaths(List<LocationDefinition> locations, Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var a in locations)
                foreach (var b in a.connections)
                {
                    var ab = World(b) - World(a);
                    float t = Mathf.Clamp01(Vector3.Dot(p - World(a), ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                    best = Mathf.Min(best, Vector3.Distance(p, World(a) + ab * t));
                }
            return best;
        }

        Transform BuildNode(Transform root, LocationDefinition location)
        {
            var node = new GameObject($"Node {location.id}").transform;
            node.SetParent(root, false);
            node.position = World(location);
            bool unlocked = session.IsLocationUnlocked(location);
            bool cleared = session.ClearedLocations.Contains(location.id);
            var baseColor = !unlocked ? new Color(0.45f, 0.45f, 0.48f) : cleared ? Palette.Gold : Color.white;
            Shapes.Part(PrimitiveType.Cylinder, node, new Vector3(0f, 0.12f, 0f), new Vector3(1.6f, 0.12f, 1.6f), baseColor).name = "Pedestal";
            Shapes.Part(PrimitiveType.Cylinder, node, new Vector3(0f, 0.26f, 0f), new Vector3(1.25f, 0.03f, 1.25f), baseColor * 0.9f);

            var accent = unlocked ? location.color : location.color * 0.5f;
            switch (location.kind)
            {
                case LocationKind.Field:
                    Shapes.Part(MeshLibrary.Icosphere(1, true, 0.06f, 3), node, new Vector3(-0.9f, 0.1f, 0.9f), new Vector3(1.7f, 1f, 1.5f), new Color(0.45f, 0.74f, 0.35f));
                    Shapes.Part(PrimitiveType.Sphere, node, new Vector3(-0.75f, 0.58f, 1.05f), new Vector3(0.32f, 0.12f, 0.3f), Palette.Plain(Color.Lerp(accent, Color.white, 0.4f)));
                    Shapes.Tree(Child(node, new Vector3(0.9f, 0f, 0.9f)), 0.6f);
                    Shapes.Tree(Child(node, new Vector3(-1.1f, 0f, -0.3f)), 0.5f);
                    break;
                case LocationKind.Town:
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new Vector3(-0.9f + i * 0.9f, 0f, 0.9f - (i % 2) * 0.6f);
                        Shapes.Part(MeshLibrary.RoundedBox(0.08f), node, p + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f), new Color(0.97f, 0.92f, 0.82f));
                        Shapes.Part(MeshLibrary.Cone(4, smooth: false), node, p + Vector3.up * 0.95f, new Vector3(0.95f, 0.5f, 0.95f), accent, new Vector3(0f, 45f, 0f));
                        Shapes.Part(MeshLibrary.RoundedBox(0.2f), node, p + new Vector3(0f, 0.42f, 0.31f), new Vector3(0.18f, 0.16f, 0.03f), Palette.Emissive(new Color(1f, 0.85f, 0.5f), 0.9f));
                    }
                    break;
                default:
                    Shapes.Part(MeshLibrary.Icosphere(1, true, 0.12f, location.id.Length), node, new Vector3(0f, 0.35f, 0.7f), new Vector3(2.1f, 1.7f, 1.5f), new Color(0.5f, 0.47f, 0.45f));
                    Shapes.Part(PrimitiveType.Sphere, node, new Vector3(0f, 0.42f, 0.05f), new Vector3(0.75f, 0.9f, 0.3f), Palette.Plain(new Color(0.05f, 0.04f, 0.07f)), new Vector3(0f, WorldProps.FacingCamera, 0f));
                    for (int i = 0; i < 3; i++)
                        Shapes.Part(MeshLibrary.Cone(5, smooth: false), node, new Vector3(-0.9f + i * 0.9f, 0.4f + (i % 2) * 0.2f, 1.3f), new Vector3(0.28f, 0.8f, 0.28f),
                            unlocked ? Palette.Emissive(accent, 1.2f, Palette.Outline) : Palette.Get(accent), new Vector3(10f, 45f, 10f));
                    break;
            }
            if (!unlocked)
            {
                var padlock = Shapes.Pivot(node, "Lock", new Vector3(0f, 1.6f, 0f), new Vector3(0f, WorldProps.FacingCamera, 0f));
                Shapes.Part(MeshLibrary.RoundedBox(0.2f), padlock, Vector3.zero, new Vector3(0.42f, 0.36f, 0.16f), Palette.Glossy(new Color(0.55f, 0.56f, 0.62f)));
                Shapes.Part(PrimitiveType.Cylinder, padlock, new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.02f, 0.3f), Palette.Glossy(new Color(0.55f, 0.56f, 0.62f)), new Vector3(90f, 0f, 0f));
                Wiggle.Add(padlock.gameObject, Wiggle.Mode.Bob, Vector3.up, 0.08f, 2f);
            }
            return node;
        }

        static Transform Child(Transform parent, Vector3 local)
        {
            var t = new GameObject("Holder").transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        // ------------------------------------------------------------------ UI

        void BuildUi()
        {
            hud = FieldHud.Create(transform);
            hud.SetHelp("Setas: viajar · Enter/Espaço: entrar · Tab: menu");
            pauseMenu = PauseMenu.Create(transform, hud);

            var root = UiKit.CreateDocument(transform, "WorldMapDocument", 5);
            uiRoot = root;
            foreach (var location in session.Db.locations)
            {
                bool unlocked = session.IsLocationUnlocked(location);
                var tag = UiKit.Text(root, unlocked ? location.displayName : $"{location.displayName} (bloqueado)", "panel-light", "map-label");
                if (!unlocked) tag.AddToClassList("muted");
                if (session.ClearedLocations.Contains(location.id)) tag.AddToClassList("gold-text");
                nameTags[location] = tag;
            }
            panel = UiKit.El(root, "panel", "map-panel");
            nameLabel = UiKit.Text(panel, "", "big");
            kindLabel = UiKit.Text(panel, "", "small", "muted");
            description = UiKit.Text(panel, "", "small");
            status = UiKit.Text(panel, "", "small");
            levels = UiKit.Text(panel, "", "small", "energy-text");

            tierPanel = UiKit.El(root, "panel", "tier-panel");
            UiKit.Text(tierPanel, "Escolha a dificuldade", "section-title");
            tierMenu = new MenuList(tierPanel);
            var tierDescription = UiKit.Text(tierPanel, "", "small", "panel-light");
            tierMenu.SelectionChanged = _ => tierDescription.text = tierMenu.Current.Description;
            UiKit.Show(tierPanel, false);
        }

        void Refresh()
        {
            bool unlocked = session.IsLocationUnlocked(current);
            nameLabel.text = current.displayName;
            kindLabel.text = current.kind switch
            {
                LocationKind.Town => "Cidade",
                LocationKind.Dungeon => "Dungeon (mapa aleatório)",
                _ => "Campo",
            };
            description.text = current.description;

            if (!unlocked) status.text = $"Bloqueado: conclua {current.requires.displayName} primeiro.";
            else if (session.BestTier.TryGetValue(current.id, out var best)) status.text = $"Concluída — melhor dificuldade: {BalanceConfig.TierName(best)}";
            else status.text = session.ClearedLocations.Contains(current.id) ? "Concluído" : "Enter/Espaço para entrar";
            status.EnableInClassList("bad", !unlocked);
            status.EnableInClassList("gold-text", unlocked);

            if (current.IsDungeon)
            {
                var b = session.Balance;
                var parts = ((DifficultyTier[])System.Enum.GetValues(typeof(DifficultyTier)))
                    .Select(t => $"{BalanceConfig.TierName(t)} Nv {EnemyScaling.DungeonLevel(session.PartyLevel, current.dungeon.baseLevel, t, b)}");
                levels.text = $"Party Nv {session.PartyLevel} → inimigos: " + string.Join(" · ", parts);
            }
            else levels.text = "";
        }

        void OpenTierMenu()
        {
            var b = session.Balance;
            var entries = ((DifficultyTier[])System.Enum.GetValues(typeof(DifficultyTier))).Select(t =>
            {
                int level = EnemyScaling.DungeonLevel(session.PartyLevel, current.dungeon.baseLevel, t, b);
                string desc = $"Inimigos Nv {level} · {b.TierFloors(t)} andares · XP/JP/ouro ×{b.TierRewardMultiplier(t):0.##} · " +
                              $"achado mágico +{b.TierMagicFind(t):0}% · tesouro do chefe: {Loot.RarityInfo.Name(b.TierBossChestFloor(t))} ou melhor" +
                              (b.TierExtraEnemies(t) > 0 ? " · grupos maiores" : "");
                return new MenuEntry(BalanceConfig.TierName(t), right: $"Nv {level}", description: desc);
            }).ToList();
            tierMenu.SetItems(entries, (int)DifficultyTier.Normal);
            UiKit.Show(tierPanel, true);
            choosingTier = true;
            tierOpenedFrame = Time.frameCount;
        }

        void LateUpdate()
        {
            if (mapCamera == null || uiRoot?.panel == null) return;
            foreach (var (location, tag) in nameTags)
            {
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(uiRoot.panel, World(location) + Vector3.up * 2.4f, mapCamera);
                tag.style.left = p.x;
                tag.style.top = p.y;
                tag.EnableInClassList("selected", location == current);
            }
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (moving || SceneFlow.IsTransitioning || pauseMenu.IsOpen) return;

            if (choosingTier)
            {
                if (Time.frameCount == tierOpenedFrame) return;
                tierMenu.HandleNavigation();
                if (GameInput.CancelDown)
                {
                    AudioManager.Play(Sfx.Cancel);
                    choosingTier = false;
                    UiKit.Show(tierPanel, false);
                }
                else if (GameInput.ConfirmDown)
                {
                    AudioManager.Play(Sfx.Confirm);
                    choosingTier = false;
                    UiKit.Show(tierPanel, false);
                    SceneFlow.EnterDungeon(current, (DifficultyTier)tierMenu.Index);
                }
                return;
            }

            if (GameInput.MenuDown)
            {
                pauseMenu.Open();
                return;
            }

            var nav = GameInput.Nav;
            if (nav != Vector2Int.zero)
            {
                var next = Neighbor(nav);
                if (next != null) TravelTo(next);
                return;
            }

            if (GameInput.ConfirmDown || GameInput.InteractDown) Enter();
        }

        /// <summary>Vizinho cujo caminho mais se alinha com a direção apertada (na tela).</summary>
        LocationDefinition Neighbor(Vector2Int nav)
        {
            if (mapCamera == null) return null;
            var right = Vector3.ProjectOnPlane(mapCamera.transform.right, Vector3.up).normalized;
            var up = Vector3.ProjectOnPlane(mapCamera.transform.up, Vector3.up).normalized;
            var wanted = (right * nav.x + up * nav.y).normalized;
            LocationDefinition best = null;
            float bestDot = 0.35f;
            foreach (var n in current.connections)
            {
                float dot = Vector3.Dot((World(n) - World(current)).normalized, wanted);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = n;
                }
            }
            return best;
        }

        async void TravelTo(LocationDefinition next)
        {
            moving = true;
            try
            {
                var from = NodeTop(current);
                var to = NodeTop(next);
                var dir = to - from;
                dir.y = 0f;
                var visual = token.GetChild(0);
                if (dir.sqrMagnitude > 0.01f) visual.rotation = Quaternion.LookRotation(dir);
                int hops = Mathf.Max(1, Mathf.RoundToInt(dir.magnitude / 1.6f));
                for (int i = 0; i < hops; i++)
                {
                    var a = Vector3.Lerp(from, to, i / (float)hops);
                    var b = Vector3.Lerp(from, to, (i + 1) / (float)hops);
                    token.position = a;
                    AudioManager.Play(Sfx.Jump, 0.35f, 1.3f);
                    await Tween.Arc(token, b, 0.35f, 0.14f, destroyCancellationToken);
                }
                token.position = to;
                visual.rotation = Quaternion.Euler(0f, WorldProps.FacingCamera, 0f);
                current = next;
                session.CurrentLocationId = current.id;
                Refresh();
            }
            catch (System.OperationCanceledException) { }
            finally
            {
                moving = false;
            }
        }

        void Enter()
        {
            if (!session.IsLocationUnlocked(current))
            {
                AudioManager.Play(Sfx.Cancel, 0.8f, 0.8f);
                hud.Toast($"Bloqueado: conclua {current.requires.displayName} primeiro.", "bad");
                return;
            }
            AudioManager.Play(Sfx.Confirm);
            if (current.IsDungeon) OpenTierMenu();
            else SceneFlow.EnterLocation(current, "saida");
        }
    }
}
