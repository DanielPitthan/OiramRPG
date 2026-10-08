using Oiram.Audio;
using Oiram.Core;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Movimento de exploração estilo Mario RPG: andar relativo à câmera isométrica e pular.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FieldPlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpHeight = 1.45f;
        public float gravity = -30f;
        public float coyoteTime = 0.12f;
        public float jumpBuffer = 0.12f;
        public Transform visual;

        CharacterController controller;
        CharacterRig rig;
        Vector3 velocity;
        float lastGroundedTime = -10f;
        float lastJumpPressedTime = -10f;
        bool wasGrounded;
        Vector3 lastSafePosition;
        Vector3 spawnPosition;
        Vector3 visualScale = Vector3.one;
        float squashStart = -10f;
        float squashAmount;
        const float SquashDuration = 0.16f;

        public float VerticalVelocity => velocity.y;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 Facing => visual ? visual.forward : transform.forward;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = lastSafePosition = transform.position;
            if (visual) visualScale = visual.localScale;
            if (visual) rig = visual.GetComponent<CharacterRig>();
        }

        /// <summary>Troca o visual (o líder mudou de job: chapéu e arma acompanham).</summary>
        public void SetVisual(Transform newVisual)
        {
            var rotation = visual ? visual.rotation : transform.rotation;
            if (visual) Destroy(visual.gameObject);
            visual = newVisual;
            visual.rotation = rotation;
            visualScale = visual.localScale;
            rig = visual.GetComponent<CharacterRig>();
        }

        void Squash(float amount)
        {
            squashStart = Time.time;
            squashAmount = amount;
        }

        void LateUpdate()
        {
            if (!visual) return;
            float k = (Time.time - squashStart) / SquashDuration;
            float s = k is >= 0f and < 1f ? Mathf.Sin(k * Mathf.PI) * squashAmount : 0f;
            visual.localScale = new Vector3(visualScale.x * (1f + s), visualScale.y * (1f - s), visualScale.z * (1f + s));
        }

        bool Locked => FieldDirector.Instance != null && FieldDirector.Instance.InputLocked;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 input = Locked ? Vector2.zero : GameInput.MoveValue;
            Vector3 move = CameraRelative(input);

            bool grounded = controller.isGrounded;
            if (grounded)
            {
                lastGroundedTime = Time.time;
                lastSafePosition = transform.position;
            }

            if (!Locked && GameInput.JumpDown) lastJumpPressedTime = Time.time;

            if (Time.time - lastJumpPressedTime <= jumpBuffer && Time.time - lastGroundedTime <= coyoteTime)
            {
                velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                lastJumpPressedTime = -10f;
                lastGroundedTime = -10f;
                Squash(-0.18f);
                AudioManager.Play(Sfx.Jump, 0.7f);
            }

            velocity.y += gravity * dt;
            if (grounded && velocity.y < 0f) velocity.y = -2f;

            float fallSpeed = velocity.y;
            var flags = controller.Move((move * moveSpeed + Vector3.up * velocity.y) * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

            if (move.sqrMagnitude > 0.001f && visual)
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(move, Vector3.up), 1f - Mathf.Exp(-15f * dt));
            if (rig)
            {
                rig.move = Mathf.MoveTowards(rig.move, controller.isGrounded ? Mathf.Clamp01(move.magnitude) : 0f, dt * 6f);
                rig.airborne = !controller.isGrounded;
            }

            if (!wasGrounded && controller.isGrounded)
            {
                Squash(0.2f);
                if (fallSpeed < -7f) Land();
            }
            wasGrounded = controller.isGrounded;

            if (transform.position.y < -6f) Teleport(lastSafePosition + Vector3.up * 0.5f);

            if (!Locked && GameInput.InteractDown)
                FieldInteractable.Nearest(transform.position)?.Interact(this);
        }

        /// <summary>Poeirinha e "tum" ao cair de um pulo.</summary>
        void Land()
        {
            AudioManager.Play(Sfx.Land, 0.6f);
            Fx.Dust(transform.position + Vector3.up * 0.1f);
        }

        static Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f) return Vector3.zero;
            var cam = Camera.main;
            Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            var dir = forward * input.y + right * input.x;
            return dir.sqrMagnitude > 1f ? dir.normalized : dir;
        }

        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            velocity = Vector3.zero;
        }

        public void RespawnAtStart() => Teleport(spawnPosition);

        public void SetSpawn(Vector3 position) => spawnPosition = position;

        /// <summary>Pulinho para frente (ao pisar num inimigo, abrir baú...).</summary>
        public void Bounce(float height = 0.8f) => velocity.y = Mathf.Sqrt(2f * -gravity * height);

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Cabeçada por baixo numa caixa flutuante.
            if (hit.normal.y < -0.5f && velocity.y > 0f)
            {
                var bumpable = hit.collider.GetComponentInParent<IHeadBumpable>();
                if (bumpable != null) bumpable.Bump(this);
            }
        }
    }
}
