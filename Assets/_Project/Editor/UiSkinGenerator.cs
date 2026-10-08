using System.IO;
using UnityEditor;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>
    /// Gera as texturas 9-slice da interface (painéis com moldura creme, botão dourado, barras com brilho)
    /// em Resources/UI/Skin — o USS referencia com resource("UI/Skin/...").
    /// </summary>
    public static class UiSkinGenerator
    {
        const string Folder = "Assets/_Project/Resources/UI/Skin";

        struct Style
        {
            public int Width, Height;
            public float Radius;
            public Color Outline;
            public float OutlineWidth;
            public Color Border;
            public float BorderWidth;
            public Color Top, Bottom;
            public Color Highlight;
            public float HighlightBand;
        }

        [MenuItem("OiramRPG/Interface/Gerar molduras da UI")]
        public static void Generate()
        {
            EditorFolders.Ensure(Folder);
            var ink = new Color(0.07f, 0.05f, 0.12f);
            var cream = new Color(1f, 0.94f, 0.8f);
            var gold = new Color(1f, 0.8f, 0.3f);

            Write("panel", new Style
            {
                Width = 64, Height = 64, Radius = 18, Outline = ink, OutlineWidth = 2.5f, Border = cream, BorderWidth = 3f,
                Top = new Color(0.22f, 0.23f, 0.46f, 0.94f), Bottom = new Color(0.12f, 0.13f, 0.29f, 0.94f),
                Highlight = new Color(1f, 1f, 1f, 0.1f), HighlightBand = 6f,
            });
            Write("panel_gold", new Style
            {
                Width = 64, Height = 64, Radius = 18, Outline = ink, OutlineWidth = 2.5f, Border = gold, BorderWidth = 3.5f,
                Top = new Color(0.27f, 0.25f, 0.46f, 0.95f), Bottom = new Color(0.15f, 0.13f, 0.29f, 0.95f),
                Highlight = new Color(1f, 0.9f, 0.6f, 0.14f), HighlightBand = 6f,
            });
            Write("panel_light", new Style
            {
                Width = 48, Height = 48, Radius = 12, Outline = new Color(ink.r, ink.g, ink.b, 0.8f), OutlineWidth = 1.5f,
                Border = new Color(cream.r, cream.g, cream.b, 0.5f), BorderWidth = 2f,
                Top = new Color(0.21f, 0.23f, 0.44f, 0.86f), Bottom = new Color(0.14f, 0.15f, 0.31f, 0.86f),
                Highlight = new Color(1f, 1f, 1f, 0.07f), HighlightBand = 4f,
            });
            Write("button_gold", new Style
            {
                Width = 48, Height = 48, Radius = 12, Outline = new Color(0.32f, 0.17f, 0.03f), OutlineWidth = 2f,
                Border = new Color(1f, 0.96f, 0.75f), BorderWidth = 1.5f,
                Top = new Color(1f, 0.88f, 0.45f), Bottom = new Color(0.98f, 0.66f, 0.2f),
                Highlight = new Color(1f, 1f, 1f, 0.35f), HighlightBand = 9f,
            });
            Write("tab", new Style
            {
                Width = 48, Height = 48, Radius = 14, Outline = new Color(ink.r, ink.g, ink.b, 0.85f), OutlineWidth = 2f,
                Border = new Color(cream.r, cream.g, cream.b, 0.35f), BorderWidth = 2f,
                Top = new Color(0.25f, 0.27f, 0.5f, 0.9f), Bottom = new Color(0.16f, 0.17f, 0.34f, 0.9f),
                Highlight = new Color(1f, 1f, 1f, 0.08f), HighlightBand = 6f,
            });
            Write("bar_track", new Style
            {
                Width = 32, Height = 16, Radius = 7.5f, Outline = new Color(0.03f, 0.02f, 0.06f, 0.9f), OutlineWidth = 1.5f,
                Border = new Color(0f, 0f, 0f, 0f), BorderWidth = 0f,
                Top = new Color(0.02f, 0.02f, 0.06f, 0.85f), Bottom = new Color(0.1f, 0.1f, 0.18f, 0.85f),
            });
            Fill("bar_green", new Color(0.55f, 1f, 0.5f), new Color(0.18f, 0.66f, 0.3f));
            Fill("bar_red", new Color(1f, 0.55f, 0.45f), new Color(0.8f, 0.2f, 0.18f));
            Fill("bar_blue", new Color(0.6f, 0.85f, 1f), new Color(0.22f, 0.48f, 0.95f));
            Fill("bar_gold", new Color(1f, 0.92f, 0.5f), new Color(0.98f, 0.62f, 0.15f));
            Fill("bar_white", Color.white, new Color(0.85f, 0.88f, 0.95f));
            AssetDatabase.Refresh();
        }

        static void Fill(string name, Color top, Color bottom) => Write(name, new Style
        {
            Width = 32, Height = 16, Radius = 7f, Outline = new Color(0, 0, 0, 0), OutlineWidth = 0f, Border = new Color(0, 0, 0, 0),
            Top = top, Bottom = bottom, Highlight = new Color(1f, 1f, 1f, 0.35f), HighlightBand = 5f,
        });

        static void Write(string name, Style s)
        {
            var tex = new Texture2D(s.Width, s.Height, TextureFormat.RGBA32, false);
            var pixels = new Color[s.Width * s.Height];
            var half = new Vector2(s.Width / 2f, s.Height / 2f);
            for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) - half;
                var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (half - Vector2.one * s.Radius);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - s.Radius;
                float coverage = Mathf.Clamp01(0.5f - d);
                float depth = -d;
                float topness = (y + 0.5f) / s.Height; // 1 = topo (texturas da Unity crescem para cima)

                Color c;
                if (depth < s.OutlineWidth) c = s.Outline;
                else if (depth < s.OutlineWidth + s.BorderWidth) c = s.Border;
                else
                {
                    c = Color.Lerp(s.Bottom, s.Top, topness);
                    float fromTop = s.Height - (y + 0.5f);
                    if (s.HighlightBand > 0f && fromTop < s.OutlineWidth + s.BorderWidth + s.HighlightBand)
                        c = Color.Lerp(c, new Color(1f, 1f, 1f, c.a), s.Highlight.a);
                }
                // Suaviza a transição entre camadas.
                c.a *= coverage;
                pixels[y * s.Width + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
    }
}
