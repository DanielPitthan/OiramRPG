using System.Collections.Generic;
using System.Linq;
using Oiram.Characters;
using Oiram.Core;
using Oiram.Field;
using Oiram.Inventory;
using Oiram.Loot;
using Oiram.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>Tela das lojas da cidade: consumíveis, ferreiro (comprar/vender), apostador e pousada.</summary>
    public sealed class ShopMenu : MonoBehaviour
    {
        const int VisibleRows = 12;
        static readonly EquipSlot[] GambleSlots = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Accessory };

        VisualElement panel, body;
        Label title, info, footer;
        ShopKind kind;
        string townId, shopName;
        int index, member, openedFrame;
        bool sellMode;
        ItemInstance lastGamble;
        IRandom rng;

        public bool IsOpen { get; private set; }

        static GameSession S => GameSession.Current;
        static FieldDirector Director => FieldDirector.Instance;

        public static void Open(ShopKind kind, string townId, string shopName)
        {
            var director = Director;
            if (director == null) return;
            var menu = director.GetComponentInChildren<ShopMenu>(true);
            if (menu == null)
            {
                var go = new GameObject("ShopMenu");
                go.transform.SetParent(director.transform, false);
                menu = go.AddComponent<ShopMenu>();
                menu.Build();
            }
            menu.Show(kind, townId, shopName);
        }

        void Build()
        {
            var root = UiKit.CreateDocument(transform, "ShopMenuDocument", 12);
            panel = UiKit.El(root, "pause-root");
            var header = UiKit.El(panel, "row");
            title = UiKit.Text(header, "", "huge", "grow");
            title.style.fontSize = 44;
            info = UiKit.Text(header, "", "big", "gold-text");
            body = UiKit.El(panel, "pause-body");
            footer = UiKit.Text(panel, "", "footer", "small", "muted");
            UiKit.Show(panel, false);
            rng = new SeededRandom();
        }

        void Show(ShopKind shopKind, string town, string name)
        {
            kind = shopKind;
            townId = town;
            shopName = name;
            index = 0;
            sellMode = false;
            lastGamble = null;
            openedFrame = Time.frameCount;
            IsOpen = true;
            Director.ModalOpen = true;
            Director.Hud.SetVisible(false);
            UiKit.Show(panel, true);
            PlaytestLog.Note("loja", $"{townId}:{kind}");
            Render();
        }

        void Close()
        {
            IsOpen = false;
            UiKit.Show(panel, false);
            if (Director != null)
            {
                Director.ModalOpen = false;
                Director.Hud.SetVisible(true);
                Director.Hud.Refresh();
            }
        }

        static int Wrap(int value, int count) => count <= 0 ? 0 : (value % count + count) % count;
        PartyMember Member => S.Party[Mathf.Clamp(member, 0, S.Party.Count - 1)];

        void Toast(string text, string css = null) => Director.ShowToast(text, css);

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (GameInput.CancelDown || GameInput.MenuDown)
            {
                Close();
                return;
            }
            bool changed = kind switch
            {
                ShopKind.Consumables => UpdateConsumables(),
                ShopKind.Blacksmith => UpdateBlacksmith(),
                ShopKind.Gambler => UpdateGambler(),
                ShopKind.Inn => UpdateInn(),
                _ => false,
            };
            if (changed) Render();
        }

        bool Navigate(int count)
        {
            var nav = GameInput.Nav;
            if (nav.y == 0 || count == 0) return false;
            index = Wrap(index - nav.y, count);
            return true;
        }

        // ------------------------------------------------------------------ consumíveis

        List<ConsumableDefinition> Consumables => S.Db.consumables;

        bool UpdateConsumables()
        {
            if (Navigate(Consumables.Count)) return true;
            if (!GameInput.ConfirmDown || Consumables.Count == 0) return false;
            var item = Consumables[index];
            if (ShopService.BuyConsumable(S, item))
            {
                Toast($"Comprou {item.displayName} por {item.price} ouro.", "gold-text");
                PlaytestLog.Note("comprou", $"{item.displayName} por {item.price}");
            }
            else Toast("Ouro insuficiente.", "bad");
            return true;
        }

        void RenderConsumables()
        {
            var list = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(list, "À venda", "section-title");
            for (int i = 0; i < Consumables.Count; i++)
            {
                var c = Consumables[i];
                ItemViews.Row(list, $"{c.displayName}  (tem {S.Inventory.Count(c)})", $"{c.price} ouro", i == index, ShopService.CanAfford(S, c.price));
            }
            var detail = UiKit.El(body, "panel", "column");
            if (Consumables.Count > 0)
            {
                var c = Consumables[index];
                UiKit.Text(detail, c.displayName, "big");
                UiKit.Text(detail, c.description, "small");
                UiKit.Text(detail, $"Preço: {c.price} ouro", "small", "gold-text");
            }
            footer.text = "↑↓: item    Enter/Espaço: comprar 1    Esc: sair";
        }

        // ------------------------------------------------------------------ ferreiro

        List<ItemInstance> CurrentList => sellMode ? S.Inventory.Sorted() : ShopService.BlacksmithStock(S, townId);

        bool UpdateBlacksmith()
        {
            if (GameInput.PrevTabDown || GameInput.NextTabDown)
            {
                sellMode = !sellMode;
                index = 0;
                return true;
            }
            var nav = GameInput.Nav;
            if (nav.x != 0)
            {
                member = Wrap(member + nav.x, S.Party.Count);
                return true;
            }
            var items = CurrentList;
            if (Navigate(items.Count)) return true;
            if (items.Count == 0) return false;
            index = Mathf.Clamp(index, 0, items.Count - 1);
            var item = items[index];

            if (sellMode && (GameInput.ConfirmDown || GameInput.SecondaryDown))
            {
                int gold = S.Inventory.Sell(item);
                Toast($"Vendeu {item.Name} por {gold} ouro.", "gold-text");
                PlaytestLog.Note("vendeu", $"{item.Name} [{item.Rarity}] por {gold}");
                index = Mathf.Clamp(index, 0, Mathf.Max(0, S.Inventory.Items.Count - 1));
                return true;
            }
            if (!sellMode && GameInput.ConfirmDown)
            {
                int price = ShopService.BuyPrice(item, S.Balance);
                if (ShopService.BuyItem(S, townId, item))
                {
                    Toast($"Comprou {item.Name} por {price} ouro.", RarityInfo.UssClass(item.Rarity));
                    PlaytestLog.Note("comprou", $"{item.Name} [{item.Rarity}] por {price}");
                }
                else Toast(S.Inventory.IsFull ? "Mochila cheia." : "Ouro insuficiente.", "bad");
                index = Mathf.Clamp(index, 0, Mathf.Max(0, ShopService.BlacksmithStock(S, townId).Count - 1));
                return true;
            }
            return false;
        }

        void RenderBlacksmith()
        {
            var items = CurrentList;
            index = Mathf.Clamp(index, 0, Mathf.Max(0, items.Count - 1));
            var list = UiKit.El(body, "panel", "column-narrow");
            var tabs = UiKit.El(list, "tabs");
            UiKit.Text(tabs, "Comprar", "tab").EnableInClassList("selected", !sellMode);
            UiKit.Text(tabs, "Vender", "tab").EnableInClassList("selected", sellMode);
            if (items.Count == 0)
                UiKit.Text(list, sellMode ? "Nada para vender." : "Estoque esgotado. Volte depois de uma dungeon!", "muted");
            ItemViews.ItemList(list, items, index, Member, VisibleRows,
                sellMode ? it => $"{it.SellValue} ouro" : it => $"{ShopService.BuyPrice(it, S.Balance)} ouro");

            var detail = UiKit.El(body, "panel", "column");
            ItemViews.MemberHeader(detail, Member);
            if (items.Count > 0)
            {
                var item = items[index];
                string price = sellMode ? $"Venda: {item.SellValue} ouro" : $"Preço: {ShopService.BuyPrice(item, S.Balance)} ouro";
                ItemViews.ItemDetail(detail, item, Member, price);
            }
            footer.text = sellMode
                ? "↑↓: item    ←→: personagem    Enter/F: vender    Q/E: comprar    Esc: sair"
                : "↑↓: item    ←→: comparar com    Enter/Espaço: comprar    Q/E: vender    Esc: sair  ·  o estoque muda depois de cada dungeon";
        }

        // ------------------------------------------------------------------ apostador

        bool UpdateGambler()
        {
            if (Navigate(GambleSlots.Length)) return true;
            if (!GameInput.ConfirmDown) return false;
            var item = ShopService.Gamble(S, GambleSlots[index], rng);
            if (item == null)
            {
                Toast(S.Inventory.IsFull ? "Mochila cheia." : "Ouro insuficiente.", "bad");
                return true;
            }
            lastGamble = item;
            Toast($"Item misterioso: {item.Name} ({RarityInfo.Name(item.Rarity)})!", RarityInfo.UssClass(item.Rarity));
            PlaytestLog.Note("aposta", $"{item.Name} [{item.Rarity}]");
            return true;
        }

        void RenderGambler()
        {
            int price = ShopService.GamblePrice(S);
            var list = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(list, "Itens misteriosos", "section-title");
            for (int i = 0; i < GambleSlots.Length; i++)
            {
                string name = GambleSlots[i] switch
                {
                    EquipSlot.Weapon => "Arma misteriosa",
                    EquipSlot.Armor => "Armadura misteriosa",
                    EquipSlot.Helmet => "Elmo misterioso",
                    _ => "Acessório misterioso",
                };
                ItemViews.Row(list, name, $"{price} ouro", i == index, ShopService.CanAfford(S, price));
            }
            UiKit.Text(list, "Você paga sem ver os afixos. Nunca vem Comum; às vezes vem Lendário...", "small", "muted");

            var detail = UiKit.El(body, "panel", "column");
            if (lastGamble != null)
            {
                UiKit.Text(detail, "Você recebeu:", "section-title");
                ItemViews.ItemDetail(detail, lastGamble, Member);
            }
            else UiKit.Text(detail, "\"Sinta a sorte, viajante. Escolha um tipo e veja o que sai!\"", "small");
            footer.text = "↑↓: tipo    Enter/Espaço: comprar item misterioso    Esc: sair";
        }

        // ------------------------------------------------------------------ pousada

        bool UpdateInn()
        {
            if (Navigate(2)) return true;
            if (!GameInput.ConfirmDown) return false;
            if (index == 0)
            {
                int price = ShopService.InnPrice(S);
                if (!ShopService.Rest(S))
                {
                    Toast("Ouro insuficiente para o quarto.", "bad");
                    return true;
                }
                Toast($"A party descansou ({price} ouro).", "good");
                PlaytestLog.Note("descanso", $"pousada {price}");
            }
            SaveSystem.Save(S, "pousada");
            Toast("Jogo salvo!", "gold-text");
            PlaytestLog.Note("salvou", S.CurrentLocationId);
            return true;
        }

        void RenderInn()
        {
            int price = ShopService.InnPrice(S);
            var list = UiKit.El(body, "panel", "column-narrow");
            UiKit.Text(list, "Pousada", "section-title");
            ItemViews.Row(list, "Descansar e salvar", $"{price} ouro", index == 0, ShopService.CanAfford(S, price));
            ItemViews.Row(list, "Só salvar", "grátis", index == 1);
            var detail = UiKit.El(body, "panel", "column");
            UiKit.Text(detail, "Descansar recupera todo o PV e PE da party.", "small");
            UiKit.Text(detail, "Salvar grava a viagem; continue pela tela de título.", "small", "muted");
            var description = SaveSystem.Describe();
            if (description != null) UiKit.Text(detail, description, "small", "gold-text");
            footer.text = "↑↓: opção    Enter/Espaço: confirmar    Esc: sair";
        }

        // ------------------------------------------------------------------

        void Render()
        {
            title.text = shopName;
            info.text = $"Ouro {S.Inventory.Gold}";
            body.Clear();
            switch (kind)
            {
                case ShopKind.Consumables: RenderConsumables(); break;
                case ShopKind.Blacksmith: RenderBlacksmith(); break;
                case ShopKind.Gambler: RenderGambler(); break;
                case ShopKind.Inn: RenderInn(); break;
            }
        }
    }
}
