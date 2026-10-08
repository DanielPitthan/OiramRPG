using UnityEngine;

namespace Oiram.Core
{
    /// <summary>
    /// Animação procedural dos heróis e moradores: respirar, piscar, andar (braços e pernas), pular
    /// e erguer/baixar a arma nos golpes. Quem move o personagem só informa <see cref="move"/> e <see cref="airborne"/>.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform body, head, armLeft, armRight, legLeft, legRight;
        public Transform[] eyes = new Transform[0];

        [Range(0f, 1f)] public float move;
        public bool airborne;
        /// <summary>0 = braço solto; 1 = arma erguida (preparando o golpe); negativo = golpe passando do ponto.</summary>
        public float armRaise;

        float walkPhase, nextBlink, blinkUntil, idlePhase;
        Vector3 bodyBase, bodyScale, headBase;
        Quaternion armLeftBase, armRightBase, legLeftBase, legRightBase;
        Vector3[] eyeScales;

        bool captured;

        void Awake()
        {
            if (body != null) Capture();
        }

        /// <summary>Liga as peças (personagens criados em runtime: o Awake roda antes de os campos existirem).</summary>
        public void Bind(Transform body, Transform head, Transform armLeft, Transform armRight, Transform legLeft, Transform legRight, Transform[] eyes)
        {
            this.body = body;
            this.head = head;
            this.armLeft = armLeft;
            this.armRight = armRight;
            this.legLeft = legLeft;
            this.legRight = legRight;
            this.eyes = eyes;
            Capture();
        }

        void Capture()
        {
            captured = true;
            if (body) { bodyBase = body.localPosition; bodyScale = body.localScale; }
            if (head) headBase = head.localPosition;
            if (armLeft) armLeftBase = armLeft.localRotation;
            if (armRight) armRightBase = armRight.localRotation;
            if (legLeft) legLeftBase = legLeft.localRotation;
            if (legRight) legRightBase = legRight.localRotation;
            eyeScales = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) if (eyes[i]) eyeScales[i] = eyes[i].localScale;
            idlePhase = Random.value * 10f;
            nextBlink = Time.time + Random.Range(1f, 4f);
        }

        void Update()
        {
            if (!captured) return;
            float dt = Time.deltaTime;
            walkPhase += dt * Mathf.Lerp(0f, 11f, move);
            float swing = Mathf.Sin(walkPhase) * 38f * move;
            float bounce = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f * move;
            float breathe = Mathf.Sin(Time.time * 2.4f + idlePhase) * 0.018f * (1f - move);

            if (body)
            {
                body.localPosition = bodyBase + Vector3.up * bounce;
                body.localScale = new Vector3(bodyScale.x * (1f - breathe * 0.5f), bodyScale.y * (1f + breathe), bodyScale.z * (1f - breathe * 0.5f));
            }
            if (head) head.localPosition = headBase + Vector3.up * (Mathf.Sin(Time.time * 2.4f + idlePhase + 0.6f) * 0.008f);

            float legAir = airborne ? 25f : 0f;
            if (legLeft) legLeft.localRotation = legLeftBase * Quaternion.Euler(swing - legAir, 0f, 0f);
            if (legRight) legRight.localRotation = legRightBase * Quaternion.Euler(-swing + legAir * 0.4f, 0f, 0f);

            float armAir = airborne ? -70f : 0f;
            if (armLeft) armLeft.localRotation = armLeftBase * Quaternion.Euler(-swing * 0.8f + armAir, 0f, airborne ? -25f : 0f);
            if (armRight)
            {
                // Erguer a arma: o braço gira para trás e para cima; negativo passa à frente (golpe).
                float raise = armRaise >= 0f ? -armRaise * 150f : -armRaise * 60f;
                float idle = Mathf.Approximately(armRaise, 0f) ? swing * 0.8f + armAir : 0f;
                armRight.localRotation = armRightBase * Quaternion.Euler(idle + raise, 0f, airborne ? 25f : 0f);
            }

            if (Time.time >= nextBlink)
            {
                blinkUntil = Time.time + 0.1f;
                nextBlink = Time.time + Random.Range(2f, 5f);
            }
            bool blinking = Time.time < blinkUntil;
            for (int i = 0; i < eyes.Length; i++)
                if (eyes[i]) eyes[i].localScale = blinking ? new Vector3(eyeScales[i].x, eyeScales[i].y * 0.12f, eyeScales[i].z) : eyeScales[i];
        }
    }
}
