using System;
using Oiram.Core;

namespace Oiram.Battle
{
    public enum TimedHitResult
    {
        Miss,
        Good,
        Perfect,
    }

    /// <summary>Opções de feedback dos timed hits (F6 nas builds de desenvolvimento mostra os milissegundos).</summary>
    public static class TimingFeedback
    {
        public static bool ShowMilliseconds
        {
            get => GameSettings.ShowMilliseconds;
            set => GameSettings.ShowMilliseconds = value;
        }

        public static string Milliseconds(double offsetSeconds) =>
            (offsetSeconds >= 0 ? "+" : "−") + (Math.Abs(offsetSeconds) * 1000).ToString("0") + " ms";
    }

    public static class TimedHitEvaluator
    {
        public static TimedHitResult Evaluate(double offsetSeconds, double perfectWindow, double goodWindow)
        {
            double abs = Math.Abs(offsetSeconds);
            if (abs <= perfectWindow) return TimedHitResult.Perfect;
            if (abs <= goodWindow) return TimedHitResult.Good;
            return TimedHitResult.Miss;
        }

        /// <summary>Janelas da config ampliadas pelo atributo TimingWindow (ms) do personagem.</summary>
        public static (double perfect, double good) Windows(BalanceConfig balance, float bonusMs)
        {
            double bonus = Math.Max(0f, bonusMs) / 1000.0;
            return (balance.perfectWindow + bonus * 0.5, balance.goodWindow + bonus);
        }

        public static string Label(TimedHitResult result) => result switch
        {
            TimedHitResult.Perfect => "PERFEITO!",
            TimedHitResult.Good => "BOM!",
            _ => "",
        };
    }

    /// <summary>
    /// Janela de um aperto único (ataque ou bloqueio). Só o primeiro aperto depois de armada conta:
    /// apertar cedo demais gasta a tentativa (anti-spam, como no Mario RPG).
    /// </summary>
    public sealed class TimedHitWindow
    {
        public double ArmedAt { get; }
        public double ImpactAt { get; }
        public double PerfectWindow { get; }
        public double GoodWindow { get; }
        public bool HasInput { get; private set; }
        public TimedHitResult Result { get; private set; } = TimedHitResult.Miss;
        /// <summary>Aperto − impacto, em segundos (negativo = cedo).</summary>
        public double PressOffset { get; private set; }

        public double CloseAt => ImpactAt + GoodWindow;

        public TimedHitWindow(double armedAt, double impactAt, double perfectWindow, double goodWindow)
        {
            ArmedAt = armedAt;
            ImpactAt = impactAt;
            PerfectWindow = perfectWindow;
            GoodWindow = goodWindow;
        }

        public void RegisterPress(double time)
        {
            if (HasInput || time < ArmedAt) return;
            HasInput = true;
            PressOffset = time - ImpactAt;
            Result = TimedHitEvaluator.Evaluate(PressOffset, PerfectWindow, GoodWindow);
        }

        public bool IsClosed(double now) => HasInput || now >= CloseAt;
    }

    /// <summary>Segurar o botão para carregar e soltar quando a carga completa.</summary>
    public sealed class HoldReleaseWindow
    {
        public double ArmedAt { get; }
        public double ChargeDuration { get; }
        public double PerfectWindow { get; }
        public double GoodWindow { get; }
        public double? PressedAt { get; private set; }
        public bool IsDone { get; private set; }
        public double? ReleasedAt { get; private set; }
        public TimedHitResult Result { get; private set; } = TimedHitResult.Miss;

        public HoldReleaseWindow(double armedAt, double chargeDuration, double perfectWindow, double goodWindow)
        {
            ArmedAt = armedAt;
            ChargeDuration = chargeDuration;
            PerfectWindow = perfectWindow;
            GoodWindow = goodWindow;
        }

        public double? TargetAt => PressedAt + ChargeDuration;

        public void RegisterPress(double time)
        {
            if (IsDone || PressedAt.HasValue || time < ArmedAt) return;
            PressedAt = time;
        }

        public void RegisterRelease(double time)
        {
            if (IsDone || !PressedAt.HasValue) return;
            IsDone = true;
            ReleasedAt = time;
            Result = TimedHitEvaluator.Evaluate(time - TargetAt.Value, PerfectWindow, GoodWindow);
        }

        /// <summary>Segurar demais estoura a carga.</summary>
        public void Update(double now)
        {
            if (!IsDone && PressedAt.HasValue && now > TargetAt.Value + GoodWindow)
            {
                IsDone = true;
                Result = TimedHitResult.Miss;
            }
        }

        public void Cancel()
        {
            IsDone = true;
            Result = TimedHitResult.Miss;
        }

        public float Charge01(double now) =>
            PressedAt.HasValue ? (float)Math.Min(1.0, Math.Max(0.0, (now - PressedAt.Value) / ChargeDuration)) : 0f;
    }
}
