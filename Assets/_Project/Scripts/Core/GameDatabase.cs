using System.Collections.Generic;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.World;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Índice de todo o conteúdo. Fica em Resources/GameDatabase.asset (gerado pelo ContentSeeder).</summary>
    [CreateAssetMenu(menuName = "OiramRPG/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "GameDatabase";

        public BalanceConfig balance;
        public AbilityDefinition basicAttack;

        public List<CharacterDefinition> characters = new();
        public List<JobDefinition> jobs = new();
        public List<AbilityDefinition> abilities = new();
        public List<ItemBaseDefinition> items = new();
        public List<AffixDefinition> affixes = new();
        public List<ConsumableDefinition> consumables = new();
        public List<EnemyDefinition> enemies = new();
        public List<EncounterDefinition> encounters = new();
        public List<LootTable> lootTables = new();
        public List<DungeonDefinition> dungeons = new();
        public List<LocationDefinition> locations = new();
        public LocationDefinition startLocation;

        [Header("Inventário inicial")]
        public List<ConsumableDefinition> startingConsumables = new();
        public int startingGold = 20;

        Dictionary<string, Definition> lookup;

        public T Find<T>(string id) where T : Definition
        {
            if (string.IsNullOrEmpty(id)) return null;
            string key = typeof(T).Name + ":" + id;
            if (lookup != null && lookup.TryGetValue(key, out var cached) && cached != null) return cached as T;
            // Cache vazio ou desatualizado (ex.: assets recarregados no editor): reconstrói uma vez.
            BuildLookup();
            return lookup.TryGetValue(key, out var def) ? def as T : null;
        }

        void BuildLookup()
        {
            lookup = new Dictionary<string, Definition>();
            Add(characters); Add(jobs); Add(abilities); Add(items); Add(affixes);
            Add(consumables); Add(enemies); Add(encounters); Add(lootTables); Add(dungeons); Add(locations);
        }

        void Add<T>(List<T> list) where T : Definition
        {
            foreach (var def in list)
                if (def != null && !string.IsNullOrEmpty(def.id))
                    lookup[typeof(T).Name + ":" + def.id] = def;
        }

        void OnValidate() => lookup = null;

        static GameDatabase loaded;

        /// <summary>Carrega o banco de Resources; se ainda não foi gerado, monta o conteúdo padrão em memória.</summary>
        public static GameDatabase Load()
        {
            if (loaded != null) return loaded;
            loaded = Resources.Load<GameDatabase>(ResourcePath);
            if (loaded == null)
            {
                Debug.LogWarning("GameDatabase não encontrado em Resources — usando conteúdo padrão em memória. " +
                                 "Rode o menu OiramRPG ▸ Construir Fatia Vertical.");
                loaded = DefaultContent.Build();
            }
            return loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => loaded = null;
    }
}
