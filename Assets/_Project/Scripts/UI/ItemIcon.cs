using System.Collections.Generic;
using Oiram.Inventory;
using Oiram.Loot;
using UnityEngine;
using UnityEngine.UIElements;

namespace Oiram.UI
{
    /// <summary>
    /// Ícone de item desenhado com vetores (Painter2D): moldura na cor da raridade e um pictograma por categoria
    /// (espada, machado, adaga, cajado, maça, armaduras, elmos, chapéu, anel, amuleto, poção).
    /// </summary>
    public sealed class ItemIcon : VisualElement
    {
        enum Kind { Sword, Axe, Dagger, Staff, Mace, HeavyArmor, LightArmor, Robe, HeavyHelm, Hood, Hat, Ring, Amulet, Potion }

        static readonly Color Steel = new(0.86f, 0.89f, 0.96f);
        static readonly Color Wood = new(0.66f, 0.45f, 0.27f);
        static readonly Color Leather = new(0.62f, 0.42f, 0.26f);
        static readonly Color GoldColor = new(1f, 0.8f, 0.3f);
        static readonly Color Ink = new(0.07f, 0.05f, 0.12f);

        readonly Kind kind;
        readonly Color frame;
        readonly Color accent;

        ItemIcon(Kind kind, Color frame, Color accent, float size)
        {
            this.kind = kind;
            this.frame = frame;
            this.accent = accent;
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            AddToClassList("item-icon");
            generateVisualContent += Draw;
        }

        public static ItemIcon For(ItemInstance item, float size = 34f) =>
            new(KindOf(item.Category), RarityInfo.Color(item.Rarity), Color.Lerp(RarityInfo.Color(item.Rarity), Color.white, 0.25f), size);

        public static ItemIcon For(ConsumableDefinition item, float size = 34f) =>
            new(Kind.Potion, new Color(0.8f, 0.8f, 0.85f), item.color, size);

        static Kind KindOf(EquipCategory c) => c switch
        {
            EquipCategory.Sword => Kind.Sword,
            EquipCategory.Axe => Kind.Axe,
            EquipCategory.Dagger => Kind.Dagger,
            EquipCategory.Staff => Kind.Staff,
            EquipCategory.Mace => Kind.Mace,
            EquipCategory.HeavyArmor => Kind.HeavyArmor,
            EquipCategory.LightArmor => Kind.LightArmor,
            EquipCategory.Robe => Kind.Robe,
            EquipCategory.HeavyHelm => Kind.HeavyHelm,
            EquipCategory.Hood => Kind.Hood,
            EquipCategory.Hat => Kind.Hat,
            EquipCategory.Ring => Kind.Ring,
            _ => Kind.Amulet,
        };

        // ------------------------------------------------------------------ desenho

        float S => Mathf.Min(contentRect.width, contentRect.height);
        Vector2 P(float x, float y) => new(contentRect.x + x * S, contentRect.y + y * S);

        void Draw(MeshGenerationContext ctx)
        {
            if (S <= 1f) return;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            RoundRect(p, 0.04f, 0.04f, 0.92f, 0.92f, 0.22f);
            p.fillColor = new Color(0.1f, 0.1f, 0.2f, 0.92f);
            p.Fill();
            p.strokeColor = frame;
            p.lineWidth = Mathf.Max(1.5f, S * 0.07f);
            p.Stroke();

            switch (kind)
            {
                case Kind.Sword:
                    Line(p, 0.3f, 0.7f, 0.78f, 0.22f, 0.13f, Steel);
                    Line(p, 0.22f, 0.58f, 0.42f, 0.78f, 0.08f, GoldColor);
                    Line(p, 0.3f, 0.7f, 0.18f, 0.82f, 0.08f, Wood);
                    break;
                case Kind.Dagger:
                    Line(p, 0.36f, 0.64f, 0.68f, 0.32f, 0.12f, Steel);
                    Line(p, 0.3f, 0.54f, 0.46f, 0.7f, 0.07f, GoldColor);
                    Line(p, 0.36f, 0.64f, 0.24f, 0.76f, 0.08f, Wood);
                    break;
                case Kind.Axe:
                    Line(p, 0.3f, 0.82f, 0.6f, 0.2f, 0.08f, Wood);
                    Poly(p, Steel, (0.52f, 0.2f), (0.82f, 0.28f), (0.78f, 0.52f), (0.55f, 0.42f));
                    break;
                case Kind.Staff:
                    Line(p, 0.3f, 0.84f, 0.6f, 0.32f, 0.08f, Wood);
                    Circle(p, 0.65f, 0.26f, 0.13f, accent);
                    Circle(p, 0.61f, 0.22f, 0.04f, Color.white);
                    break;
                case Kind.Mace:
                    Line(p, 0.3f, 0.82f, 0.56f, 0.38f, 0.08f, Wood);
                    Circle(p, 0.62f, 0.3f, 0.16f, Steel);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        Circle(p, 0.62f + Mathf.Cos(a) * 0.17f, 0.3f + Mathf.Sin(a) * 0.17f, 0.04f, Steel * 0.85f);
                    }
                    break;
                case Kind.HeavyArmor:
                case Kind.LightArmor:
                {
                    var body = kind == Kind.HeavyArmor ? Steel : Leather;
                    Poly(p, body, (0.24f, 0.26f), (0.4f, 0.2f), (0.5f, 0.3f), (0.6f, 0.2f), (0.76f, 0.26f), (0.72f, 0.78f), (0.28f, 0.78f));
                    Line(p, 0.5f, 0.34f, 0.5f, 0.74f, 0.03f, Ink * 0.9f + new Color(0, 0, 0, 0.1f));
                    Circle(p, 0.5f, 0.52f, 0.06f, accent);
                    break;
                }
                case Kind.Robe:
                    Poly(p, accent, (0.38f, 0.2f), (0.62f, 0.2f), (0.8f, 0.82f), (0.2f, 0.82f));
                    Poly(p, GoldColor, (0.42f, 0.2f), (0.58f, 0.2f), (0.5f, 0.34f));
                    Line(p, 0.26f, 0.7f, 0.74f, 0.7f, 0.04f, GoldColor);
                    break;
                case Kind.HeavyHelm:
                    Arc(p, 0.5f, 0.56f, 0.3f, Steel);
                    Poly(p, Steel, (0.2f, 0.56f), (0.8f, 0.56f), (0.76f, 0.76f), (0.24f, 0.76f));
                    Line(p, 0.32f, 0.6f, 0.68f, 0.6f, 0.05f, Ink);
                    Line(p, 0.5f, 0.24f, 0.5f, 0.14f, 0.06f, accent);
                    break;
                case Kind.Hood:
                    Poly(p, accent, (0.5f, 0.16f), (0.8f, 0.5f), (0.74f, 0.82f), (0.26f, 0.82f), (0.2f, 0.5f));
                    Circle(p, 0.5f, 0.56f, 0.15f, new Color(0.12f, 0.1f, 0.18f));
                    break;
                case Kind.Hat:
                    Poly(p, accent, (0.56f, 0.14f), (0.7f, 0.66f), (0.34f, 0.66f));
                    Poly(p, accent * 0.85f + new Color(0, 0, 0, 0.15f), (0.16f, 0.7f), (0.84f, 0.7f), (0.76f, 0.8f), (0.24f, 0.8f));
                    Line(p, 0.36f, 0.62f, 0.68f, 0.62f, 0.05f, GoldColor);
                    break;
                case Kind.Ring:
                    RingShape(p, 0.5f, 0.58f, 0.2f, GoldColor);
                    Circle(p, 0.5f, 0.34f, 0.11f, accent);
                    Circle(p, 0.47f, 0.31f, 0.035f, Color.white);
                    break;
                case Kind.Amulet:
                    Line(p, 0.26f, 0.18f, 0.5f, 0.52f, 0.04f, GoldColor);
                    Line(p, 0.74f, 0.18f, 0.5f, 0.52f, 0.04f, GoldColor);
                    Circle(p, 0.5f, 0.62f, 0.15f, accent);
                    Circle(p, 0.46f, 0.58f, 0.045f, Color.white);
                    break;
                case Kind.Potion:
                    Line(p, 0.5f, 0.18f, 0.5f, 0.32f, 0.12f, new Color(0.85f, 0.88f, 0.95f));
                    Circle(p, 0.5f, 0.6f, 0.24f, new Color(0.85f, 0.88f, 0.95f));
                    Circle(p, 0.5f, 0.62f, 0.19f, accent);
                    Circle(p, 0.43f, 0.55f, 0.05f, Color.white);
                    Line(p, 0.42f, 0.17f, 0.58f, 0.17f, 0.07f, Wood);
                    break;
            }
        }

        void Line(Painter2D p, float x0, float y0, float x1, float y1, float width, Color color)
        {
            // Contorno escuro por baixo deixa o pictograma legível em qualquer fundo.
            p.strokeColor = Ink;
            p.lineWidth = (width + 0.05f) * S;
            p.BeginPath();
            p.MoveTo(P(x0, y0));
            p.LineTo(P(x1, y1));
            p.Stroke();
            p.strokeColor = color;
            p.lineWidth = width * S;
            p.BeginPath();
            p.MoveTo(P(x0, y0));
            p.LineTo(P(x1, y1));
            p.Stroke();
        }

        void Poly(Painter2D p, Color color, params (float x, float y)[] points)
        {
            p.BeginPath();
            p.MoveTo(P(points[0].x, points[0].y));
            for (int i = 1; i < points.Length; i++) p.LineTo(P(points[i].x, points[i].y));
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
            p.strokeColor = Ink;
            p.lineWidth = 0.045f * S;
            p.Stroke();
        }

        void Circle(Painter2D p, float cx, float cy, float r, Color color)
        {
            p.BeginPath();
            const int segments = 20;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var v = P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
            if (r > 0.06f)
            {
                p.strokeColor = Ink;
                p.lineWidth = 0.04f * S;
                p.Stroke();
            }
        }

        void Arc(Painter2D p, float cx, float cy, float r, Color color)
        {
            p.BeginPath();
            const int segments = 16;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI + i / (float)segments * Mathf.PI;
                var v = P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
            p.fillColor = color;
            p.Fill();
            p.strokeColor = Ink;
            p.lineWidth = 0.045f * S;
            p.Stroke();
        }

        void RingShape(Painter2D p, float cx, float cy, float r, Color color)
        {
            var points = new List<Vector2>();
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                points.Add(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            foreach (var (width, c) in new[] { (0.16f, Ink), (0.1f, color) })
            {
                p.strokeColor = c;
                p.lineWidth = width * S;
                p.BeginPath();
                p.MoveTo(points[0]);
                for (int i = 1; i < points.Count; i++) p.LineTo(points[i]);
                p.Stroke();
            }
        }

        void RoundRect(Painter2D p, float x, float y, float w, float h, float r)
        {
            p.BeginPath();
            const int corner = 5;
            void CornerArc(float cx, float cy, float startAngle)
            {
                for (int i = 0; i <= corner; i++)
                {
                    float a = startAngle + i / (float)corner * Mathf.PI * 0.5f;
                    var v = P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                    if (i == 0 && startAngle == Mathf.PI) p.MoveTo(v);
                    else p.LineTo(v);
                }
            }
            CornerArc(x + r, y + r, Mathf.PI);
            CornerArc(x + w - r, y + r, Mathf.PI * 1.5f);
            CornerArc(x + w - r, y + h - r, 0f);
            CornerArc(x + r, y + h - r, Mathf.PI * 0.5f);
            p.ClosePath();
        }
    }
}
