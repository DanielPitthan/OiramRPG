using System.Collections.Generic;
using Oiram.Audio;
using Oiram.Core;
using Oiram.UI;
using UnityEngine;

namespace Oiram.World
{
    /// <summary>Tela de título: Continuar (save da pousada), Novo jogo, Sair. Fundo: diorama girando.</summary>
    public sealed class TitleDirector : MonoBehaviour
    {
        enum Option { Continue, NewGame, Quit }

        Transform diorama;
        MenuList menu;
        UnityEngine.UIElements.Label logo;
        readonly List<Option> options = new();

        void Awake()
        {
            GameInput.EnsureInitialized();
            BuildDiorama();
            BuildUi();
        }

        void Start()
        {
            _ = ScreenFader.FadeIn(0.5f, destroyCancellationToken);
            AudioManager.PlayMusic(MusicTrack.Title);
            ChooseLoop();
        }

        void Update()
        {
            if (logo != null)
            {
                // Logo "respirando" e balançando de leve.
                float t = Time.unscaledTime;
                float s = 1f + Mathf.Sin(t * 2.2f) * 0.025f;
                logo.style.scale = new UnityEngine.UIElements.Scale(new Vector3(s, s, 1f));
                logo.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(Mathf.Sin(t * 1.3f) * 1.5f, UnityEngine.UIElements.AngleUnit.Degree));
            }
            if (diorama) diorama.Rotate(0f, 12f * Time.deltaTime, 0f, Space.World);
        }

        void BuildDiorama()
        {
            diorama = new GameObject("Diorama").transform;
            Shapes.Part(PrimitiveType.Cylinder, diorama, new Vector3(0f, -0.55f, 0f), new Vector3(7f, 0.55f, 7f), new Color(0.62f, 0.44f, 0.28f));
            Shapes.Part(PrimitiveType.Cylinder, diorama, new Vector3(0f, -0.1f, 0f), new Vector3(7.12f, 0.07f, 7.12f), new Color(0.38f, 0.64f, 0.3f));
            Shapes.Part(PrimitiveType.Cylinder, diorama, new Vector3(0f, -0.02f, 0f), new Vector3(6.7f, 0.03f, 6.7f), new Color(0.47f, 0.76f, 0.37f));
            Shapes.Part(PrimitiveType.Plane, diorama, new Vector3(0f, -1.1f, 0f), new Vector3(6f, 1f, 6f), Palette.Water(new Color(0.33f, 0.66f, 0.93f), new Color(0.2f, 0.45f, 0.82f)));
            Shapes.MeadowScatter(diorama, "TitleMeadow", Vector3.zero, 3.1f, 0.01f, 90, 9, new Color(0.36f, 0.62f, 0.3f), p => Mathf.Abs(p.z + 0.4f) < 0.9f && Mathf.Abs(p.x) < 1.8f);
            for (int c = 0; c < 3; c++)
                Shapes.Cloud(diorama, 0.8f + c * 0.2f, 900 + c).localPosition = new Vector3(-3f + c * 3f, 3.6f + (c % 2) * 0.6f, 2.5f - c * 1.4f);
            var db = GameDatabase.Load();
            int i = 0;
            foreach (var character in db.characters)
            {
                var holder = new GameObject(character.id).transform;
                holder.SetParent(diorama, false);
                holder.localPosition = new Vector3(-1.2f + i * 1.1f, 0f, -0.6f + i * 0.2f);
                holder.localRotation = Quaternion.Euler(0f, 200f, 0f);
                Shapes.Hero(holder, character.color, character.startingJob != null ? character.startingJob.color : Color.blue, character.startingJob?.id);
                i++;
            }
            var slimeHolder = new GameObject("Slime").transform;
            slimeHolder.SetParent(diorama, false);
            slimeHolder.localPosition = new Vector3(1.8f, 0f, 1.3f);
            slimeHolder.localRotation = Quaternion.Euler(0f, 220f, 0f);
            Shapes.Enemy(slimeHolder, Battle.UnitShape.Slime, new Color(0.4f, 0.85f, 0.35f), 0.9f);
            var chest = new GameObject("Chest").transform;
            chest.SetParent(diorama, false);
            chest.localPosition = new Vector3(-2f, 0f, 1.5f);
            chest.localRotation = Quaternion.Euler(0f, 160f, 0f);
            Shapes.Chest(chest, open: true);
            Shapes.Tree(Child(new Vector3(2.4f, 0f, -1.6f)), 1f, 1);
            Shapes.Tree(Child(new Vector3(-2.6f, 0f, -1.2f)), 0.8f, 3);
            Shapes.Rock(Child(new Vector3(0.5f, 0f, 2.4f)), 1f, 2);
            Shapes.Bush(Child(new Vector3(2.6f, 0f, 1.2f)), 1f, 0);
            Shapes.Bush(Child(new Vector3(-0.6f, 0f, 2.7f)), 0.8f, 1);
        }

        Transform Child(Vector3 local)
        {
            var t = new GameObject("Decor").transform;
            t.SetParent(diorama, false);
            t.localPosition = local;
            return t;
        }

        void BuildUi()
        {
            var root = UiKit.CreateDocument(transform, "TitleDocument", 0);
            var box = UiKit.El(root, "title-root");
            var top = UiKit.El(box, "title-top");
            logo = UiKit.Text(top, "OiramRPG", "title-logo");
            UiKit.Text(top, "Timed hits · jobs · muito loot", "title-sub");
            var panel = UiKit.El(box, "panel", "title-menu");
            menu = new MenuList(panel);

            var entries = new List<MenuEntry>();
            if (SaveSystem.HasSave)
            {
                options.Add(Option.Continue);
                entries.Add(new MenuEntry("Continuar", right: SaveSystem.Describe(GameDatabase.Load())));
            }
            options.Add(Option.NewGame);
            entries.Add(new MenuEntry("Novo jogo"));
            options.Add(Option.Quit);
            entries.Add(new MenuEntry("Sair"));
            menu.SetItems(entries);
        }

        async void ChooseLoop()
        {
            try
            {
                int pick = await menu.Choose(destroyCancellationToken, allowCancel: false);
                var db = GameDatabase.Load();
                switch (options[pick])
                {
                    case Option.Continue:
                        if (!SceneFlow.Continue(db)) goto case Option.NewGame;
                        break;
                    case Option.NewGame:
                        GameSession.StartNew(new GameSession(db, new SeededRandom()));
                        PlaytestLog.Note("novo_jogo", "");
                        SceneFlow.EnterLocation(db.startLocation);
                        break;
                    case Option.Quit:
                        Application.Quit();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                        break;
                }
            }
            catch (System.OperationCanceledException) { }
        }
    }
}
