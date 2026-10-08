using System.Collections.Generic;
using System.Text;
using Oiram.Audio;
using Oiram.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>HUD da exploração: party, PE, ouro, dica de interação e avisos de loot.</summary>
    public sealed class FieldHud : MonoBehaviour
    {
        VisualElement root, toastBox, promptBox, dialogueBox;
        Label partyLabel, energyLabel, goldLabel, prompt, helpLabel, dialogueSpeaker, dialogueText;
        int dialogueFrame;
        System.Action onDialogueClosed;

        public bool DialogueOpen { get; private set; }
        readonly List<(VisualElement element, float expires)> toasts = new();
        float nextRefresh;

        public static FieldHud Create(Transform parent)
        {
            var go = new GameObject("FieldHud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<FieldHud>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            root = UiKit.CreateDocument(transform, "FieldHudDocument", 0);

            var status = UiKit.El(root, "hud-top-left", "panel-light");
            partyLabel = UiKit.Text(status, "", "small");
            var resources = UiKit.El(status, "row");
            energyLabel = UiKit.Text(resources, "", "energy-text");
            goldLabel = UiKit.Text(resources, "", "gold-text");
            goldLabel.style.marginLeft = 24;

            var help = UiKit.El(root, "hud-top-right");
            helpLabel = UiKit.Text(help, "Mover: WASD/setas · Pular: Espaço · Interagir: E · Menu: Tab", "small", "panel-light");

            toastBox = UiKit.El(root, "toasts");
            promptBox = UiKit.El(root, "prompt");
            prompt = UiKit.Text(promptBox, "", "panel");
            UiKit.Show(promptBox, false);

            dialogueBox = UiKit.El(root, "dialogue", "panel");
            dialogueSpeaker = UiKit.Text(dialogueBox, "", "section-title");
            dialogueText = UiKit.Text(dialogueBox, "", "dialogue-text");
            UiKit.Text(dialogueBox, "Confirmar para continuar", "small", "muted");
            UiKit.Show(dialogueBox, false);
        }

        public void SetHelp(string text) => helpLabel.text = text;

        /// <summary>Caixa de fala; fecha com Confirmar/Cancelar/Interagir.</summary>
        public void ShowDialogue(string speaker, string text, System.Action onClosed = null)
        {
            dialogueSpeaker.text = speaker;
            dialogueText.text = text;
            onDialogueClosed = onClosed;
            dialogueFrame = Time.frameCount;
            DialogueOpen = true;
            AudioManager.Play(Sfx.Confirm, 0.7f);
            UiKit.Show(dialogueBox, true);
            UiKit.Show(promptBox, false);
        }

        void CloseDialogue()
        {
            DialogueOpen = false;
            UiKit.Show(dialogueBox, false);
            var callback = onDialogueClosed;
            onDialogueClosed = null;
            callback?.Invoke();
        }

        public void SetVisible(bool visible) => UiKit.Show(root, visible);

        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (show && prompt.text != text) prompt.text = text;
            UiKit.Show(promptBox, show);
        }

        public void Refresh()
        {
            var session = GameSession.Current;
            var sb = new StringBuilder();
            foreach (var m in session.Party)
                sb.AppendLine($"{m.Name}  Nv {m.Level}  ·  PV {m.CurrentHp}/{m.MaxHp}  ·  {m.Job.displayName}");
            SetText(partyLabel, sb.ToString().TrimEnd());
            SetText(energyLabel, $"PE {session.Energy}/{session.MaxEnergy}");
            SetText(goldLabel, $"Ouro {session.Inventory.Gold}");
        }

        static void SetText(Label label, string text)
        {
            if (label.text != text) label.text = text;
        }

        public void Toast(string text, string cssClass = null, float seconds = 4f)
        {
            var label = UiKit.Text(toastBox, text, "panel-light", "toast");
            if (!string.IsNullOrEmpty(cssClass)) label.AddToClassList(cssClass);
            toasts.Add((label, Time.unscaledTime + seconds));
            while (toasts.Count > 8)
            {
                toasts[0].element.RemoveFromHierarchy();
                toasts.RemoveAt(0);
            }
        }

        void Update()
        {
            if (DialogueOpen && Time.frameCount != dialogueFrame &&
                (GameInput.ConfirmDown || GameInput.CancelDown || GameInput.InteractDown))
            {
                AudioManager.Play(Sfx.Cursor);
                CloseDialogue();
            }

            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var (element, expires) = toasts[i];
                float left = expires - Time.unscaledTime;
                if (left <= 0f)
                {
                    element.RemoveFromHierarchy();
                    toasts.RemoveAt(i);
                }
                else if (left < 0.5f) element.style.opacity = left / 0.5f;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.25f;
                Refresh();
            }
        }
    }
}
