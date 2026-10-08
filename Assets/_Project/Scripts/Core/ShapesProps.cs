using UnityEngine;
using UnityEngine.Rendering;

namespace Oiram.Core
{
    /// <summary>Objetos do mundo: baú, caixa, árvores, pedras, arbustos, fogueira.</summary>
    public static partial class Shapes
    {
        public static readonly Color Leaves = new(0.3f, 0.64f, 0.3f);

        /// <summary>Baú (também usado pelo Mímico). O tampo fica num pivô "Lid" para animar a abertura.</summary>
        public static Transform Chest(Transform parent, bool open = false)
        {
            var root = parent.name == "Visual" ? parent : Root(parent, "Visual");
            var gold = Palette.Glossy(Palette.Gold, 0f);
            Part(Round(0.14f), root, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.6f), Palette.Wood);
            foreach (float x in new[] { -0.3f, 0.3f })
                Part(Round(0.25f), root, new Vector3(x, 0.25f, 0f), new Vector3(0.1f, 0.52f, 0.63f), gold);
            var lid = Pivot(root, "Lid", new Vector3(0f, 0.5f, -0.3f));
            Part(Round(0.2f), lid, new Vector3(0f, 0.1f, 0.3f), new Vector3(0.92f, 0.22f, 0.62f), Palette.DarkWood);
            foreach (float x in new[] { -0.3f, 0.3f })
                Part(Round(0.3f), lid, new Vector3(x, 0.1f, 0.3f), new Vector3(0.1f, 0.24f, 0.65f), gold);
            Part(Round(0.3f), lid, new Vector3(0f, 0.04f, 0.61f), new Vector3(0.16f, 0.18f, 0.06f), gold);
            if (open) lid.localEulerAngles = new Vector3(-35, 0, 0);
            return root;
        }

        public static Transform Crate(Transform parent)
        {
            var root = Root(parent, "Visual");
            Part(Round(0.12f), root, Vector3.zero, Vector3.one * 0.8f, new Color(0.9f, 0.66f, 0.3f));
            var frame = new Color(0.55f, 0.34f, 0.18f);
            foreach (var axis in new[] { new Vector3(0.84f, 0.12f, 0.84f), new Vector3(0.12f, 0.84f, 0.84f), new Vector3(0.84f, 0.84f, 0.12f) })
                Part(Round(0.3f), root, Vector3.zero, axis, frame);
            Part(MeshLibrary.Icosphere(0), root, new Vector3(0f, 0f, -0.44f), Vector3.one * 0.2f, Palette.Emissive(Palette.Gold, 1.2f, Palette.Outline));
            Part(MeshLibrary.Icosphere(0), root, new Vector3(0f, 0f, 0.44f), Vector3.one * 0.2f, Palette.Emissive(Palette.Gold, 1.2f, Palette.Outline));
            return root;
        }

        /// <summary>Árvore com copa facetada (estilo low-poly toon). Variantes: copa redonda ou pinheiro.</summary>
        public static Transform Tree(Transform parent, float size, int variant = 0)
        {
            var root = Root(parent, "Visual");
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.5f * size, 0f), new Vector3(0.26f, 0.5f * size, 0.26f) , Palette.Wood);
            Part(MeshLibrary.Cone(8), root, new Vector3(0f, 0.12f * size, 0f), new Vector3(0.5f, 0.25f, 0.5f) * size, Palette.Wood * 0.9f);
            var leaves = Color.Lerp(Leaves, new Color(0.45f, 0.7f, 0.28f), (variant % 3) / 2f);
            if (variant % 4 == 3)
            {
                for (int i = 0; i < 3; i++)
                    Part(MeshLibrary.Cone(7, smooth: false), root, new Vector3(0f, (1.2f + i * 0.5f) * size, 0f),
                        new Vector3(1.5f - i * 0.38f, 0.9f, 1.5f - i * 0.38f) * size, leaves * (0.85f + i * 0.08f), new Vector3(0f, i * 20f, 0f));
                return root;
            }
            Part(MeshLibrary.Icosphere(1, true, 0.08f, variant), root, new Vector3(0f, 1.4f * size, 0f), Vector3.one * 1.4f * size, leaves);
            Part(MeshLibrary.Icosphere(1, true, 0.1f, variant + 7), root, new Vector3(0.32f, 1.9f * size, 0.1f), Vector3.one * 0.95f * size, leaves * 1.1f);
            Part(MeshLibrary.Icosphere(1, true, 0.1f, variant + 13), root, new Vector3(-0.35f, 1.68f * size, -0.22f), Vector3.one * 0.8f * size, leaves * 0.95f);
            return root;
        }

        public static Transform Rock(Transform parent, float size, int variant = 0)
        {
            var root = Root(parent, "Visual");
            var stone = new Color(0.6f, 0.6f, 0.64f);
            Part(MeshLibrary.Icosphere(1, true, 0.16f, 30 + variant % 4), root, new Vector3(0f, 0.22f * size, 0f), new Vector3(0.95f, 0.62f, 0.82f) * size, stone, new Vector3(0, 30, 10));
            Part(MeshLibrary.Icosphere(1, true, 0.16f, 40 + variant % 4), root, new Vector3(0.34f, 0.14f * size, 0.22f), new Vector3(0.5f, 0.42f, 0.46f) * size, stone * 0.92f);
            Part(PrimitiveType.Sphere, root, new Vector3(-0.1f, 0.42f * size, 0.05f), new Vector3(0.36f, 0.08f, 0.3f) * size, new Color(0.42f, 0.66f, 0.33f));
            return root;
        }

        public static Transform Bush(Transform parent, float size, int variant = 0)
        {
            var root = Root(parent, "Visual");
            var leaves = Color.Lerp(Leaves, new Color(0.4f, 0.7f, 0.3f), (variant % 3) / 2f);
            Part(MeshLibrary.Icosphere(1, true, 0.1f, 50 + variant % 5), root, new Vector3(0f, 0.3f * size, 0f), Vector3.one * 0.7f * size, leaves);
            Part(MeshLibrary.Icosphere(1, true, 0.1f, 60 + variant % 5), root, new Vector3(0.3f, 0.22f * size, 0.1f), Vector3.one * 0.5f * size, leaves * 1.08f);
            if (variant % 2 == 0)
                for (int i = 0; i < 3; i++)
                    Part(PrimitiveType.Sphere, root, new Vector3(-0.15f + i * 0.17f, 0.52f * size, 0.22f - i * 0.05f), Vector3.one * 0.1f, Palette.Plain(new Color(1f, 0.35f, 0.4f)));
            return root;
        }

        public static Transform Campfire(Transform parent)
        {
            var root = Root(parent, "Visual");
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.08f, 0), new Vector3(0.12f, 0.35f, 0.12f), Palette.Wood, new Vector3(90, i * 60f, 0));
            var flame = Pivot(root, "Flame", new Vector3(0f, 0.32f, 0f));
            flame.localScale = new Vector3(0.35f, 0.5f, 0.35f);
            Part(MeshLibrary.Drop(), flame, new Vector3(0f, 0.1f, 0f), Vector3.one, Palette.Emissive(new Color(1f, 0.45f, 0.12f), 2.4f));
            Part(MeshLibrary.Drop(), flame, new Vector3(0f, 0.05f, 0f), Vector3.one * 0.6f, Palette.Emissive(new Color(1f, 0.85f, 0.35f), 2.6f));
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                Part(MeshLibrary.Icosphere(0, true, 0.15f, i), root, new Vector3(Mathf.Sin(a) * 0.4f, 0.07f, Mathf.Cos(a) * 0.4f), Vector3.one * 0.2f, new Color(0.52f, 0.52f, 0.55f));
            }
            GlowQuad(root, new Vector3(0f, 0.03f, 0f), 2.2f, Palette.Glow(new Color(1f, 0.55f, 0.2f, 0.35f)), new Vector3(90f, 0f, 0f)).name = "FireGlow";
            var light = new GameObject("FireLight").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<LightFlicker>();
            Fx.Embers(root, new Vector3(0f, 0.45f, 0f), new Color(1f, 0.6f, 0.25f));
            return root;
        }

        /// <summary>Nuvem fofinha (bolhas facetadas brancas) que flutua devagar.</summary>
        public static Transform Cloud(Transform parent, float size, int seed)
        {
            var root = Root(parent, "Cloud");
            var rng = new System.Random(seed);
            int puffs = 4 + rng.Next(3);
            for (int i = 0; i < puffs; i++)
            {
                float x = (i - (puffs - 1) / 2f) * 0.55f * size;
                float s = (0.8f + (float)rng.NextDouble() * 0.6f) * size * (i == 0 || i == puffs - 1 ? 0.7f : 1f);
                Part(MeshLibrary.Icosphere(1, true, 0.06f, seed + i), root, new Vector3(x, (float)rng.NextDouble() * 0.2f * size, (float)rng.NextDouble() * 0.3f * size),
                    new Vector3(s, s * 0.7f, s * 0.8f), Palette.Toon(new Color(0.98f, 0.98f, 1f), Palette.Outline, 0f, 0.4f));
            }
            Wiggle.Add(root.gameObject, Wiggle.Mode.Bob, Vector3.up, 0.15f, 0.5f, seed);
            return root;
        }

        /// <summary>Espalha tufos e flores (uma malha só) num disco de raio <paramref name="radius"/>, evitando <paramref name="keepClear"/>.</summary>
        public static GameObject MeadowScatter(Transform parent, string name, Vector3 center, float radius, float y, int count, int seed, Color grass,
            System.Func<Vector3, bool> keepClear = null)
        {
            var builder = new MeshBuilder();
            var rng = new System.Random(seed);
            Color[] petals = { Color.white, new(1f, 0.85f, 0.3f), new(1f, 0.55f, 0.7f), new(0.65f, 0.75f, 1f) };
            var cone = MeshLibrary.Cone(4, smooth: false);
            var sphere = MeshLibrary.Icosphere(1, faceted: false); // pétalas: 42 vértices em vez de 515
            var cylinder = MeshLibrary.Cone(5, smooth: false); // caule fininho
            for (int n = 0; n < count; n++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                var p = center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                if (keepClear != null && keepClear(p)) continue;
                if (rng.NextDouble() < 0.7)
                {
                    float yaw = (float)rng.NextDouble() * 360f;
                    for (int i = 0; i < 3; i++)
                        builder.AddMesh(cone, Matrix4x4.TRS(p + Quaternion.Euler(0f, yaw, 0f) * new Vector3((i - 1) * 0.07f, 0.08f, (i % 2) * 0.04f),
                            Quaternion.Euler(0f, yaw + i * 40f, (i - 1) * 18f), new Vector3(0.07f, 0.2f - (i % 2) * 0.05f, 0.04f)), grass * (0.92f + i * 0.06f));
                }
                else
                {
                    var petal = petals[rng.Next(petals.Length)];
                    builder.AddMesh(cylinder, Matrix4x4.TRS(p + Vector3.up * 0.085f, Quaternion.identity, new Vector3(0.03f, 0.17f, 0.03f)), new Color(0.3f, 0.6f, 0.25f));
                    for (int i = 0; i < 5; i++)
                    {
                        float pa = i / 5f * Mathf.PI * 2f;
                        builder.AddMesh(sphere, Matrix4x4.TRS(p + new Vector3(Mathf.Cos(pa) * 0.05f, 0.17f, Mathf.Sin(pa) * 0.05f), Quaternion.identity, new Vector3(0.07f, 0.025f, 0.07f)), petal);
                    }
                    builder.AddMesh(sphere, Matrix4x4.TRS(p + Vector3.up * 0.18f, Quaternion.identity, Vector3.one * 0.045f), new Color(1f, 0.85f, 0.3f));
                }
            }
            var mesh = MeshLibrary.Store(builder.Build(name));
            var go = Part(mesh, parent, Vector3.zero, Vector3.one, Palette.Terrain(0f));
            go.name = "Decor";
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Tufo de grama (3 folhinhas) para espalhar no chão.</summary>
        public static Transform GrassTuft(Transform parent, Color color)
        {
            var root = Root(parent, "Tuft");
            for (int i = 0; i < 3; i++)
                Part(MeshLibrary.Cone(4, smooth: false), root, new Vector3((i - 1) * 0.07f, 0.09f, (i % 2) * 0.04f), new Vector3(0.07f, 0.2f - (i % 2) * 0.05f, 0.04f),
                    Palette.Toon(color * (0.9f + i * 0.06f), 0f), new Vector3(0f, i * 40f, (i - 1) * 18f));
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        public static Transform Flower(Transform parent, Color petals)
        {
            var root = Root(parent, "Flower");
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.08f, 0f), new Vector3(0.025f, 0.08f, 0.025f), Palette.Plain(new Color(0.3f, 0.6f, 0.25f)));
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a) * 0.05f, 0.17f, Mathf.Sin(a) * 0.05f), new Vector3(0.07f, 0.025f, 0.07f), Palette.Plain(petals));
            }
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.045f, Palette.Plain(new Color(1f, 0.85f, 0.3f)));
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }
    }
}
