using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Characters
{
    [Serializable]
    public struct JobRequirement
    {
        public JobDefinition job;
        public int level;
    }

    [CreateAssetMenu(menuName = "OiramRPG/Job", fileName = "Job")]
    public sealed class JobDefinition : Definition
    {
        public Color color = Color.white;
        [Tooltip("Nome do comando de habilidades na batalha, ex.: \"Magia Arcana\".")]
        public string skillsetName;
        [Tooltip("Multiplicadores aplicados aos atributos base do personagem.")]
        public PrimaryStats statMultipliers = PrimaryStats.One;
        public List<EquipCategory> allowedCategories = new();
        public List<AbilityDefinition> abilities = new();
        public List<JobRequirement> requirements = new();

        public bool CanEquip(EquipCategory category) => allowedCategories.Contains(category);
    }
}
