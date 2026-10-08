using System.Collections.Generic;
using Oiram.Battle;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Algo no mapa que reage ao botão Interagir quando o jogador está perto.</summary>
    public abstract class FieldInteractable : MonoBehaviour
    {
        static readonly List<FieldInteractable> all = new();

        public float radius = 1.6f;

        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact(FieldPlayerController player);

        protected virtual void OnEnable() => all.Add(this);
        protected virtual void OnDisable() => all.Remove(this);

        public static FieldInteractable Nearest(Vector3 position)
        {
            FieldInteractable best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in all)
            {
                if (!candidate.CanInteract) continue;
                var delta = candidate.transform.position - position;
                if (Mathf.Abs(delta.y) > 1.5f) continue;
                delta.y = 0f;
                float d = delta.magnitude;
                if (d <= candidate.radius && d < bestDistance)
                {
                    best = candidate;
                    bestDistance = d;
                }
            }
            return best;
        }
    }

    /// <summary>Quem inicia uma batalha a partir do mapa e precisa saber o resultado.</summary>
    public interface IBattleSource
    {
        void OnBattleEnded(BattleResult result);
    }
}
