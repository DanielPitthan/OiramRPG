using System.Linq;
using Oiram.Battle;
using Oiram.Core;
using Oiram.Field;
using Oiram.Loot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oiram.DevTools
{
    /// <summary>
    /// Atalhos de teste (apenas no editor e em development builds):
    /// F1 = 10 itens aleatórios · F2 = +100 ouro · F3 = cura total · F4 = +100 JP · F5 = +1 nível ·
    /// F6 = mostrar o desvio (ms) de cada timed hit.
    /// </summary>
    public static class DevCheats
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!Debug.isDebugBuild) return; // editor e development builds
            var go = new GameObject("DevCheats");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                var kb = Keyboard.current;
                if (kb == null) return;
                var session = GameSession.Current;

                if (kb.f1Key.wasPressedThisFrame)
                {
                    int level = Mathf.RoundToInt((float)session.Party.Average(m => m.Level)) + 2;
                    var drop = new LootDrop();
                    for (int i = 0; i < 10; i++) drop.Items.Add(session.Loot.CreateRandom(level, 100f));
                    Give(drop, "F1: 10 itens gerados");
                }
                if (kb.f2Key.wasPressedThisFrame)
                {
                    session.Inventory.Gold += 100;
                    Toast("F2: +100 ouro");
                }
                if (kb.f3Key.wasPressedThisFrame)
                {
                    session.RestoreAll();
                    Toast("F3: party curada");
                }
                if (kb.f4Key.wasPressedThisFrame)
                {
                    foreach (var m in session.Party) m.ProgressFor(m.Job).AddJp(100);
                    Toast("F4: +100 JP no job atual");
                }
                if (kb.f6Key.wasPressedThisFrame)
                {
                    TimingFeedback.ShowMilliseconds = !TimingFeedback.ShowMilliseconds;
                    Toast(TimingFeedback.ShowMilliseconds ? "F6: timing em milissegundos LIGADO" : "F6: timing em milissegundos desligado");
                }
                if (kb.f5Key.wasPressedThisFrame)
                {
                    foreach (var m in session.Party) m.GainXp(m.XpToNext - m.Xp);
                    Toast("F5: +1 nível");
                }
            }

            static void Give(LootDrop drop, string message)
            {
                var director = FieldDirector.Instance;
                if (director != null)
                {
                    director.ShowToast(message, "muted");
                    director.GiveLoot(drop, director.Player.transform.position);
                }
                else
                {
                    GameSession.Current.Inventory.AddLoot(drop);
                    Debug.Log(message);
                }
            }

            static void Toast(string message)
            {
                if (FieldDirector.Instance != null) FieldDirector.Instance.ShowToast(message, "muted");
                else Debug.Log(message);
                GameSession.Current.NotifyChanged();
            }
        }
    }
}
