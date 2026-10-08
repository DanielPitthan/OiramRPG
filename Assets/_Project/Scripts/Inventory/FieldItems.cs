using System;
using System.Linq;
using Oiram.Characters;
using Oiram.Core;

namespace Oiram.Inventory
{
    public struct FieldItemResult
    {
        public bool Used;
        public int Healed;
        public int EnergyRestored;
        public bool Revived;
        public string Message;
    }

    /// <summary>
    /// Consumíveis usados pelo menu de pausa (fora da batalha). Mesmos números da batalha; o item só é gasto
    /// se fizer efeito (não dá para desperdiçar uma Poção em quem está com a vida cheia).
    /// </summary>
    public static class FieldItems
    {
        /// <summary>PE vale para a party inteira: não precisa escolher alvo.</summary>
        public static bool NeedsTarget(ConsumableDefinition item) => item != null && item.effect != ConsumableEffect.RestoreEnergy;

        public static bool CanUse(GameSession session, ConsumableDefinition item, PartyMember target)
        {
            if (item == null || session.Inventory.Count(item) <= 0) return false;
            return item.effect switch
            {
                ConsumableEffect.RestoreEnergy => session.Energy < session.MaxEnergy,
                ConsumableEffect.HealHP => target != null && target.IsAlive && target.CurrentHp < target.MaxHp,
                ConsumableEffect.Revive => target != null && !target.IsAlive,
                _ => false,
            };
        }

        /// <summary>Alguém da party pode receber este item agora?</summary>
        public static bool AnyUseful(GameSession session, ConsumableDefinition item) =>
            NeedsTarget(item) ? session.Party.Any(m => CanUse(session, item, m)) : CanUse(session, item, null);

        public static FieldItemResult Use(GameSession session, ConsumableDefinition item, PartyMember target)
        {
            if (item == null || session.Inventory.Count(item) <= 0)
                return new FieldItemResult { Message = "Você não tem esse item." };
            if (!CanUse(session, item, target))
                return new FieldItemResult { Message = WhyNot(session, item, target) };

            session.Inventory.ConsumeOne(item);
            var result = new FieldItemResult { Used = true };
            switch (item.effect)
            {
                case ConsumableEffect.RestoreEnergy:
                {
                    int before = session.Energy;
                    session.RestoreEnergy(item.amount);
                    result.EnergyRestored = session.Energy - before;
                    result.Message = $"{item.displayName}: +{result.EnergyRestored} PE para a party.";
                    break;
                }
                case ConsumableEffect.HealHP:
                {
                    int before = target.CurrentHp;
                    target.CurrentHp = Math.Min(target.MaxHp, target.CurrentHp + item.amount);
                    result.Healed = target.CurrentHp - before;
                    result.Message = $"{target.Name} recuperou {result.Healed} PV.";
                    break;
                }
                case ConsumableEffect.Revive:
                    target.CurrentHp = Math.Max(1, (int)Math.Round(target.MaxHp * item.amount / 100f));
                    result.Revived = true;
                    result.Healed = target.CurrentHp;
                    result.Message = $"{target.Name} se levantou com {target.CurrentHp} PV!";
                    break;
            }
            return result;
        }

        static string WhyNot(GameSession session, ConsumableDefinition item, PartyMember target) => item.effect switch
        {
            ConsumableEffect.RestoreEnergy => "Os PE já estão cheios.",
            ConsumableEffect.HealHP when target != null && !target.IsAlive => $"{target.Name} está nocauteado: use uma Pena.",
            ConsumableEffect.HealHP => $"{target?.Name ?? "Ninguém"} já está com os PV cheios.",
            ConsumableEffect.Revive => $"{target?.Name ?? "Ninguém"} não está nocauteado.",
            _ => "Não dá para usar isso agora.",
        };
    }
}
