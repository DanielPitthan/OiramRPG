using System.IO;
using System.Linq;
using NUnit.Framework;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using Oiram.World;

namespace Oiram.Tests
{
    public class DungeonGeneratorTests
    {
        [Test]
        public void Floors_AreConnected_AndObjectsSitOnFloor()
        {
            for (int seed = 1; seed <= 150; seed++)
            {
                bool boss = seed % 3 == 0;
                bool first = seed % 4 == 0;
                var layout = DungeonGenerator.Generate(seed, 5, 7, boss, first);
                var map = layout.Map;

                Assert.That(layout.Rooms.Count, Is.InRange(3, 8), $"seed {seed}");
                var reachable = map.Reachable(layout.Entrance.row, layout.Entrance.col);
                Assert.AreEqual(map.FloorCount(), reachable.Count, $"seed {seed}: todo chão alcançável pela entrada (degraus ≤ 2)");

                foreach (var (r, c, ch) in map.Objects())
                    Assert.IsTrue(map.IsFloor(r, c), $"seed {seed}: '{ch}' fora do chão");

                var chars = map.Objects().Select(o => o.c).ToList();
                Assert.AreEqual(1, chars.Count(c => c == 'P'), $"seed {seed}: uma entrada");
                if (boss)
                {
                    Assert.AreEqual(1, chars.Count(c => c == 'O'), $"seed {seed}: chefe");
                    Assert.AreEqual(1, chars.Count(c => c == 'F'), $"seed {seed}: fogueira antes do chefe");
                    Assert.AreEqual(0, chars.Count(c => c == 'D'));
                }
                else Assert.AreEqual(1, chars.Count(c => c == 'D'), $"seed {seed}: escada");
                Assert.AreEqual(first ? 1 : 0, chars.Count(c => c == 'E'), $"seed {seed}: portal só no 1º andar");
                Assert.GreaterOrEqual(chars.Count(c => c == 'e'), 2, $"seed {seed}: inimigos");
            }
        }

        [Test]
        public void SameSeed_SameLayout()
        {
            var a = DungeonGenerator.Generate(42, 5, 7, false, true);
            var b = DungeonGenerator.Generate(42, 5, 7, false, true);
            CollectionAssert.AreEqual(a.Map.Objects().ToList(), b.Map.Objects().ToList());
            Assert.AreEqual(a.Map.FloorCount(), b.Map.FloorCount());
        }
    }

    public class ScalingTests
    {
        [Test]
        public void ScaledEnemies_GrowWithLevel_AndTier()
        {
            var db = DefaultContent.Build();
            var b = db.balance;
            var slime = db.Find<EnemyDefinition>("slime");
            var lv1 = EnemyScaling.Scaled(slime, 1, DifficultyTier.Normal, b);
            var lv6 = EnemyScaling.Scaled(slime, 6, DifficultyTier.Normal, b);
            var lv6Hard = EnemyScaling.Scaled(slime, 6, DifficultyTier.Hard, b);

            Assert.AreEqual(slime.stats.hp, lv1.stats.hp, 0.01f, "nível base não muda");
            Assert.Greater(lv6.stats.hp, lv1.stats.hp);
            Assert.Greater(lv6.stats.attack, lv1.stats.attack);
            Assert.Greater(lv6Hard.stats.hp, lv6.stats.hp);
            Assert.Greater(lv6Hard.xp, lv6.xp, "dificuldade maior paga mais XP");
            Assert.AreEqual(6, lv6.level);
            Assert.AreEqual(slime.id, lv6.id);
            Assert.AreEqual(48f, slime.stats.hp, 0.01f, "o original não é alterado");
        }

        [Test]
        public void DungeonLevel_FollowsPartyAndTier()
        {
            var b = DefaultContent.Build().balance;
            Assert.AreEqual(5, EnemyScaling.DungeonLevel(5, 2, DifficultyTier.Normal, b));
            Assert.AreEqual(4, EnemyScaling.DungeonLevel(5, 2, DifficultyTier.Easy, b));
            Assert.AreEqual(6, EnemyScaling.DungeonLevel(5, 2, DifficultyTier.Hard, b));
            Assert.AreEqual(8, EnemyScaling.DungeonLevel(5, 2, DifficultyTier.Nightmare, b));
            Assert.AreEqual(18, EnemyScaling.DungeonLevel(12, 2, DifficultyTier.Nightmare, b), "Pesadelo = +50% do nível");
            Assert.AreEqual(3, EnemyScaling.DungeonLevel(1, 3, DifficultyTier.Easy, b), "nunca abaixo do nível base da dungeon");
        }

        [Test]
        public void BossEncounter_PutsBossInTheMiddle()
        {
            var db = DefaultContent.Build();
            var mine = db.Find<DungeonDefinition>("dg_mina");
            var enc = DungeonPopulation.BossEncounter(mine, 5, DifficultyTier.Normal, db.balance, "x");
            Assert.IsTrue(enc.isBoss);
            Assert.IsFalse(enc.canFlee);
            Assert.AreEqual(mine.boss.id, enc.enemies[1].id);
            Assert.AreEqual(6, enc.enemies[1].level);
        }

        [Test]
        public void WorldGraph_IsConnected_AndStartsAtVale()
        {
            var db = DefaultContent.Build();
            Assert.AreEqual("loc_vale", db.startLocation.id);
            var seen = new System.Collections.Generic.HashSet<LocationDefinition> { db.startLocation };
            var queue = new System.Collections.Generic.Queue<LocationDefinition>(seen);
            while (queue.Count > 0)
                foreach (var n in queue.Dequeue().connections)
                    if (seen.Add(n)) queue.Enqueue(n);
            Assert.AreEqual(db.locations.Count, seen.Count, "todo local alcançável no mapa");
            Assert.IsTrue(db.locations.Where(l => l.kind == LocationKind.Dungeon).All(l => l.dungeon != null && l.dungeon.boss != null));
        }
    }

    public class SaveAndShopTests
    {
        GameDatabase db;
        string path;

        [SetUp]
        public void SetUp()
        {
            db = DefaultContent.Build();
            path = Path.Combine(Path.GetTempPath(), $"oiram-save-{System.Guid.NewGuid():N}.json");
            SaveSystem.PathOverride = path;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.PathOverride = null;
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void SaveLoad_RoundTripsTheWholeGame()
        {
            var s = new GameSession(db, new SeededRandom(9));
            var oiram = s.Party[0];
            oiram.GainXp(500);
            oiram.GainJp(400);
            var guardian = db.Find<JobDefinition>("guardiao");
            Assert.IsTrue(oiram.ProgressFor(guardian).TryLearn(db.Find<AbilityDefinition>("provocar")));
            Assert.IsTrue(oiram.ProgressFor(guardian).TryLearn(db.Find<AbilityDefinition>("pele_de_ferro")));
            oiram.SetSupportPassive(db.Find<AbilityDefinition>("pele_de_ferro"));
            var relic = ShopService.OpenHiddenChest(s, "loc_vila_reliquia");
            if (oiram.CanEquip(relic)) Assert.IsTrue(s.Inventory.Equip(oiram, relic));
            s.Inventory.Gold = 777;
            s.ClearedLocations.Add("loc_vale");
            s.BestTier["loc_mina"] = DifficultyTier.Hard;
            s.CurrentLocationId = "loc_vila";
            int hp = oiram.CurrentHp - 3;
            oiram.CurrentHp = hp;
            ShopService.BlacksmithStock(s, "loc_vila");

            SaveSystem.Save(s, "pousada");
            var loaded = SaveSystem.Load(db);

            var lo = loaded.Party[0];
            Assert.AreEqual(oiram.Level, lo.Level);
            Assert.AreEqual(oiram.Xp, lo.Xp);
            Assert.AreEqual(hp, lo.CurrentHp);
            Assert.AreEqual(oiram.ProgressFor(guardian).TotalJp, lo.ProgressFor(guardian).TotalJp);
            Assert.AreEqual(oiram.ProgressFor(guardian).AvailableJp, lo.ProgressFor(guardian).AvailableJp);
            Assert.IsTrue(lo.ProgressFor(guardian).IsLearned(db.Find<AbilityDefinition>("provocar")));
            Assert.AreEqual("pele_de_ferro", lo.SupportPassive.id);
            CollectionAssert.AreEquivalent(oiram.EquippedItems().Select(i => i.Name + i.Rarity), lo.EquippedItems().Select(i => i.Name + i.Rarity));
            Assert.AreEqual(oiram.MaxHp, lo.MaxHp, "mesmos atributos");
            CollectionAssert.AreEquivalent(s.Inventory.Items.Select(i => i.Id), loaded.Inventory.Items.Select(i => i.Id));
            Assert.AreEqual(777, loaded.Inventory.Gold);
            Assert.AreEqual(s.Inventory.Count(db.Find<Inventory.ConsumableDefinition>("pocao")), loaded.Inventory.Count(db.Find<Inventory.ConsumableDefinition>("pocao")));
            Assert.IsTrue(loaded.ClearedLocations.Contains("loc_vale"));
            Assert.IsTrue(loaded.ClearedFieldObjects.Contains("loc_vila_reliquia"), "baú escondido continua aberto");
            Assert.AreEqual(DifficultyTier.Hard, loaded.BestTier["loc_mina"]);
            Assert.AreEqual("loc_vila", loaded.CurrentLocationId);
            Assert.AreEqual("pousada", loaded.SpawnPointId);
            Assert.AreEqual(s.ShopStock["loc_vila"].Count, loaded.ShopStock["loc_vila"].Count);
        }

        [Test]
        public void Blacksmith_StockIsStableUntilRefresh_AndBuyingWorks()
        {
            var s = new GameSession(db, new SeededRandom(1)) { WorldSeed = 99 };
            var stock = ShopService.BlacksmithStock(s, "loc_vila");
            Assert.AreEqual(db.balance.blacksmithStock, stock.Count);
            Assert.IsTrue(stock.All(i => i.Rarity >= Rarity.Uncommon));
            Assert.AreSame(stock, ShopService.BlacksmithStock(s, "loc_vila"));

            var item = stock[0];
            int price = ShopService.BuyPrice(item, db.balance);
            s.Inventory.Gold = price - 1;
            Assert.IsFalse(ShopService.BuyItem(s, "loc_vila", item), "sem ouro");
            s.Inventory.Gold = price;
            Assert.IsTrue(ShopService.BuyItem(s, "loc_vila", item));
            Assert.AreEqual(0, s.Inventory.Gold);
            Assert.Contains(item, s.Inventory.Items.ToList());
            Assert.IsFalse(ShopService.BlacksmithStock(s, "loc_vila").Contains(item));

            s.ShopRefreshCount++;
            var renewed = ShopService.BlacksmithStock(s, "loc_vila");
            Assert.AreEqual(db.balance.blacksmithStock, renewed.Count, "renovou após a dungeon");
        }

        [Test]
        public void Gamble_NeverCommon_AndCharges()
        {
            var s = new GameSession(db, new SeededRandom(2));
            var rng = new SeededRandom(3);
            for (int i = 0; i < 50; i++)
            {
                s.Inventory.Gold = ShopService.GamblePrice(s);
                var item = ShopService.Gamble(s, EquipSlot.Weapon, rng);
                Assert.IsNotNull(item);
                Assert.AreEqual(EquipSlot.Weapon, item.Slot);
                Assert.GreaterOrEqual(item.Rarity, Rarity.Uncommon);
                Assert.Less(item.Rarity, Rarity.Relic);
                Assert.AreEqual(0, s.Inventory.Gold);
                s.Inventory.Remove(item);
            }
        }

        [Test]
        public void HiddenChest_OnlyOnce()
        {
            var s = new GameSession(db, new SeededRandom(4));
            var relic = ShopService.OpenHiddenChest(s, "x_reliquia");
            Assert.AreEqual(Rarity.Relic, relic.Rarity);
            Assert.AreEqual(s.PartyLevel + db.balance.relicLevelBonus, relic.ItemLevel);
            Assert.IsNull(ShopService.OpenHiddenChest(s, "x_reliquia"));
        }

        [Test]
        public void Inn_ChargesAndHeals()
        {
            var s = new GameSession(db, new SeededRandom(5));
            s.Party[0].CurrentHp = 1;
            s.Inventory.Gold = ShopService.InnPrice(s);
            Assert.IsTrue(ShopService.Rest(s));
            Assert.AreEqual(s.Party[0].MaxHp, s.Party[0].CurrentHp);
            Assert.AreEqual(0, s.Inventory.Gold);
            Assert.IsFalse(ShopService.Rest(s));
        }
    }
}
