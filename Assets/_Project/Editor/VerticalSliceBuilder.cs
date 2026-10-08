using System.Collections.Generic;
using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Oiram.EditorTools
{
    /// <summary>
    /// Monta o jogo inteiro: conteúdo (ScriptableObjects, sincronizado com DefaultContent), materiais, cenas
    /// (Title, Field_Vale, Battle_Arena, WorldMap, Town_Vila, Dungeon) e Build Settings.
    /// Menu: OiramRPG ▸ Construir Fatia Vertical. Batch: -executeMethod Oiram.EditorTools.VerticalSliceBuilder.BuildAll
    /// </summary>
    public static class VerticalSliceBuilder
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string FieldScenePath = ScenesFolder + "/Field_Vale.unity";
        public const string BattleScenePath = ScenesFolder + "/Battle_Arena.unity";
        public const string LayoutPath = "Assets/_Project/Levels/Field_Vale.txt";
        public const string TitleScenePath = ScenesFolder + "/Title.unity";
        public const string WorldMapScenePath = ScenesFolder + "/WorldMap.unity";
        public const string TownScenePath = ScenesFolder + "/Town_Vila.unity";
        public const string TownLayoutPath = "Assets/_Project/Levels/Town_Vila.txt";
        public const string DungeonScenePath = ScenesFolder + "/Dungeon.unity";

        static readonly Quaternion IsoRotation = Quaternion.Euler(30f, 45f, 0f);
        static readonly Color FieldSky = new(0.56f, 0.78f, 0.95f);
        static readonly Color BattleSky = new(0.42f, 0.62f, 0.86f);

        [MenuItem("OiramRPG/Construir Fatia Vertical", priority = 0)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAll();
            EditorSceneManager.OpenScene(FieldScenePath);
            EditorUtility.DisplayDialog("OiramRPG", "Jogo construído! Cena Field_Vale aberta — aperte Play (ou abra Title para começar do título).", "OK");
        }

        public static void BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentSeeder.DatabasePath) == null) ContentSeeder.Seed(overwrite: false);
            else ContentSeeder.Sync();
            EditorFolders.Ensure(ScenesFolder);
            ConfigurePipeline();
            UiSkinGenerator.Generate();
            dayProfile = dungeonProfile = null;
            EditorPalette.Begin();
            try
            {
                BuildBattleScene();
                BuildFieldScene();
                BuildTownScene();
                BuildDungeonScene();
                BuildWorldMapScene();
                BuildTitleScene();
            }
            finally
            {
                EditorPalette.End();
            }
            ConfigureBuildSettings();
            RemoveTemplateSampleScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[OiramRPG] Fatia vertical construída.");
        }

        // ------------------------------------------------------------------ comum

        static void SetupEnvironment(Color ambient)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = false;
        }

        static Light CreateSun(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            light.shadowNormalBias = 0.6f;
            return light;
        }

        // ------------------------------------------------------------------ visual (toon + pós)

        const string SettingsFolder = "Assets/_Project/Settings";

        /// <summary>Antisserrilhado (contornos limpos) e sombras no perfil de qualidade PC.</summary>
        static void ConfigurePipeline()
        {
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pc == null) return;
            pc.msaaSampleCount = 4;
            pc.shadowDistance = 45f;
            EditorUtility.SetDirty(pc);
        }

        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>Perfil de pós-processamento: bloom leve, cores vivas, vinheta. Dungeons: mais bloom e vinheta.</summary>
        static VolumeProfile PostProfile(bool dungeon)
        {
            EditorFolders.Ensure(SettingsFolder);
            string path = $"{SettingsFolder}/OiramPost_{(dungeon ? "Dungeon" : "Day")}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = Override<Bloom>(profile);
            bloom.threshold.Override(dungeon ? 0.85f : 0.95f);
            bloom.intensity.Override(dungeon ? 0.8f : 0.45f);
            bloom.scatter.Override(0.65f);

            var colors = Override<ColorAdjustments>(profile);
            colors.postExposure.Override(dungeon ? 0.1f : 0.15f);
            colors.contrast.Override(dungeon ? 12f : 8f);
            colors.saturation.Override(dungeon ? 8f : 16f);

            var tonemap = Override<Tonemapping>(profile);
            tonemap.mode.Override(TonemappingMode.Neutral);

            var white = Override<WhiteBalance>(profile);
            white.temperature.Override(dungeon ? -4f : 6f);

            var vignette = Override<Vignette>(profile);
            vignette.color.Override(new Color(0.08f, 0.06f, 0.16f));
            vignette.intensity.Override(dungeon ? 0.36f : 0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static VolumeProfile dayProfile, dungeonProfile;

        /// <summary>Céu em degradê, luz ambiente do toon e pós-processamento (filho de <paramref name="root"/>).</summary>
        static SceneAtmosphere CreateAtmosphere(Transform root, Camera cam, AtmosphereColors colors, bool dungeon = false)
        {
            var go = new GameObject("Atmosphere");
            go.transform.SetParent(root, false);
            var atmosphere = go.AddComponent<SceneAtmosphere>();
            atmosphere.targetCamera = cam;
            atmosphere.colors = colors;
            SceneAtmosphere.ApplyAmbient(colors.ambientSky, colors.ambientGround);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = dungeon ? dungeonProfile ??= PostProfile(true) : dayProfile ??= PostProfile(false);

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            return atmosphere;
        }

        static Camera CreateIsoCamera(Transform parent, string name, float size, Color background, Vector3 lookAt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = IsoRotation;
            go.transform.position = lookAt - go.transform.forward * 30f;
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            return cam;
        }

        static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ------------------------------------------------------------------ batalha

        static void BuildBattleScene()
        {
            var scene = NewScene();
            SetupEnvironment(new Color(0.55f, 0.58f, 0.68f));

            var root = new GameObject("BattleRoot").transform;
            CreateSun(root);
            var cam = CreateIsoCamera(root, "BattleCamera", 4.4f, BattleSky, new Vector3(0f, 0.6f, 0f));
            CreateAtmosphere(root, cam, AtmosphereColors.Day);

            var arena = new GameObject("Arena").transform;
            arena.SetParent(root, false);
            Shapes.Part(PrimitiveType.Cylinder, arena, new Vector3(0f, -0.65f, 0f), new Vector3(13.5f, 0.6f, 13.5f), new Color(0.62f, 0.44f, 0.28f)).name = "Cliff";
            Shapes.Part(PrimitiveType.Cylinder, arena, new Vector3(0f, -0.08f, 0f), new Vector3(13.6f, 0.08f, 13.6f), new Color(0.38f, 0.64f, 0.3f)).name = "Grass";
            Shapes.Part(PrimitiveType.Cylinder, arena, new Vector3(0f, -0.02f, 0f), new Vector3(12.8f, 0.03f, 12.8f), new Color(0.47f, 0.76f, 0.37f)).name = "Grass";
            Shapes.Part(PrimitiveType.Plane, arena, new Vector3(0f, -1.15f, 0f), new Vector3(8f, 1f, 8f), Palette.Water(new Color(0.33f, 0.66f, 0.93f), new Color(0.2f, 0.45f, 0.82f))).name = "Water";
            Shapes.MeadowScatter(arena, "ArenaMeadow", Vector3.zero, 6.2f, 0.01f, 140, 21, new Color(0.36f, 0.62f, 0.3f), p => p.magnitude < 4.6f).name = "Decor";

            // Decoração só no lado de trás (longe da câmera) para não tapar as unidades.
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < 14; i++)
            {
                float angle = i / 14f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Vector3.Dot(dir, forward) < 0.25f) continue;
                var holder = new GameObject(i % 3 == 0 ? "Rock" : "Tree").transform;
                holder.SetParent(arena, false);
                holder.position = dir * 5.6f;
                holder.rotation = Quaternion.Euler(0f, i * 47f, 0f);
                if (i % 3 == 0) Shapes.Rock(holder, 1.2f, i);
                else Shapes.Tree(holder, 1f + (i % 2) * 0.2f, i);
                var bush = new GameObject("Tree").transform;
                bush.SetParent(arena, false);
                bush.position = Quaternion.Euler(0f, 12f, 0f) * dir * 4.9f;
                Shapes.Bush(bush, 0.9f, i);
            }

            // Tochas: só aparecem nas batalhas de dungeon (ApplyTheme liga quando o tema é "indoor").
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f + 0.3f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Vector3.Dot(dir, forward) < -0.2f) continue;
                var torches = new GameObject("Torches").transform;
                torches.SetParent(arena, false);
                WorldProps.Torch(torches, dir * 5.9f, new Color(1f, 0.62f, 0.3f));
                torches.gameObject.SetActive(false);
            }

            // Posições: party à esquerda da tela, inimigos à direita.
            var right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            Transform Anchor(string name, Vector3 position)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.position = position;
                return t;
            }

            var partyAnchors = new[]
            {
                Anchor("Party_0", -2.3f * right + 2.1f * forward),
                Anchor("Party_1", -3.3f * right),
                Anchor("Party_2", -2.3f * right - 2.1f * forward),
            };
            var enemyAnchors = new[]
            {
                Anchor("Enemy_0", 1.9f * right + 2.1f * forward),
                Anchor("Enemy_1", 2.5f * right),
                Anchor("Enemy_2", 1.9f * right - 2.1f * forward),
                Anchor("Enemy_3", 3.7f * right + 1.1f * forward),
                Anchor("Enemy_4", 3.7f * right - 1.1f * forward),
            };

            var manager = root.gameObject.AddComponent<BattleManager>();
            manager.Configure(cam, partyAnchors, enemyAnchors);

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        // ------------------------------------------------------------------ exploração

        static void BuildFieldScene()
        {
            var scene = NewScene();
            // Carrega o banco só depois de criar a cena: trocar de cena descarrega assets sem referência na cena.
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentSeeder.DatabasePath);
            SetupEnvironment(new Color(0.58f, 0.6f, 0.68f));

            var fieldRoot = new GameObject("FieldRoot");
            CreateSun(fieldRoot.transform);

            var level = new LevelBuilder(LayoutPath, db, TerrainTheme.Meadow());
            var levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(fieldRoot.transform, false);
            level.Build(levelRoot);

            var cam = CreateIsoCamera(fieldRoot.transform, "FieldCamera", 6.5f, FieldSky, level.PlayerSpawn);
            cam.gameObject.tag = "MainCamera";
            var iso = cam.gameObject.AddComponent<IsoCamera>();
            iso.target = level.Player.transform;
            iso.distance = 30f;
            cam.transform.position = level.Player.transform.position + iso.offset - cam.transform.forward * iso.distance;

            CreateAtmosphere(fieldRoot.transform, cam, AtmosphereColors.Day);
            var director = new GameObject("FieldDirector").AddComponent<FieldDirector>();
            director.Configure(fieldRoot, level.Player, "loc_vale");

            EditorSceneManager.SaveScene(scene, FieldScenePath);
        }

        // ------------------------------------------------------------------ cidade

        static readonly string[][] VillagerLines =
        {
            new[] { "Bem-vindo à Vila Ventura!", "O ferreiro troca o estoque sempre que alguém volta de uma dungeon." },
            new[] { "Dizem que perto do poço tem um tesouro flutuando no ar...", "Só aparece pra quem pula exatamente embaixo dele!" },
            new[] { "Na pousada dá pra descansar e salvar a viagem.", "Dungeons mais difíceis dão loot melhor... se você sobreviver." },
            new[] { "O apostador vende itens misteriosos.", "Às vezes sai coisa lendária. Às vezes não. Hehe." },
        };

        static void BuildTownScene()
        {
            const string townId = "loc_vila";
            var scene = NewScene();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentSeeder.DatabasePath);
            SetupEnvironment(new Color(0.6f, 0.6f, 0.66f));

            var fieldRoot = new GameObject("FieldRoot");
            CreateSun(fieldRoot.transform);

            var walls = new[] { new Color(0.95f, 0.9f, 0.8f), new Color(0.9f, 0.85f, 0.7f), new Color(0.85f, 0.88f, 0.92f) };
            var roofs = new[] { new Color(0.8f, 0.3f, 0.25f), new Color(0.3f, 0.45f, 0.75f), new Color(0.35f, 0.6f, 0.35f), new Color(0.6f, 0.35f, 0.6f) };
            var people = new[] { new Color(0.4f, 0.55f, 0.8f), new Color(0.8f, 0.5f, 0.3f), new Color(0.5f, 0.7f, 0.4f), new Color(0.7f, 0.4f, 0.6f) };
            int houses = 0, villagers = 0;

            var level = new LevelBuilder(TownLayoutPath, db, TerrainTheme.Town());
            level.CustomObject = (c, pos, id, actors) =>
            {
                switch (c)
                {
                    case 'H':
                        WorldProps.House(actors, pos, walls[houses % walls.Length], roofs[houses % roofs.Length], LevelBuilder.MakeStatic);
                        houses++;
                        return true;
                    case 'L': WorldProps.Shop(actors, pos, ShopKind.Consumables, "Mercearia da Rosa", townId, new Color(0.9f, 0.35f, 0.4f)); return true;
                    case 'A': WorldProps.Shop(actors, pos, ShopKind.Blacksmith, "Ferreiro Bruno", townId, new Color(0.55f, 0.58f, 0.65f)); return true;
                    case 'J': WorldProps.Shop(actors, pos, ShopKind.Gambler, "Apostador Lupo", townId, new Color(0.6f, 0.35f, 0.75f)); return true;
                    case 'I':
                        WorldProps.Shop(actors, pos, ShopKind.Inn, "Pousada do Vento", townId, new Color(0.95f, 0.75f, 0.35f));
                        WorldProps.Spawn(actors, pos + new Vector3(-1.4f, 0f, -1.4f), "pousada");
                        return true;
                    case 'N':
                        var lines = VillagerLines[villagers % VillagerLines.Length];
                        WorldProps.Villager(actors, pos, $"Morador {villagers + 1}", people[villagers % people.Length], roofs[(villagers + 1) % roofs.Length], lines);
                        villagers++;
                        return true;
                    case 'W': WorldProps.Well(actors, pos, LevelBuilder.MakeStatic); return true;
                    case 'Q': WorldProps.HiddenChest(actors, pos, $"{townId}_reliquia"); return true;
                }
                return false;
            };
            var levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(fieldRoot.transform, false);
            level.Build(levelRoot);

            var cam = CreateIsoCamera(fieldRoot.transform, "FieldCamera", 6.5f, FieldSky, level.PlayerSpawn);
            cam.gameObject.tag = "MainCamera";
            var iso = cam.gameObject.AddComponent<IsoCamera>();
            iso.target = level.Player.transform;
            cam.transform.position = level.Player.transform.position + iso.offset - cam.transform.forward * iso.distance;

            CreateAtmosphere(fieldRoot.transform, cam, AtmosphereColors.Golden);
            var director = new GameObject("FieldDirector").AddComponent<FieldDirector>();
            director.Configure(fieldRoot, level.Player, townId);
            EditorSceneManager.SaveScene(scene, TownScenePath);
        }

        // ------------------------------------------------------------------ dungeon (montada em runtime)

        static void BuildDungeonScene()
        {
            var scene = NewScene();
            SetupEnvironment(new Color(0.45f, 0.42f, 0.42f));
            var fieldRoot = new GameObject("FieldRoot");
            var sun = CreateSun(fieldRoot.transform);
            var cam = CreateIsoCamera(fieldRoot.transform, "FieldCamera", 6.5f, new Color(0.1f, 0.1f, 0.12f), Vector3.zero);
            cam.gameObject.tag = "MainCamera";
            cam.gameObject.AddComponent<IsoCamera>();
            CreateAtmosphere(fieldRoot.transform, cam, AtmosphereColors.Underground(new Color(0.12f, 0.1f, 0.14f), new Color(0.03f, 0.03f, 0.05f), new Color(0.4f, 0.38f, 0.42f)), dungeon: true);

            var director = new GameObject("FieldDirector").AddComponent<FieldDirector>();
            var dungeon = new GameObject("DungeonDirector").AddComponent<DungeonDirector>();
            dungeon.Configure(director, fieldRoot, cam, sun);
            EditorSceneManager.SaveScene(scene, DungeonScenePath);
        }

        // ------------------------------------------------------------------ mapa-múndi e título

        static void BuildWorldMapScene()
        {
            var scene = NewScene();
            SetupEnvironment(new Color(0.6f, 0.62f, 0.7f));
            var root = new GameObject("WorldMapRoot");
            CreateSun(root.transform);
            var cam = CreateIsoCamera(root.transform, "MapCamera", 7.5f, FieldSky, Vector3.zero);
            cam.gameObject.tag = "MainCamera";
            cam.gameObject.AddComponent<IsoCamera>();
            CreateAtmosphere(root.transform, cam, AtmosphereColors.Day);
            root.AddComponent<WorldMapDirector>().Configure(cam);
            EditorSceneManager.SaveScene(scene, WorldMapScenePath);
        }

        static void BuildTitleScene()
        {
            var scene = NewScene();
            SetupEnvironment(new Color(0.6f, 0.62f, 0.7f));
            var root = new GameObject("TitleRoot");
            CreateSun(root.transform);
            var cam = CreateIsoCamera(root.transform, "TitleCamera", 4.2f, new Color(0.5f, 0.72f, 0.95f), new Vector3(0f, 1.6f, 0f));
            cam.gameObject.tag = "MainCamera";
            CreateAtmosphere(root.transform, cam, AtmosphereColors.Golden);
            root.AddComponent<TitleDirector>();
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        // ------------------------------------------------------------------ build settings

        static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(TitleScenePath, true),
                new(FieldScenePath, true),
                new(BattleScenePath, true),
                new(WorldMapScenePath, true),
                new(TownScenePath, true),
                new(DungeonScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void RemoveTemplateSampleScene()
        {
            const string sample = "Assets/Scenes/SampleScene.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sample) == null) return;
            AssetDatabase.DeleteAsset(sample);
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && !AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Any())
                AssetDatabase.DeleteAsset("Assets/Scenes");
        }
    }
}
