using Oiram.Core;
using Oiram.World;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Saída do campo/cidade para o mapa-múndi. Pode exigir um objeto derrotado antes (ex.: o Golem).</summary>
    public sealed class ExitPoint : FieldInteractable
    {
        [Tooltip("Id de um objeto do mapa que precisa estar derrotado/aberto. Vazio = sempre liberado.")]
        public string requiredClearedId;
        public string blockedMessage = "Algo bloqueia a passagem.";

        public override string Prompt => "Ir para o mapa-múndi";

        bool Open => string.IsNullOrEmpty(requiredClearedId) || GameSession.Current.ClearedFieldObjects.Contains(requiredClearedId);

        public override void Interact(FieldPlayerController player)
        {
            if (!Open)
            {
                FieldDirector.Instance.ShowToast(blockedMessage, "bad");
                return;
            }
            SceneFlow.ToWorldMap();
        }
    }
}
