using UnityEngine;

namespace Oiram.Core
{
    /// <summary>
    /// Animação procedural de uma peça (asa batendo, perna mexendo, chama tremendo, pedra orbitando).
    /// Tudo em volta da pose inicial; não precisa de Animator nem de clipes.
    /// </summary>
    public sealed class Wiggle : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>Gira para frente e para trás em volta de <see cref="axis"/> (graus).</summary>
            Swing,
            /// <summary>Sobe e desce ao longo de <see cref="axis"/> (metros).</summary>
            Bob,
            /// <summary>"Respira": escala 1 ± amplitude (squash and stretch no eixo Y).</summary>
            Pulse,
            /// <summary>Gira sem parar em volta de <see cref="axis"/> (graus por segundo = amplitude).</summary>
            Spin,
        }

        public Mode mode;
        public Vector3 axis = Vector3.forward;
        public float amplitude = 20f;
        public float speed = 6f;
        public float phase;

        Vector3 basePosition;
        Quaternion baseRotation;
        Vector3 baseScale;

        void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            switch (mode)
            {
                case Mode.Swing:
                    transform.localRotation = baseRotation * Quaternion.AngleAxis(Mathf.Sin(t) * amplitude, axis);
                    break;
                case Mode.Bob:
                    transform.localPosition = basePosition + axis * (Mathf.Sin(t) * amplitude);
                    break;
                case Mode.Pulse:
                    float s = Mathf.Sin(t) * amplitude;
                    transform.localScale = new Vector3(baseScale.x * (1f - s * 0.5f), baseScale.y * (1f + s), baseScale.z * (1f - s * 0.5f));
                    break;
                case Mode.Spin:
                    transform.localRotation = baseRotation * Quaternion.AngleAxis(Time.time * amplitude + phase, axis);
                    break;
            }
        }

        public static Wiggle Add(GameObject target, Mode mode, Vector3 axis, float amplitude, float speed, float phase = 0f)
        {
            var w = target.AddComponent<Wiggle>();
            w.mode = mode;
            w.axis = axis;
            w.amplitude = amplitude;
            w.speed = speed;
            w.phase = phase;
            return w;
        }
    }
}
