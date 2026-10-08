using System;
using System.Threading;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Tweens mínimos baseados em Awaitable (Unity 6), sem dependências externas.</summary>
    public static class Tween
    {
        public static float Linear(float t) => t;
        public static float EaseInQuad(float t) => t * t;
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>Chama <paramref name="step"/> com t de 0 a 1 ao longo de <paramref name="duration"/> segundos.</summary>
        public static async Awaitable Run(float duration, Action<float> step, CancellationToken ct, bool unscaled = false)
        {
            step(0f);
            if (duration <= 0f)
            {
                step(1f);
                return;
            }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                await Awaitable.NextFrameAsync(ct);
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                step(Mathf.Clamp01(elapsed / duration));
            }
        }

        public static Awaitable MoveTo(Transform t, Vector3 to, float duration, CancellationToken ct, Func<float, float> ease = null)
        {
            Vector3 from = t.position;
            ease ??= EaseInOutQuad;
            return Run(duration, k => { if (t) t.position = Vector3.LerpUnclamped(from, to, ease(k)); }, ct);
        }

        /// <summary>Movimento em arco (pulo) até <paramref name="to"/>.</summary>
        public static Awaitable Arc(Transform t, Vector3 to, float height, float duration, CancellationToken ct)
        {
            Vector3 from = t.position;
            return Run(duration, k =>
            {
                if (!t) return;
                var p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * height;
                t.position = p;
            }, ct);
        }

        public static Awaitable ScaleTo(Transform t, Vector3 to, float duration, CancellationToken ct, Func<float, float> ease = null)
        {
            Vector3 from = t.localScale;
            ease ??= EaseOutQuad;
            return Run(duration, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, ease(k)); }, ct);
        }

        public static async Awaitable Shake(Transform t, float duration, float magnitude, CancellationToken ct)
        {
            Vector3 origin = t.localPosition;
            await Run(duration, k =>
            {
                if (!t) return;
                float damp = 1f - k;
                t.localPosition = origin + new Vector3(
                    Mathf.Sin(k * 90f) * magnitude * damp, 0f,
                    Mathf.Cos(k * 70f) * magnitude * damp);
            }, ct);
            if (t) t.localPosition = origin;
        }

        /// <summary>"Squash and stretch" rápido para dar peso a pulos e impactos.</summary>
        public static async Awaitable Squash(Transform t, float amount, float duration, CancellationToken ct)
        {
            Vector3 baseScale = t.localScale;
            await Run(duration, k =>
            {
                if (!t) return;
                float s = Mathf.Sin(k * Mathf.PI) * amount;
                t.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
            }, ct);
            if (t) t.localScale = baseScale;
        }

        public static Awaitable Delay(float seconds, CancellationToken ct) => Awaitable.WaitForSecondsAsync(seconds, ct);
    }
}
