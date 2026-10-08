using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oiram.Characters;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.World;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Onde o "Continuar" retoma a partida.</summary>
    public enum ResumePoint
    {
        /// <summary>Campo/cidade em <see cref="SaveData.location"/>, no ponto <see cref="SaveData.spawn"/> (pousada).</summary>
        Location,
        WorldMap,
        /// <summary>Começo do andar salvo em <see cref="SaveData.run"/>.</summary>
        Dungeon,
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 2;
        public string savedAt;
        public ResumePoint resume;
        public string location;
        public string spawn;
        public bool hasRun;
        public RunData run = new();
        public int gold;
        public int energy;
        public int worldSeed;
        public int shopRefreshCount;
        public List<MemberData> party = new();
        public List<ItemData> inventory = new();
        public List<CountData> consumables = new();
        public List<string> clearedObjects = new();
        public List<string> clearedLocations = new();
        public List<CountData> bestTiers = new();
        public List<StockData> shopStock = new();
    }

    /// <summary>Descida em andamento (salva no começo de cada andar).</summary>
    [Serializable]
    public sealed class RunData
    {
        public string location;
        public int tier, level, seed, floors, floor;
        public int battlesWon, itemsFound, goldFound;
    }

    [Serializable]
    public sealed class MemberData
    {
        public string id;
        public int level, xp, hp;
        public string job, secondary, support;
        public List<JobData> jobs = new();
        public List<ItemData> equipment = new();
    }

    [Serializable]
    public sealed class JobData
    {
        public string id;
        public int total, available;
        public List<string> learned = new();
    }

    [Serializable]
    public sealed class ItemData
    {
        public string id;
        public string baseId;
        public int level;
        public int rarity;
        public List<AffixData> affixes = new();
    }

    [Serializable]
    public sealed class AffixData
    {
        public string id;
        public int tier;
        public float value;
    }

    [Serializable]
    public sealed class CountData
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class StockData
    {
        public string town;
        public int version;
        public List<ItemData> items = new();
    }

    /// <summary>
    /// Salva/carrega a partida em JSON (ids estáveis dos assets). A pousada salva na cidade; o jogo também salva
    /// sozinho no mapa-múndi e no começo de cada andar de dungeon.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Caminho alternativo (testes).</summary>
        public static string PathOverride { get; set; }
        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "save.json");
        public static bool HasSave => File.Exists(FilePath);
        /// <summary>Salvamento automático (desligado em batchmode para os testes não mexerem no save do jogador).</summary>
        public static bool AutoSaveEnabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PathOverride = null;
            AutoSaveEnabled = !Application.isBatchMode;
        }

        // ------------------------------------------------------------------ captura

        public static SaveData Capture(GameSession s, string spawnId = null, ResumePoint resume = ResumePoint.Location)
        {
            var data = new SaveData
            {
                savedAt = DateTime.Now.ToString("s"),
                resume = resume,
                location = s.CurrentLocationId,
                spawn = spawnId,
                gold = s.Inventory.Gold,
                energy = s.Energy,
                worldSeed = s.WorldSeed,
                shopRefreshCount = s.ShopRefreshCount,
                clearedObjects = s.ClearedFieldObjects.ToList(),
                clearedLocations = s.ClearedLocations.ToList(),
                bestTiers = s.BestTier.Select(kv => new CountData { id = kv.Key, count = (int)kv.Value }).ToList(),
                inventory = s.Inventory.Items.Select(ToData).ToList(),
                consumables = s.Inventory.Consumables().Select(c => new CountData { id = c.item.id, count = c.count }).ToList(),
            };
            foreach (var m in s.Party)
            {
                var md = new MemberData
                {
                    id = m.Definition.id,
                    level = m.Level,
                    xp = m.Xp,
                    hp = m.CurrentHp,
                    job = m.Job?.id,
                    secondary = m.SecondaryJob?.id,
                    support = m.SupportPassive?.id,
                    equipment = m.EquippedItems().Select(ToData).ToList(),
                };
                foreach (var p in m.AllProgress())
                    md.jobs.Add(new JobData { id = p.Job.id, total = p.TotalJp, available = p.AvailableJp, learned = p.Learned.Select(a => a.id).ToList() });
                data.party.Add(md);
            }
            if (resume == ResumePoint.Dungeon && s.ActiveRun is DungeonRun run)
            {
                data.hasRun = true;
                data.run = new RunData
                {
                    location = run.Location.id,
                    tier = (int)run.Tier,
                    level = run.Level,
                    seed = run.Seed,
                    floors = run.Floors,
                    floor = run.Floor,
                    battlesWon = run.BattlesWon,
                    itemsFound = run.ItemsFound,
                    goldFound = run.GoldFound,
                };
            }
            foreach (var (town, items) in s.ShopStock)
                data.shopStock.Add(new StockData
                {
                    town = town,
                    version = s.ShopStockVersion.TryGetValue(town, out int v) ? v : -1,
                    items = items.Select(ToData).ToList(),
                });
            return data;
        }

        static ItemData ToData(ItemInstance item) => new()
        {
            id = item.Id,
            baseId = item.Base.id,
            level = item.ItemLevel,
            rarity = (int)item.Rarity,
            affixes = item.Affixes.Select(a => new AffixData { id = a.affix.id, tier = a.tier, value = a.value }).ToList(),
        };

        // ------------------------------------------------------------------ restauração

        public static GameSession Restore(SaveData data, GameDatabase db, IRandom rng = null)
        {
            var s = new GameSession(db, rng ?? new SeededRandom());
            s.Inventory.Clear();
            s.Inventory.Gold = data.gold;
            s.WorldSeed = data.worldSeed;
            s.ShopRefreshCount = data.shopRefreshCount;
            s.CurrentLocationId = data.location;
            s.SpawnPointId = data.spawn;
            foreach (var id in data.clearedObjects) s.ClearedFieldObjects.Add(id);
            foreach (var id in data.clearedLocations) s.ClearedLocations.Add(id);
            foreach (var t in data.bestTiers) s.BestTier[t.id] = (DifficultyTier)t.count;

            foreach (var md in data.party)
            {
                var member = s.Party.FirstOrDefault(m => m.Definition.id == md.id);
                if (member == null) continue;
                foreach (var jd in md.jobs)
                {
                    var job = db.Find<JobDefinition>(jd.id);
                    if (job == null) continue;
                    member.ProgressFor(job).Restore(jd.total, jd.available, jd.learned.Select(db.Find<AbilityDefinition>));
                }
                member.RestoreJobs(db.Find<JobDefinition>(md.job), db.Find<JobDefinition>(md.secondary), db.Find<AbilityDefinition>(md.support));
                member.ClearEquipment();
                foreach (var item in md.equipment.Select(d => FromData(d, db)).Where(i => i != null))
                    member.RestoreEquipped(item);
                member.RestoreState(md.level, md.xp, md.hp);
            }

            foreach (var item in data.inventory.Select(d => FromData(d, db)).Where(i => i != null))
                s.Inventory.TryAdd(item);
            foreach (var c in data.consumables)
                s.Inventory.AddConsumable(db.Find<ConsumableDefinition>(c.id), c.count);
            foreach (var stock in data.shopStock)
            {
                s.ShopStock[stock.town] = stock.items.Select(d => FromData(d, db)).Where(i => i != null).ToList();
                s.ShopStockVersion[stock.town] = stock.version;
            }
            s.SetEnergy(data.energy);
            s.ActiveRun = RestoreRun(data, db);
            return s;
        }

        static DungeonRun RestoreRun(SaveData data, GameDatabase db)
        {
            if (data.resume != ResumePoint.Dungeon || !data.hasRun || data.run == null) return null;
            var location = db.Find<LocationDefinition>(data.run.location);
            if (location == null || !location.IsDungeon) return null;
            return new DungeonRun(location, (DifficultyTier)data.run.tier, data.run.level, data.run.seed, data.run.floors)
            {
                Floor = Math.Max(0, Math.Min(data.run.floor, data.run.floors - 1)),
                BattlesWon = data.run.battlesWon,
                ItemsFound = data.run.itemsFound,
                GoldFound = data.run.goldFound,
            };
        }

        /// <summary>Onde retomar: dungeon só se a descida pôde ser reconstruída.</summary>
        public static ResumePoint ResumeOf(SaveData data, GameSession restored) =>
            data.resume == ResumePoint.Dungeon && restored.ActiveRun == null ? ResumePoint.WorldMap : data.resume;

        static ItemInstance FromData(ItemData d, GameDatabase db)
        {
            var baseDef = db.Find<ItemBaseDefinition>(d.baseId);
            if (baseDef == null) return null; // item removido do conteúdo
            var affixes = d.affixes
                .Select(a => (def: db.Find<AffixDefinition>(a.id), a))
                .Where(x => x.def != null)
                .Select(x => new RolledAffix(x.def, x.a.tier, x.a.value))
                .ToList();
            return new ItemInstance(baseDef, d.level, (Rarity)d.rarity, affixes, db.balance.itemScalingPerLevel, d.id);
        }

        // ------------------------------------------------------------------ arquivo

        public static void Save(GameSession session, string spawnId = null, ResumePoint resume = ResumePoint.Location)
        {
            var json = JsonUtility.ToJson(Capture(session, spawnId, resume), prettyPrint: true);
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        /// <summary>Salvamento automático: nunca derruba o jogo por erro de disco. Devolve true se gravou.</summary>
        public static bool AutoSave(GameSession session, ResumePoint resume)
        {
            if (!AutoSaveEnabled || session == null) return false;
            try
            {
                Save(session, null, resume);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Salvamento automático falhou: {e.Message}");
                return false;
            }
        }

        public static SaveData LoadData()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)); }
            catch (Exception e)
            {
                Debug.LogWarning($"Save ilegível: {e.Message}");
                return null;
            }
        }

        public static GameSession Load(GameDatabase db)
        {
            var data = LoadData();
            return data == null ? null : Restore(data, db);
        }

        public static string Describe(GameDatabase db = null)
        {
            if (!HasSave) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                var levels = string.Join("/", data.party.Select(p => p.level));
                string where = data.resume switch
                {
                    ResumePoint.Dungeon when data.hasRun => $"{Name(db, data.run.location)}, andar {data.run.floor + 1}",
                    ResumePoint.WorldMap => "mapa-múndi",
                    _ => Name(db, data.location),
                };
                return $"{where} · níveis {levels} · {data.gold} ouro · {data.savedAt?.Replace('T', ' ')}";
            }
            catch (Exception)
            {
                return "Save encontrado";
            }
        }

        static string Name(GameDatabase db, string locationId) =>
            db?.Find<LocationDefinition>(locationId)?.displayName ?? locationId ?? "?";
    }
}
