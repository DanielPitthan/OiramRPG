using System;
using System.Collections.Generic;
using Oiram.Battle;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Oiram.Core
{
    public enum GlowShape
    {
        /// <summary>Círculo suave pelo UV (quads e partículas).</summary>
        Circle = 0,
        /// <summary>Some nas bordas vistas de lado (pilares, auras).</summary>
        Fresnel = 1,
        /// <summary>Cor chapada.</summary>
        Solid = 2,
    }

    /// <summary>
    /// Materiais toon do jogo (um por cor/estilo, em cache). Todos derivam de materiais-base em Resources,
    /// o que garante os shaders no build. No editor, <see cref="Persist"/> salva cada material como asset.
    /// </summary>
    public static class Palette
    {
        public const string ToonResource = "Materials/OiramToon";
        public const string GlowResource = "Materials/OiramGlow";
        public const string WaterResource = "Materials/OiramWater";
        public const string SkyResource = "Materials/OiramSky";
        public const float Outline = 0.022f;

        /// <summary>O editor troca isto para salvar os materiais como assets ao "assar" as cenas.</summary>
        public static Func<Material, Material> Persist;

        static readonly Dictionary<string, Material> cache = new();
        static readonly Dictionary<string, Material> bases = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            bases.Clear();
        }

        public static void ClearCache()
        {
            cache.Clear();
            bases.Clear();
        }

        /// <summary>Chave da cor (cores HDR > 1 entram com o multiplicador, para não colidirem com a versão normal).</summary>
        static string Hex(Color c)
        {
            float m = c.maxColorComponent;
            if (m <= 1f) return ColorUtility.ToHtmlStringRGBA(c);
            return ColorUtility.ToHtmlStringRGBA(new Color(c.r / m, c.g / m, c.b / m, c.a)) + "x" + (m * 100f).ToString("0");
        }

        static Material Base(string resource, string shaderName)
        {
            if (bases.TryGetValue(resource, out var mat) && mat != null) return mat;
            mat = Resources.Load<Material>(resource);
            if (mat == null)
            {
                var shader = Shader.Find(shaderName) ?? Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader) { name = resource };
            }
            bases[resource] = mat;
            return mat;
        }

        static Material Cached(string key, string resource, string shader, Action<Material> setup)
        {
            if (cache.TryGetValue(key, out var mat) && mat != null) return mat;
            mat = new Material(Base(resource, shader)) { name = key };
            setup(mat);
            if (Persist != null) mat = Persist(mat);
            cache[key] = mat;
            return mat;
        }

        /// <summary>Cor do contorno: um tom bem escuro puxado para a própria cor (mais macio que preto puro).</summary>
        public static Color OutlineFor(Color c) => Color.Lerp(new Color(0.08f, 0.06f, 0.09f), c * 0.35f, 0.35f);

        static void SetToon(Material m, Color color, float outline, float gloss, float rim)
        {
            m.SetColor("_BaseColor", color);
            m.SetColor("_ShadowColor", new Color(0.66f, 0.63f, 0.84f));
            m.SetFloat("_OutlineWidth", outline);
            m.SetColor("_OutlineColor", OutlineFor(color));
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_RimStrength", rim);
        }

        /// <summary>Material padrão: toon com contorno.</summary>
        public static Material Get(Color color) => Toon(color);

        public static Material Toon(Color color, float outline = Outline, float gloss = 0f, float rim = 0.25f) =>
            Cached($"T_{Hex(color)}_{outline * 1000f:0}_{gloss * 100f:0}_{rim * 100f:0}", ToonResource, "Oiram/Toon",
                m => SetToon(m, color, outline, gloss, rim));

        /// <summary>Sem contorno (olhos, bocas e detalhes pequenos).</summary>
        public static Material Plain(Color color) => Toon(color, 0f);

        /// <summary>Com brilho especular (slimes, metal, olhos vivos).</summary>
        public static Material Glossy(Color color, float outline = Outline) => Toon(color, outline, 0.75f, 0.35f);

        /// <summary>Brilha sozinho (vira bloom): chamas, cristais, olhos de golem, janelas acesas.</summary>
        public static Material Emissive(Color color, float intensity = 1.6f, float outline = 0f) =>
            Cached($"E_{Hex(color)}_{intensity * 100f:0}_{outline * 1000f:0}", ToonResource, "Oiram/Toon", m =>
            {
                SetToon(m, color, outline, 0f, 0f);
                m.SetColor("_EmissionColor", color * intensity);
            });

        /// <summary>Translúcido (fantasmas, vidro).</summary>
        public static Material Ghost(Color color, float alpha = 0.6f) =>
            Cached($"G_{Hex(color)}_{alpha * 100f:0}", ToonResource, "Oiram/Toon", m =>
            {
                var c = color;
                c.a = alpha;
                SetToon(m, c, 0f, 0.5f, 0.5f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
            });

        /// <summary>Terreno: cores por vértice (grama, laterais em degradê).</summary>
        public static Material Terrain(float outline = 0.018f) =>
            Cached($"V_{outline * 1000f:0}", ToonResource, "Oiram/Toon", m =>
            {
                SetToon(m, Color.white, outline, 0f, 0.12f);
                m.SetColor("_OutlineColor", new Color(0.1f, 0.08f, 0.07f));
                m.SetFloat("_UseVertexColor", 1f);
            });

        /// <summary>Brilho sem luz: aditivo (luz, faíscas) ou alfa normal (sombra suave, fumaça).</summary>
        public static Material Glow(Color color, GlowShape shape = GlowShape.Circle, bool additive = true, float softness = 0.6f) =>
            Cached($"F_{Hex(color)}_{(int)shape}_{(additive ? 1 : 0)}_{softness * 100f:0}", GlowResource, "Oiram/Glow", m =>
            {
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Shape", (float)shape);
                m.SetFloat("_Softness", softness);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.renderQueue = (int)RenderQueue.Transparent;
            });

        public static Material Water(Color shallow, Color deep) =>
            Cached($"W_{Hex(shallow)}_{Hex(deep)}", WaterResource, "Oiram/Water", m =>
            {
                m.SetColor("_BaseColor", shallow);
                m.SetColor("_DeepColor", deep);
            });

        public static Material Sky(Color top, Color horizon, Color bottom) =>
            Cached($"S_{Hex(top)}_{Hex(horizon)}_{Hex(bottom)}", SkyResource, "Oiram/Sky", m =>
            {
                m.SetColor("_TopColor", top);
                m.SetColor("_HorizonColor", horizon);
                m.SetColor("_BottomColor", bottom);
            });

        /// <summary>Branco "aceso" para o piscar de dano.</summary>
        public static Material Flash => Emissive(Color.white, 1.2f);

        public static readonly Color Skin = new(1f, 0.84f, 0.68f);
        public static readonly Color Eye = new(0.08f, 0.08f, 0.1f);
        public static readonly Color Shadow = new(0.12f, 0.13f, 0.16f);
        public static readonly Color Wood = new(0.55f, 0.36f, 0.2f);
        public static readonly Color DarkWood = new(0.38f, 0.24f, 0.13f);
        public static readonly Color Gold = new(0.98f, 0.78f, 0.25f);
        public static readonly Color Blush = new(1f, 0.55f, 0.55f);
    }

    /// <summary>Monta personagens e objetos com primitivas e malhas geradas (troque por prefabs de arte depois).</summary>
    public static partial class Shapes
    {
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color,
            Vector3 localEuler = default, bool keepCollider = false) =>
            Part(type, parent, localPos, localScale, Palette.Get(color), localEuler, keepCollider);

        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material material,
            Vector3 localEuler = default, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshFilter>().sharedMesh = MeshLibrary.Get(type);
            Place(go.transform, parent, localPos, localScale, localEuler);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Peça com uma malha gerada (<see cref="MeshLibrary"/>): caixa arredondada, cone, icosfera...</summary>
        public static GameObject Part(Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale, Material material, Vector3 localEuler = default)
        {
            var go = new GameObject(mesh.name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            Place(go.transform, parent, localPos, localScale, localEuler);
            return go;
        }

        public static GameObject Part(Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Vector3 localEuler = default) =>
            Part(mesh, parent, localPos, localScale, Palette.Get(color), localEuler);

        static void Place(Transform t, Transform parent, Vector3 localPos, Vector3 localScale, Vector3 localEuler)
        {
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = localScale;
            t.localEulerAngles = localEuler;
        }

        /// <summary>Quad de brilho/sombra (sem sombra projetada).</summary>
        public static GameObject GlowQuad(Transform parent, Vector3 localPos, float size, Material material, Vector3 localEuler = default)
        {
            var go = Part(MeshLibrary.Quad, parent, localPos, Vector3.one * size, material, localEuler);
            go.name = "Glow";
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static Transform Root(Transform parent, string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
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

        /// <summary>Pilar de luz + gema girando na cor da melhor raridade que caiu (o "brilho do loot").</summary>
        public static async Awaitable LootBeam(Transform parent, Vector3 at, Color color, System.Threading.CancellationToken ct)
        {
            var beam = Part(PrimitiveType.Cylinder, parent, Vector3.zero, new Vector3(0.3f, 0.01f, 0.3f),
                Palette.Glow(new Color(color.r, color.g, color.b, 0.85f) * 2.2f, GlowShape.Fresnel, additive: true, softness: 0.9f)).transform;
            beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var gem = Part(MeshLibrary.Icosphere(0), parent, Vector3.zero, Vector3.one * 0.32f, Palette.Emissive(color, 2.2f, Palette.Outline)).transform;
            var pool = GlowQuad(parent, Vector3.zero, 1.6f, Palette.Glow(new Color(color.r, color.g, color.b, 0.5f)), new Vector3(90f, 0f, 0f)).transform;
            beam.position = at;
            gem.position = at;
            pool.position = at + Vector3.up * 0.04f;
            Fx.Rising(at, color, 0.35f, 16, 1.2f);
            try
            {
                await Tween.Run(1.4f, k =>
                {
                    if (!beam || !gem || !pool) return;
                    float h = Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI * 0.5f) * 3.2f;
                    float fade = 1f - Mathf.Max(0f, k - 0.7f) / 0.3f;
                    beam.localScale = new Vector3(0.3f * fade, h, 0.3f * fade);
                    beam.position = at + Vector3.up * h;
                    gem.position = at + Vector3.up * (0.5f + Mathf.Sin(k * Mathf.PI) * 1.2f);
                    gem.localScale = Vector3.one * 0.32f * Mathf.Max(0.2f, fade);
                    gem.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
                    pool.localScale = Vector3.one * 1.6f * fade;
                }, ct);
            }
            finally
            {
                if (beam) Object.Destroy(beam.gameObject);
                if (gem) Object.Destroy(gem.gameObject);
                if (pool) Object.Destroy(pool.gameObject);
            }
        }

        /// <summary>
        /// Explosão de pedacinhos que voam para fora e encolhem (poeira ao cair, estrelas no golpe perfeito).
        /// <paramref name="up"/> &gt; 0 joga as partículas para cima.
        /// </summary>
        public static async Awaitable Burst(Transform parent, Vector3 at, Color color, int count, float radius, float size,
            float duration, float up, System.Threading.CancellationToken ct, bool glowing = false)
        {
            var parts = new Transform[count];
            var dirs = new Vector3[count];
            var material = glowing ? Palette.Emissive(color, 2.2f) : Palette.Plain(color);
            for (int i = 0; i < count; i++)
            {
                float a = (i + UnityEngine.Random.value * 0.5f) / count * Mathf.PI * 2f;
                dirs[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var go = Part(MeshLibrary.Icosphere(0), parent, Vector3.zero, Vector3.one * size, material, new Vector3(45, i * 30f, 45));
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                parts[i] = go.transform;
                parts[i].position = at;
            }
            try
            {
                await Tween.Run(duration, k =>
                {
                    float e = Tween.EaseOutQuad(k);
                    for (int i = 0; i < count; i++)
                    {
                        if (!parts[i]) continue;
                        parts[i].position = at + dirs[i] * (radius * e) + Vector3.up * (up * Mathf.Sin(k * Mathf.PI));
                        parts[i].localScale = Vector3.one * (size * (1f - k));
                    }
                }, ct);
            }
            finally
            {
                foreach (var p in parts) if (p) Object.Destroy(p.gameObject);
            }
        }

        /// <summary>Sombra-redonda suave no chão (fundamental para ler altura na câmera isométrica, principalmente no pulo).</summary>
        public static Transform BlobShadow(Transform parent, float radius)
        {
            var go = GlowQuad(parent, Vector3.zero, radius * 2.3f, Palette.Glow(new Color(0.06f, 0.05f, 0.14f, 0.42f), GlowShape.Circle, additive: false, softness: 0.75f),
                new Vector3(90f, 0f, 0f));
            go.name = "BlobShadow";
            return go.transform;
        }
    }
}
