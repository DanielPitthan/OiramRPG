using System.Collections.Generic;
using System.Threading;
using Oiram.Audio;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>Cria documentos de UI Toolkit em runtime com PanelSettings e estilos compartilhados.</summary>
    public static class UiKit
    {
        static PanelSettings panelSettings;
        static StyleSheet styles;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            panelSettings = null;
            styles = null;
        }

        public static PanelSettings Panel
        {
            get
            {
                if (panelSettings != null) return panelSettings;
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "OiramPanelSettings";
                panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/OiramTheme");
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                return panelSettings;
            }
        }

        public static StyleSheet Styles => styles != null ? styles : styles = Resources.Load<StyleSheet>("UI/OiramStyles");

        /// <summary>Cria um UIDocument filho de <paramref name="parent"/> e devolve a raiz já estilizada.</summary>
        public static VisualElement CreateDocument(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            if (parent != null) go.transform.SetParent(parent, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = Panel;
            doc.sortingOrder = sortingOrder;
            go.SetActive(true);

            var root = doc.rootVisualElement;
            if (Styles != null) root.styleSheets.Add(Styles);
            root.AddToClassList("oiram-root");
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            return root;
        }

        public static VisualElement El(VisualElement parent, params string[] classes)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var c in classes) el.AddToClassList(c);
            parent?.Add(el);
            return el;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("text");
            foreach (var c in classes) label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        public static void Show(VisualElement el, bool visible) => el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public static void SetRarity(VisualElement el, Rarity rarity)
        {
            foreach (Rarity r in System.Enum.GetValues(typeof(Rarity))) el.RemoveFromClassList(RarityInfo.UssClass(r));
            el.AddToClassList(RarityInfo.UssClass(rarity));
        }

        /// <summary>Barra de PV/progresso: devolve o preenchimento para atualizar a largura.</summary>
        public static VisualElement Bar(VisualElement parent, string fillClass = "bar-fill")
        {
            var bar = El(parent, "bar");
            return El(bar, fillClass);
        }

        public static void SetFill(VisualElement fill, float percent01) =>
            fill.style.width = Length.Percent(Mathf.Clamp01(percent01) * 100f);
    }

    public struct MenuEntry
    {
        public string Text;
        public string Right;
        public bool Enabled;
        public string CssClass;
        public string Description;

        public MenuEntry(string text, bool enabled = true, string right = null, string cssClass = null, string description = null)
        {
            Text = text;
            Enabled = enabled;
            Right = right;
            CssClass = cssClass;
            Description = description;
        }
    }

    /// <summary>Lista navegável com teclado/gamepad (sem depender do EventSystem).</summary>
    public sealed class MenuList
    {
        readonly List<VisualElement> rows = new();
        readonly List<MenuEntry> entries = new();
        readonly ScrollView scroll;

        public VisualElement Root { get; }
        public int Index { get; private set; }
        public int Count => entries.Count;
        public System.Action<int> SelectionChanged;

        public MenuList(VisualElement parent, params string[] classes)
        {
            Root = UiKit.El(parent, classes);
            Root.AddToClassList("menu");
            scroll = new ScrollView(ScrollViewMode.Vertical) { pickingMode = PickingMode.Ignore };
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("menu-scroll");
            Root.Add(scroll);
        }

        public void SetItems(IEnumerable<MenuEntry> items, int startIndex = 0)
        {
            entries.Clear();
            entries.AddRange(items);
            rows.Clear();
            scroll.Clear();
            foreach (var entry in entries)
            {
                var row = UiKit.El(scroll.contentContainer, "menu-item");
                if (!entry.Enabled) row.AddToClassList("disabled");
                var left = UiKit.Text(row, entry.Text, "menu-text");
                if (!string.IsNullOrEmpty(entry.CssClass)) left.AddToClassList(entry.CssClass);
                if (!string.IsNullOrEmpty(entry.Right)) UiKit.Text(row, entry.Right, "menu-right");
                rows.Add(row);
            }
            Select(Mathf.Clamp(startIndex, 0, Mathf.Max(0, entries.Count - 1)), notify: true);
        }

        public MenuEntry Current => entries.Count > 0 ? entries[Index] : default;

        public void Select(int index, bool notify = true)
        {
            if (entries.Count == 0) { Index = 0; return; }
            Index = (index % entries.Count + entries.Count) % entries.Count;
            for (int i = 0; i < rows.Count; i++) rows[i].EnableInClassList("selected", i == Index);
            if (rows.Count > 0) scroll.ScrollTo(rows[Index]);
            if (notify) SelectionChanged?.Invoke(Index);
        }

        /// <summary>Processa a navegação vertical deste frame. Devolve true se a seleção mudou.</summary>
        public bool HandleNavigation()
        {
            var nav = GameInput.Nav;
            if (nav.y == 0 || entries.Count == 0) return false;
            Select(Index - nav.y);
            AudioManager.Play(Sfx.Cursor);
            return true;
        }

        /// <summary>Espera o jogador escolher. Devolve o índice ou -1 ao cancelar (se permitido).</summary>
        public async Awaitable<int> Choose(CancellationToken ct, bool allowCancel = true)
        {
            // Um frame de folga para o mesmo aperto não confirmar dois menus seguidos.
            await Awaitable.NextFrameAsync(ct);
            while (true)
            {
                HandleNavigation();
                if (GameInput.ConfirmDown && entries.Count > 0)
                {
                    if (entries[Index].Enabled)
                    {
                        AudioManager.Play(Sfx.Confirm);
                        return Index;
                    }
                    AudioManager.Play(Sfx.Cancel, 0.8f, 0.8f);
                }
                if (allowCancel && GameInput.CancelDown)
                {
                    AudioManager.Play(Sfx.Cancel);
                    return -1;
                }
                await Awaitable.NextFrameAsync(ct);
            }
        }
    }

    /// <summary>Tela preta para transições entre cenas/batalhas.</summary>
    public static class ScreenFader
    {
        static VisualElement overlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            overlay = null;
            opacity = 0f;
        }

        static VisualElement Overlay
        {
            get
            {
                if (overlay != null && overlay.panel != null) return overlay;
                var holder = new GameObject("ScreenFader");
                Object.DontDestroyOnLoad(holder);
                var root = UiKit.CreateDocument(holder.transform, "FaderDocument", 1000);
                overlay = UiKit.El(root, "fader");
                overlay.style.opacity = 0f;
                return overlay;
            }
        }

        public static Awaitable FadeOut(float duration, CancellationToken ct = default) => FadeTo(1f, duration, ct);
        public static Awaitable FadeIn(float duration, CancellationToken ct = default) => FadeTo(0f, duration, ct);

        static float opacity;

        static async Awaitable FadeTo(float target, float duration, CancellationToken ct)
        {
            var el = Overlay;
            float from = opacity;
            await Tween.Run(duration, t =>
            {
                opacity = Mathf.Lerp(from, target, t);
                el.style.opacity = opacity;
            }, ct, unscaled: true);
            opacity = target;
            el.style.opacity = target;
        }
    }
}
