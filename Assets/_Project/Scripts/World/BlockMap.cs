using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oiram.World
{
    /// <summary>
    /// Mapa em blocos: altura por célula (null = vazio/água/abismo) e um caractere de objeto por célula.
    /// Linha 0 fica ao fundo da tela (+Z); coluna 0 à esquerda (+X).
    /// </summary>
    public sealed class BlockMap
    {
        public const float StepHeight = 0.5f;
        public const char Empty = '.';

        readonly int?[,] heights;
        readonly char[,] objects;

        public int Width { get; }
        public int Depth { get; }

        public BlockMap(int width, int depth)
        {
            Width = width;
            Depth = depth;
            heights = new int?[depth, width];
            objects = new char[depth, width];
            for (int r = 0; r < depth; r++)
            for (int c = 0; c < width; c++)
                objects[r, c] = Empty;
        }

        public bool InBounds(int row, int col) => row >= 0 && row < Depth && col >= 0 && col < Width;

        public int? HeightAt(int row, int col) => InBounds(row, col) ? heights[row, col] : null;
        public bool IsFloor(int row, int col) => HeightAt(row, col) != null;
        public void SetHeight(int row, int col, int? height)
        {
            if (InBounds(row, col)) heights[row, col] = height;
        }

        public char ObjectAt(int row, int col) => InBounds(row, col) ? objects[row, col] : Empty;
        public void SetObject(int row, int col, char c)
        {
            if (InBounds(row, col)) objects[row, col] = c;
        }

        /// <summary>Centro do topo do bloco.</summary>
        public Vector3 TileTop(int row, int col) => new(col, (HeightAt(row, col) ?? 0) * StepHeight, Depth - 1 - row);

        public IEnumerable<(int row, int col, char c)> Objects()
        {
            for (int r = 0; r < Depth; r++)
            for (int c = 0; c < Width; c++)
                if (objects[r, c] != Empty && objects[r, c] != ' ')
                    yield return (r, c, objects[r, c]);
        }

        public int FloorCount()
        {
            int n = 0;
            for (int r = 0; r < Depth; r++)
            for (int c = 0; c < Width; c++)
                if (heights[r, c] != null) n++;
            return n;
        }

        /// <summary>
        /// Lê o formato de texto dos níveis: seções <c>[alturas]</c> (dígito = altura, '.' = vazio)
        /// e <c>[objetos]</c> (um caractere por célula). Linhas começando com '#' são comentários.
        /// </summary>
        public static BlockMap Parse(string text)
        {
            var heightRows = new List<string>();
            var objectRows = new List<string>();
            List<string> current = null;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line == "[alturas]") { current = heightRows; continue; }
                if (line == "[objetos]") { current = objectRows; continue; }
                current?.Add(line);
            }
            if (heightRows.Count == 0) throw new InvalidDataException("Layout sem seção [alturas].");

            int width = 0;
            foreach (var row in heightRows) width = Math.Max(width, row.Length);
            var map = new BlockMap(width, heightRows.Count);
            for (int r = 0; r < heightRows.Count; r++)
            for (int c = 0; c < heightRows[r].Length; c++)
                if (char.IsDigit(heightRows[r][c])) map.SetHeight(r, c, heightRows[r][c] - '0');
            for (int r = 0; r < objectRows.Count && r < map.Depth; r++)
            for (int c = 0; c < objectRows[r].Length && c < map.Width; c++)
                map.SetObject(r, c, objectRows[r][c]);
            return map;
        }

        /// <summary>Todas as células de chão alcançáveis a partir de (row, col) com degraus de até <paramref name="maxStep"/>.</summary>
        public HashSet<(int, int)> Reachable(int row, int col, int maxStep = 2)
        {
            var seen = new HashSet<(int, int)>();
            if (!IsFloor(row, col)) return seen;
            var queue = new Queue<(int, int)>();
            queue.Enqueue((row, col));
            seen.Add((row, col));
            while (queue.Count > 0)
            {
                var (r, c) = queue.Dequeue();
                int h = heights[r, c].Value;
                foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nr = r + dr, nc = c + dc;
                    if (seen.Contains((nr, nc)) || HeightAt(nr, nc) is not int nh || Math.Abs(nh - h) > maxStep) continue;
                    seen.Add((nr, nc));
                    queue.Enqueue((nr, nc));
                }
            }
            return seen;
        }
    }
}
