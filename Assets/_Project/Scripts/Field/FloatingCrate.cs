using System;
using Oiram.Audio;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Caixa flutuante: bata de baixo para cima com um pulo para soltar loot.</summary>
    public sealed class FloatingCrate : MonoBehaviour, IHeadBumpable
    {
        public LootTable lootTable;
        public int level = 1;
        public string uniqueId;
        public Transform visual;

        bool used;

        void Start()
        {
            if (GameSession.Current.ClearedFieldObjects.Contains(uniqueId)) MarkUsed();
        }

        void Update()
        {
            if (visual && !used) visual.localRotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
        }

        public void Bump(FieldPlayerController player)
        {
            if (used) return;
            MarkUsed();
            AudioManager.Play(Sfx.Bump);
            var session = GameSession.Current;
            session.ClearedFieldObjects.Add(uniqueId);
            _ = Tween.Arc(visual, visual.position, 0.4f, 0.2f, destroyCancellationToken);
            FieldDirector.Instance.GiveLoot(session.RollLoot(lootTable, level), transform.position);
        }

        void MarkUsed()
        {
            used = true;
            if (!visual) return;
            visual.localRotation = Quaternion.identity;
            foreach (var r in visual.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = Palette.Get(new Color(0.42f, 0.36f, 0.3f));
        }
    }
}
