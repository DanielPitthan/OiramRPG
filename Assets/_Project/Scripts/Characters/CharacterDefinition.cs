using System;
using System.Collections.Generic;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;

namespace Oiram.Characters
{
    [CreateAssetMenu(menuName = "OiramRPG/Character", fileName = "Character")]
    public sealed class CharacterDefinition : Definition
    {
        public Color color = Color.white;
        public PrimaryStats baseStats;
        public PrimaryStats growthPerLevel;
        public JobDefinition startingJob;
        public List<AbilityDefinition> startingAbilities = new();
        public List<ItemBaseDefinition> startingEquipment = new();
    }
}
