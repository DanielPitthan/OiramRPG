using System;
using System.Linq;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Field;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.World
{
    /// <summary>
    /// Fábrica dos objetos do mapa (jogador, inimigos, baús, escadas, portais...). Funciona em runtime
    /// (dungeons geradas) e no editor (cenas "assadas"), usando a <see cref="Palette"/> para os materiais.
    /// </summary>
    public static class WorldProps
    {
        const int IgnoreRaycastLayer = 2;

        static GameObject Root(Transform parent, string name, Vector3 position, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>Gira para a frente ficar voltada para a câmera isométrica.</summary>
        public const float FacingCamera = 225f;

        public static FieldPlayerController Player(Transform parent, Vector3 position, GameDatabase db)
        {
            var go = Root(parent, "Player", position + Vector3.up * 0.05f);
            var controller = go.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.78f, 0f);
            controller.height = 1.5f;
            controller.radius = 0.35f;
            controller.stepOffset = 0.25f; // degraus de 0.5 m exigem pulo (Mario RPG!)
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;

            var hero = db.characters.FirstOrDefault();
            var visual = Shapes.Hero(go.transform, hero != null ? hero.color : Color.red,
                hero != null && hero.startingJob != null ? hero.startingJob.color : Color.blue, hero?.startingJob?.id);

            var player = go.AddComponent<FieldPlayerController>();
            player.visual = visual;
            var follower = go.AddComponent<BlobShadowFollower>();
            follower.shadow = Shapes.BlobShadow(go.transform, 0.36f);

            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
            return player;
        }

        /// <summary>Visual do líder da party (chapéu/arma do job atual) para o jogador do mapa.</summary>
        public static Transform LeaderVisual(Transform player, PartyMember leader)
        {
            var visual = Shapes.Hero(player, leader.Definition.color, leader.Job.color, leader.Job.id);
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
            return visual;
        }

        public static SpawnPoint Spawn(Transform parent, Vector3 position, string id)
        {
            var spawn = Root(parent, $"Spawn ({id})", position).AddComponent<SpawnPoint>();
            spawn.id = id;
            return spawn;
        }

        public static Campfire Campfire(Transform parent, Vector3 position)
        {
            var go = Root(parent, "Campfire", position);
            var visual = Shapes.Campfire(go.transform);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.4f;
            col.height = 0.8f;
            col.center = Vector3.up * 0.4f;
            var fire = go.AddComponent<Campfire>();
            fire.flame = visual.Find("Flame");
            return fire;
        }

        public static LootChest Chest(Transform parent, Vector3 position, string id, LootTable table, int level, EncounterDefinition mimic = null)
        {
            var go = Root(parent, mimic != null ? "Chest (Mimic)" : "Chest", position, FacingCamera);
            var visual = Shapes.Chest(go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.35f, 0f);
            box.size = new Vector3(0.95f, 0.7f, 0.65f);

            var chest = go.AddComponent<LootChest>();
            chest.uniqueId = id;
            chest.level = level;
            chest.lootTable = table;
            chest.lid = visual.Find("Lid");
            chest.mimicEncounter = mimic;
            return chest;
        }

        public static FloatingCrate Crate(Transform parent, Vector3 position, string id, LootTable table, int level)
        {
            var go = Root(parent, "FloatingCrate", position + Vector3.up * 2.45f);
            var visual = Shapes.Crate(go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.8f;
            var crate = go.AddComponent<FloatingCrate>();
            crate.uniqueId = id;
            crate.level = level;
            crate.lootTable = table;
            crate.visual = visual;
            Shapes.BlobShadow(parent, 0.3f).position = position + Vector3.up * 0.02f;
            return crate;
        }

        public static FieldEnemy Enemy(Transform parent, Vector3 position, string id, EncounterDefinition encounter, bool boss)
        {
            if (encounter == null || encounter.enemies.Count == 0) throw new ArgumentException("Encontro vazio.", nameof(encounter));
            var leader = boss
                ? encounter.enemies.OrderByDescending(e => e.xp).First()
                : encounter.enemies.OrderByDescending(e => e.level).ThenByDescending(e => e.scale).First();

            var go = Root(parent, $"Enemy ({encounter.displayName})", position, FacingCamera);
            var visual = Shapes.EnemyFor(go.transform, leader);
            if (leader.flying) visual.localPosition = Vector3.up * 0.9f;
            Shapes.BlobShadow(go.transform, 0.45f * leader.scale).localPosition = Vector3.up * 0.02f;

            var enemy = go.AddComponent<FieldEnemy>();
            enemy.encounter = encounter;
            enemy.uniqueId = id;
            enemy.visual = visual;
            enemy.flying = leader.flying;
            enemy.bodyRadius = 0.45f * leader.scale;
            enemy.bodyHeight = leader.shape switch
            {
                UnitShape.Golem => 1.8f,
                UnitShape.Slime or UnitShape.KingSlime => 0.7f,
                UnitShape.Spider => 0.6f,
                _ => 1.2f,
            } * leader.scale;
            if (leader.flying) enemy.bodyHeight += 0.9f;
            if (boss)
            {
                enemy.wanderRadius = 0f;
                enemy.chaseRadius = 0f;
            }
            return enemy;
        }

        public static GameObject Decor(Transform parent, Vector3 position, bool tree, int variant, Action<GameObject> markStatic = null)
        {
            var holder = Root(parent, tree ? "Tree" : "Rock", position, (position.x * 37f + position.z * 53f) % 360f);
            var visual = tree
                ? Shapes.Tree(holder.transform, 0.9f + variant % 3 * 0.12f, variant)
                : variant % 3 == 2 ? Shapes.Bush(holder.transform, 1.1f, variant) : Shapes.Rock(holder.transform, 1f + variant % 2 * 0.3f, variant);
            var col = holder.AddComponent<CapsuleCollider>();
            col.radius = tree ? 0.35f : 0.5f;
            col.height = tree ? 2f : 0.6f;
            col.center = Vector3.up * col.height / 2f;
            markStatic?.Invoke(holder);
            foreach (var child in visual.GetComponentsInChildren<Transform>()) markStatic?.Invoke(child.gameObject);
            return holder;
        }

        /// <summary>Placa de saída para o mapa-múndi.</summary>
        public static ExitPoint Exit(Transform parent, Vector3 position, string requiredClearedId = null, string blockedMessage = null)
        {
            var go = Root(parent, "Exit", position, FacingCamera);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), Palette.Wood);
            Shapes.Part(MeshLibrary.RoundedBox(0.2f), go.transform, new Vector3(0f, 1.05f, 0.05f), new Vector3(0.95f, 0.34f, 0.08f), Palette.Gold);
            Shapes.Part(MeshLibrary.Cone(3, smooth: false), go.transform, new Vector3(0.62f, 1.05f, 0.05f), new Vector3(0.22f, 0.2f, 0.08f), Palette.Gold, new Vector3(0, 0, -90));
            Shapes.Part(MeshLibrary.RoundedBox(0.3f), go.transform, new Vector3(-0.05f, 1.05f, 0.1f), new Vector3(0.55f, 0.07f, 0.02f), Palette.Plain(Palette.DarkWood));
            var exit = go.AddComponent<ExitPoint>();
            exit.requiredClearedId = requiredClearedId;
            if (blockedMessage != null) exit.blockedMessage = blockedMessage;
            return exit;
        }

        public static Stairs StairsDown(Transform parent, Vector3 position, Color accent)
        {
            var go = Root(parent, "Stairs", position, FacingCamera);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.95f, 0.06f, 0.95f), Palette.Plain(new Color(0.04f, 0.04f, 0.06f)));
            for (int i = 0; i < 3; i++)
                Shapes.Part(MeshLibrary.RoundedBox(0.2f), go.transform, new Vector3(0f, 0.08f, 0.3f - i * 0.25f), new Vector3(0.8f, 0.06f, 0.2f), accent * (0.9f - i * 0.25f));
            var stone = accent * 0.75f;
            foreach (float x in new[] { -0.52f, 0.52f })
                Shapes.Part(MeshLibrary.RoundedBox(0.25f), go.transform, new Vector3(x, 0.5f, 0.48f), new Vector3(0.16f, 1f, 0.16f), stone);
            var arch = Shapes.Part(MeshLibrary.RoundedBox(0.3f), go.transform, new Vector3(0f, 1.02f, 0.48f), new Vector3(1.24f, 0.18f, 0.2f), accent);
            arch.name = "Arch";
            Shapes.Part(MeshLibrary.Icosphere(0), go.transform, new Vector3(0f, 1.02f, 0.6f), Vector3.one * 0.14f, Palette.Emissive(accent, 2f, Palette.Outline));
            Shapes.GlowQuad(go.transform, new Vector3(0f, 0.08f, 0f), 1.3f, Palette.Glow(new Color(0f, 0f, 0f, 0.6f), GlowShape.Circle, additive: false), new Vector3(90f, 0f, 0f));
            return go.AddComponent<Stairs>();
        }

        public static DungeonPortal Portal(Transform parent, Vector3 position, Color color, bool completesRun)
        {
            var go = Root(parent, completesRun ? "Portal (Vitória)" : "Portal (Saída)", position, FacingCamera);
            var ring = Shapes.Pivot(go.transform, "Ring", new Vector3(0f, 0.8f, 0f));
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Shapes.Part(MeshLibrary.Icosphere(0, true, 0.1f, i), ring, new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.65f, 0f),
                    Vector3.one * 0.2f, color * 0.85f, new Vector3(0, 0, i * 36f));
            }
            Wiggle.Add(ring.gameObject, Wiggle.Mode.Spin, Vector3.forward, 40f, 1f);
            var core = Shapes.Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 1.15f, 0.06f),
                Palette.Glow(new Color(color.r, color.g, color.b, 0.6f), GlowShape.Fresnel, additive: true, softness: 0.7f));
            core.name = "Core";
            Wiggle.Add(core, Wiggle.Mode.Pulse, Vector3.up, 0.04f, 3f);
            Shapes.GlowQuad(go.transform, new Vector3(0f, 0.05f, 0f), 2f, Palette.Glow(new Color(color.r, color.g, color.b, 0.45f)), new Vector3(90f, 0f, 0f));
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 0.9f, 0.4f);
            light.type = LightType.Point;
            light.color = color;
            light.range = 4f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            var portal = go.AddComponent<DungeonPortal>();
            portal.completesRun = completesRun;
            return portal;
        }

        // ------------------------------------------------------------------ cidade

        /// <summary>Casa 2×2: paredes com vigas, telhado de duas águas, chaminé, janelas acesas e porta (com colisão).</summary>
        public static GameObject House(Transform parent, Vector3 position, Color wall, Color roof, Action<GameObject> markStatic = null)
        {
            var go = Root(parent, "House", position, FacingCamera);
            var t = go.transform;
            var body = Shapes.Part(PrimitiveType.Cube, t, new Vector3(0f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 2.2f), wall, keepCollider: true);
            body.GetComponent<MeshFilter>().sharedMesh = MeshLibrary.RoundedBox(0.04f);
            Shapes.Part(MeshLibrary.RoundedBox(0.1f), t, new Vector3(0f, 0.12f, 0f), new Vector3(2.55f, 0.24f, 2.35f), new Color(0.6f, 0.58f, 0.56f));
            foreach (float x in new[] { -1.17f, 1.17f })
                Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(x, 1.1f, 1.1f), new Vector3(0.16f, 2.2f, 0.12f), Palette.Wood);
            Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(0f, 2.15f, 1.1f), new Vector3(2.5f, 0.14f, 0.12f), Palette.Wood);

            // Telhado: duas águas com beiral.
            foreach (float side in new[] { -1f, 1f })
                Shapes.Part(MeshLibrary.RoundedBox(0.06f), t, new Vector3(0.66f * side, 2.68f, 0f), new Vector3(1.72f, 0.16f, 2.75f), roof, new Vector3(0f, 0f, -36f * side));
            Shapes.Part(MeshLibrary.Cone(3, smooth: false), t, new Vector3(0f, 2.62f, 1.08f), new Vector3(2.3f, 0.95f, 0.05f), wall * 0.95f, new Vector3(-90f, 0f, 0f));
            Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(0.65f, 3.1f, -0.4f), new Vector3(0.36f, 0.75f, 0.36f), new Color(0.62f, 0.4f, 0.32f));
            Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(0.65f, 3.5f, -0.4f), new Vector3(0.44f, 0.1f, 0.44f), new Color(0.5f, 0.32f, 0.26f));

            // Porta, degrau e janelas acesas (brilham à noite e no bloom).
            Shapes.Part(MeshLibrary.RoundedBox(0.15f), t, new Vector3(0f, 0.62f, 1.11f), new Vector3(0.6f, 1.15f, 0.06f), Palette.DarkWood);
            Shapes.Part(PrimitiveType.Sphere, t, new Vector3(0.18f, 0.62f, 1.15f), Vector3.one * 0.07f, Palette.Glossy(Palette.Gold, 0f));
            foreach (float x in new[] { -0.78f, 0.78f })
            {
                Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(x, 1.38f, 1.11f), new Vector3(0.56f, 0.5f, 0.06f), Palette.Wood);
                Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(x, 1.38f, 1.13f), new Vector3(0.44f, 0.38f, 0.04f), Palette.Emissive(new Color(1f, 0.85f, 0.5f), 0.9f));
                Shapes.Part(MeshLibrary.RoundedBox(0.3f), t, new Vector3(x, 1.08f, 1.16f), new Vector3(0.6f, 0.1f, 0.16f), new Color(0.62f, 0.4f, 0.25f));
                for (int i = 0; i < 3; i++)
                    Shapes.Part(PrimitiveType.Sphere, t, new Vector3(x - 0.18f + i * 0.18f, 1.16f, 1.18f), Vector3.one * 0.1f,
                        Palette.Plain(i == 1 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.55f)));
            }
            markStatic?.Invoke(go);
            foreach (var child in go.GetComponentsInChildren<Transform>()) markStatic?.Invoke(child.gameObject);
            return body;
        }

        public static GameObject Well(Transform parent, Vector3 position, Action<GameObject> markStatic = null)
        {
            var go = Root(parent, "Well", position);
            var t = go.transform;
            Shapes.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.35f, 1.1f), new Color(0.66f, 0.66f, 0.7f), keepCollider: true);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Shapes.Part(MeshLibrary.RoundedBox(0.25f), t, new Vector3(Mathf.Cos(a) * 0.55f, 0.66f, Mathf.Sin(a) * 0.55f), new Vector3(0.3f, 0.12f, 0.2f),
                    new Color(0.58f, 0.58f, 0.62f) * (i % 2 == 0 ? 1f : 0.92f), new Vector3(0f, -a * Mathf.Rad2Deg + 90f, 0f));
            }
            Shapes.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.85f, 0.02f, 0.85f), Palette.Water(new Color(0.35f, 0.65f, 0.95f), new Color(0.2f, 0.4f, 0.8f)));
            foreach (float x in new[] { -0.5f, 0.5f })
                Shapes.Part(MeshLibrary.RoundedBox(0.3f), t, new Vector3(x, 1.05f, 0f), new Vector3(0.1f, 0.8f, 0.1f), Palette.Wood);
            foreach (float side in new[] { -1f, 1f })
                Shapes.Part(MeshLibrary.RoundedBox(0.1f), t, new Vector3(0f, 1.6f, 0.28f * side), new Vector3(1.4f, 0.08f, 0.62f), new Color(0.78f, 0.32f, 0.26f), new Vector3(-28f * side, 0f, 0f));
            Shapes.Part(PrimitiveType.Cylinder, t, new Vector3(0f, 1.3f, 0f), new Vector3(0.08f, 0.5f, 0.08f), Palette.DarkWood, new Vector3(0f, 0f, 90f));
            Shapes.Part(MeshLibrary.RoundedBox(0.25f), t, new Vector3(0.15f, 1.0f, 0f), new Vector3(0.22f, 0.22f, 0.22f), Palette.Wood);
            markStatic?.Invoke(go);
            foreach (var child in go.GetComponentsInChildren<Transform>()) markStatic?.Invoke(child.gameObject);
            return go;
        }

        /// <summary>Morador (herói genérico com cores próprias).</summary>
        public static GameObject Person(Transform parent, Vector3 position, Color body, Color hat, string name)
        {
            var go = Root(parent, name, position, FacingCamera);
            Shapes.Hero(go.transform, hat, body, null);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 1.6f;
            col.center = Vector3.up * 0.8f;
            return go;
        }

        public static Villager Villager(Transform parent, Vector3 position, string name, Color body, Color hat, params string[] lines)
        {
            var villager = Person(parent, position, body, hat, name).AddComponent<Villager>();
            villager.villagerName = name;
            villager.lines = lines;
            villager.radius = 1.8f;
            return villager;
        }

        /// <summary>Barraca de loja: balcão, toldo listrado, placa com ícone e o lojista atrás (de frente para a câmera).</summary>
        public static ShopKeeper Shop(Transform parent, Vector3 position, ShopKind kind, string shopName, string townId, Color color)
        {
            var go = Root(parent, $"Shop ({shopName})", position, FacingCamera);
            var t = go.transform;
            var keeper = Person(t, position, color, color * 0.7f, "Keeper");
            keeper.transform.localRotation = Quaternion.identity;
            var counter = Shapes.Part(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0.75f), new Vector3(1.6f, 0.9f, 0.55f), Palette.Wood, keepCollider: true);
            counter.name = "Counter";
            counter.GetComponent<MeshFilter>().sharedMesh = MeshLibrary.RoundedBox(0.08f);
            Shapes.Part(MeshLibrary.RoundedBox(0.2f), t, new Vector3(0f, 0.92f, 0.75f), new Vector3(1.75f, 0.08f, 0.68f), Palette.DarkWood);
            foreach (float x in new[] { -0.82f, 0.82f })
                Shapes.Part(MeshLibrary.RoundedBox(0.3f), t, new Vector3(x, 1.35f, 0.95f), new Vector3(0.1f, 1.8f, 0.1f), Palette.DarkWood);
            // Toldo listrado.
            for (int i = 0; i < 6; i++)
                Shapes.Part(MeshLibrary.RoundedBox(0.15f), t, new Vector3(-0.75f + i * 0.3f, 2.2f, 0.75f), new Vector3(0.3f, 0.07f, 0.9f),
                    i % 2 == 0 ? color : new Color(0.98f, 0.96f, 0.9f), new Vector3(-16f, 0f, 0f));
            for (int i = 0; i < 6; i++)
                Shapes.Part(PrimitiveType.Sphere, t, new Vector3(-0.75f + i * 0.3f, 2.08f, 1.2f), new Vector3(0.28f, 0.12f, 0.06f), i % 2 == 0 ? color : new Color(0.98f, 0.96f, 0.9f));
            // Placa com um símbolo do tipo de loja.
            Shapes.Part(MeshLibrary.RoundedBox(0.25f), t, new Vector3(0f, 2.55f, 0.6f), new Vector3(1.0f, 0.42f, 0.08f), Palette.Wood);
            var icon = Shapes.Pivot(t, "Icon", new Vector3(0f, 2.55f, 0.66f));
            switch (kind)
            {
                case ShopKind.Consumables:
                    Shapes.Part(PrimitiveType.Sphere, icon, new Vector3(0f, -0.03f, 0f), Vector3.one * 0.26f, Palette.Glossy(new Color(0.95f, 0.3f, 0.35f), 0f));
                    Shapes.Part(PrimitiveType.Cylinder, icon, new Vector3(0f, 0.13f, 0f), new Vector3(0.09f, 0.05f, 0.09f), Palette.Plain(Palette.Wood));
                    break;
                case ShopKind.Blacksmith:
                    Shapes.Part(MeshLibrary.RoundedBox(0.3f), icon, Vector3.zero, new Vector3(0.07f, 0.32f, 0.03f), Palette.Glossy(new Color(0.85f, 0.88f, 0.95f), 0f), new Vector3(0, 0, 45));
                    Shapes.Part(MeshLibrary.RoundedBox(0.3f), icon, Vector3.zero, new Vector3(0.07f, 0.32f, 0.03f), Palette.Glossy(new Color(0.85f, 0.88f, 0.95f), 0f), new Vector3(0, 0, -45));
                    break;
                case ShopKind.Gambler:
                    Shapes.Part(MeshLibrary.Icosphere(0), icon, Vector3.zero, Vector3.one * 0.28f, Palette.Emissive(new Color(0.75f, 0.45f, 1f), 1.4f, Palette.Outline));
                    break;
                default:
                    Shapes.Part(PrimitiveType.Sphere, icon, Vector3.zero, new Vector3(0.3f, 0.2f, 0.06f), Palette.Plain(new Color(0.98f, 0.92f, 0.6f)));
                    Shapes.Part(PrimitiveType.Sphere, icon, new Vector3(0.06f, 0.04f, 0.01f), new Vector3(0.24f, 0.18f, 0.06f), Palette.Plain(new Color(0.55f, 0.42f, 0.32f)));
                    break;
            }
            var shop = go.AddComponent<ShopKeeper>();
            shop.kind = kind;
            shop.shopName = shopName;
            shop.townId = townId;
            shop.radius = 2.4f;
            return shop;
        }

        /// <summary>Baú invisível flutuando (revelado com uma cabeçada) + brilho ocasional.</summary>
        public static HiddenChest HiddenChest(Transform parent, Vector3 position, string id)
        {
            var go = Root(parent, "HiddenChest", position + Vector3.up * 2.45f, FacingCamera);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.8f;
            var holder = new GameObject("Visual").transform;
            holder.SetParent(go.transform, false);
            holder.localPosition = Vector3.down * 0.35f;
            Shapes.Chest(holder);
            var glint = Shapes.Part(MeshLibrary.Icosphere(0), go.transform, Vector3.zero, Vector3.one * 0.2f, Palette.Emissive(new Color(1f, 1f, 0.75f), 2.5f)).transform;
            Shapes.GlowQuad(glint, Vector3.zero, 3.5f, Palette.Glow(new Color(1f, 0.95f, 0.6f, 0.8f))).AddComponent<Billboard>();
            glint.name = "Glint";
            var chest = go.AddComponent<HiddenChest>();
            chest.uniqueId = id;
            chest.visual = holder;
            chest.glint = glint;
            return chest;
        }

        /// <summary>Tocha com chama brilhante, halo e luz pontual tremulando na cor do tema da dungeon.</summary>
        public static void Torch(Transform parent, Vector3 position, Color color)
        {
            var go = Root(parent, "Torch", position);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.45f, 0.1f), Palette.DarkWood);
            Shapes.Part(MeshLibrary.Cone(6), go.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.24f, 0.16f, 0.24f), new Color(0.45f, 0.42f, 0.42f), new Vector3(180f, 0f, 0f));
            var flame = Shapes.Pivot(go.transform, "Flame", new Vector3(0f, 1.05f, 0f));
            Shapes.Part(MeshLibrary.Drop(), flame, new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.36f, 0.24f), Palette.Emissive(color, 2.6f));
            Shapes.Part(MeshLibrary.Drop(), flame, new Vector3(0f, 0.05f, 0f), new Vector3(0.13f, 0.22f, 0.13f), Palette.Emissive(Color.Lerp(color, Color.white, 0.6f), 2.6f));
            Wiggle.Add(flame.gameObject, Wiggle.Mode.Pulse, Vector3.up, 0.12f, 11f, position.x);
            Shapes.GlowQuad(flame, new Vector3(0f, 0.1f, 0f), 1.4f, Palette.Glow(new Color(color.r, color.g, color.b, 0.5f))).AddComponent<Billboard>();
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            light.type = LightType.Point;
            light.color = color;
            light.range = 7f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>();
            Fx.Embers(flame, new Vector3(0f, 0.2f, 0f), color, rate: 2.5f, spread: 0.05f);
        }
    }
}
