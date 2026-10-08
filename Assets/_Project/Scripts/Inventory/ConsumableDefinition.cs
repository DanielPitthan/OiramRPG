using Oiram.Core;
using UnityEngine;

namespace Oiram.Inventory
{
    public enum ConsumableEffect
    {
        HealHP,
        RestoreEnergy,
        Revive,
    }

    [CreateAssetMenu(menuName = "OiramRPG/Consumable", fileName = "Consumable")]
    public sealed class ConsumableDefinition : Definition
    {
        public ConsumableEffect effect;
        [Tooltip("PV curados, PE restaurados ou % de PV ao reviver.")]
        public int amount;
        public TargetType target = TargetType.SingleAlly;
        public int price = 10;
        public Color color = Color.white;
    }
}
