using System;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Dificuldade escolhida ao entrar numa dungeon (estilo Diablo).</summary>
    public enum DifficultyTier
    {
        Easy,
        Normal,
        Hard,
        Nightmare,
    }

    /// <summary>Constantes de balanceamento num só lugar, editáveis no Inspector.</summary>
    [CreateAssetMenu(menuName = "OiramRPG/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Timed hits (segundos)")]
        public float perfectWindow = 0.06f;
        public float goodWindow = 0.15f;
        public float attackGoodMultiplier = 1.25f;
        public float attackPerfectMultiplier = 1.5f;
        public float blockGoodMultiplier = 0.5f;
        public float blockPerfectMultiplier = 0.25f;
        [Tooltip("Atraso típico entre o quadro do impacto e a tela (s). O centro da janela é deslocado por este valor.")]
        public float timingLatencyCompensation = 0.035f;

        [Header("Dano")]
        public float varianceMin = 0.9f;
        public float varianceMax = 1.1f;
        public float critMultiplier = 1.5f;
        public float defendMultiplier = 0.5f;
        public float burnPercentOfMaxHp = 0.08f;
        public float buffPercent = 30f;

        [Header("Progressão")]
        public int startingEnergy = 14;
        public int energyPerLevel = 2;
        public int energyRestoredAfterBattle = 5;
        [Tooltip("JP total necessário para cada nível de job (índice 0 = Nv 1).")]
        public int[] jobLevelThresholds = { 0, 60, 160, 300, 500, 800, 1200, 1700 };
        public int maxCharacterLevel = 30;

        [Header("Loot")]
        [Tooltip("Pesos base: Comum, Incomum, Raro, Épico, Lendário (Relíquia nunca cai aleatoriamente).")]
        public float[] rarityWeights = { 60f, 25f, 10f, 4f, 1f };
        [Tooltip("Aumento do item por nível de item (0.15 = +15% por nível).")]
        public float itemScalingPerLevel = 0.15f;
        [Tooltip("Ouro das tabelas de loot cresce com o nível da fonte (0.25 = +25% por nível acima do 1).")]
        public float goldGrowthPerLevel = 0.15f;
        public int inventoryCapacity = 80;
        public float fleeChance = 0.6f;

        [Header("Inimigos escalados (dungeons): crescimento por nível acima do nível base")]
        public float enemyHpGrowth = 0.26f;
        public float enemyAttackGrowth = 0.11f;
        public float enemyDefenseGrowth = 0.10f;
        public float enemyMagicGrowth = 0.11f;
        public float enemyResistanceGrowth = 0.10f;
        public float enemySpeedGrowth = 0.03f;
        [Tooltip("XP de um inimigo escalado = peso (xp base/10) × XP do nível ÷ este divisor.")]
        public float dungeonXpDivisor = 40f;
        public float jpGrowthPerLevel = 0.1f;

        [Header("Dungeons por dificuldade: Fácil, Normal, Difícil, Pesadelo")]
        [Tooltip("Deslocamento do nível dos inimigos em % do nível da party (mantém a dificuldade relativa estável em qualquer nível).")]
        public float[] tierLevelPercent = { -0.15f, 0f, 0.25f, 0.5f };
        [Tooltip("Deslocamento mínimo absoluto (níveis baixos).")]
        public int[] tierMinLevelOffset = { -1, 0, 1, 2 };
        public float[] tierStatMultiplier = { 0.9f, 1f, 1.1f, 1.12f };
        [Tooltip("Multiplica XP, JP e ouro.")]
        public float[] tierRewardMultiplier = { 0.75f, 1f, 1.35f, 1.8f };
        public float[] tierMagicFind = { 0f, 20f, 50f, 100f };
        public int[] tierFloors = { 2, 3, 3, 4 };
        public Rarity[] tierBossChestFloor = { Rarity.Uncommon, Rarity.Rare, Rarity.Rare, Rarity.Epic };
        [Tooltip("Inimigos extras por grupo (Pesadelo põe mais gente em cada sala).")]
        public int[] tierExtraEnemies = { 0, 0, 0, 1 };

        [Header("Cidade")]
        [Tooltip("Preço de compra = valor de venda × este multiplicador.")]
        public float shopBuyMultiplier = 6f;
        [Tooltip("Preço do item misterioso = (4 + 3 × nível) × este multiplicador.")]
        public float gamblePriceMultiplier = 9f;
        [Tooltip("Pesos do apostador: Comum, Incomum, Raro, Épico, Lendário.")]
        public float[] gambleWeights = { 0f, 55f, 30f, 12f, 3f };
        public int blacksmithStock = 8;
        public int innBasePrice = 10;
        public int innPricePerLevel = 4;
        [Tooltip("Relíquia do baú escondido: nível do item = nível médio da party + este bônus.")]
        public int relicLevelBonus = 3;

        /// <summary>XP necessário para sair do nível atual.</summary>
        public int XpToNextLevel(int level) => 10 + 10 * level * level;

        public int JobLevelForJp(int totalJp)
        {
            int level = 1;
            for (int i = 1; i < jobLevelThresholds.Length; i++)
                if (totalJp >= jobLevelThresholds[i]) level = i + 1;
            return level;
        }

        /// <summary>JP total para alcançar o próximo nível, ou -1 se já está no máximo.</summary>
        public int JpForNextJobLevel(int totalJp)
        {
            int level = JobLevelForJp(totalJp);
            return level < jobLevelThresholds.Length ? jobLevelThresholds[level] : -1;
        }

        static T At<T>(T[] values, DifficultyTier tier, T fallback) =>
            values != null && values.Length > 0 ? values[Math.Min((int)tier, values.Length - 1)] : fallback;

        /// <summary>Quantos níveis acima/abaixo da party ficam os inimigos nesta dificuldade.</summary>
        public int TierLevelOffset(DifficultyTier t, int partyLevel)
        {
            float pct = At(tierLevelPercent, t, 0f);
            int min = At(tierMinLevelOffset, t, 0);
            int byPercent = (int)Math.Round(Math.Abs(pct) * partyLevel, MidpointRounding.AwayFromZero);
            int magnitude = Math.Max(Math.Abs(min), byPercent);
            return pct < 0 || min < 0 ? -magnitude : magnitude;
        }
        public float TierStatMultiplier(DifficultyTier t) => At(tierStatMultiplier, t, 1f);
        public float TierRewardMultiplier(DifficultyTier t) => At(tierRewardMultiplier, t, 1f);
        public float TierMagicFind(DifficultyTier t) => At(tierMagicFind, t, 0f);
        public int TierFloors(DifficultyTier t) => At(tierFloors, t, 3);
        public Rarity TierBossChestFloor(DifficultyTier t) => At(tierBossChestFloor, t, Rarity.Rare);
        public int TierExtraEnemies(DifficultyTier t) => At(tierExtraEnemies, t, 0);

        public static string TierName(DifficultyTier t) => t switch
        {
            DifficultyTier.Easy => "Fácil",
            DifficultyTier.Normal => "Normal",
            DifficultyTier.Hard => "Difícil",
            DifficultyTier.Nightmare => "Pesadelo",
            _ => t.ToString(),
        };

        public int InnPrice(int partyLevel) => innBasePrice + innPricePerLevel * Math.Max(0, partyLevel - 1);
        public int GamblePrice(int partyLevel) => (int)Math.Round((4 + 3 * partyLevel) * gamblePriceMultiplier);

        public static BalanceConfig CreateDefault() => CreateInstance<BalanceConfig>();
    }
}
