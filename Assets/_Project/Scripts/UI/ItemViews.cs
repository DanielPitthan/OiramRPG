using System.Collections.Generic;
using Oiram.Characters;
using Oiram.Loot;
using Oiram.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>Peças de UI compartilhadas entre o menu de pausa e as lojas (linhas, detalhe de item, comparação).</summary>
    public static class ItemViews
    {
        public static VisualElement Row(VisualElement parent, string text, string right, bool selected, bool enabled = true, string textClass = null)
        {
            var row = UiKit.El(parent, "menu-item");
            row.EnableInClassList("selected", selected);
            row.EnableInClassList("disabled", !enabled);
            var label = UiKit.Text(row, text, "menu-text");
            if (!string.IsNullOrEmpty(textClass)) label.AddToClassList(textClass);
            if (!string.IsNullOrEmpty(right)) UiKit.Text(row, right, "menu-right");
            return row;
        }

        public static void StatLine(VisualElement parent, string name, string value, string valueClass = null)
        {
            var line = UiKit.El(parent, "stat-line");
            UiKit.Text(line, name, "small");
            var v = UiKit.Text(line, value, "small");
            if (valueClass != null) v.AddToClassList(valueClass);
        }

        public static string ItemLabel(ItemInstance item) => item == null ? "—" : item.Name;

        /// <summary>Atributos são guardados com fração (multiplicadores de job); na tela, inteiros.</summary>
        public static string Shown(float value) => StatText.Number(Mathf.Round(value));

        public static void EquipmentSection(VisualElement parent, PartyMember m)
        {
            UiKit.Text(parent, "Equipamento", "section-title");
            foreach (EquipSlot slot in System.Enum.GetValues(typeof(EquipSlot)))
            {
                var item = m.GetEquipped(slot);
                var line = UiKit.El(parent, "stat-line");
                UiKit.Text(line, RarityInfo.SlotName(slot), "small", "muted");
                var name = UiKit.Text(line, ItemLabel(item), "small");
                if (item != null) UiKit.SetRarity(name, item.Rarity);
            }
        }

        public static void MemberHeader(VisualElement parent, PartyMember m)
        {
            var row = UiKit.El(parent, "row");
            UiKit.Text(row, $"◀  {m.Name}  ▶", "big", "grow");
            UiKit.Text(row, $"{m.Job.displayName} Nv {m.JobLevel(m.Job)}", "small", "muted");
        }

        /// <summary>Lista rolável de itens (cor da raridade; esmaece o que o personagem não pode usar).</summary>
        public static void ItemList(VisualElement parent, IReadOnlyList<ItemInstance> items, int selected, PartyMember member,
            int visibleRows, System.Func<ItemInstance, string> right = null)
        {
            int first = Mathf.Clamp(selected - visibleRows / 2, 0, Mathf.Max(0, items.Count - visibleRows));
            if (first > 0) UiKit.Text(parent, "▲", "small", "muted");
            for (int i = first; i < Mathf.Min(items.Count, first + visibleRows); i++)
            {
                var it = items[i];
                Row(parent, $"[{RarityInfo.SlotName(it.Slot)}] {it.Name}", right != null ? right(it) : $"Nv {it.ItemLevel}", i == selected,
                    member == null || member.CanEquip(it), RarityInfo.UssClass(it.Rarity));
            }
            if (first + visibleRows < items.Count) UiKit.Text(parent, "▼", "small", "muted");
        }

        /// <summary>Nome, raridade, atributos e comparação ▲▼ com o que o personagem usa no mesmo slot.</summary>
        public static void ItemDetail(VisualElement parent, ItemInstance item, PartyMember m, string priceLine = null)
        {
            var name = UiKit.Text(parent, item.Name, "big");
            UiKit.SetRarity(name, item.Rarity);
            UiKit.Text(parent, $"{RarityInfo.Name(item.Rarity)} · Nv {item.ItemLevel} · {RarityInfo.SlotName(item.Slot)} ({RarityInfo.CategoryName(item.Category)})", "small", "muted");
            foreach (var mod in item.BaseModifiers) UiKit.Text(parent, StatText.Describe(mod), "small");
            foreach (var affix in item.Affixes) UiKit.Text(parent, StatText.Describe(affix.Modifier), "small", affix.affix.isSpecial ? "gold-text" : "good");
            UiKit.Text(parent, priceLine ?? $"Venda: {item.SellValue} ouro", "small", "gold-text");

            if (m == null) return;
            UiKit.Text(parent, m.CanEquip(item) ? $"Se {m.Name} equipar (no lugar de {ItemLabel(m.GetEquipped(item.Slot))}):" : $"{m.Job.displayName} não pode usar este item.", "section-title");
            if (!m.CanEquip(item)) return;
            var now = m.ComputeStats();
            var preview = m.PreviewWith(item);
            bool any = false;
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                float before = Mathf.Round(now[stat]);
                float after = Mathf.Round(preview[stat]);
                if (Mathf.Approximately(before, after)) continue;
                any = true;
                string arrow = after > before ? "▲" : "▼";
                StatLine(parent, StatText.ShortName(stat), $"{Shown(before)} → {Shown(after)} {arrow}", after > before ? "good" : "bad");
            }
            if (!any) UiKit.Text(parent, "Sem mudança nos atributos.", "small", "muted");
        }
    }
}
