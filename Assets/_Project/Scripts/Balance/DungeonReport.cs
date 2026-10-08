using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Oiram.Battle;
using Oiram.Core;
using Oiram.World;

namespace Oiram.Balance
{
    /// <summary>Uma combinação dungeon × nível da party × dificuldade × perfil de jogador.</summary>
    public sealed class DungeonCell
    {
        public string LocationId;
        public string DungeonName;
        public int PartyLevel;
        public DifficultyTier Tier;
        public string Profile;
        public int Runs;
        public float ClearRate;
        public float AvgRounds;
        public float AvgHpEnd;
        public float BossWinRate;
        public float BossRounds;
        public float LevelsPerRun;
        public float Gold;
        public float Items;
        public float EpicPlus;
        public float ShopPrice;
    }

    public static class DungeonReport
    {
        /// <summary>Cada dungeon no nível em que normalmente é visitada, mais a Mina bem mais tarde (teste da escala).</summary>
        public static readonly (string location, int level)[] Scenarios =
        {
            ("loc_mina", 4), ("loc_cripta", 6), ("loc_caverna", 8), ("loc_mina", 12),
        };

        public static List<DungeonCell> Run(GameDatabase db, int runs)
        {
            var cells = new List<DungeonCell>();
            foreach (var (locationId, level) in Scenarios)
            {
                var location = db.Find<LocationDefinition>(locationId);
                float shopPrice = AverageShopPrice(db, level);
                foreach (DifficultyTier tier in Enum.GetValues(typeof(DifficultyTier)))
                    foreach (var profile in SkillProfile.Players)
                    {
                        var results = Enumerable.Range(0, runs)
                            .Select(i => DungeonSimulator.Run(db, location, tier, profile, level, 20000 + i * 7 + level * 1000 + (int)tier * 100))
                            .ToList();
                        var regularWins = results.SelectMany(r => r.Regular).Where(b => b.Result == BattleResult.Victory).ToList();
                        var bosses = results.Where(r => r.Boss != null).Select(r => r.Boss).ToList();
                        var bossWins = bosses.Where(b => b.Result == BattleResult.Victory).ToList();
                        cells.Add(new DungeonCell
                        {
                            LocationId = locationId,
                            DungeonName = location.displayName,
                            PartyLevel = level,
                            Tier = tier,
                            Profile = profile.Name,
                            Runs = runs,
                            ClearRate = results.Count(r => r.Cleared) / (float)runs,
                            AvgRounds = regularWins.Count == 0 ? 0 : (float)regularWins.Average(b => b.Rounds),
                            AvgHpEnd = regularWins.Count == 0 ? 0 : (float)regularWins.Average(b => b.PartyHpPercentEnd),
                            BossWinRate = bosses.Count == 0 ? 0 : bossWins.Count / (float)bosses.Count,
                            BossRounds = bossWins.Count == 0 ? 0 : (float)bossWins.Average(b => b.Rounds),
                            LevelsPerRun = (float)results.Average(r => r.LevelsGained) / 3f,
                            Gold = (float)results.Average(r => r.Gold),
                            Items = (float)results.Average(r => r.Items),
                            EpicPlus = (float)results.Average(r => r.EpicOrBetter),
                            ShopPrice = shopPrice,
                        });
                    }
            }
            return cells;
        }

        /// <summary>Preço médio de um item do ferreiro com a party naquele nível.</summary>
        public static float AverageShopPrice(GameDatabase db, int level)
        {
            var prices = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                var s = DungeonSimulator.TypicalParty(db, level, 900 + i);
                s.WorldSeed = 77 + i;
                prices.AddRange(ShopService.BlacksmithStock(s, "loc_vila").Select(item => ShopService.BuyPrice(item, db.balance)));
            }
            return prices.Count == 0 ? 0 : (float)prices.Average();
        }

        static IEnumerable<DungeonCell> Typical(BalanceReport.Report r) => r.Dungeons.Where(c => c.PartyLevel < 12);

        static float Avg(BalanceReport.Report r, DifficultyTier tier, string profile, Func<DungeonCell, float> pick)
        {
            var cells = Typical(r).Where(c => c.Tier == tier && c.Profile == profile).ToList();
            return cells.Count == 0 ? 0 : cells.Average(pick);
        }

        public static List<Target> Targets() => new()
        {
            new Target { Description = "Dungeon Fácil (Iniciante) termina a descida", Measure = r => Avg(r, DifficultyTier.Easy, "Iniciante", c => c.ClearRate), Min = 0.85f, Format = "0%" },
            new Target { Description = "Dungeon Normal (Médio) termina a descida", Measure = r => Avg(r, DifficultyTier.Normal, "Médio", c => c.ClearRate), Min = 0.85f, Format = "0%" },
            new Target { Description = "Dungeon Difícil (Médio) termina — desafio real", Measure = r => Avg(r, DifficultyTier.Hard, "Médio", c => c.ClearRate), Min = 0.45f, Max = 0.92f, Format = "0%" },
            new Target { Description = "Dungeon Difícil (Experiente) termina", Measure = r => Avg(r, DifficultyTier.Hard, "Experiente", c => c.ClearRate), Min = 0.85f, Format = "0%" },
            new Target { Description = "Dungeon Pesadelo (Experiente) termina", Measure = r => Avg(r, DifficultyTier.Nightmare, "Experiente", c => c.ClearRate), Min = 0.30f, Max = 0.88f, Format = "0%" },
            new Target { Description = "Dungeon Pesadelo (Médio) termina — só para experts", Measure = r => Avg(r, DifficultyTier.Nightmare, "Médio", c => c.ClearRate), Max = 0.50f, Format = "0%" },
            new Target { Description = "Rodadas por batalha na dungeon (Normal, Médio)", Measure = r => Avg(r, DifficultyTier.Normal, "Médio", c => c.AvgRounds), Min = 2f, Max = 4.5f, Format = "0.0" },
            new Target { Description = "Níveis por descida, por personagem (Normal, Médio)", Measure = r => Avg(r, DifficultyTier.Normal, "Médio", c => c.LevelsPerRun), Min = 0.5f, Max = 2f, Format = "0.00" },
            new Target { Description = "Ouro de uma descida Normal ÷ preço médio no ferreiro", Measure = r => Avg(r, DifficultyTier.Normal, "Médio", c => c.ShopPrice <= 0 ? 0 : c.Gold / c.ShopPrice), Min = 0.8f, Max = 3f, Format = "0.0" },
            new Target { Description = "Pesadelo dá mais Épicos+ que Normal (Experiente)", Measure = r => Avg(r, DifficultyTier.Nightmare, "Experiente", c => c.EpicPlus) - Avg(r, DifficultyTier.Normal, "Experiente", c => c.EpicPlus), Min = 0.5f, Format = "0.0" },
            new Target
            {
                Description = "Escala: Mina Normal (Médio) no Nv 12 vs Nv 4 — diferença na conclusão",
                Measure = r =>
                {
                    var early = r.Dungeons.FirstOrDefault(c => c.LocationId == "loc_mina" && c.PartyLevel == 4 && c.Tier == DifficultyTier.Normal && c.Profile == "Médio");
                    var late = r.Dungeons.FirstOrDefault(c => c.LocationId == "loc_mina" && c.PartyLevel == 12 && c.Tier == DifficultyTier.Normal && c.Profile == "Médio");
                    return early == null || late == null ? 1f : Math.Abs(early.ClearRate - late.ClearRate);
                },
                Max = 0.15f, Format = "0.00",
            },
        };

        static string Pct(float v) => (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string F(float v, string format = "0.0") => v.ToString(format, CultureInfo.InvariantCulture);

        public static void AppendMarkdown(StringBuilder sb, List<DungeonCell> cells)
        {
            if (cells == null || cells.Count == 0) return;
            sb.AppendLine("## Dungeons (descidas completas)");
            sb.AppendLine();
            sb.AppendLine("Party \"típica\" do nível (equipamento Incomum/Raro um nível abaixo, habilidades do job inicial), " +
                          "limpando todas as salas. PV/PE carregam entre batalhas; fogueira antes do chefe.");
            sb.AppendLine();
            foreach (var group in cells.GroupBy(c => (c.DungeonName, c.PartyLevel)))
            {
                sb.AppendLine($"### {group.Key.DungeonName} — party Nv {group.Key.PartyLevel} ({group.First().Runs} descidas por linha, preço médio no ferreiro {F(group.First().ShopPrice, "0")} ouro)");
                sb.AppendLine();
                sb.AppendLine("| Dificuldade | Perfil | Termina | Rodadas | PV ao fim | Chefe vence | Rodadas chefe | Níveis/descida | Ouro | Itens | Épico+ |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var c in group)
                    sb.AppendLine($"| {BalanceConfig.TierName(c.Tier)} | {c.Profile} | {Pct(c.ClearRate)} | {F(c.AvgRounds)} | {Pct(c.AvgHpEnd)} | {Pct(c.BossWinRate)} | " +
                                  $"{F(c.BossRounds)} | {F(c.LevelsPerRun, "0.00")} | {F(c.Gold, "0")} | {F(c.Items)} | {F(c.EpicPlus)} |");
                sb.AppendLine();
            }
        }
    }
}
