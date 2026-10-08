using System.Collections.Generic;
using System.Linq;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;
using UnityEngine.UIElements;
using static Oiram.UI.ItemViews;

namespace Oiram.UI
{
    /// <summary>
    /// Menu de pausa com três abas: Equipe (atributos), Inventário (equipar/vender com comparação)
    /// e Jobs (job principal, skillset secundário, aprender habilidades com JP, passiva de suporte).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        enum Tab { Party, Inventory, Jobs }
        enum JobsMode { JobList, JobActions, Abilities }

        static readonly string[] TabNames = { "Equipe", "Inventário", "Jobs" };
        static readonly StatType[] MainStats =
            { StatType.MaxHP, StatType.Attack, StatType.Defense, StatType.Magic, StatType.Resistance, StatType.Speed, StatType.CritChance };

        const int VisibleRows = 13;

        VisualElement panel, tabsRow, body;
        Label info, footer;
        FieldHud hud;

        Tab tab;
        JobsMode jobsMode;
        int member, itemIndex, jobIndex, actionIndex, abilityIndex;
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

            bool inSubMenu = tab == Tab.Jobs && jobsMode != JobsMode.JobList;
            if (GameInput.MenuDown || (GameInput.CancelDown && !inSubMenu))
            {
                Close();
                return;
            }
            if (GameInput.PrevTabDown || GameInput.NextTabDown)
            {
                int step = GameInput.NextTabDown ? 1 : -1;
                tab = (Tab)(((int)tab + step + TabNames.Length) % TabNames.Length);
                jobsMode = JobsMode.JobList;
                Render();
                return;
            }

            bool changed = tab switch
            {
                Tab.Party => false,
                Tab.Inventory => UpdateInventory(),
                Tab.Jobs => UpdateJobs(),
                _ => false,
            };
            if (changed) Render();
        }

        static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;

        // ======================================================================= input

        bool UpdateInventory()
        {
            var nav = GameInput.Nav;
            if (nav.x != 0)
            {
                member = Wrap(member + nav.x, S.Party.Count);
                return true;
            }
            var items = S.Inventory.Sorted();
            if (items.Count == 0) return false;
            if (nav.y != 0)
            {
                itemIndex = Wrap(itemIndex - nav.y, items.Count);
                return true;
            }

            var item = items[Mathf.Clamp(itemIndex, 0, items.Count - 1)];
            if (GameInput.ConfirmDown)
            {
                if (S.Inventory.Equip(Member, item))
                {
                    hud.Toast($"{Member.Name} equipou {item.Name}", RarityInfo.UssClass(item.Rarity));
                    PlaytestLog.Note("equipou", $"{Member.Name}: {item.Name} [{item.Rarity}]");
                }
                else
                    hud.Toast($"{Member.Job.displayName} não pode usar {RarityInfo.CategoryName(item.Category)}.", "bad");
                itemIndex = Mathf.Clamp(itemIndex, 0, Mathf.Max(0, S.Inventory.Items.Count - 1));
                return true;
            }
            if (GameInput.SecondaryDown)
            {
                int gold = S.Inventory.Sell(item);
                PlaytestLog.Note("vendeu", $"{item.Name} [{item.Rarity}] por {gold}");
                hud.Toast($"Vendeu {item.Name} por {gold} ouro", "gold-text");
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
                    hud.Toast($"{m.Name} agora é {job.displayName}!", "good");
                    jobsMode = JobsMode.JobList;
                }),
                ("Usar como habilidade secundária", job != m.Job && job != m.SecondaryJob, () =>
                {
                    m.SetSecondaryJob(job);
                    PlaytestLog.Note("secundaria", $"{m.Name}: {job.skillsetName}");
                    hud.Toast($"{m.Name}: secundária = {job.skillsetName}", "good");
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
                    if (nav.x != 0) { member = Wrap(member + nav.x, S.Party.Count); return true; }
                    if (nav.y != 0) { jobIndex = Wrap(jobIndex - nav.y, jobs.Count); return true; }
                    if (GameInput.ConfirmDown)
                    {
                        if (m.IsJobUnlocked(job))
                        {
                            jobsMode = JobsMode.JobActions;
                            actionIndex = 0;
                        }
                        else hud.Toast($"{job.displayName} bloqueado: {RequirementText(job)}", "bad");
                        return true;
                    }
                    return false;

                case JobsMode.JobActions:
                {
                    var actions = JobActions(job);
                    if (nav.y != 0) { actionIndex = Wrap(actionIndex - nav.y, actions.Count); return true; }
                    if (GameInput.CancelDown) { jobsMode = JobsMode.JobList; return true; }
                    if (GameInput.ConfirmDown)
                    {
                        var (_, enabled, action) = actions[Mathf.Clamp(actionIndex, 0, actions.Count - 1)];
                        if (enabled) action();
                        return true;
                    }
                    return false;
                }

                case JobsMode.Abilities:
                {
                    var abilities = job.abilities;
                    if (abilities.Count == 0) { jobsMode = JobsMode.JobList; return true; }
                    if (nav.y != 0) { abilityIndex = Wrap(abilityIndex - nav.y, abilities.Count); return true; }
                    if (GameInput.CancelDown) { jobsMode = JobsMode.JobList; return true; }
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
                                hud.Toast(equip ? $"Suporte: {ability.displayName}" : "Suporte removido", "good");
                            }
                            else hud.Toast($"{ability.displayName} já foi aprendida.", "muted");
                        }
                        else if (progress.TryLearn(ability))
                        {
                            hud.Toast($"{m.Name} aprendeu {ability.displayName}!", "good");
                            PlaytestLog.Note("aprendeu", $"{m.Name}: {ability.displayName}");
                        }
                        else
                            hud.Toast($"JP insuficiente ({progress.AvailableJp}/{ability.jpCost}).", "bad");
                        return true;
                    }
                    return false;
                }
            }
            return false;
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
