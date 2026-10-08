using System;
using System.Collections.Generic;
using Oiram.Battle;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oiram.Core
{
    /// <summary>Materiais de cor chapada (visual low-poly provisório). Um material por cor, em cache.</summary>
    public static class Palette
    {
        public const string BaseMaterialResource = "Materials/OiramLit";

        /// <summary>O editor substitui isto para gerar materiais como assets ao "assar" as cenas.</summary>
        public static Func<Color, Material> Override;

        static readonly Dictionary<Color32, Material> cache = new();
        static Material baseMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            baseMaterial = null;
        }

        public static Material Get(Color color)
        {
            if (Override != null) return Override(color);

            Color32 key = color;
            if (cache.TryGetValue(key, out var mat) && mat != null) return mat;

            if (baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>(BaseMaterialResource);
                if (baseMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    baseMaterial = new Material(shader);
                }
            }
            mat = new Material(baseMaterial) { name = "Oiram_" + ColorUtility.ToHtmlStringRGB(color), color = color };
            cache[key] = mat;
            return mat;
        }

        public static readonly Color Skin = new(1f, 0.84f, 0.68f);
        public static readonly Color Eye = new(0.08f, 0.08f, 0.1f);
        public static readonly Color Shadow = new(0.12f, 0.13f, 0.16f);
        public static readonly Color Wood = new(0.55f, 0.36f, 0.2f);
        public static readonly Color DarkWood = new(0.38f, 0.24f, 0.13f);
        public static readonly Color Gold = new(0.98f, 0.78f, 0.25f);
    }

    /// <summary>Monta personagens e objetos com primitivas (troque por prefabs de arte depois).</summary>
    public static class Shapes
    {
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color,
            Vector3 localEuler = default, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localEulerAngles = localEuler;
            go.GetComponent<Renderer>().sharedMaterial = Palette.Get(color);
            return go;
        }

        static Transform Root(Transform parent, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        static void Eyes(Transform root, float y, float z, float spacing, float size, Color color)
        {
            Part(PrimitiveType.Sphere, root, new Vector3(-spacing, y, z), Vector3.one * size, color);
            Part(PrimitiveType.Sphere, root, new Vector3(spacing, y, z), Vector3.one * size, color);
        }

        /// <summary>Herói: corpo na cor do job, boné na cor do personagem. Frente = +Z.</summary>
        public static Transform Hero(Transform parent, Color characterColor, Color jobColor)
        {
            var root = Root(parent, "Visual");
            Part(PrimitiveType.Capsule, root, new Vector3(0, 0.55f, 0), new Vector3(0.62f, 0.5f, 0.55f), jobColor);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.22f, 0), Vector3.one * 0.6f, Palette.Skin);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.42f, -0.02f), new Vector3(0.64f, 0.28f, 0.64f), characterColor);
            Part(PrimitiveType.Cube, root, new Vector3(0, 1.36f, 0.3f), new Vector3(0.42f, 0.06f, 0.22f), characterColor);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.17f, 0.3f), Vector3.one * 0.14f, Palette.Skin * 0.95f);
            Eyes(root, 1.27f, 0.25f, 0.12f, 0.1f, Palette.Eye);
            Part(PrimitiveType.Capsule, root, new Vector3(-0.16f, 0.12f, 0.05f), new Vector3(0.22f, 0.12f, 0.3f), Palette.DarkWood, new Vector3(90, 0, 0));
            Part(PrimitiveType.Capsule, root, new Vector3(0.16f, 0.12f, 0.05f), new Vector3(0.22f, 0.12f, 0.3f), Palette.DarkWood, new Vector3(90, 0, 0));
            return root;
        }

        public static Transform Enemy(Transform parent, UnitShape shape, Color color, float scale)
        {
            var root = Root(parent, "Visual");
            switch (shape)
            {
                case UnitShape.Slime:
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.36f, 0), new Vector3(1f, 0.72f, 1f), color);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Part(PrimitiveType.Cube, root, new Vector3(Mathf.Sin(a) * 0.28f, 0.7f, Mathf.Cos(a) * 0.28f - 0.05f),
                            Vector3.one * 0.16f, color * 0.7f, new Vector3(45, i * 72f, 45));
                    }
                    Eyes(root, 0.48f, 0.42f, 0.16f, 0.13f, Palette.Eye);
                    break;

                case UnitShape.Bat:
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0, 0), Vector3.one * 0.55f, color);
                    Part(PrimitiveType.Cube, root, new Vector3(-0.5f, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.35f), color * 0.75f, new Vector3(0, 0, 20));
                    Part(PrimitiveType.Cube, root, new Vector3(0.5f, 0.05f, 0), new Vector3(0.6f, 0.05f, 0.35f), color * 0.75f, new Vector3(0, 0, -20));
                    Part(PrimitiveType.Cube, root, new Vector3(-0.14f, 0.3f, 0), new Vector3(0.1f, 0.2f, 0.1f), color * 0.8f, new Vector3(0, 0, 15));
                    Part(PrimitiveType.Cube, root, new Vector3(0.14f, 0.3f, 0), new Vector3(0.1f, 0.2f, 0.1f), color * 0.8f, new Vector3(0, 0, -15));
                    Eyes(root, 0.06f, 0.24f, 0.1f, 0.1f, new Color(1f, 0.25f, 0.2f));
                    break;

                case UnitShape.Goblin:
                    Part(PrimitiveType.Capsule, root, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.45f, 0.5f), new Color(0.5f, 0.32f, 0.2f));
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 1.08f, 0.02f), Vector3.one * 0.58f, color);
                    Part(PrimitiveType.Cube, root, new Vector3(-0.36f, 1.12f, 0), new Vector3(0.3f, 0.1f, 0.12f), color, new Vector3(0, 0, 25));
                    Part(PrimitiveType.Cube, root, new Vector3(0.36f, 1.12f, 0), new Vector3(0.3f, 0.1f, 0.12f), color, new Vector3(0, 0, -25));
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 1.0f, 0.3f), Vector3.one * 0.16f, color * 0.85f);
                    Eyes(root, 1.15f, 0.24f, 0.12f, 0.11f, new Color(1f, 0.9f, 0.2f));
                    Part(PrimitiveType.Cylinder, root, new Vector3(0.42f, 0.65f, 0.15f), new Vector3(0.14f, 0.38f, 0.14f), Palette.Wood, new Vector3(25, 0, -15));
                    break;

                case UnitShape.Mimic:
                    Chest(root, open: true);
                    for (int i = 0; i < 4; i++)
                        Part(PrimitiveType.Cube, root, new Vector3(-0.27f + i * 0.18f, 0.6f, 0.3f), new Vector3(0.08f, 0.12f, 0.08f), Color.white, new Vector3(0, 0, 45));
                    Part(PrimitiveType.Cube, root, new Vector3(0, 0.62f, 0.25f), new Vector3(0.3f, 0.04f, 0.4f), new Color(0.85f, 0.2f, 0.3f), new Vector3(-20, 0, 0));
                    Eyes(root, 0.9f, 0.22f, 0.18f, 0.12f, new Color(1f, 0.3f, 0.2f));
                    break;

                case UnitShape.Golem:
                    Part(PrimitiveType.Cube, root, new Vector3(0, 0.75f, 0), new Vector3(1.1f, 1f, 0.75f), color);
                    Part(PrimitiveType.Cube, root, new Vector3(0, 1.48f, 0.05f), new Vector3(0.55f, 0.45f, 0.5f), color * 1.08f);
                    Part(PrimitiveType.Cube, root, new Vector3(-0.75f, 0.7f, 0.05f), new Vector3(0.38f, 0.95f, 0.42f), color * 0.9f, new Vector3(0, 0, 8));
                    Part(PrimitiveType.Cube, root, new Vector3(0.75f, 0.7f, 0.05f), new Vector3(0.38f, 0.95f, 0.42f), color * 0.9f, new Vector3(0, 0, -8));
                    Part(PrimitiveType.Cube, root, new Vector3(-0.28f, 0.12f, 0), new Vector3(0.38f, 0.3f, 0.45f), color * 0.8f);
                    Part(PrimitiveType.Cube, root, new Vector3(0.28f, 0.12f, 0), new Vector3(0.38f, 0.3f, 0.45f), color * 0.8f);
                    Eyes(root, 1.52f, 0.29f, 0.13f, 0.12f, new Color(1f, 0.85f, 0.2f));
                    Part(PrimitiveType.Cube, root, new Vector3(0, 0.95f, 0.38f), new Vector3(0.3f, 0.3f, 0.05f), new Color(0.3f, 0.8f, 0.9f), new Vector3(0, 0, 45));
                    break;

                case UnitShape.Spider:
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.38f, -0.15f), new Vector3(0.7f, 0.5f, 0.8f), color);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.36f, 0.3f), Vector3.one * 0.38f, color * 1.15f);
                    for (int i = 0; i < 4; i++)
                    {
                        float z = 0.25f - i * 0.18f;
                        Part(PrimitiveType.Cube, root, new Vector3(-0.42f, 0.25f, z), new Vector3(0.55f, 0.05f, 0.06f), color * 0.7f, new Vector3(0, 0, -35));
                        Part(PrimitiveType.Cube, root, new Vector3(0.42f, 0.25f, z), new Vector3(0.55f, 0.05f, 0.06f), color * 0.7f, new Vector3(0, 0, 35));
                    }
                    Eyes(root, 0.42f, 0.47f, 0.08f, 0.08f, new Color(1f, 0.2f, 0.25f));
                    Eyes(root, 0.48f, 0.44f, 0.15f, 0.06f, new Color(1f, 0.2f, 0.25f));
                    break;

                case UnitShape.Skeleton:
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.62f, 0), new Vector3(0.14f, 0.32f, 0.14f), color);
                    for (int i = 0; i < 3; i++)
                        Part(PrimitiveType.Cube, root, new Vector3(0, 0.5f + i * 0.13f, 0), new Vector3(0.5f, 0.05f, 0.25f), color * 0.95f);
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 1.15f, 0.02f), Vector3.one * 0.46f, color);
                    Eyes(root, 1.17f, 0.18f, 0.1f, 0.12f, Palette.Eye);
                    Part(PrimitiveType.Cube, root, new Vector3(-0.1f, 0.15f, 0), new Vector3(0.08f, 0.32f, 0.08f), color);
                    Part(PrimitiveType.Cube, root, new Vector3(0.1f, 0.15f, 0), new Vector3(0.08f, 0.32f, 0.08f), color);
                    Part(PrimitiveType.Cube, root, new Vector3(0.38f, 0.72f, 0.2f), new Vector3(0.06f, 0.75f, 0.12f), new Color(0.7f, 0.72f, 0.78f), new Vector3(20, 0, 0));
                    break;

                case UnitShape.Ghost:
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.15f, 0), new Vector3(0.75f, 0.85f, 0.7f), color);
                    Part(PrimitiveType.Cube, root, new Vector3(0, -0.28f, 0), new Vector3(0.55f, 0.35f, 0.5f), color * 0.95f, new Vector3(0, 45, 0));
                    Eyes(root, 0.25f, 0.32f, 0.13f, 0.14f, new Color(0.15f, 0.1f, 0.3f));
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.05f, 0.34f), new Vector3(0.12f, 0.16f, 0.05f), new Color(0.15f, 0.1f, 0.3f));
                    break;

                case UnitShape.Elemental:
                    Part(PrimitiveType.Cube, root, new Vector3(0, 0.65f, 0), new Vector3(0.55f, 0.9f, 0.55f), color, new Vector3(0, 45, 12));
                    Part(PrimitiveType.Cube, root, new Vector3(-0.38f, 0.45f, 0.05f), new Vector3(0.28f, 0.55f, 0.28f), color * 0.9f, new Vector3(0, 30, 25));
                    Part(PrimitiveType.Cube, root, new Vector3(0.38f, 0.5f, -0.05f), new Vector3(0.3f, 0.6f, 0.3f), color * 0.85f, new Vector3(0, 60, -20));
                    Part(PrimitiveType.Cube, root, new Vector3(0, 1.25f, 0), new Vector3(0.3f, 0.4f, 0.3f), color * 1.15f, new Vector3(0, 45, 0));
                    Eyes(root, 0.85f, 0.3f, 0.12f, 0.1f, Color.white);
                    break;

                case UnitShape.KingSlime:
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.36f, 0), new Vector3(1f, 0.72f, 1f), color);
                    Eyes(root, 0.48f, 0.42f, 0.16f, 0.13f, Palette.Eye);
                    Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.8f, 0), new Vector3(0.42f, 0.1f, 0.42f), Palette.Gold);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Part(PrimitiveType.Cube, root, new Vector3(Mathf.Sin(a) * 0.17f, 0.95f, Mathf.Cos(a) * 0.17f), new Vector3(0.07f, 0.16f, 0.07f), Palette.Gold);
                    }
                    Part(PrimitiveType.Sphere, root, new Vector3(0, 0.92f, 0.2f), Vector3.one * 0.08f, new Color(1f, 0.2f, 0.3f));
                    break;

                case UnitShape.Knight:
                    Part(PrimitiveType.Capsule, root, new Vector3(0, 0.6f, 0), new Vector3(0.66f, 0.55f, 0.55f), color);
                    Part(PrimitiveType.Cube, root, new Vector3(0, 1.28f, 0), new Vector3(0.48f, 0.5f, 0.48f), color * 0.8f);
                    Part(PrimitiveType.Cube, root, new Vector3(0, 1.3f, 0.25f), new Vector3(0.34f, 0.07f, 0.02f), new Color(0.5f, 0.9f, 1f));
                    Part(PrimitiveType.Cube, root, new Vector3(0, 1.62f, 0), new Vector3(0.08f, 0.2f, 0.4f), new Color(0.5f, 0.2f, 0.7f));
                    Part(PrimitiveType.Cube, root, new Vector3(0.45f, 0.75f, 0.25f), new Vector3(0.08f, 1.1f, 0.14f), new Color(0.75f, 0.78f, 0.85f), new Vector3(25, 0, 0));
                    Part(PrimitiveType.Cube, root, new Vector3(-0.42f, 0.65f, 0.15f), new Vector3(0.08f, 0.6f, 0.45f), color * 0.7f);
                    break;

                default:
                    Part(PrimitiveType.Capsule, root, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.6f, 0.6f), color);
                    break;
            }
            root.localScale = Vector3.one * scale;
            return root;
        }

        public static Transform EnemyFor(Transform parent, EnemyDefinition def)
        {
            if (def.visualPrefab != null)
            {
                var instance = Object.Instantiate(def.visualPrefab, parent, false);
                instance.name = "Visual";
                return instance.transform;
            }
            return Enemy(parent, def.shape, def.color, def.scale);
        }

        /// <summary>Baú (também usado pelo Mímico). O tampo fica num pivô "Lid" para animar a abertura.</summary>
        public static Transform Chest(Transform parent, bool open = false)
        {
            var root = parent.name == "Visual" ? parent : Root(parent, "Visual");
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), Palette.Wood);
            Part(PrimitiveType.Cube, root, new Vector3(0, 0.25f, 0.305f), new Vector3(0.92f, 0.08f, 0.02f), Palette.Gold);
            var lid = new GameObject("Lid").transform;
            lid.SetParent(root, false);
            lid.localPosition = new Vector3(0, 0.5f, -0.3f);
            Part(PrimitiveType.Cube, lid, new Vector3(0, 0.1f, 0.3f), new Vector3(0.92f, 0.2f, 0.62f), Palette.DarkWood);
            Part(PrimitiveType.Cube, lid, new Vector3(0, 0.1f, 0.6f), new Vector3(0.14f, 0.16f, 0.04f), Palette.Gold);
            if (open) lid.localEulerAngles = new Vector3(-35, 0, 0);
            return root;
        }

        public static Transform Crate(Transform parent)
        {
            var root = Root(parent, "Visual");
            Part(PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.8f, new Color(0.85f, 0.62f, 0.3f));
            Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.84f, 0.12f, 0.84f), Palette.DarkWood);
            Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.12f, 0.84f, 0.84f), Palette.DarkWood);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 0, -0.43f), Vector3.one * 0.18f, Palette.Gold);
            return root;
        }

        public static Transform Tree(Transform parent, float size)
        {
            var root = Root(parent, "Visual");
            Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.5f * size, 0), new Vector3(0.28f, 0.5f * size, 0.28f), Palette.Wood);
            var leaves = new Color(0.25f, 0.6f, 0.3f);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 1.35f * size, 0), Vector3.one * 1.3f * size, leaves);
            Part(PrimitiveType.Sphere, root, new Vector3(0.25f, 1.85f * size, 0.1f), Vector3.one * 0.85f * size, leaves * 1.1f);
            return root;
        }

        public static Transform Rock(Transform parent, float size)
        {
            var root = Root(parent, "Visual");
            Part(PrimitiveType.Sphere, root, new Vector3(0, 0.25f * size, 0), new Vector3(0.95f, 0.6f, 0.8f) * size, new Color(0.55f, 0.55f, 0.58f), new Vector3(0, 30, 10));
            Part(PrimitiveType.Sphere, root, new Vector3(0.3f, 0.18f * size, 0.2f), new Vector3(0.5f, 0.4f, 0.45f) * size, new Color(0.5f, 0.5f, 0.53f));
            return root;
        }

        public static Transform Campfire(Transform parent)
        {
            var root = Root(parent, "Visual");
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.35f, 0.12f), Palette.Wood, new Vector3(90, i * 60f, 0));
            var flame = Part(PrimitiveType.Sphere, root, new Vector3(0, 0.32f, 0), new Vector3(0.35f, 0.5f, 0.35f), new Color(1f, 0.55f, 0.15f));
            flame.name = "Flame";
            Part(PrimitiveType.Sphere, flame.transform, new Vector3(0, 0.1f, 0), Vector3.one * 0.6f, new Color(1f, 0.9f, 0.4f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Sin(a) * 0.38f, 0.06f, Mathf.Cos(a) * 0.38f), Vector3.one * 0.18f, new Color(0.5f, 0.5f, 0.52f));
            }
            return root;
        }

        /// <summary>Pilar de luz + gema girando na cor da melhor raridade que caiu (o "brilho do loot").</summary>
        public static async Awaitable LootBeam(Transform parent, Vector3 at, Color color, System.Threading.CancellationToken ct)
        {
            var beam = Part(PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.25f, 0.01f, 0.25f), color).transform;
            var gem = Part(PrimitiveType.Cube, parent, Vector3.zero, Vector3.one * 0.3f, color, new Vector3(45, 0, 45)).transform;
            beam.position = at;
            gem.position = at;
            try
            {
                await Tween.Run(1.3f, k =>
                {
                    if (!beam || !gem) return;
                    float h = Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) * 3f;
                    float w = 0.25f * (1f - Mathf.Max(0f, k - 0.7f) / 0.3f);
                    beam.localScale = new Vector3(w, h, w);
                    beam.position = at + Vector3.up * h;
                    gem.position = at + Vector3.up * (0.5f + Mathf.Sin(k * Mathf.PI) * 1.2f);
                    gem.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
                }, ct);
            }
            finally
            {
                if (beam) Object.Destroy(beam.gameObject);
                if (gem) Object.Destroy(gem.gameObject);
            }
        }

        /// <summary>Sombra-redonda escura no chão (fundamental para ler altura na câmera isométrica).</summary>
        public static Transform BlobShadow(Transform parent, float radius)
        {
            var go = Part(PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f), Palette.Shadow);
            go.name = "BlobShadow";
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }
    }
}
