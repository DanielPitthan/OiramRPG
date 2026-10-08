using Oiram.Core;

namespace Oiram.World
{
    /// <summary>Uma descida em andamento numa dungeon (não é salva: sair do jogo encerra a descida).</summary>
    public sealed class DungeonRun
    {
        public LocationDefinition Location { get; }
        public DungeonDefinition Dungeon => Location.dungeon;
        public DifficultyTier Tier { get; }
        public int Level { get; }
        public int Seed { get; }
        public int Floors { get; }
        public int Floor { get; set; }
        public bool BossDefeated { get; set; }
        public int BattlesWon { get; set; }
        public int ItemsFound { get; set; }
        public int GoldFound { get; set; }

        public DungeonRun(LocationDefinition location, DifficultyTier tier, int level, int seed, int floors)
        {
            Location = location;
            Tier = tier;
            Level = level;
            Seed = seed;
            Floors = floors < 1 ? 1 : floors;
        }

        public bool IsLastFloor => Floor >= Floors - 1;

        /// <summary>Semente do andar atual (o mesmo andar gera sempre o mesmo layout nesta descida).</summary>
        public int FloorSeed => unchecked(Seed * 7919 + Floor * 104729);

        /// <summary>Prefixo dos ids de objetos deste andar (inimigos/baús não se repetem entre descidas).</summary>
        public string ObjectPrefix => $"{Location.id}_{Seed}_{Floor}";
    }
}
