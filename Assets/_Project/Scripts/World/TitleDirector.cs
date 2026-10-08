using System.Collections.Generic;
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
            ChooseLoop();
        }

        void Update()
        {
            if (diorama) diorama.Rotate(0f, 12f * Time.deltaTime, 0f, Space.World);
        }

        void BuildDiorama()
        {
            diorama = new GameObject("Diorama").transform;
            Shapes.Part(PrimitiveType.Cylinder, diorama, new Vector3(0f, -0.5f, 0f), new Vector3(7f, 0.5f, 7f), new Color(0.58f, 0.42f, 0.27f));
            Shapes.Part(PrimitiveType.Cylinder, diorama, new Vector3(0f, -0.02f, 0f), new Vector3(6.6f, 0.03f, 6.6f), new Color(0.45f, 0.74f, 0.36f));
            var db = GameDatabase.Load();
            int i = 0;
            foreach (var character in db.characters)
            {
                var holder = new GameObject(character.id).transform;
                holder.SetParent(diorama, false);
                holder.localPosition = new Vector3(-1.2f + i * 1.1f, 0f, -0.6f + i * 0.2f);
                holder.localRotation = Quaternion.Euler(0f, 200f, 0f);
                Shapes.Hero(holder, character.color, character.startingJob != null ? character.startingJob.color : Color.blue);
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
            Shapes.Tree(Child(new Vector3(2.4f, 0f, -1.6f)), 1f);
            Shapes.Tree(Child(new Vector3(-2.6f, 0f, -1.2f)), 0.8f);
            Shapes.Rock(Child(new Vector3(0.5f, 0f, 2.4f)), 1f);
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
            UiKit.Text(top, "OiramRPG", "title-logo");
            UiKit.Text(top, "Timed hits · jobs · muito loot", "title-sub");
            var panel = UiKit.El(box, "panel", "title-menu");
            menu = new MenuList(panel);

            var entries = new List<MenuEntry>();
            if (SaveSystem.HasSave)
            {
                options.Add(Option.Continue);
                entries.Add(new MenuEntry("Continuar", right: SaveSystem.Describe()));
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
                        var loaded = SaveSystem.Load(db);
                        if (loaded == null) goto case Option.NewGame;
                        GameSession.StartNew(loaded);
                        var location = db.Find<LocationDefinition>(loaded.CurrentLocationId) ?? db.startLocation;
                        PlaytestLog.Note("continuar", location.id);
                        SceneFlow.EnterLocation(location, loaded.SpawnPointId ?? "pousada");
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
