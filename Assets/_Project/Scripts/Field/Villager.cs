using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Morador com falas (dicas, rumores). Cada conversa mostra a próxima fala.</summary>
    public sealed class Villager : FieldInteractable
    {
        public string villagerName = "Morador";
        [TextArea] public string[] lines = { "Olá!" };

        int next;

        public override string Prompt => $"Falar com {villagerName}";

        public override void Interact(FieldPlayerController player)
        {
            var director = FieldDirector.Instance;
            if (director == null || lines == null || lines.Length == 0) return;
            var toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toPlayer);

            director.ModalOpen = true;
            director.Hud.ShowDialogue(villagerName, lines[next % lines.Length], () => director.ModalOpen = false);
            next++;
        }
    }
}
