using System;
using Oiram.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oiram.World
{
    public enum TerrainDecor
    {
        None,
        /// <summary>Grama alta e flores (Vale).</summary>
        Meadow,
        /// <summary>Menos mato, mais flores (cidade).</summary>
        Town,
        /// <summary>Pedrinhas e cristais brilhando na cor do tema (dungeons).</summary>
        Cave,
    }

    /// <summary>Cores do terreno em blocos (Vale: grama e água; dungeons: pedra e abismo).</summary>
    public sealed class TerrainTheme
    {
        public Color TopLow = new(0.42f, 0.72f, 0.34f);
        public Color TopHigh = new(0.62f, 0.83f, 0.38f);
        public Color Side = new(0.62f, 0.44f, 0.28f);
        public Color Void = new(0.27f, 0.56f, 0.86f);
        /// <summary>Altura da superfície do vazio: logo abaixo do chão (água) ou bem fundo (abismo).</summary>
        public float VoidY = -0.4f;
        public float Checker = 0.965f;
        public int MaxHeightForColor = 4;
        public bool Water = true;
        public TerrainDecor Decor = TerrainDecor.Meadow;
        public Color Accent = new(1f, 0.8f, 0.4f);

        public static TerrainTheme Meadow() => new();

        public static TerrainTheme Town() => new()
        {
            TopLow = new Color(0.55f, 0.76f, 0.4f),
            TopHigh = new Color(0.86f, 0.76f, 0.58f),
            Side = new Color(0.64f, 0.52f, 0.4f),
            Decor = TerrainDecor.Town,
        };

        public static TerrainTheme FromDungeon(DungeonDefinition d) => new()
        {
            TopLow = d.floorColor,
            TopHigh = d.floorHighColor,
            Side = d.sideColor,
            Void = d.voidColor,
            VoidY = -8f,
            Checker = 0.94f,
            MaxHeightForColor = 2,
            Water = false,
            Decor = TerrainDecor.Cave,
            Accent = d.accentColor,
        };
    }

    /// <summary>
    /// Constrói o terreno de um <see cref="BlockMap"/> como uma malha única com cores por vértice: tampo com
    /// variação, borda de grama, laterais em camadas que escurecem para baixo e sombra nos cantos junto a paredes.
    /// Mais água animada com espuma (ou abismo), decoração espalhada, colisão e paredes invisíveis.
    /// </summary>
    public static class BlockTerrain
    {
        const float Band = 0.13f;

        public static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static float Hash(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static string MapKey(BlockMap map)
        {
            unchecked
            {
                int h = map.Width * 31 + map.Depth;
                for (int r = 0; r < map.Depth; r++)
                for (int c = 0; c < map.Width; c++)
                    h = h * 31 + (map.HeightAt(r, c) ?? -1) * 7 + map.ObjectAt(r, c);
                return $"{map.Width}x{map.Depth}_{h:X8}";
            }
        }

        /// <param name="markStatic">O editor marca como estático (batching); em runtime pode ser null.</param>
        public static void Build(BlockMap map, Transform parent, TerrainTheme theme, Action<GameObject> markStatic = null)
        {
            string key = MapKey(map);
            float bottom = theme.VoidY < -1f ? theme.VoidY + 0.5f : -1f;
            var terrain = Group(parent, "Terrain");
            var voidRoot = Group(parent, "Void");

            // ---------------- malha do terreno
            var builder = new MeshBuilder();
            var foam = theme.Water ? new MeshBuilder() : null;
            int? H(int r, int c) => map.HeightAt(r, c);

            for (int row = 0; row < map.Depth; row++)
            for (int col = 0; col < map.Width; col++)
            {
                if (H(row, col) is not int h) continue;
                var center = map.TileTop(row, col);
                float top = center.y;
                float x0 = col - 0.5f, x1 = col + 0.5f, z0 = center.z - 0.5f, z1 = center.z + 0.5f;

                float variation = 1f + (Hash(row, col, 1) - 0.5f) * 0.08f;
                var topColor = Color.Lerp(theme.TopLow, theme.TopHigh, Mathf.Clamp01(h / (float)Mathf.Max(1, theme.MaxHeightForColor)));
                topColor *= variation * ((row + col) % 2 == 0 ? 1f : theme.Checker);
                topColor.a = 1f;

                // Oclusão nos cantos: vizinhos mais altos escurecem o canto (dá volume sem luz baked).
                Color Corner(int dc, int dr)
                {
                    int occluders = 0;
                    if ((H(row, col + dc) ?? -99) > h) occluders++;
                    if ((H(row + dr, col) ?? -99) > h) occluders++;
                    if ((H(row + dr, col + dc) ?? -99) > h) occluders++;
                    var c = topColor * (1f - 0.13f * occluders);
                    c.a = 1f;
                    return c;
                }

                // +z é a linha de cima (row − 1).
                builder.Quad(new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), Vector3.up,
                    Corner(-1, 1), Corner(-1, -1), Corner(1, -1), Corner(1, 1));

                // Laterais: só onde o vizinho é mais baixo (ou vazio).
                SideFace(builder, theme, map, row, col, h, top, bottom, 0, -1, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), Vector3.forward, topColor);
                SideFace(builder, theme, map, row, col, h, top, bottom, 0, 1, new Vector3(x1, 0, z0), new Vector3(x0, 0, z0), Vector3.back, topColor);
                SideFace(builder, theme, map, row, col, h, top, bottom, 1, 0, new Vector3(x1, 0, z1), new Vector3(x1, 0, z0), Vector3.right, topColor);
                SideFace(builder, theme, map, row, col, h, top, bottom, -1, 0, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), Vector3.left, topColor);

                // Espuma onde o bloco encontra a água.
                if (foam != null)
                {
                    float y = theme.VoidY + 0.06f;
                    Foam(foam, H(row - 1, col) == null, new Vector3(x0, y, z1), new Vector3(x1, y, z1), Vector3.forward);
                    Foam(foam, H(row + 1, col) == null, new Vector3(x1, y, z0), new Vector3(x0, y, z0), Vector3.back);
                    Foam(foam, H(row, col + 1) == null, new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.right);
                    Foam(foam, H(row, col - 1) == null, new Vector3(x0, y, z0), new Vector3(x0, y, z1), Vector3.left);
                }
            }

            var mesh = builder.Build("Terrain_" + key);
            MeshLibrary.BakeOutlineNormals(mesh);
            mesh = MeshLibrary.Store(mesh);
            var surface = Shapes.Part(mesh, terrain, Vector3.zero, Vector3.one, Palette.Terrain());
            surface.name = "TerrainMesh";
            markStatic?.Invoke(surface);

            if (foam != null && foam.VertexCount > 0)
            {
                var foamMesh = MeshLibrary.Store(foam.Build("Foam_" + key));
                var foamGo = Shapes.Part(foamMesh, terrain, Vector3.zero, Vector3.one, Palette.Glow(new Color(1f, 1f, 1f, 0.75f), GlowShape.Solid, additive: false));
                foamGo.name = "Foam";
                foamGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                markStatic?.Invoke(foamGo);
            }

            // ---------------- colisão: uma caixa por trecho contínuo de mesma altura em cada linha
            var collision = new GameObject("TerrainCollision");
            collision.transform.SetParent(terrain, false);
            for (int row = 0; row < map.Depth; row++)
            {
                int col = 0;
                while (col < map.Width)
                {
                    if (H(row, col) is not int h) { col++; continue; }
                    int start = col;
                    while (col < map.Width && H(row, col) == h) col++;
                    float top = h * BlockMap.StepHeight;
                    var box = collision.AddComponent<BoxCollider>();
                    float z = map.Depth - 1 - row;
                    box.center = new Vector3((start + col - 1) / 2f, (top + bottom) / 2f, z);
                    box.size = new Vector3(col - start, top - bottom, 1f);
                }
            }
            markStatic?.Invoke(collision);

            // ---------------- água ou abismo
            var size = new Vector3(map.Width + 60f, 1f, map.Depth + 60f);
            var middle = new Vector3((map.Width - 1) / 2f, theme.VoidY, (map.Depth - 1) / 2f);
            GameObject plane;
            if (theme.Water)
            {
                plane = Shapes.Part(PrimitiveType.Plane, voidRoot, middle, new Vector3(size.x / 10f, 1f, size.z / 10f),
                    Palette.Water(Color.Lerp(theme.Void, Color.white, 0.12f), theme.Void * 0.75f));
                plane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                plane = Shapes.Part(PrimitiveType.Cube, voidRoot, middle, new Vector3(size.x, 0.1f, size.z), Palette.Plain(theme.Void));
            }
            plane.name = "VoidPlane";
            markStatic?.Invoke(plane);

            // ---------------- decoração
            Decorate(map, terrain, theme, key, markStatic);

            // Paredes invisíveis sobre o vazio (uma por trecho contínuo de cada linha), inclusive em volta do mapa.
            var walls = new GameObject("VoidWalls");
            walls.transform.SetParent(voidRoot, false);
            for (int row = -1; row <= map.Depth; row++)
            {
                int col = -1;
                while (col <= map.Width)
                {
                    if (map.IsFloor(row, col)) { col++; continue; }
                    int start = col;
                    while (col <= map.Width && !map.IsFloor(row, col)) col++;
                    var box = walls.AddComponent<BoxCollider>();
                    float z = map.Depth - 1 - row;
                    box.center = new Vector3((start + col - 1) / 2f, 2.5f, z);
                    box.size = new Vector3(col - start, 8f, 1f);
                }
            }
        }

        /// <summary>Lateral de um bloco: faixa de grama no topo e camadas de 0,5 m escurecendo para baixo.</summary>
        static void SideFace(MeshBuilder b, TerrainTheme theme, BlockMap map, int row, int col, int h, float top, float bottom,
            int dc, int dr, Vector3 left, Vector3 right, Vector3 normal, Color topColor)
        {
            var neighbor = map.HeightAt(row + dr, col + dc);
            if (neighbor is int nh && nh >= h) return;
            float lower = neighbor is int n ? n * BlockMap.StepHeight : bottom;
            bool toVoid = neighbor == null;

            Vector3 At(Vector3 p, float y) => new(p.x, y, p.z);

            // Faixa de grama/borda do tampo.
            float bandBottom = Mathf.Max(lower, top - Band);
            var band = topColor * 0.86f;
            band.a = 1f;
            b.Quad(At(left, bandBottom), At(left, top), At(right, top), At(right, bandBottom), normal, band * 0.92f, band, band, band * 0.92f);

            // Camadas: cada degrau de 0,5 m alterna o tom; tudo escurece para baixo (e some no abismo).
            float y = bandBottom;
            int layer = 0;
            while (y > lower + 0.001f)
            {
                float next = Mathf.Max(lower, Mathf.Floor((y - 0.001f) / BlockMap.StepHeight) * BlockMap.StepHeight);
                if (next >= y) next = Mathf.Max(lower, y - BlockMap.StepHeight);
                var shade = theme.Side * (layer % 2 == 0 ? 1f : 0.93f) * (1f - (Hash(row, col, 7 + layer) - 0.5f) * 0.05f);
                Color ColorAt(float yy)
                {
                    float depth = Mathf.Clamp01((top - yy) / 3f);
                    var c = shade * Mathf.Lerp(1f, 0.6f, depth);
                    if (toVoid && !theme.Water) c = Color.Lerp(c, theme.Void, Mathf.Clamp01((top - yy) / 6f));
                    if (!toVoid && yy <= lower + 0.001f) c *= 0.8f; // contato com o chão de baixo
                    c.a = 1f;
                    return c;
                }
                b.Quad(At(left, next), At(left, y), At(right, y), At(right, next), normal, ColorAt(next), ColorAt(y), ColorAt(y), ColorAt(next));
                y = next;
                layer++;
            }
        }

        static void Foam(MeshBuilder b, bool exposed, Vector3 left, Vector3 right, Vector3 normal)
        {
            if (!exposed) return;
            const float width = 0.28f;
            var inner = new Color(1f, 1f, 1f, 0.9f);
            var outer = new Color(1f, 1f, 1f, 0f);
            var l = left - Vector3.Cross(Vector3.up, normal) * 0.05f;
            var r = right + Vector3.Cross(Vector3.up, normal) * 0.05f;
            b.Quad(l, l + normal * width, r + normal * width, r, Vector3.up, inner, outer, outer, inner);
        }

        /// <summary>Tufos, flores, pedrinhas e cristais espalhados de forma determinística (não ocupam tiles com objetos).</summary>
        static void Decorate(BlockMap map, Transform parent, TerrainTheme theme, string key, Action<GameObject> markStatic)
        {
            if (theme.Decor == TerrainDecor.None) return;
            var decor = new MeshBuilder();
            Color[] petals = { Color.white, new(1f, 0.85f, 0.3f), new(1f, 0.55f, 0.7f), new(0.65f, 0.75f, 1f) };
            var cone = MeshLibrary.Cone(4, smooth: false);
            var sphere = MeshLibrary.Icosphere(1, faceted: false); // pétalas: 42 vértices em vez de 515
            var cylinder = MeshLibrary.Cone(5, smooth: false); // caule fininho
            var pebble = MeshLibrary.Icosphere(0, true, 0.2f, 3);
            int crystals = 0;

            for (int row = 0; row < map.Depth; row++)
            for (int col = 0; col < map.Width; col++)
            {
                if (map.HeightAt(row, col) is not int h || map.ObjectAt(row, col) != BlockMap.Empty) continue;
                var center = map.TileTop(row, col);
                float roll = Hash(row, col, 11);
                var offset = new Vector3(Hash(row, col, 12) - 0.5f, 0f, Hash(row, col, 13) - 0.5f) * 0.6f;
                var p = center + offset;
                var grass = Color.Lerp(theme.TopLow, theme.TopHigh, Mathf.Clamp01(h / (float)Mathf.Max(1, theme.MaxHeightForColor))) * 0.85f;

                switch (theme.Decor)
                {
                    case TerrainDecor.Meadow:
                    case TerrainDecor.Town:
                    {
                        float tuftChance = theme.Decor == TerrainDecor.Meadow ? 0.38f : 0.12f;
                        float flowerChance = theme.Decor == TerrainDecor.Meadow ? 0.14f : 0.1f;
                        if (roll < tuftChance)
                        {
                            float yaw = Hash(row, col, 14) * 360f;
                            for (int i = 0; i < 3; i++)
                            {
                                var rot = Quaternion.Euler(0f, yaw + i * 40f, (i - 1) * 18f);
                                var pos = p + Quaternion.Euler(0f, yaw, 0f) * new Vector3((i - 1) * 0.07f, 0.08f, (i % 2) * 0.04f);
                                decor.AddMesh(cone, Matrix4x4.TRS(pos, rot, new Vector3(0.07f, 0.2f - (i % 2) * 0.05f, 0.04f)), grass * (0.92f + i * 0.06f));
                            }
                        }
                        else if (roll < tuftChance + flowerChance)
                        {
                            var petal = petals[(int)(Hash(row, col, 15) * petals.Length) % petals.Length];
                            decor.AddMesh(cylinder, Matrix4x4.TRS(p + Vector3.up * 0.085f, Quaternion.identity, new Vector3(0.03f, 0.17f, 0.03f)), new Color(0.3f, 0.6f, 0.25f));
                            for (int i = 0; i < 5; i++)
                            {
                                float a = i / 5f * Mathf.PI * 2f;
                                decor.AddMesh(sphere, Matrix4x4.TRS(p + new Vector3(Mathf.Cos(a) * 0.05f, 0.17f, Mathf.Sin(a) * 0.05f), Quaternion.identity, new Vector3(0.07f, 0.025f, 0.07f)), petal);
                            }
                            decor.AddMesh(sphere, Matrix4x4.TRS(p + Vector3.up * 0.18f, Quaternion.identity, Vector3.one * 0.045f), new Color(1f, 0.85f, 0.3f));
                        }
                        break;
                    }
                    case TerrainDecor.Cave:
                    {
                        if (roll < 0.16f)
                        {
                            var stone = theme.Side * 1.15f;
                            stone.a = 1f;
                            decor.AddMesh(pebble, Matrix4x4.TRS(p + Vector3.up * 0.04f, Quaternion.Euler(0f, roll * 900f, 0f), new Vector3(0.18f, 0.1f, 0.15f)), stone);
                            decor.AddMesh(pebble, Matrix4x4.TRS(p + new Vector3(0.12f, 0.03f, 0.08f), Quaternion.Euler(0f, roll * 500f, 0f), new Vector3(0.1f, 0.07f, 0.1f)), stone * 0.9f);
                        }
                        else if (roll > 0.965f && crystals < 18)
                        {
                            crystals++;
                            var holder = Group(parent, "Crystal");
                            holder.position = p;
                            var glow = Palette.Emissive(theme.Accent, 1.1f, Palette.Outline);
                            for (int i = 0; i < 3; i++)
                                Shapes.Part(MeshLibrary.Cone(5, smooth: false), holder, new Vector3((i - 1) * 0.12f, 0.2f + (i % 2) * 0.08f, 0f),
                                    new Vector3(0.14f, 0.42f + (i % 2) * 0.18f, 0.14f), glow, new Vector3((i - 1) * 18f, i * 50f, 0f));
                            Shapes.GlowQuad(holder, new Vector3(0f, 0.03f, 0f), 1.4f, Palette.Glow(new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.35f)), new Vector3(90f, 0f, 0f));
                            markStatic?.Invoke(holder.gameObject);
                        }
                        break;
                    }
                }
            }

            if (decor.VertexCount == 0) return;
            var mesh = MeshLibrary.Store(decor.Build("Decor_" + key));
            var go = Shapes.Part(mesh, parent, Vector3.zero, Vector3.one, Palette.Terrain(0f));
            go.name = "DecorMesh";
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            markStatic?.Invoke(go);
        }
    }
}
