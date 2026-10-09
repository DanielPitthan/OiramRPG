using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oiram.Core
{
    /// <summary>
    /// Ações de entrada definidas em código (teclado + gamepad), sem assets.
    /// Os apertos de Confirmar trazem o timestamp do Input System para os timed hits não dependerem do FPS.
    /// </summary>
    public static class GameInput
    {
        static InputActionMap map;

        public static InputAction Move { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Confirm { get; private set; }
        public static InputAction Cancel { get; private set; }
        public static InputAction Menu { get; private set; }
        public static InputAction Secondary { get; private set; }
        public static InputAction PrevTab { get; private set; }
        public static InputAction NextTab { get; private set; }
        /// <summary>Tecla Tab: abre o menu no mapa e, dentro dele, troca de aba (Shift+Tab volta).</summary>
        public static InputAction TabKey { get; private set; }

        /// <summary>Disparado no aperto de Confirmar, com o horário (em <see cref="Now"/>) do evento.</summary>
        public static event Action<double> ConfirmPressed;
        public static event Action<double> ConfirmReleased;

        /// <summary>Relógio em tempo real usado pelos timed hits.</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble;

        public static bool ConfirmDown => Ready && Confirm.WasPressedThisFrame();
        public static bool CancelDown => Ready && Cancel.WasPressedThisFrame();
        public static bool MenuDown => Ready && Menu.WasPressedThisFrame();
        public static bool SecondaryDown => Ready && Secondary.WasPressedThisFrame();
        public static bool PrevTabDown => Ready && PrevTab.WasPressedThisFrame();
        public static bool NextTabDown => Ready && NextTab.WasPressedThisFrame();
        public static bool TabKeyDown => Ready && TabKey.WasPressedThisFrame();
        public static bool ShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        /// <summary>Abrir o menu de pausa: Tab, I, Esc (teclado) ou Start (controle).</summary>
        public static bool OpenMenuDown => MenuDown || TabKeyDown || CancelDown;
        public static bool JumpDown => Ready && Jump.WasPressedThisFrame();
        public static bool InteractDown => Ready && Interact.WasPressedThisFrame();
        public static Vector2 MoveValue => Ready ? Move.ReadValue<Vector2>() : Vector2.zero;

        static bool Ready
        {
            get
            {
                EnsureInitialized();
                return map != null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            map?.Disable();
            map?.Dispose();
            map = null;
            ConfirmPressed = null;
            ConfirmReleased = null;
            navFrame = -1;
            heldDirection = Vector2Int.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureInitialized()
        {
            if (map != null) return;
            map = new InputActionMap("Oiram");

            Move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");
            Move.AddBinding("<Gamepad>/dpad");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Interact = Button("Interact", "<Keyboard>/e", "<Keyboard>/enter", "<Gamepad>/buttonWest");
            Confirm = Button("Confirm", "<Keyboard>/space", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/z", "<Gamepad>/buttonSouth");
            Cancel = Button("Cancel", "<Keyboard>/escape", "<Keyboard>/backspace", "<Keyboard>/x", "<Gamepad>/buttonEast");
            Menu = Button("Menu", "<Keyboard>/i", "<Gamepad>/start");
            TabKey = Button("TabKey", "<Keyboard>/tab");
            Secondary = Button("Secondary", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            PrevTab = Button("PrevTab", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            NextTab = Button("NextTab", "<Keyboard>/e", "<Gamepad>/rightShoulder");

            Confirm.started += ctx => ConfirmPressed?.Invoke(Stamp(ctx.time));
            Confirm.canceled += ctx => ConfirmReleased?.Invoke(Stamp(ctx.time));

            map.Enable();
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (var path in bindings) action.AddBinding(path);
            return action;
        }

        /// <summary>O tempo do evento deveria estar na mesma base que realtimeSinceStartup; se não estiver, usa "agora".</summary>
        static double Stamp(double eventTime)
        {
            double now = Now;
            return Math.Abs(eventTime - now) < 0.5 ? eventTime : now;
        }

        /// <summary>Inscreve callbacks de aperto/soltura enquanto o retorno não for descartado.</summary>
        public static IDisposable ListenConfirm(Action<double> onPress, Action<double> onRelease = null)
        {
            EnsureInitialized();
            ConfirmPressed += onPress;
            if (onRelease != null) ConfirmReleased += onRelease;
            return new Subscription(() =>
            {
                ConfirmPressed -= onPress;
                if (onRelease != null) ConfirmReleased -= onRelease;
            });
        }

        sealed class Subscription : IDisposable
        {
            Action dispose;
            public Subscription(Action dispose) => this.dispose = dispose;
            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }

        // ---------- Navegação de menus com auto-repetição ----------

        static int navFrame = -1;
        static Vector2Int navThisFrame;
        static Vector2Int heldDirection;
        static double nextRepeat;

        /// <summary>Direção de navegação neste frame (primeiro aperto + repetição ao segurar).</summary>
        public static Vector2Int Nav
        {
            get
            {
                if (!Ready) return Vector2Int.zero;
                if (navFrame == Time.frameCount) return navThisFrame;
                navFrame = Time.frameCount;

                var v = Move.ReadValue<Vector2>();
                var dir = Vector2Int.zero;
                if (Mathf.Abs(v.x) > 0.5f || Mathf.Abs(v.y) > 0.5f)
                {
                    dir = Mathf.Abs(v.x) > Mathf.Abs(v.y)
                        ? new Vector2Int(v.x > 0 ? 1 : -1, 0)
                        : new Vector2Int(0, v.y > 0 ? 1 : -1);
                }

                double now = Now;
                if (dir == Vector2Int.zero)
                {
                    heldDirection = Vector2Int.zero;
                    navThisFrame = Vector2Int.zero;
                }
                else if (dir != heldDirection)
                {
                    heldDirection = dir;
                    navThisFrame = dir;
                    nextRepeat = now + 0.35;
                }
                else if (now >= nextRepeat)
                {
                    navThisFrame = dir;
                    nextRepeat = now + 0.09;
                }
                else
                {
                    navThisFrame = Vector2Int.zero;
                }
                return navThisFrame;
            }
        }
    }
}
