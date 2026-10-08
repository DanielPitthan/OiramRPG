using System;
using System.Linq;
using Oiram.Battle;
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
                hero != null && hero.startingJob != null ? hero.startingJob.color : Color.blue);

            var player = go.AddComponent<FieldPlayerController>();
            player.visual = visual;
            var follower = go.AddComponent<BlobShadowFollower>();
            follower.shadow = Shapes.BlobShadow(go.transform, 0.36f);

            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
            return player;
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
            var visual = tree ? Shapes.Tree(holder.transform, 0.9f + variant % 3 * 0.12f) : Shapes.Rock(holder.transform, 1f + variant % 2 * 0.3f);
            var col = holder.AddComponent<CapsuleCollider>();
            col.radius = tree ? 0.35f : 0.5f;
            col.height = tree ? 2f : 0.6f;
            col.center = Vector3.up * col.height / 2f;
            markStatic?.Invoke(holder);
            foreach (Transform child in visual) markStatic?.Invoke(child.gameObject);
            return holder;
        }

        /// <summary>Placa de saída para o mapa-múndi.</summary>
        public static ExitPoint Exit(Transform parent, Vector3 position, string requiredClearedId = null, string blockedMessage = null)
        {
            var go = Root(parent, "Exit", position, FacingCamera);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 0.12f), Palette.Wood);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.05f, 0.05f), new Vector3(0.9f, 0.32f, 0.06f), Palette.Gold);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0.38f, 1.05f, 0.09f), new Vector3(0.18f, 0.18f, 0.04f), Palette.DarkWood, new Vector3(0, 0, 45));
            var exit = go.AddComponent<ExitPoint>();
            exit.requiredClearedId = requiredClearedId;
            if (blockedMessage != null) exit.blockedMessage = blockedMessage;
            return exit;
        }

        public static Stairs StairsDown(Transform parent, Vector3 position, Color accent)
        {
            var go = Root(parent, "Stairs", position, FacingCamera);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.95f, 0.06f, 0.95f), new Color(0.05f, 0.05f, 0.06f));
            for (int i = 0; i < 3; i++)
                Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.08f, 0.3f - i * 0.25f), new Vector3(0.8f, 0.06f, 0.2f), accent * (0.9f - i * 0.25f));
            var arch = Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.9f, 0.48f), new Vector3(1.1f, 0.15f, 0.12f), accent);
            arch.name = "Arch";
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(-0.5f, 0.45f, 0.48f), new Vector3(0.12f, 0.9f, 0.12f), accent * 0.8f);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0.5f, 0.45f, 0.48f), new Vector3(0.12f, 0.9f, 0.12f), accent * 0.8f);
            return go.AddComponent<Stairs>();
        }

        public static DungeonPortal Portal(Transform parent, Vector3 position, Color color, bool completesRun)
        {
            var go = Root(parent, completesRun ? "Portal (Vitória)" : "Portal (Saída)", position, FacingCamera);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(Mathf.Cos(a) * 0.5f, 0.75f + Mathf.Sin(a) * 0.6f, 0f),
                    Vector3.one * 0.16f, color, new Vector3(0, 0, i * 45f));
            }
            var core = Shapes.Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.9f, 0.08f), color * 0.6f);
            core.name = "Core";
            var portal = go.AddComponent<DungeonPortal>();
            portal.completesRun = completesRun;
            return portal;
        }

        // ------------------------------------------------------------------ cidade

        /// <summary>Casa 2×2 com telhado (decoração com colisão).</summary>
        public static GameObject House(Transform parent, Vector3 position, Color wall, Color roof, Action<GameObject> markStatic = null)
        {
            var go = Root(parent, "House", position, FacingCamera);
            var body = Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 2.2f), wall, keepCollider: true);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.55f, 0f), new Vector3(1.9f, 1.9f, 2.5f), roof, new Vector3(0f, 0f, 45f));
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.55f, 1.11f), new Vector3(0.55f, 1.1f, 0.04f), Palette.DarkWood);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(-0.75f, 1.35f, 1.11f), new Vector3(0.45f, 0.4f, 0.04f), new Color(0.55f, 0.75f, 0.95f));
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0.75f, 1.35f, 1.11f), new Vector3(0.45f, 0.4f, 0.04f), new Color(0.55f, 0.75f, 0.95f));
            markStatic?.Invoke(go);
            foreach (Transform child in go.transform) markStatic?.Invoke(child.gameObject);
            return body;
        }

        public static GameObject Well(Transform parent, Vector3 position, Action<GameObject> markStatic = null)
        {
            var go = Root(parent, "Well", position);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.35f, 0f), new Vector3(1.1f, 0.35f, 1.1f), new Color(0.6f, 0.6f, 0.62f), keepCollider: true);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.68f, 0f), new Vector3(0.85f, 0.02f, 0.85f), new Color(0.2f, 0.4f, 0.75f));
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(-0.5f, 1.05f, 0f), new Vector3(0.1f, 0.8f, 0.1f), Palette.Wood);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0.5f, 1.05f, 0f), new Vector3(0.1f, 0.8f, 0.1f), Palette.Wood);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.4f, 0.12f, 0.9f), new Color(0.75f, 0.3f, 0.25f), new Vector3(0f, 0f, 0f));
            markStatic?.Invoke(go);
            return go;
        }

        /// <summary>Morador (herói genérico com cores próprias).</summary>
        public static GameObject Person(Transform parent, Vector3 position, Color body, Color hat, string name)
        {
            var go = Root(parent, name, position, FacingCamera);
            Shapes.Hero(go.transform, hat, body);
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

        /// <summary>Balcão de loja com o lojista atrás (de frente para a câmera) e uma placa colorida.</summary>
        public static ShopKeeper Shop(Transform parent, Vector3 position, ShopKind kind, string shopName, string townId, Color color)
        {
            var go = Root(parent, $"Shop ({shopName})", position, FacingCamera);
            var keeper = Person(go.transform, position, color, color * 0.7f, "Keeper");
            keeper.transform.localRotation = Quaternion.identity;
            var counter = Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.45f, 0.75f), new Vector3(1.6f, 0.9f, 0.55f), Palette.Wood, keepCollider: true);
            counter.name = "Counter";
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.92f, 0.75f), new Vector3(1.7f, 0.06f, 0.65f), Palette.DarkWood);
            Shapes.Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 2.25f, 0.15f), new Vector3(1.4f, 0.45f, 0.08f), color);
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
            var glint = Shapes.Part(PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * 0.18f, new Color(1f, 1f, 0.75f), new Vector3(45, 0, 45)).transform;
            glint.name = "Glint";
            var chest = go.AddComponent<HiddenChest>();
            chest.uniqueId = id;
            chest.visual = holder;
            chest.glint = glint;
            return chest;
        }

        /// <summary>Tocha com luz pontual na cor do tema da dungeon.</summary>
        public static void Torch(Transform parent, Vector3 position, Color color)
        {
            var go = Root(parent, "Torch", position);
            Shapes.Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.45f, 0.1f), Palette.DarkWood);
            Shapes.Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.98f, 0f), new Vector3(0.22f, 0.3f, 0.22f), color);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            light.type = LightType.Point;
            light.color = color;
            light.range = 7f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
        }
    }
}
