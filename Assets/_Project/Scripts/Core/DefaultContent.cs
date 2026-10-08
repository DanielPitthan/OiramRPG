using System.Collections.Generic;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;
using Oiram.World;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>
    /// Conteúdo inicial da fatia vertical montado em código. O ContentSeeder (editor) salva tudo como assets;
    /// depois disso os assets são a fonte da verdade. Também serve de fallback em memória e para os testes.
    /// </summary>
    public static class DefaultContent
    {
        const EquipSlot W = EquipSlot.Weapon;
        const EquipSlot A = EquipSlot.Armor;
        const EquipSlot H = EquipSlot.Helmet;
        const EquipSlot X = EquipSlot.Accessory;

        public static GameDatabase Build()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.name = "GameDatabase";
            db.balance = BalanceConfig.CreateDefault();
            db.balance.name = "BalanceConfig";

            BuildAbilitiesAndJobs(db);
            BuildItems(db);
            BuildAffixes(db);
            BuildConsumables(db);
            BuildLootTablesAndEnemies(db);
            BuildCharacters(db);
            BuildWorld(db);
            return db;
        }

        // ------------------------------------------------------------------ helpers

        static T Def<T>(string id, string name, string description = null) where T : Definition
        {
            var def = ScriptableObject.CreateInstance<T>();
            def.id = id;
            def.displayName = name;
            def.description = description;
            def.name = id;
            return def;
        }

        static StatModifier Flat(StatType stat, float value) => new(stat, value);
        static StatModifier Pct(StatType stat, float value) => new(stat, value, ModifierKind.Percent);

        static AbilityDefinition Ability(GameDatabase db, string id, string name, string description, AbilityKind kind,
            TargetType target, float power = 1f, int energy = 0, int jp = 0, TimedHitType timing = TimedHitType.SinglePress, int hits = 1)
        {
            var a = Def<AbilityDefinition>(id, name, description);
            a.kind = kind;
            a.target = target;
            a.power = power;
            a.energyCost = energy;
            a.jpCost = jp;
            a.timing = timing;
            a.hits = hits;
            db.abilities.Add(a);
            return a;
        }

        static AbilityDefinition WithStatus(this AbilityDefinition a, StatusType status, float chance, int turns)
        {
            a.appliesStatus = true;
            a.status = status;
            a.statusChance = chance;
            a.statusTurns = turns;
            return a;
        }

        static AbilityDefinition Passive(GameDatabase db, string id, string name, string description, int jp, params StatModifier[] mods)
        {
            var a = Ability(db, id, name, description, AbilityKind.Passive, TargetType.Self, 0f, 0, jp, TimedHitType.None);
            a.passiveModifiers.AddRange(mods);
            return a;
        }

        static JobDefinition Job(GameDatabase db, string id, string name, string skillset, string description, Color color,
            PrimaryStats multipliers, EquipCategory[] categories, params AbilityDefinition[] abilities)
        {
            var job = Def<JobDefinition>(id, name, description);
            job.skillsetName = skillset;
            job.color = color;
            job.statMultipliers = multipliers;
            job.allowedCategories.AddRange(categories);
            job.abilities.AddRange(abilities);
            db.jobs.Add(job);
            return job;
        }

        // ------------------------------------------------------------------ habilidades e jobs

        static readonly Dictionary<string, AbilityDefinition> A_ = new();
        static readonly Dictionary<string, JobDefinition> J_ = new();
        static readonly Dictionary<string, ItemBaseDefinition> I_ = new();
        static readonly Dictionary<string, ConsumableDefinition> C_ = new();

        static void BuildAbilitiesAndJobs(GameDatabase db)
        {
            A_.Clear(); J_.Clear();

            db.basicAttack = Ability(db, "ataque", "Ataque", "Golpe básico. Aperte no impacto para um acerto melhor.",
                AbilityKind.PhysicalAttack, TargetType.SingleEnemy);

            // Aprendiz
            var golpeDuplo = Ability(db, "golpe_duplo", "Golpe Duplo", "Dois golpes rápidos: aperte no tempo de cada um.",
                AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 0.7f, 2, 50, TimedHitType.MultiPress, hits: 2);
            var incentivo = Ability(db, "incentivo", "Incentivo", "Aumenta o ATQ de um aliado por 3 turnos.",
                AbilityKind.Buff, TargetType.SingleAlly, 0f, 2, 40, TimedHitType.None).WithStatus(StatusType.AttackUp, 1f, 3);
            var disciplina = Passive(db, "disciplina", "Disciplina", "+10% de JP ganho.", 80, Flat(StatType.JPBonus, 10));

            // Guardião
            var investida = Ability(db, "investida", "Investida", "Segure o botão para carregar e solte quando brilhar.",
                AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.7f, 3, 60, TimedHitType.HoldRelease);
            var provocar = Ability(db, "provocar", "Provocar", "Atrai os ataques inimigos por 3 turnos.",
                AbilityKind.Taunt, TargetType.Self, 0f, 1, 50, TimedHitType.None).WithStatus(StatusType.Taunt, 1f, 3);
            var muralha = Ability(db, "muralha", "Muralha", "Aumenta DEF e RES de toda a party por 3 turnos.",
                AbilityKind.Buff, TargetType.AllAllies, 0f, 4, 90, TimedHitType.None).WithStatus(StatusType.DefenseUp, 1f, 3);
            var peleDeFerro = Passive(db, "pele_de_ferro", "Pele de Ferro", "+10% DEF.", 100, Pct(StatType.Defense, 10));

            // Arcanista
            var faisca = Ability(db, "faisca", "Faísca", "Magia de fogo. Pode causar Queimadura.",
                AbilityKind.MagicAttack, TargetType.SingleEnemy, 1.4f, 3, 50).WithStatus(StatusType.Burn, 0.3f, 3);
            var nevasca = Ability(db, "nevasca", "Nevasca", "Gelo em todos os inimigos.",
                AbilityKind.MagicAttack, TargetType.AllEnemies, 0.9f, 6, 100);
            var trovao = Ability(db, "trovao", "Trovão", "Raio poderoso. Pode atordoar.",
                AbilityKind.MagicAttack, TargetType.SingleEnemy, 2.0f, 7, 150).WithStatus(StatusType.Stun, 0.2f, 1);
            var menteAfiada = Passive(db, "mente_afiada", "Mente Afiada", "+10% MAG.", 100, Pct(StatType.Magic, 10));

            // Clérigo
            var cura = Ability(db, "cura", "Cura", "Recupera PV de um aliado.",
                AbilityKind.Heal, TargetType.SingleAlly, 1.6f, 3, 50);
            var curaGrupo = Ability(db, "cura_em_grupo", "Cura em Grupo", "Recupera PV de toda a party.",
                AbilityKind.Heal, TargetType.AllAllies, 1.0f, 6, 100);
            var reviver = Ability(db, "reviver", "Reviver", "Revive um aliado com 40% dos PV.",
                AbilityKind.Revive, TargetType.SingleFallenAlly, 0.4f, 8, 150, TimedHitType.None);
            var bencao = Passive(db, "bencao", "Bênção", "+10% RES e +10% PV.", 80, Pct(StatType.Resistance, 10), Pct(StatType.MaxHP, 10));

            // Ladino
            var roubar = Ability(db, "roubar", "Roubar", "Rola o loot do inimigo para você! Um Perfeito melhora a raridade. 1× por inimigo.",
                AbilityKind.Steal, TargetType.SingleEnemy, 0f, 2, 60);
            var golpeRapido = Ability(db, "golpe_rapido", "Golpe Rápido", "Ataque barato e veloz.",
                AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.2f, 1, 70);
            var faro = Passive(db, "faro_de_tesouro", "Faro de Tesouro", "+20% de achado mágico e de ouro.", 120,
                Flat(StatType.MagicFind, 20), Flat(StatType.GoldFind, 20));

            var aprendiz = Job(db, "aprendiz", "Aprendiz", "Técnicas", "Job básico e versátil. Abre caminho para os outros.",
                new Color(0.78f, 0.72f, 0.6f), new PrimaryStats(1, 1, 1, 1, 1, 1),
                new[] { EquipCategory.Sword, EquipCategory.Dagger, EquipCategory.Mace, EquipCategory.LightArmor, EquipCategory.Robe,
                        EquipCategory.Hood, EquipCategory.Hat, EquipCategory.Ring, EquipCategory.Amulet },
                golpeDuplo, incentivo, disciplina);

            var guardiao = Job(db, "guardiao", "Guardião", "Proezas", "Linha de frente: muito PV e DEF.",
                new Color(0.8f, 0.3f, 0.25f), new PrimaryStats(1.2f, 1.15f, 1.25f, 0.7f, 0.9f, 0.9f),
                new[] { EquipCategory.Sword, EquipCategory.Axe, EquipCategory.Mace, EquipCategory.HeavyArmor, EquipCategory.LightArmor,
                        EquipCategory.HeavyHelm, EquipCategory.Ring, EquipCategory.Amulet },
                investida, provocar, muralha, peleDeFerro);

            var arcanista = Job(db, "arcanista", "Arcanista", "Magia Arcana", "Dano mágico alto, corpo frágil.",
                new Color(0.35f, 0.4f, 0.9f), new PrimaryStats(0.85f, 0.7f, 0.8f, 1.3f, 1.15f, 1f),
                new[] { EquipCategory.Staff, EquipCategory.Dagger, EquipCategory.Robe, EquipCategory.Hood, EquipCategory.Hat,
                        EquipCategory.Ring, EquipCategory.Amulet },
                faisca, nevasca, trovao, menteAfiada);

            var clerigo = Job(db, "clerigo", "Clérigo", "Milagres", "Cura e suporte.",
                new Color(0.95f, 0.93f, 0.78f), new PrimaryStats(1f, 0.8f, 0.95f, 1.15f, 1.25f, 0.95f),
                new[] { EquipCategory.Staff, EquipCategory.Mace, EquipCategory.Robe, EquipCategory.LightArmor, EquipCategory.Hood,
                        EquipCategory.Hat, EquipCategory.Ring, EquipCategory.Amulet },
                cura, curaGrupo, reviver, bencao);

            var ladino = Job(db, "ladino", "Ladino", "Truques", "Veloz, rouba itens e acha mais loot.",
                new Color(0.32f, 0.32f, 0.36f), new PrimaryStats(0.9f, 1.05f, 0.85f, 0.8f, 0.9f, 1.35f),
                new[] { EquipCategory.Dagger, EquipCategory.Sword, EquipCategory.LightArmor, EquipCategory.Hood,
                        EquipCategory.Ring, EquipCategory.Amulet },
                roubar, golpeRapido, faro);

            guardiao.requirements.Add(new JobRequirement { job = aprendiz, level = 2 });
            arcanista.requirements.Add(new JobRequirement { job = aprendiz, level = 2 });
            clerigo.requirements.Add(new JobRequirement { job = aprendiz, level = 2 });
            ladino.requirements.Add(new JobRequirement { job = aprendiz, level = 3 });

            foreach (var job in db.jobs) J_[job.id] = job;
            foreach (var a in db.abilities) A_[a.id] = a;

            // Habilidades de inimigos (não aprendíveis).
            Ability(db, "e_investida_viscosa", "Investida Viscosa", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.0f);
            Ability(db, "e_mordida", "Mordida", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.0f);
            Ability(db, "e_rasante", "Rasante", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.2f);
            Ability(db, "e_clava", "Golpe de Clava", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.15f);
            Ability(db, "e_mordida_voraz", "Mordida Voraz", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.35f);
            Ability(db, "e_bafo", "Bafo Ardente", null, AbilityKind.MagicAttack, TargetType.SingleEnemy, 1.1f).WithStatus(StatusType.Burn, 0.4f, 3);
            Ability(db, "e_punho", "Punho de Pedra", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.3f).WithStatus(StatusType.Stun, 0.25f, 1);
            Ability(db, "e_soco_sismico", "Soco Sísmico", null, AbilityKind.PhysicalAttack, TargetType.AllEnemies, 0.75f);
            foreach (var a in db.abilities) A_[a.id] = a;
        }

        // ------------------------------------------------------------------ itens

        static void Item(GameDatabase db, string id, string name, GrammaticalGender gender, EquipSlot slot, EquipCategory category,
            Color color, params StatModifier[] mods)
        {
            var item = Def<ItemBaseDefinition>(id, name);
            item.gender = gender;
            item.slot = slot;
            item.category = category;
            item.color = color;
            item.baseModifiers.AddRange(mods);
            db.items.Add(item);
            I_[id] = item;
        }

        static void HighItem(GameDatabase db, string id, string name, GrammaticalGender gender, EquipSlot slot, EquipCategory category,
            Color color, params StatModifier[] mods)
        {
            Item(db, id, name, gender, slot, category, color, mods);
            I_[id].minItemLevel = 5;
        }

        static void BuildItems(GameDatabase db)
        {
            I_.Clear();
            const GrammaticalGender M = GrammaticalGender.Masculine, F = GrammaticalGender.Feminine;
            var steel = new Color(0.75f, 0.77f, 0.82f);
            var wood = new Color(0.55f, 0.38f, 0.22f);
            var cloth = new Color(0.45f, 0.35f, 0.65f);
            var gold = new Color(0.95f, 0.8f, 0.3f);

            Item(db, "espada", "Espada", F, W, EquipCategory.Sword, steel, Flat(StatType.Attack, 5));
            Item(db, "machado", "Machado", M, W, EquipCategory.Axe, steel, Flat(StatType.Attack, 7), Flat(StatType.Speed, -1));
            Item(db, "adaga", "Adaga", F, W, EquipCategory.Dagger, steel, Flat(StatType.Attack, 3), Flat(StatType.Speed, 2), Flat(StatType.CritChance, 5));
            Item(db, "cajado", "Cajado", M, W, EquipCategory.Staff, wood, Flat(StatType.Magic, 5), Flat(StatType.Attack, 1));
            Item(db, "maca", "Maça", F, W, EquipCategory.Mace, steel, Flat(StatType.Attack, 3), Flat(StatType.Magic, 3));
            Item(db, "armadura_placas", "Armadura de Placas", F, A, EquipCategory.HeavyArmor, steel,
                Flat(StatType.Defense, 6), Flat(StatType.MaxHP, 5), Flat(StatType.Speed, -1));
            Item(db, "gibao", "Gibão de Couro", M, A, EquipCategory.LightArmor, wood, Flat(StatType.Defense, 3), Flat(StatType.Speed, 1));
            Item(db, "tunica", "Túnica", F, A, EquipCategory.Robe, cloth, Flat(StatType.Defense, 1), Flat(StatType.Resistance, 4), Flat(StatType.Magic, 1));
            Item(db, "elmo", "Elmo", M, H, EquipCategory.HeavyHelm, steel, Flat(StatType.Defense, 3), Flat(StatType.MaxHP, 3));
            Item(db, "capuz", "Capuz", M, H, EquipCategory.Hood, cloth, Flat(StatType.Defense, 1), Flat(StatType.Speed, 1), Flat(StatType.Resistance, 1));
            Item(db, "chapeu", "Chapéu", M, H, EquipCategory.Hat, cloth, Flat(StatType.Resistance, 3), Flat(StatType.Magic, 1));
            Item(db, "anel", "Anel", M, X, EquipCategory.Ring, gold, Flat(StatType.MaxHP, 4));
            Item(db, "amuleto", "Amuleto", M, X, EquipCategory.Amulet, gold, Flat(StatType.Resistance, 2));

            // Bases avançadas: só aparecem a partir do nível de item 5 (dungeons e loja).
            var rune = new Color(0.55f, 0.75f, 0.95f);
            HighItem(db, "lamina_runica", "Lâmina Rúnica", F, W, EquipCategory.Sword, rune, Flat(StatType.Attack, 9), Flat(StatType.CritChance, 3));
            HighItem(db, "machado_guerra", "Machado de Guerra", M, W, EquipCategory.Axe, steel, Flat(StatType.Attack, 12), Flat(StatType.Speed, -1));
            HighItem(db, "punhal", "Punhal", M, W, EquipCategory.Dagger, steel, Flat(StatType.Attack, 6), Flat(StatType.Speed, 3), Flat(StatType.CritChance, 8));
            HighItem(db, "cajado_ancestral", "Cajado Ancestral", M, W, EquipCategory.Staff, wood, Flat(StatType.Magic, 9), Flat(StatType.Resistance, 2));
            HighItem(db, "martelo_sagrado", "Martelo Sagrado", M, W, EquipCategory.Mace, gold, Flat(StatType.Attack, 6), Flat(StatType.Magic, 6));
            HighItem(db, "armadura_runica", "Armadura Rúnica", F, A, EquipCategory.HeavyArmor, rune, Flat(StatType.Defense, 10), Flat(StatType.MaxHP, 10), Flat(StatType.Speed, -1));
            HighItem(db, "cota_malha", "Cota de Malha", F, A, EquipCategory.LightArmor, steel, Flat(StatType.Defense, 6), Flat(StatType.Speed, 1), Flat(StatType.MaxHP, 4));
            HighItem(db, "manto_arcano", "Manto Arcano", M, A, EquipCategory.Robe, cloth, Flat(StatType.Defense, 2), Flat(StatType.Resistance, 7), Flat(StatType.Magic, 3));
            HighItem(db, "elmo_alado", "Elmo Alado", M, H, EquipCategory.HeavyHelm, steel, Flat(StatType.Defense, 5), Flat(StatType.MaxHP, 6));
            HighItem(db, "tiara", "Tiara", F, H, EquipCategory.Hat, gold, Flat(StatType.Resistance, 5), Flat(StatType.Magic, 3));
            HighItem(db, "talisma", "Talismã", M, X, EquipCategory.Amulet, gold, Flat(StatType.Resistance, 3), Flat(StatType.Magic, 2), Flat(StatType.MaxHP, 5));
            HighItem(db, "anel_rubi", "Anel de Rubi", M, X, EquipCategory.Ring, new Color(0.9f, 0.2f, 0.3f), Flat(StatType.MaxHP, 8), Flat(StatType.Attack, 2));
        }

        // ------------------------------------------------------------------ afixos

        static void Affix(GameDatabase db, string id, AffixPosition position, string masculine, string feminine, string group,
            StatType stat, ModifierKind kind, bool special, float weight, EquipSlot[] slots, params AffixTier[] tiers)
        {
            var affix = Def<AffixDefinition>(id, masculine);
            affix.position = position;
            affix.masculineName = masculine;
            affix.feminineName = feminine;
            affix.group = group;
            affix.stat = stat;
            affix.kind = kind;
            affix.isSpecial = special;
            affix.weight = weight;
            affix.allowedSlots.AddRange(slots);
            affix.tiers.AddRange(tiers);
            db.affixes.Add(affix);
        }

        static AffixTier T(int minLevel, float min, float max) => new(minLevel, min, max);

        static void BuildAffixes(GameDatabase db)
        {
            const AffixPosition P = AffixPosition.Prefix, S = AffixPosition.Suffix;
            const ModifierKind F = ModifierKind.Flat, Pc = ModifierKind.Percent;

            // Prefixos (adjetivos com concordância)
            Affix(db, "afiado", P, "Afiado", "Afiada", "atk_flat", StatType.Attack, F, false, 1f, new[] { W, X }, T(1, 2, 4), T(4, 5, 8), T(8, 9, 13), T(12, 14, 19));
            Affix(db, "robusto", P, "Robusto", "Robusta", "def_flat", StatType.Defense, F, false, 1f, new[] { A, H, X }, T(1, 2, 3), T(4, 4, 6), T(8, 7, 10), T(12, 11, 15));
            Affix(db, "arcano", P, "Arcano", "Arcana", "mag_flat", StatType.Magic, F, false, 1f, new[] { W, H, X }, T(1, 2, 4), T(4, 5, 8), T(8, 9, 13), T(12, 14, 19));
            Affix(db, "veloz", P, "Veloz", "Veloz", "spd_flat", StatType.Speed, F, false, 1f, new[] { W, A, H, X }, T(1, 1, 2), T(4, 3, 4), T(8, 5, 6), T(12, 7, 8));
            Affix(db, "vital", P, "Vital", "Vital", "hp_flat", StatType.MaxHP, F, false, 1f, new[] { A, H, X }, T(1, 5, 10), T(4, 11, 20), T(8, 21, 35), T(12, 36, 55));
            Affix(db, "sortudo", P, "Sortudo", "Sortuda", "magic_find", StatType.MagicFind, F, false, 0.8f, new[] { H, X }, T(1, 5, 10), T(4, 11, 20), T(8, 21, 30), T(12, 31, 40));
            Affix(db, "preciso", P, "Preciso", "Precisa", "timing", StatType.TimingWindow, F, true, 0.7f, new[] { W, X }, T(1, 15, 25), T(5, 26, 40), T(10, 41, 55));
            Affix(db, "flamejante", P, "Flamejante", "Flamejante", "burn", StatType.BurnOnPerfect, F, true, 0.7f, new[] { W }, T(1, 25, 40), T(5, 41, 60), T(10, 61, 80));
            Affix(db, "vampirico", P, "Vampírico", "Vampírica", "lifesteal", StatType.LifeSteal, F, true, 0.6f, new[] { W }, T(1, 4, 7), T(5, 8, 12), T(10, 13, 16));

            // Sufixos
            Affix(db, "do_tigre", S, "do Tigre", null, "atk_pct", StatType.Attack, Pc, false, 1f, new[] { W, X }, T(1, 4, 7), T(4, 8, 12), T(8, 13, 18), T(12, 19, 25));
            Affix(db, "da_tartaruga", S, "da Tartaruga", null, "def_pct", StatType.Defense, Pc, false, 1f, new[] { A, H }, T(1, 4, 7), T(4, 8, 12), T(8, 13, 18), T(12, 19, 25));
            Affix(db, "da_coruja", S, "da Coruja", null, "mag_pct", StatType.Magic, Pc, false, 1f, new[] { W, H, X }, T(1, 4, 7), T(4, 8, 12), T(8, 13, 18), T(12, 19, 25));
            Affix(db, "do_vento", S, "do Vento", null, "spd_pct", StatType.Speed, Pc, false, 1f, new[] { A, H, X }, T(1, 4, 8), T(5, 9, 14), T(10, 15, 20));
            Affix(db, "da_muralha", S, "da Muralha", null, "res_flat", StatType.Resistance, F, false, 1f, new[] { A, H, X }, T(1, 2, 4), T(4, 5, 8), T(8, 9, 12), T(12, 13, 17));
            Affix(db, "do_falcao", S, "do Falcão", null, "crit", StatType.CritChance, F, false, 0.8f, new[] { W, X }, T(1, 3, 6), T(4, 7, 10), T(10, 11, 14));
            Affix(db, "da_fortuna", S, "da Fortuna", null, "gold_find", StatType.GoldFind, F, false, 0.8f, new[] { H, X }, T(1, 10, 20), T(4, 21, 35), T(10, 36, 50));
            Affix(db, "do_aprendiz", S, "do Aprendiz", null, "jp", StatType.JPBonus, F, false, 0.8f, new[] { H, X }, T(1, 5, 10), T(4, 11, 20), T(10, 21, 30));
            Affix(db, "do_reflexo", S, "do Reflexo", null, "reflect", StatType.ReflectOnBlock, F, true, 0.7f, new[] { A, H }, T(1, 20, 35), T(5, 36, 50), T(10, 51, 65));
            Affix(db, "da_energia", S, "da Energia", null, "energy", StatType.EnergyCostReduction, F, true, 0.5f, new[] { X }, T(1, 1, 1), T(6, 2, 2), T(12, 3, 3));
        }

        // ------------------------------------------------------------------ consumíveis

        static void BuildConsumables(GameDatabase db)
        {
            C_.Clear();
            ConsumableDefinition Make(string id, string name, string desc, ConsumableEffect effect, int amount, TargetType target, int price, Color color)
            {
                var c = Def<ConsumableDefinition>(id, name, desc);
                c.effect = effect;
                c.amount = amount;
                c.target = target;
                c.price = price;
                c.color = color;
                db.consumables.Add(c);
                C_[id] = c;
                return c;
            }

            var pocao = Make("pocao", "Poção", "Recupera 35 PV de um aliado.", ConsumableEffect.HealHP, 35, TargetType.SingleAlly, 15, new Color(0.9f, 0.25f, 0.3f));
            Make("pocao_grande", "Poção Grande", "Recupera 110 PV de um aliado.", ConsumableEffect.HealHP, 110, TargetType.SingleAlly, 45, new Color(1f, 0.35f, 0.5f));
            var eter = Make("eter", "Éter", "Recupera 8 PE da party.", ConsumableEffect.RestoreEnergy, 8, TargetType.Self, 40, new Color(0.3f, 0.6f, 1f));
            var pena = Make("pena", "Pena Revigorante", "Revive um aliado com 50% dos PV.", ConsumableEffect.Revive, 50, TargetType.SingleFallenAlly, 60, new Color(1f, 0.85f, 0.4f));

            db.startingConsumables.AddRange(new[] { pocao, pocao, pocao, eter, pena });
            db.startingGold = 20;
        }

        // ------------------------------------------------------------------ loot e inimigos

        static LootTable Table(GameDatabase db, string id, string name, int goldMin, int goldMax, int itemsMin, int itemsMax,
            Rarity floor = Rarity.Common, int itemLevelBonus = 0)
        {
            var t = Def<LootTable>(id, name);
            t.goldMin = goldMin;
            t.goldMax = goldMax;
            t.itemsMin = itemsMin;
            t.itemsMax = itemsMax;
            t.rarityFloor = floor;
            t.itemLevelBonus = itemLevelBonus;
            db.lootTables.Add(t);
            return t;
        }

        static LootTable WithConsumable(this LootTable t, string consumableId, float chance)
        {
            t.consumables.Add(new ConsumableDrop { consumable = C_[consumableId], chance = chance });
            return t;
        }

        static EnemyDefinition Enemy(GameDatabase db, string id, string name, int level, PrimaryStats stats, int xp, int jp,
            LootTable loot, UnitShape shape, Color color, float scale, bool flying, params (string ability, float weight)[] abilities)
        {
            var e = Def<EnemyDefinition>(id, name);
            e.level = level;
            e.stats = stats;
            e.xp = xp;
            e.jp = jp;
            e.lootTable = loot;
            e.shape = shape;
            e.color = color;
            e.scale = scale;
            e.flying = flying;
            foreach (var (abilityId, weight) in abilities)
                e.abilities.Add(new EnemyAbility(A_[abilityId], weight));
            db.enemies.Add(e);
            return e;
        }

        static EncounterDefinition Encounter(GameDatabase db, string id, string name, bool canFlee, bool boss, params EnemyDefinition[] enemies)
        {
            var enc = Def<EncounterDefinition>(id, name);
            enc.canFlee = canFlee;
            enc.isBoss = boss;
            enc.enemies.AddRange(enemies);
            db.encounters.Add(enc);
            return enc;
        }

        static void BuildLootTablesAndEnemies(GameDatabase db)
        {
            var ltSlime = Table(db, "lt_slime", "Loot: Slime", 3, 7, 0, 1).WithConsumable("pocao", 0.15f);
            var ltBat = Table(db, "lt_morcego", "Loot: Morcego", 4, 8, 0, 1).WithConsumable("pocao", 0.1f).WithConsumable("eter", 0.08f);
            var ltGoblin = Table(db, "lt_goblin", "Loot: Goblin", 15, 30, 0, 2).WithConsumable("pocao", 0.2f);
            var ltMimic = Table(db, "lt_mimico", "Loot: Mímico", 40, 80, 3, 5, Rarity.Uncommon, 1).WithConsumable("eter", 0.5f);
            ltMimic.guaranteedDrops.Add(Rarity.Rare);
            var ltGolem = Table(db, "lt_golem", "Loot: Golem", 80, 120, 2, 4, Rarity.Uncommon, 1).WithConsumable("pena", 0.5f);
            ltGolem.guaranteedDrops.Add(Rarity.Epic);

            Table(db, "lt_bau", "Baú", 10, 25, 1, 3).WithConsumable("pocao", 0.4f);
            Table(db, "lt_bau_raro", "Baú Reforçado", 20, 40, 2, 3, Rarity.Uncommon).WithConsumable("eter", 0.5f);
            Table(db, "lt_caixa", "Caixa Flutuante", 5, 15, 1, 1).WithConsumable("pocao", 0.3f);

            var slime = Enemy(db, "slime", "Slime Espinhoso", 1, new PrimaryStats(48, 14, 4, 4, 3, 5), 8, 10, ltSlime,
                UnitShape.Slime, new Color(0.4f, 0.85f, 0.35f), 0.9f, false, ("e_investida_viscosa", 1f));
            var bat = Enemy(db, "morcego", "Morcego Trapaceiro", 2, new PrimaryStats(38, 15, 3, 6, 5, 11), 10, 12, ltBat,
                UnitShape.Bat, new Color(0.45f, 0.3f, 0.6f), 0.8f, true, ("e_mordida", 2f), ("e_rasante", 1f));
            var goblin = Enemy(db, "goblin", "Goblin Saqueador", 2, new PrimaryStats(62, 18, 6, 3, 4, 8), 14, 14, ltGoblin,
                UnitShape.Goblin, new Color(0.55f, 0.72f, 0.25f), 1f, false, ("e_clava", 1f));
            var mimic = Enemy(db, "mimico", "Mímico", 3, new PrimaryStats(120, 20, 10, 12, 6, 6), 40, 30, ltMimic,
                UnitShape.Mimic, new Color(0.65f, 0.42f, 0.2f), 1.1f, false, ("e_mordida_voraz", 2f), ("e_bafo", 1f));
            var golem = Enemy(db, "golem", "Golem de Pedra", 4, new PrimaryStats(270, 32, 12, 5, 8, 3), 80, 60, ltGolem,
                UnitShape.Golem, new Color(0.55f, 0.55f, 0.6f), 1.6f, false, ("e_punho", 1f), ("e_soco_sismico", 1f));

            Encounter(db, "enc_slimes2", "Dupla de Slimes", true, false, slime, slime);
            Encounter(db, "enc_slimes3", "Trio de Slimes", true, false, slime, slime, slime);
            Encounter(db, "enc_morcegos", "Revoada", true, false, bat, bat);
            Encounter(db, "enc_goblin_slime", "Emboscada", true, false, goblin, slime);
            Encounter(db, "enc_goblins", "Bando Saqueador", true, false, goblin, goblin, bat);
            Encounter(db, "enc_mimico", "Mímico!", false, false, mimic);
            Encounter(db, "enc_golem", "Golem de Pedra", false, true, slime, golem, slime);
            Encounter(db, "enc_teste", "Teste", true, false, slime);
        }

        // ------------------------------------------------------------------ mundo: dungeons e mapa-múndi

        static void BuildWorld(GameDatabase db)
        {
            // Habilidades dos novos inimigos
            void Reg(AbilityDefinition a) => A_[a.id] = a;
            Reg(Ability(db, "e_teia", "Teia Pegajosa", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 0.6f).WithStatus(StatusType.Stun, 0.3f, 1));
            Reg(Ability(db, "e_espada_ossea", "Espada Óssea", null, AbilityKind.PhysicalAttack, TargetType.SingleEnemy, 1.15f));
            Reg(Ability(db, "e_toque_gelido", "Toque Gélido", null, AbilityKind.MagicAttack, TargetType.SingleEnemy, 1.1f).WithStatus(StatusType.Stun, 0.15f, 1));
            Reg(Ability(db, "e_lamento", "Lamento", null, AbilityKind.MagicAttack, TargetType.AllEnemies, 0.65f));
            Reg(Ability(db, "e_raio_gelido", "Raio Gélido", null, AbilityKind.MagicAttack, TargetType.SingleEnemy, 1.2f));
            Reg(Ability(db, "e_estilhaco", "Estilhaço de Cristal", null, AbilityKind.MagicAttack, TargetType.AllEnemies, 0.7f));
            Reg(Ability(db, "e_grito", "Grito de Guerra", null, AbilityKind.Buff, TargetType.Self, 0f, timing: TimedHitType.None).WithStatus(StatusType.AttackUp, 1f, 3));
            Reg(Ability(db, "e_terremoto", "Terremoto", null, AbilityKind.PhysicalAttack, TargetType.AllEnemies, 0.75f));
            Reg(Ability(db, "e_chuva_viscosa", "Chuva Viscosa", null, AbilityKind.PhysicalAttack, TargetType.AllEnemies, 0.7f));

            EnemyDefinition E(string id) => db.Find<EnemyDefinition>(id);
            LootTable L(string id) => db.Find<LootTable>(id);

            var ltSpider = Table(db, "lt_aranha", "Loot: Aranha", 6, 12, 0, 1).WithConsumable("pocao", 0.15f);
            var ltSkeleton = Table(db, "lt_esqueleto", "Loot: Esqueleto", 8, 14, 0, 1).WithConsumable("pena", 0.05f);
            var ltGhost = Table(db, "lt_fantasma", "Loot: Fantasma", 8, 14, 0, 1).WithConsumable("eter", 0.12f);
            var ltElemental = Table(db, "lt_elemental", "Loot: Elemental", 10, 16, 0, 1).WithConsumable("eter", 0.15f);
            var ltBoss = Table(db, "lt_chefe_dungeon", "Loot: Chefe de dungeon", 50, 80, 1, 2, Rarity.Uncommon).WithConsumable("pocao_grande", 0.6f);
            Table(db, "lt_dg_bau", "Baú da Dungeon", 15, 30, 1, 2).WithConsumable("pocao", 0.4f).WithConsumable("eter", 0.2f);
            Table(db, "lt_dg_chefe", "Tesouro do Chefe", 60, 100, 2, 3, Rarity.Uncommon).WithConsumable("pena", 0.5f);

            var spider = Enemy(db, "aranha", "Aranha das Sombras", 3, new PrimaryStats(70, 17, 5, 4, 5, 10), 12, 14, ltSpider,
                UnitShape.Spider, new Color(0.32f, 0.28f, 0.34f), 0.9f, false, ("e_mordida", 2f), ("e_teia", 1f));
            var skeleton = Enemy(db, "esqueleto", "Esqueleto Guerreiro", 4, new PrimaryStats(90, 20, 8, 3, 5, 7), 16, 16, ltSkeleton,
                UnitShape.Skeleton, new Color(0.92f, 0.9f, 0.82f), 1f, false, ("e_espada_ossea", 1f));
            var ghost = Enemy(db, "fantasma", "Fantasma Lamentoso", 4, new PrimaryStats(70, 10, 4, 19, 12, 9), 16, 16, ltGhost,
                UnitShape.Ghost, new Color(0.8f, 0.82f, 0.96f), 1f, true, ("e_toque_gelido", 2f), ("e_lamento", 1f));
            var elemental = Enemy(db, "elemental", "Elemental de Gelo", 5, new PrimaryStats(100, 12, 10, 21, 14, 6), 20, 18, ltElemental,
                UnitShape.Elemental, new Color(0.5f, 0.88f, 1f), 1f, false, ("e_raio_gelido", 2f), ("e_estilhaco", 1f));

            var foreman = Enemy(db, "capataz", "Capataz Goblin", 4, new PrimaryStats(240, 27, 10, 6, 8, 7), 70, 50, ltBoss,
                UnitShape.Goblin, new Color(0.78f, 0.48f, 0.22f), 1.5f, false, ("e_clava", 2f), ("e_grito", 1f), ("e_terremoto", 1f));
            var knight = Enemy(db, "cavaleiro", "Cavaleiro Espectral", 5, new PrimaryStats(270, 25, 12, 17, 12, 8), 80, 55, ltBoss,
                UnitShape.Knight, new Color(0.45f, 0.4f, 0.62f), 1.35f, false, ("e_espada_ossea", 2f), ("e_lamento", 1f), ("e_toque_gelido", 1f));
            var kingSlime = Enemy(db, "rei_slime", "Rei Slime", 6, new PrimaryStats(330, 26, 9, 15, 10, 5), 90, 60, ltBoss,
                UnitShape.KingSlime, new Color(0.45f, 0.8f, 0.95f), 2f, false, ("e_investida_viscosa", 2f), ("e_chuva_viscosa", 1f), ("e_estilhaco", 1f));

            DungeonDefinition Dungeon(string id, string name, string description, int baseLevel,
                Color floor, Color high, Color side, Color voidColor, Color accent, Color sky, Color ambient,
                EnemyDefinition boss, EnemyDefinition[] minions, params (EnemyDefinition enemy, float weight)[] population)
            {
                var d = Def<DungeonDefinition>(id, name, description);
                d.baseLevel = baseLevel;
                d.floorColor = floor;
                d.floorHighColor = high;
                d.sideColor = side;
                d.voidColor = voidColor;
                d.accentColor = accent;
                d.skyColor = sky;
                d.ambientColor = ambient;
                d.boss = boss;
                d.bossMinions.AddRange(minions);
                foreach (var (enemy, weight) in population) d.enemies.Add(new DungeonEnemy(enemy, weight));
                d.chestLoot = L("lt_dg_bau");
                d.bossChestLoot = L("lt_dg_chefe");
                db.dungeons.Add(d);
                return d;
            }

            var mine = Dungeon("dg_mina", "Mina Abandonada", "Túneis escavados por goblins. Cuidado com as aranhas no escuro.", 2,
                new Color(0.55f, 0.45f, 0.35f), new Color(0.66f, 0.53f, 0.38f), new Color(0.36f, 0.27f, 0.2f), new Color(0.07f, 0.05f, 0.04f),
                new Color(1f, 0.7f, 0.3f), new Color(0.12f, 0.1f, 0.09f), new Color(0.5f, 0.44f, 0.38f),
                foreman, new[] { E("goblin"), spider }, (E("goblin"), 3f), (E("morcego"), 2f), (spider, 2f), (E("slime"), 1f));
            var crypt = Dungeon("dg_cripta", "Cripta Esquecida", "Corredores frios onde os mortos não descansam.", 3,
                new Color(0.42f, 0.4f, 0.48f), new Color(0.52f, 0.5f, 0.58f), new Color(0.25f, 0.23f, 0.3f), new Color(0.04f, 0.03f, 0.07f),
                new Color(0.6f, 0.4f, 1f), new Color(0.08f, 0.06f, 0.12f), new Color(0.42f, 0.4f, 0.5f),
                knight, new[] { skeleton, ghost }, (skeleton, 3f), (ghost, 2f), (E("morcego"), 2f), (spider, 1f));
            var cave = Dungeon("dg_caverna", "Caverna de Cristal", "Cristais que zumbem com magia de gelo.", 4,
                new Color(0.45f, 0.6f, 0.7f), new Color(0.56f, 0.72f, 0.82f), new Color(0.24f, 0.34f, 0.44f), new Color(0.03f, 0.05f, 0.1f),
                new Color(0.4f, 1f, 1f), new Color(0.05f, 0.1f, 0.15f), new Color(0.42f, 0.5f, 0.58f),
                kingSlime, new[] { E("slime"), elemental }, (elemental, 2f), (spider, 2f), (E("slime"), 2f), (ghost, 1f));

            LocationDefinition Place(string id, string name, string description, LocationKind kind, string scene,
                float x, float z, Color color, DungeonDefinition dungeon = null)
            {
                var loc = Def<LocationDefinition>(id, name, description);
                loc.kind = kind;
                loc.sceneName = scene;
                loc.mapPosition = new Vector2(x, z);
                loc.color = color;
                loc.dungeon = dungeon;
                db.locations.Add(loc);
                return loc;
            }

            void Link(LocationDefinition a, LocationDefinition b)
            {
                a.connections.Add(b);
                b.connections.Add(a);
            }

            var vale = Place("loc_vale", "Vale de Oiram", "Onde tudo começou. Campos, slimes e o Golem que guardava a saída.",
                LocationKind.Field, "Field_Vale", 0f, 0f, new Color(0.4f, 0.75f, 0.35f));
            var town = Place("loc_vila", "Vila Ventura", "Lojas, o ferreiro, um apostador suspeito e a pousada. Dizem que há um tesouro escondido por aqui...",
                LocationKind.Town, "Town_Vila", 6f, 3f, new Color(0.95f, 0.8f, 0.4f));
            var locMine = Place("loc_mina", mine.displayName, mine.description, LocationKind.Dungeon, "Dungeon", 12f, -1f, new Color(0.75f, 0.5f, 0.25f), mine);
            var locCrypt = Place("loc_cripta", crypt.displayName, crypt.description, LocationKind.Dungeon, "Dungeon", 13f, 7f, new Color(0.6f, 0.45f, 0.9f), crypt);
            var locCave = Place("loc_caverna", cave.displayName, cave.description, LocationKind.Dungeon, "Dungeon", 19f, 3f, new Color(0.4f, 0.9f, 1f), cave);

            town.requires = vale;
            locMine.requires = vale;
            locCrypt.requires = locMine;
            locCave.requires = locCrypt;
            Link(vale, town);
            Link(town, locMine);
            Link(town, locCrypt);
            Link(locMine, locCave);
            Link(locCrypt, locCave);
            db.startLocation = vale;
        }

        // ------------------------------------------------------------------ personagens

        static void BuildCharacters(GameDatabase db)
        {
            CharacterDefinition Hero(string id, string name, Color color, PrimaryStats b, PrimaryStats g, string job, string ability, params string[] items)
            {
                var c = Def<CharacterDefinition>(id, name);
                c.color = color;
                c.baseStats = b;
                c.growthPerLevel = g;
                c.startingJob = J_[job];
                c.startingAbilities.Add(A_[ability]);
                foreach (var item in items) c.startingEquipment.Add(I_[item]);
                db.characters.Add(c);
                return c;
            }

            Hero("oiram", "Oiram", new Color(0.85f, 0.25f, 0.2f), new PrimaryStats(40, 10, 8, 4, 5, 7), new PrimaryStats(7, 2, 1.6f, 0.8f, 1, 0.6f),
                "guardiao", "investida", "espada", "armadura_placas");
            Hero("lia", "Lia", new Color(0.3f, 0.45f, 0.9f), new PrimaryStats(30, 6, 5, 12, 9, 9), new PrimaryStats(5, 1.2f, 1, 2.4f, 1.6f, 0.8f),
                "arcanista", "faisca", "cajado", "tunica");
            Hero("teo", "Teo", new Color(0.95f, 0.8f, 0.3f), new PrimaryStats(34, 7, 6, 10, 10, 6), new PrimaryStats(6, 1.4f, 1.2f, 2, 1.8f, 0.6f),
                "clerigo", "cura", "maca", "gibao");
        }
    }
}
