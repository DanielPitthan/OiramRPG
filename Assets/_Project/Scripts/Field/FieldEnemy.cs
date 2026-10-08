using Oiram.Audio;
using Oiram.Battle;
using Oiram.Core;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>
    /// Inimigo visível no mapa (sem encontros aleatórios). Vagueia, persegue quando vê o jogador e
    /// inicia a batalha ao encostar. Se o jogador cair em cima dele: ataque preventivo.
    /// </summary>
    public sealed class FieldEnemy : MonoBehaviour, IBattleSource
    {
        /// <summary>Congela a IA de todos os inimigos do mapa (ferramentas de teste).</summary>
        public static bool AiPaused { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AiPaused = false;

        /// <summary>Segundos de trégua depois que o mapa carrega.</summary>
        const float StartGrace = 2.5f;

        public EncounterDefinition encounter;
        public string uniqueId;
        public float wanderRadius = 2.5f;
        public float chaseRadius = 4.5f;
        public float wanderSpeed = 1.4f;
        public float chaseSpeed = 2.8f;
        public float bodyRadius = 0.5f;
        public float bodyHeight = 1f;
        public bool flying;
        public Transform visual;

        Vector3 home;
        Vector3 wanderTarget;
        float nextWanderTime;
        float invulnerableUntil;
        float bobPhase;
        Vector3 visualBase;

        void Start()
        {
            if (GameSession.Current.ClearedFieldObjects.Contains(uniqueId))
            {
                Destroy(gameObject);
                return;
            }
            home = transform.position;
            wanderTarget = home;
            bobPhase = Random.value * 10f;
            if (visual) visualBase = visual.localPosition;
        }

        void Update()
        {
            var director = FieldDirector.Instance;
            if (AiPaused || director == null || director.InputLocked) return;
            var player = director.Player;
            if (player == null) return;

            Animate();
            if (Time.time < invulnerableUntil || Time.timeSinceLevelLoad < StartGrace) return;

            Vector3 toPlayer = player.transform.position - transform.position;
            float verticalGap = toPlayer.y;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            // Contato: pisão por cima (preventivo) ou encostão normal.
            if (distance < bodyRadius + 0.45f)
            {
                bool fromAbove = verticalGap > bodyHeight * 0.6f && player.VerticalVelocity < 0f;
                if (fromAbove || Mathf.Abs(verticalGap) < bodyHeight + 0.6f)
                {
                    if (fromAbove)
                    {
                        player.Bounce(0.6f);
                        AudioManager.Play(Sfx.Bump);
                    }
                    director.StartBattle(encounter, this, fromAbove);
                    return;
                }
            }

            bool chasing = distance < chaseRadius && Mathf.Abs(verticalGap) < 1.2f && wanderRadius > 0f;
            Vector3 goal;
            float speed;
            if (chasing)
            {
                goal = player.transform.position;
                speed = chaseSpeed;
            }
            else
            {
                if (Time.time >= nextWanderTime || (wanderTarget - transform.position).sqrMagnitude < 0.05f)
                {
                    var offset = Random.insideUnitCircle * wanderRadius;
                    wanderTarget = home + new Vector3(offset.x, 0f, offset.y);
                    nextWanderTime = Time.time + Random.Range(2f, 4f);
                }
                goal = wanderTarget;
                speed = wanderSpeed;
            }
            MoveTowards(goal, speed);
        }

        void MoveTowards(Vector3 goal, float speed)
        {
            Vector3 dir = goal - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();

            Vector3 next = transform.position + dir * speed * Time.deltaTime;
            // Não sai de perto de casa e não cai de desníveis.
            if ((next - home).sqrMagnitude > (wanderRadius + 2.5f) * (wanderRadius + 2.5f)) return;
            if (!flying)
            {
                if (!Physics.Raycast(next + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return;
                if (Mathf.Abs(hit.point.y - transform.position.y) > 0.3f)
                {
                    nextWanderTime = 0f;
                    return;
                }
                next.y = hit.point.y;
            }
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void Animate()
        {
            if (!visual) return;
            float amplitude = flying ? 0.15f : 0.05f;
            float speed = flying ? 5f : 7f;
            visual.localPosition = visualBase + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * speed + bobPhase)) * amplitude;
        }

        public void OnBattleEnded(BattleResult result)
        {
            if (result == BattleResult.Victory)
            {
                GameSession.Current.ClearedFieldObjects.Add(uniqueId);
                Destroy(gameObject);
            }
            else
            {
                // Dá um tempo para o jogador se afastar.
                invulnerableUntil = Time.time + 3f;
                transform.position = home;
            }
        }
    }
}
