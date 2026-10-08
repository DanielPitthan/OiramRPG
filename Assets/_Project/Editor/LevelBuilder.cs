using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.Loot;
using Oiram.World;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>
    /// "Assa" um layout de texto (Levels/*.txt) numa cena: terreno em blocos + objetos.
    /// Usa as mesmas peças de runtime das dungeons procedurais (<see cref="BlockTerrain"/>, <see cref="WorldProps"/>).
    /// Legenda no cabeçalho de cada arquivo de layout.
    /// </summary>
    public sealed class LevelBuilder
    {
        readonly BlockMap map;
        readonly GameDatabase db;
        readonly TerrainTheme theme;

        public Vector3 PlayerSpawn { get; private set; }
        public FieldPlayerController Player { get; private set; }

        /// <summary>Caracteres extras (ex.: lojas da cidade). Retorna true se tratou o caractere.</summary>
        public System.Func<char, Vector3, string, Transform, bool> CustomObject;

        static readonly Dictionary<char, string> EncounterByChar = new()
        {
            ['S'] = "enc_slimes2",
            ['s'] = "enc_slimes3",
            ['B'] = "enc_morcegos",
            ['G'] = "enc_goblin_slime",
            ['g'] = "enc_goblins",
            ['O'] = "enc_golem",
        };

        public LevelBuilder(string layoutPath, GameDatabase db, TerrainTheme theme)
        {
            map = BlockMap.Parse(File.ReadAllText(layoutPath));
            this.db = db;
            this.theme = theme;
        }

        public static void MakeStatic(GameObject go) =>
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic);

        static string IdOf(char c, int row, int col) => $"{c}_{row}_{col}";

        public void Build(Transform root)
        {
            BlockTerrain.Build(map, root, theme, MakeStatic);
            var decor = BlockTerrain.Group(root, "Decor");
            var actors = BlockTerrain.Group(root, "Actors");

            // O chefe do mapa (se houver) bloqueia a saída para o mapa-múndi.
            var boss = map.Objects().FirstOrDefault(o => o.c == 'O');
            string bossId = boss.c == 'O' ? IdOf('O', boss.row, boss.col) : null;

            int index = 0;
            foreach (var (row, col, c) in map.Objects())
            {
                if (!map.IsFloor(row, col))
                {
                    Debug.LogWarning($"[LevelBuilder] Objeto '{c}' fora do chão (linha {row}, coluna {col}) — ignorado.");
                    continue;
                }
                var pos = map.TileTop(row, col);
                string id = IdOf(c, row, col);
                index++;

                switch (c)
                {
                    case 'P':
                        PlayerSpawn = pos;
                        Player = WorldProps.Player(actors, pos, db);
                        break;
                    case 'X':
                        WorldProps.Exit(actors, pos, bossId, bossId != null ? "Um Golem bloqueia a saída do vale! Derrote-o primeiro." : null);
                        WorldProps.Spawn(actors, pos, "saida");
                        break;
                    case 'F': WorldProps.Campfire(actors, pos); break;
                    case 'C': WorldProps.Chest(actors, pos, id, db.Find<LootTable>("lt_bau"), 2); break;
                    case 'c': WorldProps.Chest(actors, pos, id, db.Find<LootTable>("lt_bau_raro"), 3); break;
                    case 'M': WorldProps.Chest(actors, pos, id, db.Find<LootTable>("lt_bau"), 3, db.Find<EncounterDefinition>("enc_mimico")); break;
                    case 'K': WorldProps.Crate(actors, pos, id, db.Find<LootTable>("lt_caixa"), 2); break;
                    case 'T': WorldProps.Decor(decor, pos, tree: true, index, MakeStatic); break;
                    case 'R': WorldProps.Decor(decor, pos, tree: false, index, MakeStatic); break;
                    default:
                        if (EncounterByChar.TryGetValue(c, out var encounterId))
                        {
                            var encounter = db.Find<EncounterDefinition>(encounterId);
                            if (encounter == null) throw new InvalidDataException($"Encontro '{encounterId}' não existe.");
                            WorldProps.Enemy(actors, pos, id, encounter, c == 'O');
                        }
                        else if (CustomObject == null || !CustomObject(c, pos, id, actors))
                            Debug.LogWarning($"[LevelBuilder] Caractere desconhecido '{c}' (linha {row}, coluna {col}).");
                        break;
                }
            }
            if (Player == null) throw new InvalidDataException("Layout sem 'P' (início do jogador).");
        }
    }
}
