using System.Collections;
using NUnit.Framework;
using Oiram.Core;
using Oiram.Field;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Oiram.Tests
{
    /// <summary>Entrada real (teclado virtual do Input System) passando pelos menus do mapa.</summary>
    public class InputFlowTests
    {
        Keyboard keyboard;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;

        [SetUp]
        public void SetUp()
        {
            GameSession.StartNew(new GameSession(GameDatabase.Load(), new SeededRandom(3)));
            // Em batchmode o editor nunca tem foco; sem isto o Input System ignora o teclado.
            previousBackground = InputSystem.settings.backgroundBehavior;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            Time.timeScale = 1f;
        }

        void SetKey(Key key, bool down)
        {
            using (StateEvent.From(keyboard, out var eventPtr))
            {
                keyboard[key].WriteValueIntoEvent(down ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        IEnumerator Press(Key key)
        {
            SetKey(key, true);
            yield return null;
            yield return null;
            SetKey(key, false);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tab_OpensAndClosesPauseMenu()
        {
            var load = SceneManager.LoadSceneAsync("Field_Vale", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null;
            var director = FieldDirector.Instance;
            Assert.IsNotNull(director);
            Assert.IsFalse(director.InputLocked);

            yield return Press(Key.Tab);
            Assert.IsTrue(director.InputLocked, "Tab deveria abrir o menu de pausa");
            Assert.AreEqual(0f, Time.timeScale, "jogo pausado com o menu aberto");

            yield return Press(Key.E); // próxima aba (não deve interagir com nada)
            Assert.IsTrue(director.InputLocked, "E troca de aba sem fechar o menu");
            yield return Press(Key.Escape);
            Assert.IsFalse(director.InputLocked, "Esc fecha o menu");
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
