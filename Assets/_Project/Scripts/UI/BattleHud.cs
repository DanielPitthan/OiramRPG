using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>HUD da batalha: ordem de turnos, party, PE, menus de comando, popups e telas de resultado.</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        sealed class PartyCard
        {
            public VisualElement Root;
            public Label Name, Job, Hp, Status;
            public VisualElement HpFill;
        }

        sealed class EnemyTag
        {
            public VisualElement Root;
            public VisualElement HpFill;
            public Label Status;
        }

        sealed class ActivePopup
        {
            public Label Label;
            public Vector3 World;
            public float Born;
            public float Life;
            public float Rise;
        }

        VisualElement root, turnStrip, partyPanel, commandPanel, popupLayer, tagLayer, chargeBox, chargeFill, overlay;
        Label banner, targetInfo, energyLabel, commandTitle, description;
        Camera cam;
        GameSession session;
        Func<BattleUnit, BattleUnitView> viewOf;
        readonly Dictionary<BattleUnit, PartyCard> cards = new();
        readonly Dictionary<BattleUnit, EnemyTag> tags = new();
        readonly List<ActivePopup> popups = new();
        int bannerToken;

        public MenuList Menu { get; private set; }

        public static BattleHud Create(Transform parent, Camera camera)
        {
            var go = new GameObject("BattleHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<BattleHud>();
            hud.cam = camera;
            hud.Build();
            return hud;
        }

        void Build()
        {
            root = UiKit.CreateDocument(transform, "BattleHudDocument", 20);

            tagLayer = UiKit.El(root);
            tagLayer.style.position = Position.Absolute;
            tagLayer.style.left = 0; tagLayer.style.top = 0; tagLayer.style.right = 0; tagLayer.style.bottom = 0;

            turnStrip = UiKit.El(root, "turn-strip");
            banner = UiKit.Text(root, "", "banner");
            targetInfo = UiKit.Text(root, "", "target-info");

            partyPanel = UiKit.El(root, "party-panel");
            var energyBox = UiKit.El(partyPanel, "panel", "energy-box");
            UiKit.Text(energyBox, "PE", "small", "energy-text");
            energyLabel = UiKit.Text(energyBox, "0/0", "big", "energy-text");

            commandPanel = UiKit.El(root, "command-panel");
            var menuBox = UiKit.El(commandPanel, "panel");
            commandTitle = UiKit.Text(menuBox, "", "section-title");
            Menu = new MenuList(menuBox);
            description = UiKit.Text(commandPanel, "", "panel-light", "description", "small");
            Menu.SelectionChanged = _ => SetDescription(Menu.Current.Description);
            ShowCommandMenu(null);

            chargeBox = UiKit.El(root, "charge");
            UiKit.Text(chargeBox, "Segure e solte quando a barra brilhar!", "small");
            chargeFill = UiKit.Bar(chargeBox, "bar-charge");
            UiKit.Show(chargeBox, false);

            popupLayer = UiKit.El(root);
            popupLayer.style.position = Position.Absolute;
            popupLayer.style.left = 0; popupLayer.style.top = 0; popupLayer.style.right = 0; popupLayer.style.bottom = 0;

            UiKit.Show(banner, false);
        }

        public void Bind(GameSession s, IEnumerable<BattleUnit> party, IEnumerable<BattleUnit> enemies, Func<BattleUnit, BattleUnitView> views)
        {
            session = s;
            viewOf = views;
            foreach (var unit in party)
            {
                var card = new PartyCard { Root = UiKit.El(partyPanel, "panel", "party-card") };
                var header = UiKit.El(card.Root, "row");
                card.Name = UiKit.Text(header, unit.Name, "big", "grow");
                card.Job = UiKit.Text(header, "", "small", "muted");
                card.HpFill = UiKit.Bar(card.Root);
                card.Hp = UiKit.Text(card.Root, "", "small");
                card.Status = UiKit.Text(card.Root, "", "small", "muted");
                cards[unit] = card;
            }
            foreach (var unit in enemies)
            {
                var tag = new EnemyTag { Root = UiKit.El(tagLayer, "enemy-tag") };
                UiKit.Text(tag.Root, unit.Name, "small");
                tag.HpFill = UiKit.Bar(tag.Root);
                tag.Status = UiKit.Text(tag.Root, "", "small", "muted");
                tags[unit] = tag;
            }
            Refresh();
        }

        public void Refresh()
        {
            foreach (var (unit, card) in cards)
            {
                card.Job.text = unit.Member.Job.displayName;
                card.Hp.text = $"PV {unit.Hp}/{unit.MaxHp}";
                UiKit.SetFill(card.HpFill, unit.HpPercent);
                card.HpFill.EnableInClassList("low", unit.HpPercent < 0.3f);
                card.Root.EnableInClassList("fallen", !unit.IsAlive);
                card.Status.text = StatusLine(unit);
            }
            foreach (var (unit, tag) in tags)
            {
                UiKit.SetFill(tag.HpFill, unit.HpPercent);
                tag.Status.text = StatusLine(unit);
                UiKit.Show(tag.Root, unit.IsAlive);
            }
            if (session != null) energyLabel.text = $"{session.Energy}/{session.MaxEnergy}";
        }

        static string StatusLine(BattleUnit unit) =>
            unit.IsAlive ? string.Join(" ", unit.Statuses.Where(s => s.Type != StatusType.Defending).Select(s => StatusText.Name(s.Type))) : "Nocaute";

        public void SetActive(BattleUnit active)
        {
            foreach (var (unit, card) in cards) card.Root.EnableInClassList("active", unit == active);
        }

        public void SetTurnOrder(IReadOnlyList<BattleUnit> order, int current)
        {
            turnStrip.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                var unit = order[i];
                if (!unit.IsAlive && i != current) continue;
                var chip = UiKit.Text(turnStrip, unit.Name, "turn-chip", unit.Side == Side.Party ? "party" : "enemy");
                chip.EnableInClassList("current", i == current);
                chip.EnableInClassList("done", i < current);
            }
        }

        // ---------- Mensagens ----------

        public void ShowBanner(string text, float seconds = 1.2f)
        {
            banner.text = text;
            UiKit.Show(banner, !string.IsNullOrEmpty(text));
            int token = ++bannerToken;
            if (seconds > 0f) _ = HideBannerLater(token, seconds);
        }

        async Awaitable HideBannerLater(int token, float seconds)
        {
            try { await Tween.Delay(seconds, destroyCancellationToken); }
            catch (OperationCanceledException) { return; }
            if (token == bannerToken) UiKit.Show(banner, false);
        }

        public async Awaitable Banner(string text, float seconds, CancellationToken ct)
        {
            ShowBanner(text, seconds);
            await Tween.Delay(seconds, ct);
        }

        public void ShowTargetInfo(string text)
        {
            targetInfo.text = text ?? "";
            UiKit.Show(targetInfo, !string.IsNullOrEmpty(text));
        }

        public void ShowCommandMenu(string actorName)
        {
            bool visible = actorName != null;
            UiKit.Show(commandPanel, visible);
            if (visible) commandTitle.text = actorName;
        }

        public void SetDescription(string text)
        {
            description.text = text ?? "";
            UiKit.Show(description, !string.IsNullOrEmpty(text));
        }

        public void ShowCharge(bool visible)
        {
            UiKit.Show(chargeBox, visible);
            if (visible) SetCharge(0f);
        }

        public void SetCharge(float charge01)
        {
            UiKit.SetFill(chargeFill, charge01);
            chargeFill.EnableInClassList("full", charge01 >= 0.999f);
        }

        // ---------- Popups no mundo ----------

        public void Popup(Vector3 world, string text, string cssClass, float life = 1.1f, float rise = 70f)
        {
            if (string.IsNullOrEmpty(text)) return;
            var label = UiKit.Text(popupLayer, text, "popup", cssClass);
            var popup = new ActivePopup { Label = label, World = world, Born = Time.time, Life = life, Rise = rise };
            popups.Add(popup);
            Place(label, world, 0f);
        }

        public void PopupRarity(Vector3 world, ItemInstance item)
        {
            var label = UiKit.Text(popupLayer, item.Name, "popup", "loot");
            UiKit.SetRarity(label, item.Rarity);
            popups.Add(new ActivePopup { Label = label, World = world, Born = Time.time, Life = 1.8f, Rise = 50f });
            Place(label, world, 0f);
        }

        void Place(VisualElement el, Vector3 world, float yOffset)
        {
            if (cam == null || root.panel == null) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, cam);
            el.style.left = p.x;
            el.style.top = p.y - yOffset;
        }

        void LateUpdate()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = (Time.time - p.Born) / p.Life;
                if (age >= 1f)
                {
                    p.Label.RemoveFromHierarchy();
                    popups.RemoveAt(i);
                    continue;
                }
                Place(p.Label, p.World, Tween.EaseOutQuad(Mathf.Min(1f, age * 2f)) * p.Rise + 30f);
                p.Label.style.opacity = age < 0.7f ? 1f : 1f - (age - 0.7f) / 0.3f;
            }

            if (viewOf == null) return;
            foreach (var (unit, tag) in tags)
            {
                var view = viewOf(unit);
                if (view != null && unit.IsAlive) Place(tag.Root, view.Top + Vector3.up * 0.15f, 40f);
            }
        }

        // ---------- Resultados ----------

        async Awaitable WaitConfirm(CancellationToken ct, bool waitForInput)
        {
            if (!waitForInput)
            {
                await Tween.Delay(0.5f, ct);
                return;
            }
            await Awaitable.NextFrameAsync(ct);
            while (!GameInput.ConfirmDown) await Awaitable.NextFrameAsync(ct);
        }

        public async Awaitable ShowVictory(VictorySummary summary, CancellationToken ct, bool waitForInput = true)
        {
            ShowCommandMenu(null);
            ShowTargetInfo(null);
            turnStrip.Clear();
            overlay = UiKit.El(root, "center-overlay");
            var panel = UiKit.El(overlay, "panel", "result-panel");
            UiKit.Text(panel, "VITÓRIA!", "huge", "gold-text").style.unityTextAlign = TextAnchor.MiddleCenter;

            var rewards = UiKit.El(panel, "row");
            UiKit.Text(rewards, $"+{summary.Xp} XP     ", "big");
            UiKit.Text(rewards, $"+{summary.Jp} JP     ", "big", "energy-text");
            UiKit.Text(rewards, $"+{summary.Gold} ouro", "big", "gold-text");

            foreach (var note in summary.Notes) UiKit.Text(panel, note, "good");

            if (summary.Items.Count > 0 || summary.Consumables.Count > 0)
            {
                UiKit.Text(panel, "Loot", "section-title");
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.maxHeight = 520;
                panel.Add(scroll);

                foreach (var item in summary.Items.OrderByDescending(i => i.Rarity))
                {
                    var line = UiKit.El(scroll.contentContainer, "loot-line");
                    var name = UiKit.Text(line, $"{item.Name}  —  {RarityInfo.Name(item.Rarity)} · Nv {item.ItemLevel} · {RarityInfo.SlotName(item.Slot)}");
                    UiKit.SetRarity(name, item.Rarity);
                    if (item.Affixes.Count > 0)
                        UiKit.Text(line, string.Join("  ·  ", item.Affixes.Select(a => StatText.Describe(a.Modifier))), "loot-affixes");
                    line.style.opacity = 0f;
                    await Tween.Run(0.12f, k => line.style.opacity = k, ct);
                }
                foreach (var (consumable, count) in summary.Consumables)
                    UiKit.Text(scroll.contentContainer, $"{consumable.displayName} ×{count}", "loot-line");
            }

            if (summary.LostItems > 0)
                UiKit.Text(panel, $"Mochila cheia: {summary.LostItems} item(ns) ficaram para trás.", "bad");

            UiKit.Text(panel, "Confirmar para continuar", "small", "muted").style.marginTop = 10;
            await WaitConfirm(ct, waitForInput);
            overlay.RemoveFromHierarchy();
        }

        public async Awaitable ShowDefeat(CancellationToken ct, bool waitForInput = true)
        {
            ShowCommandMenu(null);
            overlay = UiKit.El(root, "center-overlay");
            var panel = UiKit.El(overlay, "panel", "result-panel");
            UiKit.Text(panel, "DERROTA...", "huge", "bad").style.unityTextAlign = TextAnchor.MiddleCenter;
            UiKit.Text(panel, "A party desmaiou. Vocês acordam perto da fogueira, com tudo o que já tinham coletado.");
            UiKit.Text(panel, "Confirmar para continuar", "small", "muted").style.marginTop = 10;
            await WaitConfirm(ct, waitForInput);
            overlay.RemoveFromHierarchy();
        }
    }
}
