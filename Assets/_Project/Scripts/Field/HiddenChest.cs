using Oiram.Core;
using Oiram.Loot;
using Oiram.World;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>
    /// Baú invisível flutuando no ar (estilo Mario RPG): aparece quando o jogador pula exatamente embaixo dele
    /// e entrega uma Relíquia. Um por cidade. Um brilho rápido de tempos em tempos denuncia o lugar.
    /// </summary>
    public sealed class HiddenChest : MonoBehaviour, IHeadBumpable
    {
        public string uniqueId;
        public Transform visual;
        public Transform glint;

        bool revealed;
        float nextGlint;

        void Start()
        {
            revealed = GameSession.Current.ClearedFieldObjects.Contains(uniqueId);
            SetVisible(revealed);
            if (glint) glint.gameObject.SetActive(false);
            nextGlint = Time.time + 2f;
        }

        void SetVisible(bool visible)
        {
            if (visual == null) return;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        void Update()
        {
            if (revealed || glint == null) return;
            if (Time.time >= nextGlint)
            {
                glint.gameObject.SetActive(true);
                if (Time.time >= nextGlint + 0.25f)
                {
                    glint.gameObject.SetActive(false);
                    nextGlint = Time.time + Random.Range(3.5f, 6f);
                }
            }
            glint.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
        }

        public void Bump(FieldPlayerController player)
        {
            if (revealed) return;
            revealed = true;
            SetVisible(true);
            if (glint) glint.gameObject.SetActive(false);
            _ = Tween.Arc(visual, visual.position, 0.4f, 0.2f, destroyCancellationToken);

            var relic = ShopService.OpenHiddenChest(GameSession.Current, uniqueId);
            var director = FieldDirector.Instance;
            if (relic == null || director == null) return;
            director.ShowToast("Um baú escondido apareceu do nada!", "gold-text");
            director.ShowToast($"{relic.Name} ({RarityInfo.Name(relic.Rarity)}, Nv {relic.ItemLevel})", RarityInfo.UssClass(relic.Rarity));
            _ = Shapes.LootBeam(transform.parent, transform.position, RarityInfo.Color(relic.Rarity), destroyCancellationToken);
            PlaytestLog.Note("reliquia", $"{uniqueId}: {relic.Name}");
            director.Hud.Refresh();
        }
    }
}
