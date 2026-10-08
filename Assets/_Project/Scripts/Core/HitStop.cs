using UnityEngine;

namespace Oiram.Core
{
    /// <summary>
    /// "Congela" o jogo por alguns centésimos (tempo real) para dar peso ao impacto.
    /// Respeita a escala de tempo vigente (testes aceleram o jogo) e se estende se chamado de novo.
    /// </summary>
    public static class HitStop
    {
        const float SlowFactor = 0.04f;
        static bool active;
        static double until;
        static float restoreScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active = false;
            restoreScale = 1f;
        }

        public static bool IsActive => active;

        public static async void Freeze(float realSeconds)
        {
            if (realSeconds <= 0f || Time.timeScale <= 0f) return;
            double end = Time.realtimeSinceStartupAsDouble + realSeconds;
            if (active)
            {
                if (end > until) until = end;
                return;
            }
            active = true;
            until = end;
            restoreScale = Time.timeScale;
            Time.timeScale = restoreScale * SlowFactor;
            try
            {
                while (Time.realtimeSinceStartupAsDouble < until) await Awaitable.NextFrameAsync();
            }
            finally
            {
                // Se alguém pausou (menu) ou trocou a escala no meio, não sobrescreve.
                if (Mathf.Approximately(Time.timeScale, restoreScale * SlowFactor)) Time.timeScale = restoreScale;
                active = false;
            }
        }
    }
}
