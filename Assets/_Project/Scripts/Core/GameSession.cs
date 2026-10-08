using System;
using System.Collections.Generic;
using System.Linq;
using Oiram.Characters;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;
using Oiram.World;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>
    /// Estado da partida em andamento: party, mochila, ouro, PE compartilhado e o que já foi limpo no mapa.
    /// Classe C# pura — sobrevive à troca de cenas por ser estática.
    /// </summary>
    public sealed class GameSession
    {
        static GameSession current;

        public static GameSession Current => current ??= new GameSession(GameDatabase.Load(), new SeededRandom());
        public static bool HasCurrent => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public static void StartNew(GameSession session) => current = session;

        public GameDatabase Db { get; }
        public IRandom Rng { get; }
        public BalanceConfig Balance => Db.balance;
        public LootGenerator Loot { get; }
        public List<PartyMember> Party { get; } = new();
        public PartyInventory Inventory { get; }
        public HashSet<string> ClearedFieldObjects { get; } = new();
        public int Energy { get; private set; }

        // ---------- Mundo ----------
        /// <summary>Locais concluídos (chefe derrotado). Liberam novos pontos no mapa-múndi.</summary>
        public HashSet<string> ClearedLocations { get; } = new();
        /// <summary>Maior dificuldade vencida em cada dungeon.</summary>
        public Dictionary<string, DifficultyTier> BestTier { get; } = new();
        /// <summary>Onde a party está (id de LocationDefinition) e por qual ponto entrou na cena.</summary>
        public string CurrentLocationId { get; set; }
        public string SpawnPointId { get; set; }
        public DungeonRun ActiveRun { get; set; }
        /// <summary>Mensagem para mostrar quando a próxima cena abrir (ex.: resultado da dungeon no mapa-múndi).</summary>
        public string PendingMessage { get; set; }
        /// <summary>Aumenta a cada dungeon concluída ou abandonada: o ferreiro renova o estoque.</summary>
        public int ShopRefreshCount { get; set; }
        public Dictionary<string, List<ItemInstance>> ShopStock { get; } = new();
        public Dictionary<string, int> ShopStockVersion { get; } = new();
        /// <summary>Semente fixa da partida (estoques e dungeons reproduzíveis). Sorteada no primeiro uso.</summary>
        public int WorldSeed
        {
            get
            {
                if (worldSeed == 0) worldSeed = new System.Random().Next(1, int.MaxValue);
                return worldSeed;
            }
            set => worldSeed = value;
        }
        int worldSeed;

        public event Action Changed;

        public GameSession(GameDatabase db, IRandom rng)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Loot = new LootGenerator(db, rng);
            Inventory = new PartyInventory(Balance.inventoryCapacity) { Gold = db.startingGold };

            foreach (var def in db.characters.Take(3))
            {
                var member = new PartyMember(def, Balance);
                foreach (var baseItem in def.startingEquipment)
                {
                    var item = Loot.Create(baseItem, 1, Rarity.Common);
                    if (member.CanEquip(item)) member.Equip(item);
                    else Inventory.TryAdd(item);
                }
                member.FullHeal();
                Party.Add(member);
            }

            foreach (var consumable in db.startingConsumables)
                Inventory.AddConsumable(consumable);

            Energy = MaxEnergy;
            CurrentLocationId = db.startLocation != null ? db.startLocation.id : null;
        }

        /// <summary>Nível médio da party (arredondado).</summary>
        public int PartyLevel => Party.Count == 0 ? 1 : Math.Max(1, (int)Math.Round(Party.Average(m => m.Level)));

        public bool IsLocationUnlocked(LocationDefinition location) =>
            location != null && (location.requires == null || ClearedLocations.Contains(location.requires.id));

        public void MarkLocationCleared(string locationId)
        {
            if (!string.IsNullOrEmpty(locationId)) ClearedLocations.Add(locationId);
        }

        public void RecordDungeonClear(LocationDefinition location, DifficultyTier tier)
        {
            MarkLocationCleared(location.id);
            if (!BestTier.TryGetValue(location.id, out var best) || tier > best) BestTier[location.id] = tier;
        }

        public void SetEnergy(int value) => Energy = Math.Max(0, Math.Min(MaxEnergy, value));

        public int MaxEnergy
        {
            get
            {
                int avgLevel = Party.Count == 0 ? 1 : (int)Math.Round(Party.Average(m => m.Level));
                return Balance.startingEnergy + Balance.energyPerLevel * (avgLevel - 1);
            }
        }

        public bool SpendEnergy(int amount)
        {
            if (amount > Energy) return false;
            Energy -= Math.Max(0, amount);
            NotifyChanged();
            return true;
        }

        public void RestoreEnergy(int amount)
        {
            Energy = Math.Min(MaxEnergy, Energy + Math.Max(0, amount));
            NotifyChanged();
        }

        public void RestoreAll()
        {
            foreach (var member in Party) member.FullHeal();
            Energy = MaxEnergy;
            NotifyChanged();
        }

        public float PartyStat(StatType stat) => Party.Sum(m => m.ComputeStats()[stat]);
        public float MagicFind => PartyStat(StatType.MagicFind);
        public float GoldFind => PartyStat(StatType.GoldFind);

        /// <summary>Bônus de achado mágico da dificuldade da dungeon em andamento.</summary>
        public float RunMagicFind => ActiveRun != null ? Balance.TierMagicFind(ActiveRun.Tier) : 0f;
        public float RunRewardMultiplier => ActiveRun != null ? Balance.TierRewardMultiplier(ActiveRun.Tier) : 1f;

        /// <summary>
        /// Rola uma tabela de loot com Achado Mágico/Ouro da party (+ bônus da dificuldade da dungeon).
        /// O ouro cresce com o nível da fonte.
        /// </summary>
        public LootDrop RollLoot(LootTable table, int level)
        {
            var drop = Loot.Roll(table, level, MagicFind + RunMagicFind, GoldFind);
            float growth = 1f + Balance.goldGrowthPerLevel * Math.Max(0, level - 1);
            drop.Gold = (int)Math.Round(drop.Gold * growth * RunRewardMultiplier);
            return drop;
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
