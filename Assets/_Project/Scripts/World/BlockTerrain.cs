using System;
using Oiram.Core;
using UnityEngine;

namespace Oiram.World
{
    /// <summary>Cores do terreno em blocos (Vale: grama e água; dungeons: pedra e abismo).</summary>
    public sealed class TerrainTheme
    {
        public Color TopLow = new(0.40f, 0.70f, 0.33f);
        public Color TopHigh = new(0.62f, 0.82f, 0.38f);
        public Color Side = new(0.58f, 0.42f, 0.27f);
        public Color Void = new(0.27f, 0.56f, 0.86f);
        /// <summary>Altura da superfície do vazio: logo abaixo do chão (água) ou bem fundo (abismo).</summary>
        public float VoidY = -0.4f;
        public float Checker = 0.94f;
        public int MaxHeightForColor = 4;

        public static TerrainTheme Meadow() => new();

        public static TerrainTheme Town() => new()
        {
            TopLow = new Color(0.55f, 0.75f, 0.4f),
            TopHigh = new Color(0.78f, 0.72f, 0.58f),
            Side = new Color(0.6f, 0.5f, 0.38f),
        };

        public static TerrainTheme FromDungeon(DungeonDefinition d) => new()
        {
            TopLow = d.floorColor,
            TopHigh = d.floorHighColor,
            Side = d.sideColor,
            Void = d.voidColor,
            VoidY = -8f,
            Checker = 0.9f,
            MaxHeightForColor = 2,
        };
    }

    /// <summary>Constrói a geometria de um <see cref="BlockMap"/>: colunas com tampo, vazio e paredes invisíveis.</summary>
    public static class BlockTerrain
    {
        public static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <param name="markStatic">O editor marca como estático (batching); em runtime pode ser null.</param>
        public static void Build(BlockMap map, Transform parent, TerrainTheme theme, Action<GameObject> markStatic = null)
        {
            var terrain = Group(parent, "Terrain");
            var voidRoot = Group(parent, "Void");

            for (int row = 0; row < map.Depth; row++)
            for (int col = 0; col < map.Width; col++)
            {
                if (map.HeightAt(row, col) is not int h) continue;
                var top = map.TileTop(row, col);
                float bottom = theme.VoidY < -1f ? theme.VoidY + 0.5f : -1f;
                float height = top.y - bottom;
                bool even = (row + col) % 2 == 0;

                var column = Shapes.Part(PrimitiveType.Cube, terrain, new Vector3(col, top.y - height / 2f, top.z),
                    new Vector3(1f, height, 1f), even ? theme.Side : theme.Side * theme.Checker, keepCollider: true);
                column.name = $"Tile_{row}_{col}";
                markStatic?.Invoke(column);

                var topColor = Color.Lerp(theme.TopLow, theme.TopHigh, Mathf.Clamp01(h / (float)Mathf.Max(1, theme.MaxHeightForColor)));
                var cap = Shapes.Part(PrimitiveType.Cube, terrain, new Vector3(col, top.y - 0.04f, top.z),
                    new Vector3(1.002f, 0.12f, 1.002f), even ? topColor : topColor * theme.Checker);
                cap.name = "Top";
                cap.transform.SetParent(column.transform, true);
                markStatic?.Invoke(cap);
            }

            var plane = Shapes.Part(PrimitiveType.Cube, voidRoot, new Vector3((map.Width - 1) / 2f, theme.VoidY, (map.Depth - 1) / 2f),
                new Vector3(map.Width + 40f, 0.1f, map.Depth + 40f), theme.Void);
            plane.name = "VoidPlane";
            markStatic?.Invoke(plane);

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
    }
}
