using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Core;

namespace Oiram.World
{
    public sealed class DungeonRoom
    {
        public int Index;
        public int Row, Col, Rows, Cols;
        public int Height;

        public int CenterRow => Row + Rows / 2;
        public int CenterCol => Col + Cols / 2;
        public int Area => Rows * Cols;

        public bool Overlaps(DungeonRoom other, int margin) =>
            Row - margin < other.Row + other.Rows && other.Row - margin < Row + Rows &&
            Col - margin < other.Col + other.Cols && other.Col - margin < Col + Cols;

        /// <summary>Células internas (sem a borda), boas para objetos.</summary>
        public IEnumerable<(int row, int col)> Interior()
        {
            for (int r = Row + 1; r < Row + Rows - 1; r++)
            for (int c = Col + 1; c < Col + Cols - 1; c++)
                yield return (r, c);
        }
    }

    /// <summary>
    /// Andar gerado. Objetos no mapa: P entrada · E portal de saída · D escada · O chefe · F fogueira ·
    /// e grupo de inimigos · C baú · K caixa flutuante · T tocha.
    /// </summary>
    public sealed class DungeonFloorLayout
    {
        public BlockMap Map;
        public readonly List<DungeonRoom> Rooms = new();
        public int StartRoom;
        public int EndRoom;
        public bool BossFloor;
        public (int row, int col) Entrance;
        public (int row, int col) Goal;
    }

    /// <summary>Gera andares de dungeon com salas e corredores (determinístico pela semente).</summary>
    public static class DungeonGenerator
    {
        public const int Width = 44;
        public const int Depth = 34;
        public const int MaxHeight = 2; // alturas 0..2: qualquer degrau dá para subir pulando

        public static DungeonFloorLayout Generate(int seed, int minRooms, int maxRooms, bool bossFloor, bool firstFloor)
        {
            var rng = new SeededRandom(seed);
            var layout = new DungeonFloorLayout { BossFloor = bossFloor, Map = new BlockMap(Width, Depth) };

            PlaceRooms(layout, rng, rng.RangeInclusive(Math.Min(minRooms, maxRooms), Math.Max(minRooms, maxRooms)), bossFloor);
            // Raro: não coube sala suficiente — tenta outra semente derivada (continua determinístico).
            if (layout.Rooms.Count < Math.Max(3, Math.Min(minRooms, maxRooms) - 1))
                return Generate(unchecked(seed * 31 + 17), minRooms, maxRooms, bossFloor, firstFloor);
            foreach (var room in layout.Rooms)
                for (int r = room.Row; r < room.Row + room.Rows; r++)
                for (int c = room.Col; c < room.Col + room.Cols; c++)
                    layout.Map.SetHeight(r, c, room.Height);

            foreach (var (a, b) in Connections(layout.Rooms, rng))
                CarveCorridor(layout.Map, layout.Rooms[a], layout.Rooms[b], rng);

            ChooseStartAndGoal(layout, rng);
            PlaceObjects(layout, rng, firstFloor);
            return layout;
        }

        // ------------------------------------------------------------------ salas

        static void PlaceRooms(DungeonFloorLayout layout, IRandom rng, int target, bool bossFloor)
        {
            if (bossFloor)
            {
                // Sala grande do chefe primeiro (garante que caiba).
                layout.Rooms.Add(new DungeonRoom
                {
                    Rows = 9, Cols = 9, Height = 1,
                    Row = rng.RangeInclusive(2, Depth - 11),
                    Col = rng.RangeInclusive(2, Width - 11),
                });
            }

            for (int attempt = 0; attempt < 600 && layout.Rooms.Count < target + (bossFloor ? 1 : 0); attempt++)
            {
                var room = new DungeonRoom
                {
                    Rows = rng.RangeInclusive(5, 8),
                    Cols = rng.RangeInclusive(5, 8),
                    Height = rng.RangeInclusive(0, MaxHeight),
                };
                room.Row = rng.RangeInclusive(2, Depth - room.Rows - 2);
                room.Col = rng.RangeInclusive(2, Width - room.Cols - 2);
                if (layout.Rooms.Any(other => room.Overlaps(other, 2))) continue;
                layout.Rooms.Add(room);
            }
            for (int i = 0; i < layout.Rooms.Count; i++) layout.Rooms[i].Index = i;
        }

        static int Distance(DungeonRoom a, DungeonRoom b) =>
            Math.Abs(a.CenterRow - b.CenterRow) + Math.Abs(a.CenterCol - b.CenterCol);

        /// <summary>Árvore geradora mínima (Prim) + alguns atalhos para criar laços.</summary>
        static List<(int a, int b)> Connections(List<DungeonRoom> rooms, IRandom rng)
        {
            var edges = new List<(int, int)>();
            if (rooms.Count < 2) return edges;
            var inTree = new HashSet<int> { 0 };
            while (inTree.Count < rooms.Count)
            {
                int bestA = -1, bestB = -1, best = int.MaxValue;
                foreach (int a in inTree)
                    for (int b = 0; b < rooms.Count; b++)
                    {
                        if (inTree.Contains(b)) continue;
                        int d = Distance(rooms[a], rooms[b]);
                        if (d < best) { best = d; bestA = a; bestB = b; }
                    }
                edges.Add((bestA, bestB));
                inTree.Add(bestB);
            }
            for (int a = 0; a < rooms.Count; a++)
                for (int b = a + 1; b < rooms.Count; b++)
                    if (!edges.Contains((a, b)) && !edges.Contains((b, a)) && Distance(rooms[a], rooms[b]) < 16 && rng.Chance(0.2f))
                        edges.Add((a, b));
            return edges;
        }

        /// <summary>Corredor em L com 2 de largura; não altera células que já são chão.</summary>
        static void CarveCorridor(BlockMap map, DungeonRoom from, DungeonRoom to, IRandom rng)
        {
            int r0 = from.CenterRow, c0 = from.CenterCol, r1 = to.CenterRow, c1 = to.CenterCol;
            bool horizontalFirst = rng.Chance(0.5f);

            void Dig(int r, int c, int height)
            {
                for (int dr = 0; dr < 2; dr++)
                for (int dc = 0; dc < 2; dc++)
                    if (!map.IsFloor(r + dr, c + dc) && r + dr > 0 && r + dr < Depth - 1 && c + dc > 0 && c + dc < Width - 1)
                        map.SetHeight(r + dr, c + dc, height);
            }

            if (horizontalFirst)
            {
                for (int c = Math.Min(c0, c1); c <= Math.Max(c0, c1); c++) Dig(r0, c, from.Height);
                for (int r = Math.Min(r0, r1); r <= Math.Max(r0, r1); r++) Dig(r, c1, to.Height);
            }
            else
            {
                for (int r = Math.Min(r0, r1); r <= Math.Max(r0, r1); r++) Dig(r, c0, from.Height);
                for (int c = Math.Min(c0, c1); c <= Math.Max(c0, c1); c++) Dig(r1, c, to.Height);
            }
        }

        // ------------------------------------------------------------------ início, objetivo e objetos

        static Dictionary<(int, int), int> Distances(BlockMap map, (int row, int col) from)
        {
            var dist = new Dictionary<(int, int), int> { [from] = 0 };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var (r, c) = queue.Dequeue();
                foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var next = (r + dr, c + dc);
                    if (dist.ContainsKey(next) || !map.IsFloor(next.Item1, next.Item2)) continue;
                    dist[next] = dist[(r, c)] + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        static void ChooseStartAndGoal(DungeonFloorLayout layout, IRandom rng)
        {
            var rooms = layout.Rooms;
            if (layout.BossFloor)
            {
                layout.EndRoom = 0; // a sala do chefe
                var fromBoss = Distances(layout.Map, (rooms[0].CenterRow, rooms[0].CenterCol));
                layout.StartRoom = rooms.Skip(1).OrderByDescending(r => fromBoss.TryGetValue((r.CenterRow, r.CenterCol), out int d) ? d : 0).First().Index;
            }
            else
            {
                layout.StartRoom = rng.Range(0, rooms.Count);
                var fromStart = Distances(layout.Map, (rooms[layout.StartRoom].CenterRow, rooms[layout.StartRoom].CenterCol));
                layout.EndRoom = rooms.OrderByDescending(r => fromStart.TryGetValue((r.CenterRow, r.CenterCol), out int d) ? d : 0).First().Index;
            }
            layout.Entrance = (rooms[layout.StartRoom].CenterRow, rooms[layout.StartRoom].CenterCol);
            layout.Goal = (rooms[layout.EndRoom].CenterRow, rooms[layout.EndRoom].CenterCol);
        }

        static void PlaceObjects(DungeonFloorLayout layout, IRandom rng, bool firstFloor)
        {
            var map = layout.Map;
            var used = new HashSet<(int, int)>();

            bool Put(int r, int c, char what)
            {
                if (!map.IsFloor(r, c) || used.Contains((r, c))) return false;
                map.SetObject(r, c, what);
                used.Add((r, c));
                return true;
            }

            // Reserva a vizinhança da entrada para nada nascer em cima do jogador.
            var start = layout.Rooms[layout.StartRoom];
            Put(layout.Entrance.row, layout.Entrance.col, 'P');
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    used.Add((layout.Entrance.row + dr, layout.Entrance.col + dc));

            if (firstFloor) Put(start.CenterRow, start.CenterCol + 2, 'E');
            if (layout.BossFloor)
            {
                Put(layout.Goal.row, layout.Goal.col, 'O');
                Put(start.CenterRow + 2, start.CenterCol, 'F'); // fogueira antes do chefe
            }
            else Put(layout.Goal.row, layout.Goal.col, 'D');

            foreach (var room in layout.Rooms)
            {
                // Tochas nos cantos.
                Put(room.Row, room.Col, 'T');
                if (rng.Chance(0.5f)) Put(room.Row + room.Rows - 1, room.Col + room.Cols - 1, 'T');

                if (room.Index == layout.StartRoom || (layout.BossFloor && room.Index == layout.EndRoom)) continue;

                var spots = room.Interior().ToList();
                rng.Shuffle(spots);
                int groups = room.Area >= 42 && rng.Chance(0.5f) ? 2 : 1;
                foreach (var (r, c) in spots)
                {
                    if (groups == 0) break;
                    if (Math.Abs(r - layout.Goal.row) + Math.Abs(c - layout.Goal.col) < 2) continue;
                    if (Put(r, c, 'e')) groups--;
                }
                if (rng.Chance(0.35f))
                {
                    var corner = rng.Chance(0.5f) ? (room.Row + 1, room.Col + room.Cols - 2) : (room.Row + room.Rows - 2, room.Col + 1);
                    Put(corner.Item1, corner.Item2, 'C');
                }
                if (rng.Chance(0.2f))
                    foreach (var (r, c) in spots)
                        if (Put(r, c, 'K')) break;
            }
        }
    }
}
