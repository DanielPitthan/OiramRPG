using System;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Baú com loot. Se tiver um encontro de Mímico, vira batalha ao abrir.</summary>
    public sealed class LootChest : FieldInteractable, IBattleSource
    {
        public LootTable lootTable;
        public int level = 1;
        public string uniqueId;
        [Tooltip("Se preenchido, o baú é um Mímico!")]
        public EncounterDefinition mimicEncounter;
        public Transform lid;

        bool opened;

        public override string Prompt => "Abrir baú";
        public override bool CanInteract => !opened;

        void Start()
        {
            if (GameSession.Current.ClearedFieldObjects.Contains(uniqueId)) SetOpened(destroyIfMimic: true);
        }

        public override void Interact(FieldPlayerController player)
        {
            if (opened) return;
            if (mimicEncounter != null)
            {
                _ = Tween.Shake(transform.GetChild(0), 0.4f, 0.1f, destroyCancellationToken);
                FieldDirector.Instance.ShowToast("É um Mímico!", "bad");
                FieldDirector.Instance.StartBattle(mimicEncounter, this, false);
                return;
            }

            SetOpened(false);
            var session = GameSession.Current;
            session.ClearedFieldObjects.Add(uniqueId);
            var drop = session.RollLoot(lootTable, level);
            FieldDirector.Instance.GiveLoot(drop, transform.position);
        }

        void SetOpened(bool destroyIfMimic)
        {
            opened = true;
            if (destroyIfMimic && mimicEncounter != null)
            {
                Destroy(gameObject);
                return;
            }
            if (lid) lid.localEulerAngles = new Vector3(-110f, 0f, 0f);
        }

        public void OnBattleEnded(BattleResult result)
        {
            if (result != BattleResult.Victory) return;
            GameSession.Current.ClearedFieldObjects.Add(uniqueId);
            Destroy(gameObject);
        }
    }
}
