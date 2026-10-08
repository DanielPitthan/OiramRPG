using System.Collections.Generic;
using System.Linq;
using Oiram.Audio;
using Oiram.Battle;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;
using UnityEngine.UIElements;
using static Oiram.UI.ItemViews;

namespace Oiram.UI
{
    /// <summary>
    /// Menu de pausa: Equipe (atributos), Inventário (equipar/vender com comparação), Itens (consumíveis fora
    /// da batalha), Jobs (job principal, skillset secundário, aprender com JP, passiva) e Opções (volume, timing).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        enum Tab { Party, Inventory, Items, Jobs, Options }
        enum JobsMode { JobList, JobActions, Abilities }
        enum Option { Music, Sfx, Ring, Milliseconds }

        static readonly string[] TabNames = { "Equipe", "Inventário", "Itens", "Jobs", "Opções" };
        static readonly StatType[] MainStats =
            { StatType.MaxHP, StatType.Attack, StatType.Defense, StatType.Magic, StatType.Resistance, StatType.Speed, StatType.CritChance };

        const int VisibleRows = 13;

        VisualElement panel, tabsRow, body;
        Label info, footer, notice;
        FieldHud hud;

        Tab tab;
        JobsMode jobsMode;
        int member, itemIndex, jobIndex, actionIndex, abilityIndex;
        int consumableIndex, targetIndex, optionIndex;
        bool pickingTarget;
        float noticeUntil;
        int openedFrame;
        float previousTimeScale = 1f;

        public bool IsOpen { get; private set; }

        static GameSession S => GameSession.Current;
        PartyMember Member => S.Party[Mathf.Clamp(member, 0, S.Party.Count - 1)];

        public static PauseMenu Create(Transform parent, FieldHud hud)
        {
            var go = new GameObject("PauseMenu");
            go.transform.SetParent(parent, false);
            var menu = go.AddComponent<PauseMenu>();
            menu.hud = hud;
            menu.Build();
            return menu;
        }

        void Build()
        {
            var root = UiKit.CreateDocument(transform, "PauseMenuDocument", 10);
            panel = UiKit.El(root, "pause-root");
            var header = UiKit.El(panel, "row");
            tabsRow = UiKit.El(header, "tabs", "grow");
            info = UiKit.Text(header, "", "big");
            body = UiKit.El(panel, "pause-body");
            notice = UiKit.Text(panel, "", "notice");
            footer = UiKit.Text(panel, "", "footer", "small", "muted");
            UiKit.Show(panel, false);
        }

        public void Open()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            jobsMode = JobsMode.JobList;
            pickingTarget = false;
            Notice(null);
            AudioManager.Play(Sfx.Confirm);
            hud.SetVisible(false);
            UiKit.Show(panel, true);
            Render();
        }

        public void Close()
        {
            IsOpen = false;
            Time.timeScale = previousTimeScale;
            UiKit.Show(panel, false);
            hud.SetVisible(true);
            hud.Refresh();
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (noticeUntil > 0f && Time.unscaledTime > noticeUntil) Notice(null);

            bool inSubMenu = (tab == Tab.Jobs && jobsMode != JobsMode.JobList) || (tab == Tab.Items && pickingTarget);
            if (GameInput.MenuDown || (GameInput.CancelDown && !inSubMenu))
            {
                AudioManager.Play(Sfx.Cancel);
                Close();
                return;
            }
            if (GameInput.PrevTabDown || GameInput.NextTabDown)
            {
                int step = GameInput.NextTabDown ? 1 : -1;
                tab = (Tab)(((int)tab + step + TabNames.Length) % TabNames.Length);
                jobsMode = JobsMode.JobList;
                pickingTarget = false;
                Cursor();
                Render();
                return;
            }

            bool changed = tab switch
            {
                Tab.Party => false,
                Tab.Inventory => UpdateInventory(),
                Tab.Items => UpdateItems(),
                Tab.Jobs => UpdateJobs(),
                Tab.Options => UpdateOptions(),
                _ => false,
            };
            if (changed) Render();
        }

        /// <summary>Recado dentro do menu (o HUD do campo fica escondido enquanto o menu está aberto).</summary>
        void Notice(string text, string cssClass = null)
        {
            notice.text = text ?? "";
            notice.ClearClassList();
            notice.AddToClassList("text");
            notice.AddToClassList("notice");
            if (!string.IsNullOrEmpty(cssClass)) notice.AddToClassList(cssClass);
            noticeUntil = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + 3f;
        }

        static void Cursor() => AudioManager.Play(Sfx.Cursor);
        static void Ok() => AudioManager.Play(Sfx.Confirm);
        static void Denied() => AudioManager.Play(Sfx.Cancel, 0.8f, 0.8f);

        static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;

        // ======================================================================= input

        bool UpdateInventory()
        {
            var nav = GameInput.Nav;
            if (nav.x != 0)
            {
                member = Wrap(member + nav.x, S.Party.Count);
                Cursor();
                return true;
            }
            var items = S.Inventory.Sorted();
            if (items.Count == 0) return false;
            if (nav.y != 0)
            {
                itemIndex = Wrap(itemIndex - nav.y, items.Count);
                Cursor();
                return true;
            }

            var item = items[Mathf.Clamp(itemIndex, 0, items.Count - 1)];
            if (GameInput.ConfirmDown)
            {
                if (S.Inventory.Equip(Member, item))
                {
                    Ok();
                    Notice($"{Member.Name} equipou {item.Name}", RarityInfo.UssClass(item.Rarity));
                    PlaytestLog.Note("equipou", $"{Member.Name}: {item.Name} [{item.Rarity}]");
                }
                else
                {
                    Denied();
                    Notice($"{Member.Job.displayName} não pode usar {RarityInfo.CategoryName(item.Category)}.", "bad");
                }
                itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, S.Inventory.Items.Count - 1));
                return true;
            }
            if (GameInput.SecondaryDown)
            {
                int gold = S.Inventory.Sell(item);
                AudioManager.Play(Sfx.Coin);
                PlaytestLog.Note("vendeu", $"{item.Name} [{item.Rarity}] por {gold}");
                Notice($"Vendeu {item.Name} por {gold} ouro", "gold-text");
                itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, S.Inventory.Items.Count - 1));
                return true;
            }
            return false;
        }

        List<(string text, bool enabled, System.Action action)> JobActions(JobDefinition job)
        {
            var m = Member;
            var actions = new List<(string, bool, System.Action)>
            {
                ("Usar como job principal", job != m.Job, () =>
                {
                    S.Inventory.ChangeJob(m, job);
                    PlaytestLog.Note("job", $"{m.Name}: {job.displayName}");
                    Notice($"{m.Name} agora é {job.displayName}!", "good");
                    jobsMode = JobsMode.JobList;
                }),
                ("Usar como habilidade secundária", job != m.Job && job != m.SecondaryJob, () =>
                {
                    m.SetSecondaryJob(job);
                    PlaytestLog.Note("secundaria", $"{m.Name}: {job.skillsetName}");
                    Notice($"{m.Name}: secundária = {job.skillsetName}", "good");
                    jobsMode = JobsMode.JobList;
                }),
                ("Aprender habilidades", true, () =>
                {
                    jobsMode = JobsMode.Abilities;
                    abilityIndex = 0;
                }),
            };
            if (m.SecondaryJob == job)
                actions.Add(("Remover habilidade secundária", true, () =>
                {
                    m.SetSecondaryJob(null);
                    jobsMode = JobsMode.JobList;
                }));
            return actions;
        }

        bool UpdateJobs()
        {
            var nav = GameInput.Nav;
            var jobs = S.Db.jobs;
            var job = jobs[Mathf.Clamp(jobIndex, 0, jobs.Count - 1)];
            var m = Member;

            switch (jobsMode)
            {
                case JobsMode.JobList:
                    if (nav.x != 0) { member = Wrap(member + nav.x, S.Party.Count); Cursor(); return true; }
                    if (nav.y != 0) { jobIndex = Wrap(jobIndex - nav.y, jobs.Count); Cursor(); return true; }
                    if (GameInput.ConfirmDown)
                    {
                        if (m.IsJobUnlocked(job))
                        {
                            Ok();
                            jobsMode = JobsMode.JobActions;
                            actionIndex = 0;
                        }
                        else
                        {
                            Denied();
                            Notice($"{job.displayName} bloqueado: {RequirementText(job)}", "bad");
                        }
                        return true;
                    }
                    return false;

                case JobsMode.JobActions:
                {
                    var actions = JobActions(job);
                    if (nav.y != 0) { actionIndex = Wrap(actionIndex - nav.y, actions.Count); Cursor(); return true; }
                    if (GameInput.CancelDown) { jobsMode = JobsMode.JobList; AudioManager.Play(Sfx.Cancel); return true; }
                    if (GameInput.ConfirmDown)
                    {
                        var (_, enabled, action) = actions[Mathf.Clamp(actionIndex, 0, actions.Count - 1)];
                        if (enabled)
                        {
                            Ok();
                            action();
                        }
                        else Denied();
                        return true;
                    }
                    return false;
                }

                case JobsMode.Abilities:
                {
                    var abilities = job.abilities;
                    if (abilities.Count == 0) { jobsMode = JobsMode.JobList; return true; }
                    if (nav.y != 0) { abilityIndex = Wrap(abilityIndex - nav.y, abilities.Count); Cursor(); return true; }
                    if (GameInput.CancelDown) { jobsMode = JobsMode.JobList; AudioManager.Play(Sfx.Cancel); return true; }
                    if (GameInput.ConfirmDown)
                    {
                        var ability = abilities[Mathf.Clamp(abilityIndex, 0, abilities.Count - 1)];
                        var progress = m.ProgressFor(job);
                        if (progress.IsLearned(ability))
                        {
                            if (ability.IsPassive)
                            {
                                bool equip = m.SupportPassive != ability;
                                m.SetSupportPassive(equip ? ability : null);
                                Ok();
                                Notice(equip ? $"Suporte: {ability.displayName}" : "Suporte removido", "good");
                            }
                            else
                            {
                                Denied();
                                Notice($"{ability.displayName} já foi aprendida.", "muted");
                            }
                        }
                        else if (progress.TryLearn(ability))
                        {
                            AudioManager.Play(Sfx.Buff);
                            Notice($"{m.Name} aprendeu {ability.displayName}!", "good");
                            PlaytestLog.Note("aprendeu", $"{m.Name}: {ability.displayName}");
                        }
                        else
                        {
                            Denied();
                            Notice($"JP insuficiente ({progress.AvailableJp}/{ability.jpCost}).", "bad");
                        }
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        bool UpdateItems()
        {
            var nav = GameInput.Nav;
            var list = S.Inventory.Consumables().ToList();
            if (list.Count == 0)
            {
                pickingTarget = false;
                return false;
            }
            consumableIndex = Mathf.Clamp(consumableIndex, 0, list.Count - 1);
            var item = list[consumableIndex].item;

            if (!pickingTarget)
            {
                if (nav.y != 0) { consumableIndex = Wrap(consumableIndex - nav.y, list.Count); Cursor(); return true; }
                if (!GameInput.ConfirmDown) return false;
                if (!FieldItems.NeedsTarget(item))
                {
                    UseItem(item, null);
                    return true;
                }
                if (!FieldItems.AnyUseful(S, item))
                {
                    Denied();
                    Notice(FieldItems.Use(S, item, S.Party.FirstOrDefault(m => !m.IsAlive) ?? S.Party[0]).Message, "bad");
                    return true;
                }
                Ok();
                pickingTarget = true;
                int useful = S.Party.FindIndex(m => FieldItems.CanUse(S, item, m));
                targetIndex = useful >= 0 ? useful : 0;
                return true;
            }

            if (GameInput.CancelDown) { pickingTarget = false; AudioManager.Play(Sfx.Cancel); return true; }
            int step = nav.x != 0 ? nav.x : -nav.y;
            if (step != 0) { targetIndex = Wrap(targetIndex + step, S.Party.Count); Cursor(); return true; }
            if (!GameInput.ConfirmDown) return false;
            UseItem(item, S.Party[Mathf.Clamp(targetIndex, 0, S.Party.Count - 1)]);
            if (S.Inventory.Count(item) == 0 || !FieldItems.AnyUseful(S, item)) pickingTarget = false;
            return true;
        }

        void UseItem(ConsumableDefinition item, PartyMember target)
        {
            var result = FieldItems.Use(S, item, target);
            if (!result.Used)
            {
                Denied();
                Notice(result.Message, "bad");
                return;
            }
            AudioManager.Play(item.effect == ConsumableEffect.RestoreEnergy ? Sfx.Buff : Sfx.Heal);
            Notice(result.Message, "good");
            PlaytestLog.Note("item_mapa", $"{item.id} -> {target?.Name ?? "party"}");
        }

        bool UpdateOptions()
        {
            var nav = GameInput.Nav;
            int count = System.Enum.GetValues(typeof(Option)).Length;
            if (nav.y != 0) { optionIndex = Wrap(optionIndex - nav.y, count); Cursor(); return true; }
            var option = (Option)optionIndex;
            int delta = nav.x != 0 ? nav.x : GameInput.ConfirmDown ? 1 : 0;
            if (delta == 0) return false;
            switch (option)
            {
                case Option.Music:
                    GameSettings.MusicVolume = GameSettings.FromSteps(GameSettings.ToSteps(GameSettings.MusicVolume) + delta);
                    break;
                case Option.Sfx:
                    GameSettings.SfxVolume = GameSettings.FromSteps(GameSettings.ToSteps(GameSettings.SfxVolume) + delta);
                    break;
                case Option.Ring:
                    GameSettings.TimingRing = !GameSettings.TimingRing;
                    break;
                case Option.Milliseconds:
                    TimingFeedback.ShowMilliseconds = !TimingFeedback.ShowMilliseconds;
                    break;
            }
            // O som já sai no volume novo: dá para ouvir o ajuste.
            AudioManager.Play(option == Option.Sfx ? Sfx.Coin : Sfx.Cursor);
            return true;
        }

        // ======================================================================= render

        void Render()
        {
            tabsRow.Clear();
            for (int i = 0; i < TabNames.Length; i++)
                UiKit.Text(tabsRow, TabNames[i], "tab").EnableInClassList("selected", i == (int)tab);
            info.text = $"Ouro {S.Inventory.Gold}    PE {S.Energy}/{S.MaxEnergy}";

            body.Clear();
            switch (tab)
            {
                case Tab.Party:
                    RenderParty();
                    footer.text = "Q/E: trocar aba    Esc/Tab: fechar";
                    break;
                case Tab.Inventory:
                    RenderInventory();
                    footer.text = "↑↓: item    ←→: personagem    Enter/Espaço: equipar    F: vender    Q/E: aba    Esc: fechar";
                    break;
                case Tab.Items:
                    RenderItems();
                    footer.text = pickingTarget
                        ? "←→/↑↓: escolher aliado    Enter/Espaço: usar    Esc: voltar"
                        : "↑↓: item    Enter/Espaço: usar    Q/E: aba    Esc: fechar";
                    break;
                case Tab.Options:
                    RenderOptions();
                    footer.text = "↑↓: opção    ←→: ajustar    Enter/Espaço: alternar    Q/E: aba    Esc: fechar";
                    break;
                case Tab.Jobs:
                    RenderJobs();
                    footer.text = jobsMode switch
                    {
                        JobsMode.JobList => "↑↓: job    ←→: personagem    Enter/Espaço: opções    Q/E: aba    Esc: fechar",
                        JobsMode.JobActions => "↑↓: opção    Enter/Espaço: confirmar    Esc: voltar",
                        _ => "↑↓: habilidade    Enter/Espaço: aprender / equipar passiva    Esc: voltar",
                    };
                    break;
            }
        }

        void RenderParty()
        {
            foreach (var m in S.Party)
            {
                var card = UiKit.El(body, "panel", "member-card");
                var header = UiKit.El(card, "row");
                UiKit.Text(header, m.Name, "big", "grow");
                UiKit.Text(header, $"Nv {m.Level}", "big");

                UiKit.Text(card, $"PV {m.CurrentHp}/{m.MaxHp}", "small");
                UiKit.SetFill(UiKit.Bar(card), m.MaxHp > 0 ? (float)m.CurrentHp / m.MaxHp : 0f);
                UiKit.Text(card, $"XP {m.Xp}/{m.XpToNext}", "small", "muted");
                UiKit.SetFill(UiKit.Bar(card, "bar-xp"), (float)m.Xp / Mathf.Max(1, m.XpToNext));

                var progress = m.ProgressFor(m.Job);
                UiKit.Text(card, $"Job: {m.Job.displayName} Nv {m.JobLevel(m.Job)}  (JP {progress.AvailableJp})", "small");
                UiKit.Text(card, $"Secundária: {(m.SecondaryJob != null ? m.SecondaryJob.skillsetName : "—")}", "small", "muted");
                UiKit.Text(card, $"Suporte: {(m.SupportPassive != null ? m.SupportPassive.displayName : "—")}", "small", "muted");

                UiKit.Text(card, "Atributos", "section-title");
                var stats = m.ComputeStats();
                foreach (var stat in MainStats)
                    StatLine(card, StatText.ShortName(stat), Shown(stats[stat]) + (stat == StatType.CritChance ? "%" : ""));
                foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
                {
                    if (StatText.IsPrimary(stat) || stat == StatType.CritChance || Mathf.Abs(stats[stat]) < 0.01f) continue;
                    StatLine(card, StatText.ShortName(stat), StatText.Describe(new StatModifier(stat, stats[stat])), "good");
                }
                EquipmentSection(card, m);
            }
        }

        void RenderInventory()
        {
            var items = S.Inventory.Sorted();
            itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, items.Count - 1));
            var m = Member;

            var listPanel = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(listPanel, $"Mochila ({S.Inventory.Items.Count}/{S.Inventory.Capacity})", "section-title");
            if (items.Count == 0) UiKit.Text(listPanel, "Vazia. Vá abrir uns baús!", "muted");

            ItemViews.ItemList(listPanel, items, itemIndex, m, VisibleRows);

            var consumables = S.Inventory.Consumables().ToList();
            if (consumables.Count > 0)
            {
                UiKit.Text(listPanel, "Consumíveis", "section-title");
                UiKit.Text(listPanel, string.Join("   ", consumables.Select(c => $"{c.item.displayName} ×{c.count}")), "small");
            }

            var detail = UiKit.El(body, "panel", "column");
            MemberHeader(detail, m);
            if (items.Count > 0)
            {
                ItemViews.ItemDetail(detail, items[itemIndex], m);
            }
            EquipmentSection(detail, m);
        }

        void RenderItems()
        {
            var list = S.Inventory.Consumables().ToList();
            consumableIndex = Mathf.Clamp(consumableIndex, 0, Mathf.Max(0, list.Count - 1));

            var left = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(left, "Consumíveis", "section-title");
            if (list.Count == 0) UiKit.Text(left, "Nenhum consumível. A mercearia da vila vende Poções e Éteres.", "muted");
            for (int i = 0; i < list.Count; i++)
            {
                var (item, count) = list[i];
                Row(left, item.displayName, $"×{count}", i == consumableIndex && !pickingTarget, FieldItems.AnyUseful(S, item), icon: ItemIcon.For(item, 30f));
            }
            var current = list.Count > 0 ? list[consumableIndex].item : null;
            if (current != null && !string.IsNullOrEmpty(current.description))
                UiKit.Text(left, current.description, "panel-light", "small");

            var right = UiKit.El(body, "panel", "column");
            UiKit.Text(right, pickingTarget ? "Usar em quem?" : "Party", "section-title");
            UiKit.Text(right, $"PE da party {S.Energy}/{S.MaxEnergy}", "energy-text");
            UiKit.SetFill(UiKit.Bar(right, "bar-xp"), S.MaxEnergy > 0 ? (float)S.Energy / S.MaxEnergy : 0f);
            for (int i = 0; i < S.Party.Count; i++)
            {
                var m = S.Party[i];
                bool canUse = current == null || !FieldItems.NeedsTarget(current) || FieldItems.CanUse(S, current, m);
                var row = Row(right, $"{m.Name}  ·  {m.Job.displayName} Nv {m.Level}", m.IsAlive ? $"PV {m.CurrentHp}/{m.MaxHp}" : "Nocaute",
                    pickingTarget && i == targetIndex, !pickingTarget || canUse);
                row.style.marginTop = 8;
                UiKit.SetFill(UiKit.Bar(right), m.MaxHp > 0 ? (float)m.CurrentHp / m.MaxHp : 0f);
            }
        }

        static string VolumeBar(float volume)
        {
            int steps = GameSettings.ToSteps(volume);
            return new string('■', steps) + new string('□', 10 - steps) + $"  {steps * 10}%";
        }

        static string OnOff(bool value) => value ? "Ligado" : "Desligado";

        void RenderOptions()
        {
            var left = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(left, "Opções", "section-title");
            var rows = new (string name, string value, string help)[]
            {
                ("Música", VolumeBar(GameSettings.MusicVolume), "Volume das músicas."),
                ("Efeitos sonoros", VolumeBar(GameSettings.SfxVolume), "Volume dos efeitos (golpes, menus, baús)."),
                ("Anel de timing", OnOff(GameSettings.TimingRing),
                    "Um anel fecha sobre o alvo no instante exato do impacto: aperte quando ele encostar no círculo. Ótimo para aprender o ritmo."),
                ("Mostrar ms do timing", OnOff(TimingFeedback.ShowMilliseconds),
                    "Mostra quantos milissegundos cedo (−) ou tarde (+) foi cada aperto. Útil para calibrar."),
            };
            optionIndex = Mathf.Clamp(optionIndex, 0, rows.Length - 1);
            for (int i = 0; i < rows.Length; i++) Row(left, rows[i].name, rows[i].value, i == optionIndex);
            UiKit.Text(left, rows[optionIndex].help, "panel-light", "small");

            var right = UiKit.El(body, "panel", "column");
            UiKit.Text(right, "Dicas de timing", "section-title");
            UiKit.Text(right, "Ataque: aperte Confirmar quando o golpe acertar o inimigo.", "small");
            UiKit.Text(right, "Defesa: aperte Confirmar quando o golpe inimigo chegar em você.", "small");
            UiKit.Text(right, "Investida (Guardião): segure Confirmar e solte quando a barra brilhar.", "small");
            UiKit.Text(right, "PERFEITO: +50% de dano (ou −75% de dano recebido). BOM: +25% (ou −50%).", "small", "muted");
        }

        string RequirementText(JobDefinition job) =>
            job.requirements.Count == 0 ? "—" : string.Join(", ", job.requirements.Select(r => $"{r.job.displayName} Nv {r.level}"));

        void RenderJobs()
        {
            var m = Member;
            var jobs = S.Db.jobs;
            jobIndex = Mathf.Clamp(jobIndex, 0, jobs.Count - 1);
            var job = jobs[jobIndex];

            var left = UiKit.El(body, "panel", "column-narrow");
            MemberHeader(left, m);
            UiKit.Text(left, $"Secundária: {(m.SecondaryJob != null ? m.SecondaryJob.skillsetName : "—")}    Suporte: {(m.SupportPassive != null ? m.SupportPassive.displayName : "—")}", "small", "muted");
            UiKit.Text(left, "Jobs", "section-title");
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                bool unlocked = m.IsJobUnlocked(j);
                string tag = j == m.Job ? " (principal)" : j == m.SecondaryJob ? " (secundária)" : "";
                Row(left, j.displayName + tag, unlocked ? $"Nv {m.JobLevel(j)}" : "bloqueado", i == jobIndex && jobsMode == JobsMode.JobList, unlocked);
            }

            var right = UiKit.El(body, "panel", "column");
            UiKit.Text(right, job.displayName, "big");
            UiKit.Text(right, job.description, "small", "muted");

            var progress = m.ProgressFor(job);
            int next = S.Balance.JpForNextJobLevel(progress.TotalJp);
            UiKit.Text(right, $"Nv {m.JobLevel(job)}   ·   JP disponível {progress.AvailableJp}   ·   " +
                              (next > 0 ? $"próximo nível com {next} JP total" : "nível máximo"), "small", "energy-text");
            if (!m.IsJobUnlocked(job)) UiKit.Text(right, $"Requer: {RequirementText(job)}", "small", "bad");

            var mult = job.statMultipliers;
            UiKit.Text(right, $"Atributos: PV×{mult.hp:0.##} ATQ×{mult.attack:0.##} DEF×{mult.defense:0.##} MAG×{mult.magic:0.##} RES×{mult.resistance:0.##} VEL×{mult.speed:0.##}", "small");
            UiKit.Text(right, "Usa: " + string.Join(", ", job.allowedCategories.Select(RarityInfo.CategoryName)), "small", "muted");

            if (jobsMode == JobsMode.JobActions)
            {
                UiKit.Text(right, "Opções", "section-title");
                var actions = JobActions(job);
                actionIndex = Mathf.Clamp(actionIndex, 0, actions.Count - 1);
                for (int i = 0; i < actions.Count; i++)
                    Row(right, actions[i].text, null, i == actionIndex, actions[i].enabled);
            }

            UiKit.Text(right, $"Habilidades — {job.skillsetName}", "section-title");
            AbilityDefinition selected = null;
            for (int i = 0; i < job.abilities.Count; i++)
            {
                var a = job.abilities[i];
                bool learned = progress.IsLearned(a);
                bool isSelected = jobsMode == JobsMode.Abilities && i == abilityIndex;
                if (isSelected) selected = a;
                string kind = a.IsPassive ? (m.SupportPassive == a ? "passiva · EQUIPADA" : "passiva") : (a.energyCost > 0 ? $"{a.energyCost} PE" : "");
                string right2 = learned ? $"aprendida   {kind}" : $"{a.jpCost} JP   {kind}";
                Row(right, a.displayName, right2, isSelected, learned || progress.AvailableJp >= a.jpCost, learned ? "good" : null);
            }
            if (selected != null && !string.IsNullOrEmpty(selected.description))
                UiKit.Text(right, selected.description, "panel-light", "small");
        }
    }
}
