using System;
using Oiram.Audio;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Fogueira: recupera PV e PE da party.</summary>
    public sealed class Campfire : FieldInteractable
    {
        public Transform flame;

        public override string Prompt => "Descansar na fogueira";

        void Update()
        {
            if (flame) flame.localScale = new Vector3(0.35f, 0.5f + Mathf.Sin(Time.time * 9f) * 0.06f, 0.35f);
        }

        public override void Interact(FieldPlayerController player)
        {
            GameSession.Current.RestoreAll();
            AudioManager.Play(Sfx.Heal);
            PlaytestLog.Note("descanso", "fogueira");
            FieldDirector.Instance.ShowToast("A party descansou: PV e PE recuperados!", "good");
        }
    }
}
