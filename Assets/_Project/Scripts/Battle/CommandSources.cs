using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.UI;
using UnityEngine;

namespace Oiram.Battle
{
    /// <summary>Menus de comando do jogador (Atacar, skillsets primário/secundário, Item, Defender, Fugir).</summary>
    public sealed class PlayerCommandSource : IBattleCommandSource
    {
        enum Option { Attack, Primary, Secondary, Item, Defend, Flee }

        int lastIndex;

        public async Awaitable<BattleCommand> Choose(BattleManager battle, BattleUnit actor, CancellationToken ct)
        {
            var hud = battle.Hud;
            var member = actor.Member;
            var session = battle.Session;

            while (true)
            {
                var primary = member.PrimaryAbilities().ToList();
                var secondary = member.SecondaryAbilities().ToList();
                var options = new List<Option>();
                var entries = new List<MenuEntry>();

                void Add(Option option, MenuEntry entry)
                {
                    options.Add(option);
                    entries.Add(entry);
                }

                Add(Option.Attack, new MenuEntry("Atacar", description: "Ataque básico. Aperte Confirmar no impacto!"));
                Add(Option.Primary, new MenuEntry(member.Job.skillsetName, primary.Count > 0,
                    description: $"Habilidades de {member.Job.displayName}."));
                if (member.SecondaryJob != null)
                    Add(Option.Secondary, new MenuEntry(member.SecondaryJob.skillsetName, secondary.Count > 0,
                        description: $"Habilidade secundária ({member.SecondaryJob.displayName})."));
                Add(Option.Item, new MenuEntry("Item", session.Inventory.Consumables().Any(), description: "Usar um consumível."));
                Add(Option.Defend, new MenuEntry("Defender", description: "Reduz o dano recebido até o seu próximo turno."));
                Add(Option.Flee, new MenuEntry("Fugir", battle.CanFlee, description: battle.CanFlee ? "Tentar fugir da batalha." : "Não dá para fugir desta luta!"));

                hud.ShowCommandMenu($"{actor.Name} — {member.Job.displayName}");
                hud.Menu.SetItems(entries, Mathf.Min(lastIndex, entries.Count - 1));
                int pick = await hud.Menu.Choose(ct, allowCancel: false);
                lastIndex = pick;

                BattleCommand command = null;
                switch (options[pick])
                {
                    case Option.Attack:
                        command = await WithTargets(battle, actor, new BattleCommand { Type = CommandType.Attack, Actor = actor }, TargetType.SingleEnemy, ct);
                        break;
                    case Option.Primary:
                    case Option.Secondary:
                    {
                        var list = options[pick] == Option.Primary ? primary : secondary;
                        var ability = await PickAbility(battle, actor, list, ct);
                        if (ability != null)
                            command = await WithTargets(battle, actor, new BattleCommand { Type = CommandType.Ability, Actor = actor, Ability = ability }, ability.target, ct);
                        break;
                    }
                    case Option.Item:
                    {
                        var item = await PickItem(battle, actor, ct);
                        if (item != null)
                            command = await WithTargets(battle, actor, new BattleCommand { Type = CommandType.Item, Actor = actor, Item = item }, item.target, ct);
                        break;
                    }
                    case Option.Defend:
                        command = BattleCommand.Defend(actor);
                        break;
                    case Option.Flee:
                        command = BattleCommand.Flee(actor);
                        break;
                }

                if (command != null)
                {
                    hud.ShowCommandMenu(null);
                    hud.SetDescription(null);
                    return command;
                }
            }
        }

        static async Awaitable<BattleCommand> WithTargets(BattleManager battle, BattleUnit actor, BattleCommand command, TargetType type, CancellationToken ct)
        {
            battle.Hud.ShowCommandMenu(null);
            var targets = await battle.SelectTargets(actor, type, ct);
            if (targets == null) return null;
            command.Targets = targets;
            return command;
        }

        static async Awaitable<AbilityDefinition> PickAbility(BattleManager battle, BattleUnit actor, List<AbilityDefinition> abilities, CancellationToken ct)
        {
            var entries = abilities.Select(a =>
            {
                int cost = battle.Rules.EnergyCost(actor, a);
                bool hasTarget = Targeting.Candidates(actor, a.target, battle.Units).Count > 0;
                bool enabled = cost <= battle.Session.Energy && hasTarget;
                string timing = a.timing switch
                {
                    TimedHitType.HoldRelease => " [segurar e soltar]",
                    TimedHitType.MultiPress => " [aperte a cada golpe]",
                    _ => "",
                };
                return new MenuEntry(a.displayName, enabled, cost > 0 ? $"{cost} PE" : "", description: a.description + timing);
            }).ToList();

            battle.Hud.Menu.SetItems(entries);
            int pick = await battle.Hud.Menu.Choose(ct);
            return pick < 0 ? null : abilities[pick];
        }

        static async Awaitable<ConsumableDefinition> PickItem(BattleManager battle, BattleUnit actor, CancellationToken ct)
        {
            var items = battle.Session.Inventory.Consumables().ToList();
            var entries = items.Select(i =>
            {
                bool hasTarget = i.item.target == TargetType.Self || Targeting.Candidates(actor, i.item.target, battle.Units).Count > 0;
                return new MenuEntry(i.item.displayName, hasTarget, $"×{i.count}", description: i.item.description);
            }).ToList();

            battle.Hud.Menu.SetItems(entries);
            int pick = await battle.Hud.Menu.Choose(ct);
            return pick < 0 ? null : items[pick].item;
        }
    }

    /// <summary>Comandos automáticos (teste de ponta a ponta / demonstração): ataca o inimigo mais ferido.</summary>
    public sealed class AutoCommandSource : IBattleCommandSource
    {
        public async Awaitable<BattleCommand> Choose(BattleManager battle, BattleUnit actor, CancellationToken ct)
        {
            await Awaitable.NextFrameAsync(ct);
            var target = Targeting.Candidates(actor, TargetType.SingleEnemy, battle.Units).OrderBy(u => u.Hp).First();
            return new BattleCommand { Type = CommandType.Attack, Actor = actor, Targets = { target } };
        }
    }
}
