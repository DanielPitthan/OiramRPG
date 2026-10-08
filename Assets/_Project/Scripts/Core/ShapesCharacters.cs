using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Heróis e moradores chibi: cabeça grande, rosto expressivo, chapéu e arma do job, rig animado.</summary>
    public static partial class Shapes
    {
        static Mesh Round(float radius = 0.2f) => MeshLibrary.RoundedBox(radius);

        public static Transform Pivot(Transform parent, string name, Vector3 localPos, Vector3 localEuler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localEulerAngles = localEuler;
            return t;
        }

        /// <summary>Olho estilo anime: branco, pupila e brilho. Devolve o pivô (o rig "pisca" escalando o Y).</summary>
        public static Transform Eye(Transform parent, Vector3 localPos, float size, Color iris, bool glowing = false)
        {
            var eye = Pivot(parent, "Eye", localPos);
            if (glowing)
            {
                Part(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.15f, 0.17f, 0.07f) * size, Palette.Emissive(iris, 2.2f));
                Part(PrimitiveType.Sphere, eye, new Vector3(0f, 0f, 0.03f) * size, new Vector3(0.04f, 0.12f, 0.04f) * size, Palette.Plain(new Color(0.1f, 0.03f, 0.05f)));
                return eye;
            }
            Part(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.15f, 0.19f, 0.08f) * size, Palette.Plain(Color.white));
            Part(PrimitiveType.Sphere, eye, new Vector3(0f, -0.012f, 0.03f) * size, new Vector3(0.095f, 0.13f, 0.05f) * size, Palette.Plain(iris));
            Part(PrimitiveType.Sphere, eye, new Vector3(0.022f, 0.03f, 0.055f) * size, Vector3.one * 0.045f * size, Palette.Emissive(Color.white, 0.6f));
            return eye;
        }

        public static Transform[] Eyes(Transform parent, Vector3 center, float spacing, float size, Color iris, bool glowing = false) => new[]
        {
            Eye(parent, center + Vector3.left * spacing, size, iris, glowing),
            Eye(parent, center + Vector3.right * spacing, size, iris, glowing),
        };

        static Color HairFor(Color c)
        {
            if (c.r > 0.75f && c.g > 0.6f) return new Color(0.96f, 0.8f, 0.42f);  // loiro
            if (c.b > c.r) return new Color(0.16f, 0.14f, 0.22f);                   // preto azulado
            return new Color(0.42f, 0.24f, 0.14f);                                   // castanho
        }

        /// <summary>
        /// Herói/morador. <paramref name="characterColor"/> = boné/cachecol; <paramref name="jobColor"/> = roupa;
        /// <paramref name="jobId"/> escolhe chapéu e arma (null = morador). Frente = +Z, pés em y = 0.
        /// </summary>
        public static Transform Hero(Transform parent, Color characterColor, Color jobColor, string jobId = null)
        {
            var root = Root(parent, "Visual");
            var hair = HairFor(characterColor);
            var pants = Color.Lerp(jobColor * 0.45f, Palette.DarkWood, 0.5f);
            var shoe = new Color(0.32f, 0.2f, 0.13f);

            // Pernas e sapatos.
            var legs = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.12f : 0.12f;
                var leg = Pivot(root, side == 0 ? "LegL" : "LegR", new Vector3(x, 0.24f, 0f));
                Part(PrimitiveType.Capsule, leg, new Vector3(0f, -0.09f, 0f), new Vector3(0.16f, 0.12f, 0.16f), pants);
                Part(Round(0.3f), leg, new Vector3(0f, -0.185f, 0.04f), new Vector3(0.19f, 0.11f, 0.27f), shoe);
                legs[side] = leg;
            }

            // Tronco.
            var body = Pivot(root, "Body", new Vector3(0f, 0.24f, 0f));
            Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.27f, 0f), new Vector3(0.54f, 0.3f, 0.48f), jobColor);
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.13f, 0f), new Vector3(0.56f, 0.03f, 0.5f), new Color(0.36f, 0.22f, 0.14f));
            Part(Round(0.25f), body, new Vector3(0f, 0.13f, 0.25f), new Vector3(0.11f, 0.08f, 0.04f), Palette.Glossy(Palette.Gold, 0f));
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 0.05f, 0.38f), characterColor);

            // Braços (o direito segura a arma).
            var arms = new Transform[2];
            Transform hand = null;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                var arm = Pivot(body, side == 0 ? "ArmL" : "ArmR", new Vector3(0.29f * sign, 0.46f, 0f), new Vector3(0f, 0f, 8f * sign));
                Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.14f, 0f), new Vector3(0.15f, 0.15f, 0.15f), jobColor * 0.9f);
                var h = Part(PrimitiveType.Sphere, arm, new Vector3(0f, -0.3f, 0.01f), Vector3.one * 0.15f, Palette.Skin);
                if (side == 1) hand = h.transform;
                arms[side] = arm;
            }

            // Cabeça.
            var head = Pivot(body, "Head", new Vector3(0f, 0.56f, 0f));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.66f, Palette.Skin);
            Part(PrimitiveType.Sphere, head, new Vector3(-0.33f, 0.29f, 0f), Vector3.one * 0.12f, Palette.Skin);
            Part(PrimitiveType.Sphere, head, new Vector3(0.33f, 0.29f, 0f), Vector3.one * 0.12f, Palette.Skin);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.37f, -0.07f), new Vector3(0.7f, 0.64f, 0.66f), hair);
            var eyes = Eyes(head, new Vector3(0f, 0.31f, 0.3f), 0.115f, 1f, new Color(0.12f, 0.1f, 0.16f));
            foreach (float x in new[] { -0.2f, 0.2f })
                Part(PrimitiveType.Sphere, head, new Vector3(x, 0.215f, 0.27f), new Vector3(0.1f, 0.05f, 0.04f), Palette.Plain(Palette.Blush));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.175f, 0.315f), new Vector3(0.08f, 0.03f, 0.03f), Palette.Plain(new Color(0.45f, 0.12f, 0.12f)));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.25f, 0.34f), Vector3.one * 0.055f, Palette.Plain(Palette.Skin * 0.93f));
            foreach (float x in new[] { -0.12f, 0.12f })
                Part(Round(0.4f), head, new Vector3(x, 0.425f, 0.3f), new Vector3(0.1f, 0.025f, 0.02f), Palette.Plain(hair * 0.8f), new Vector3(0f, 0f, x * -60f));
            // Franja.
            for (int i = -1; i <= 1; i++)
                Part(PrimitiveType.Sphere, head, new Vector3(i * 0.15f, 0.53f - Mathf.Abs(i) * 0.03f, 0.2f), new Vector3(0.22f, 0.16f, 0.18f), hair);

            Hat(head, jobId, characterColor, jobColor);
            if (hand != null) Weapon(hand, jobId, jobColor);

            root.gameObject.AddComponent<CharacterRig>().Bind(body, head, arms[0], arms[1], legs[0], legs[1], eyes);
            return root;
        }

        static void Hat(Transform head, string jobId, Color characterColor, Color jobColor)
        {
            switch (jobId)
            {
                case "guardiao":
                {
                    var steel = Palette.Glossy(new Color(0.72f, 0.76f, 0.84f));
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.48f, -0.02f), new Vector3(0.74f, 0.56f, 0.74f), steel);
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.45f, 0f), new Vector3(0.76f, 0.035f, 0.76f), Palette.Glossy(new Color(0.6f, 0.63f, 0.72f)));
                    Part(Round(0.4f), head, new Vector3(0f, 0.76f, -0.04f), new Vector3(0.09f, 0.22f, 0.5f), characterColor);
                    break;
                }
                case "arcanista":
                {
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.5f, 0f), new Vector3(0.86f, 0.025f, 0.86f), jobColor);
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.545f, 0f), new Vector3(0.54f, 0.05f, 0.54f), Palette.Gold);
                    Part(MeshLibrary.Cone(), head, new Vector3(0.02f, 0.86f, -0.05f), new Vector3(0.52f, 0.66f, 0.52f), jobColor, new Vector3(-14f, 0f, -10f));
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.6f, 0.26f), Vector3.one * 0.09f, Palette.Emissive(Palette.Gold, 1.4f));
                    break;
                }
                case "clerigo":
                {
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.36f, -0.11f), new Vector3(0.8f, 0.76f, 0.74f), jobColor);
                    Part(Round(0.3f), head, new Vector3(0f, 0.78f, -0.02f), new Vector3(0.06f, 0.2f, 0.06f), Palette.Glossy(Palette.Gold, 0f));
                    Part(Round(0.3f), head, new Vector3(0f, 0.8f, -0.02f), new Vector3(0.16f, 0.05f, 0.06f), Palette.Glossy(Palette.Gold, 0f));
                    break;
                }
                case "ladino":
                {
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.46f, 0f), new Vector3(0.7f, 0.07f, 0.7f), jobColor, new Vector3(-8f, 0f, 0f));
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.47f, -0.35f), Vector3.one * 0.12f, jobColor * 0.85f);
                    Part(Round(0.4f), head, new Vector3(0.06f, 0.36f, -0.38f), new Vector3(0.08f, 0.2f, 0.03f), jobColor * 0.85f, new Vector3(0f, 0f, -20f));
                    Part(Round(0.4f), head, new Vector3(-0.06f, 0.35f, -0.38f), new Vector3(0.08f, 0.2f, 0.03f), jobColor * 0.85f, new Vector3(0f, 0f, 25f));
                    break;
                }
                case "aprendiz":
                {
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.5f, -0.01f), new Vector3(0.7f, 0.42f, 0.7f), characterColor);
                    Part(Round(0.4f), head, new Vector3(0f, 0.47f, 0.3f), new Vector3(0.48f, 0.06f, 0.3f), characterColor * 0.92f, new Vector3(8f, 0f, 0f));
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.7f, -0.01f), Vector3.one * 0.09f, characterColor * 0.8f);
                    break;
                }
                default:
                {
                    // Morador: chapéu de palha numa cor alegre.
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.55f, 0f), new Vector3(0.84f, 0.02f, 0.84f), new Color(0.93f, 0.8f, 0.5f));
                    Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.3f, 0.5f), new Color(0.93f, 0.8f, 0.5f));
                    Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.6f, 0f), new Vector3(0.51f, 0.04f, 0.51f), characterColor);
                    break;
                }
            }
        }

        static void Weapon(Transform hand, string jobId, Color jobColor)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            var grip = Pivot(hand.parent, "Weapon", hand.localPosition, new Vector3(80f, 0f, 0f));
            var steel = Palette.Glossy(new Color(0.82f, 0.86f, 0.92f));
            switch (jobId)
            {
                case "arcanista":
                case "clerigo":
                {
                    grip.localEulerAngles = new Vector3(15f, 0f, 0f);
                    Part(PrimitiveType.Cylinder, grip, new Vector3(0f, 0.12f, 0f), new Vector3(0.05f, 0.36f, 0.05f), Palette.Wood);
                    if (jobId == "arcanista")
                    {
                        var orb = Part(MeshLibrary.Icosphere(1), grip, new Vector3(0f, 0.52f, 0f), Vector3.one * 0.17f, Palette.Emissive(Color.Lerp(jobColor, Color.white, 0.4f), 2f));
                        Wiggle.Add(orb, Wiggle.Mode.Spin, Vector3.up, 90f, 1f);
                    }
                    else
                    {
                        Part(PrimitiveType.Cylinder, grip, new Vector3(0f, 0.52f, 0f), new Vector3(0.2f, 0.015f, 0.2f), Palette.Glossy(Palette.Gold, 0f), new Vector3(90f, 0f, 0f));
                        Part(PrimitiveType.Sphere, grip, new Vector3(0f, 0.52f, 0f), Vector3.one * 0.09f, Palette.Emissive(new Color(1f, 0.95f, 0.75f), 1.6f));
                    }
                    break;
                }
                case "ladino":
                    Part(PrimitiveType.Cylinder, grip, new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.06f, 0.05f), Palette.DarkWood);
                    Part(Round(0.3f), grip, new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.03f, 0.05f), Palette.Gold);
                    Part(Round(0.4f), grip, new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.22f, 0.02f), steel);
                    break;
                default:
                {
                    var blade = jobId == "aprendiz" ? Palette.Toon(new Color(0.78f, 0.6f, 0.4f)) : steel;
                    Part(PrimitiveType.Cylinder, grip, new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.07f, 0.05f), Palette.DarkWood);
                    Part(Round(0.3f), grip, new Vector3(0f, 0.1f, 0f), new Vector3(0.22f, 0.04f, 0.06f), jobId == "aprendiz" ? Palette.Get(Palette.DarkWood) : Palette.Glossy(Palette.Gold, 0f));
                    Part(Round(0.4f), grip, new Vector3(0f, 0.34f, 0f), new Vector3(0.075f, 0.44f, 0.025f), blade);
                    break;
                }
            }
        }
    }
}
