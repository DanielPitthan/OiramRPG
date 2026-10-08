using Oiram.Battle;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Inimigos: formas arredondadas, olhos expressivos (ou brilhantes) e partes animadas com <see cref="Wiggle"/>.</summary>
    public static partial class Shapes
    {
        static readonly Color Dark = new(0.1f, 0.06f, 0.08f);
        static readonly Color Bone = new(0.93f, 0.9f, 0.82f);

        static void Smile(Transform parent, Vector3 pos, float width = 0.12f) =>
            Part(PrimitiveType.Sphere, parent, pos, new Vector3(width, 0.04f, 0.04f), Palette.Plain(Dark));

        public static Transform Enemy(Transform parent, UnitShape shape, Color color, float scale)
        {
            var root = Root(parent, "Visual");
            switch (shape)
            {
                case UnitShape.Slime: Slime(root, color, crown: false); break;
                case UnitShape.KingSlime: Slime(root, color, crown: true); break;
                case UnitShape.Bat: Bat(root, color); break;
                case UnitShape.Goblin: Goblin(root, color); break;
                case UnitShape.Mimic: Mimic(root); break;
                case UnitShape.Golem: Golem(root, color); break;
                case UnitShape.Spider: Spider(root, color); break;
                case UnitShape.Skeleton: Skeleton(root, color); break;
                case UnitShape.Ghost: Ghost(root, color); break;
                case UnitShape.Elemental: Elemental(root, color); break;
                case UnitShape.Knight: Knight(root, color); break;
                default: Part(PrimitiveType.Capsule, root, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.6f, 0.6f), color); break;
            }
            root.localScale = Vector3.one * scale;
            return root;
        }

        static void Slime(Transform root, Color color, bool crown)
        {
            var body = Pivot(root, "Body", Vector3.zero);
            Wiggle.Add(body.gameObject, Wiggle.Mode.Pulse, Vector3.up, 0.05f, 4f);
            var jelly = Palette.Glossy(color);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.38f, 0f), new Vector3(1f, 0.76f, 1f), jelly);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.18f, 0f), new Vector3(1.06f, 0.36f, 1.06f), jelly);
            Eyes(body, new Vector3(0f, 0.47f, 0.41f), 0.17f, 1.05f, Dark);
            Smile(body, new Vector3(0f, 0.33f, 0.47f));
            if (!crown)
            {
                for (int i = 0; i < 5; i++)
                {
                    float a = (i * 72f + 36f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a) - 0.25f);
                    Part(MeshLibrary.Cone(8), body, new Vector3(dir.x * 0.27f, 0.7f, dir.z * 0.27f), new Vector3(0.17f, 0.24f, 0.17f), color * 0.72f,
                        new Vector3(dir.z * 25f, 0f, -dir.x * 25f));
                }
                return;
            }
            var gold = Palette.Glossy(Palette.Gold);
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.8f, 0f), new Vector3(0.46f, 0.07f, 0.46f), gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Part(MeshLibrary.Cone(6), body, new Vector3(Mathf.Sin(a) * 0.19f, 0.95f, Mathf.Cos(a) * 0.19f), new Vector3(0.1f, 0.2f, 0.1f), gold);
            }
            Part(MeshLibrary.Icosphere(0), body, new Vector3(0f, 0.82f, 0.23f), Vector3.one * 0.1f, Palette.Emissive(new Color(1f, 0.2f, 0.35f), 1.8f));
        }

        static void Bat(Transform root, Color color)
        {
            Part(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.55f, color);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.38f, 0.34f, 0.38f), Color.Lerp(color, Color.white, 0.25f));
            foreach (float s in new[] { -1f, 1f })
            {
                Part(MeshLibrary.Cone(6), root, new Vector3(0.14f * s, 0.3f, 0f), new Vector3(0.14f, 0.24f, 0.14f), color * 0.85f, new Vector3(0f, 0f, -15f * s));
                var wing = Pivot(root, "Wing", new Vector3(0.22f * s, 0.05f, 0f));
                Part(Round(0.5f), wing, new Vector3(0.3f * s, 0f, 0f), new Vector3(0.62f, 0.04f, 0.38f), color * 0.7f);
                Part(PrimitiveType.Sphere, wing, new Vector3(0.58f * s, 0f, -0.12f), new Vector3(0.12f, 0.05f, 0.2f), color * 0.7f);
                Wiggle.Add(wing.gameObject, Wiggle.Mode.Swing, Vector3.forward * s, 38f, 14f);
                Part(MeshLibrary.Cone(6), root, new Vector3(0.05f * s, -0.13f, 0.24f), new Vector3(0.04f, 0.08f, 0.04f), Palette.Plain(Color.white), new Vector3(180f, 0f, 0f));
            }
            Eyes(root, new Vector3(0f, 0.07f, 0.24f), 0.1f, 0.75f, new Color(1f, 0.3f, 0.25f), glowing: true);
        }

        static void Goblin(Transform root, Color color)
        {
            var tunic = new Color(0.5f, 0.32f, 0.2f);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Capsule, root, new Vector3(0.13f * s, 0.15f, 0f), new Vector3(0.17f, 0.14f, 0.17f), color * 0.8f);
                Part(Round(0.3f), root, new Vector3(0.13f * s, 0.04f, 0.05f), new Vector3(0.2f, 0.09f, 0.27f), Palette.DarkWood);
            }
            Part(PrimitiveType.Capsule, root, new Vector3(0f, 0.5f, 0f), new Vector3(0.58f, 0.3f, 0.5f), tunic);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.38f, 0f), new Vector3(0.6f, 0.03f, 0.52f), Palette.DarkWood);

            var head = Pivot(root, "Head", new Vector3(0f, 0.86f, 0f));
            Wiggle.Add(head.gameObject, Wiggle.Mode.Swing, Vector3.forward, 4f, 3f);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.6f, color);
            foreach (float s in new[] { -1f, 1f })
            {
                var ear = Pivot(head, "Ear", new Vector3(0.27f * s, 0.27f, 0f), new Vector3(0f, 0f, -70f * s));
                Part(MeshLibrary.Cone(8), ear, new Vector3(0f, 0.17f, 0f), new Vector3(0.14f, 0.36f, 0.1f), color);
                Wiggle.Add(ear.gameObject, Wiggle.Mode.Swing, Vector3.forward, 6f, 5f, s);
            }
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.16f, 0.3f), new Vector3(0.16f, 0.14f, 0.2f), color * 0.85f);
            Eyes(head, new Vector3(0f, 0.3f, 0.24f), 0.13f, 0.85f, new Color(0.95f, 0.8f, 0.15f));
            Smile(head, new Vector3(0f, 0.06f, 0.27f), 0.16f);
            Part(MeshLibrary.Cone(6), head, new Vector3(0.05f, 0.07f, 0.28f), new Vector3(0.04f, 0.06f, 0.04f), Palette.Plain(Color.white));

            var arm = Pivot(root, "Arm", new Vector3(0.3f, 0.62f, 0f), new Vector3(0f, 0f, 15f));
            Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.13f, 0f), new Vector3(0.14f, 0.14f, 0.14f), color);
            Part(MeshLibrary.Cone(8), arm, new Vector3(0f, -0.3f, 0.2f), new Vector3(0.18f, 0.45f, 0.18f), Palette.Wood, new Vector3(-70f, 0f, 0f));
            Wiggle.Add(arm.gameObject, Wiggle.Mode.Swing, Vector3.right, 12f, 3.5f);
            var other = Pivot(root, "Arm", new Vector3(-0.3f, 0.62f, 0f), new Vector3(0f, 0f, -15f));
            Part(PrimitiveType.Capsule, other, new Vector3(0f, -0.13f, 0f), new Vector3(0.14f, 0.14f, 0.14f), color);
        }

        static void Mimic(Transform root)
        {
            Part(Round(0.12f), root, new Vector3(0f, 0.25f, 0f), new Vector3(0.92f, 0.5f, 0.62f), Palette.Wood);
            Part(Round(0.2f), root, new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.52f, 0.65f), Palette.Glossy(Palette.Gold, 0f));
            var lid = Pivot(root, "Lid", new Vector3(0f, 0.5f, -0.3f), new Vector3(-38f, 0f, 0f));
            Part(Round(0.15f), lid, new Vector3(0f, 0.1f, 0.3f), new Vector3(0.94f, 0.22f, 0.64f), Palette.DarkWood);
            Wiggle.Add(lid.gameObject, Wiggle.Mode.Swing, Vector3.right, 9f, 6f);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.32f + i * 0.16f;
                Part(MeshLibrary.Cone(6), lid, new Vector3(x, -0.03f, 0.56f), new Vector3(0.09f, 0.12f, 0.09f), Palette.Plain(Color.white), new Vector3(180f, 0f, 0f));
                Part(MeshLibrary.Cone(6), root, new Vector3(x + 0.08f, 0.55f, 0.24f), new Vector3(0.08f, 0.1f, 0.08f), Palette.Plain(Color.white));
            }
            var tongue = Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.52f, 0.12f), new Vector3(0.32f, 0.06f, 0.42f), new Color(0.9f, 0.3f, 0.4f), new Vector3(-12f, 0f, 0f));
            Wiggle.Add(tongue, Wiggle.Mode.Bob, Vector3.up, 0.02f, 7f);
            Eyes(lid, new Vector3(0f, 0.26f, 0.5f), 0.17f, 0.85f, new Color(1f, 0.3f, 0.2f), glowing: true);
        }

        static void Golem(Transform root, Color color)
        {
            foreach (float s in new[] { -1f, 1f })
                Part(Round(0.25f), root, new Vector3(0.28f * s, 0.16f, 0f), new Vector3(0.4f, 0.34f, 0.48f), color * 0.8f);
            Part(Round(0.18f), root, new Vector3(0f, 0.8f, 0f), new Vector3(1.1f, 0.95f, 0.75f), color);
            var core = Part(MeshLibrary.Icosphere(1), root, new Vector3(0f, 0.86f, 0.36f), Vector3.one * 0.3f, Palette.Emissive(new Color(0.3f, 0.85f, 1f), 2.2f));
            Wiggle.Add(core, Wiggle.Mode.Pulse, Vector3.up, 0.08f, 3f);
            Part(Round(0.22f), root, new Vector3(0f, 1.5f, 0.05f), new Vector3(0.56f, 0.46f, 0.5f), color * 1.08f);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(Round(0.4f), root, new Vector3(0.13f * s, 1.53f, 0.3f), new Vector3(0.12f, 0.07f, 0.04f), Palette.Emissive(new Color(1f, 0.85f, 0.25f), 2.4f));
                Part(MeshLibrary.Icosphere(1, true, 0.12f, s > 0 ? 3 : 4), root, new Vector3(0.62f * s, 1.18f, 0f), Vector3.one * 0.48f, color * 0.95f);
                Part(PrimitiveType.Sphere, root, new Vector3(0.62f * s, 1.38f, 0f), new Vector3(0.3f, 0.08f, 0.26f), new Color(0.4f, 0.65f, 0.32f));
                var arm = Pivot(root, "Arm", new Vector3(0.74f * s, 1.12f, 0.05f));
                Part(Round(0.25f), arm, new Vector3(0f, -0.38f, 0f), new Vector3(0.36f, 0.8f, 0.42f), color * 0.9f);
                Part(MeshLibrary.Icosphere(1, true, 0.1f, s > 0 ? 5 : 6), arm, new Vector3(0f, -0.82f, 0.05f), Vector3.one * 0.44f, color * 0.85f);
                Wiggle.Add(arm.gameObject, Wiggle.Mode.Swing, Vector3.right, 6f, 1.6f, s);
            }
        }

        static void Spider(Transform root, Color color)
        {
            var body = Pivot(root, "Body", Vector3.zero);
            Wiggle.Add(body.gameObject, Wiggle.Mode.Bob, Vector3.up, 0.02f, 6f);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.44f, -0.2f), new Vector3(0.72f, 0.58f, 0.82f), color);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.6f, -0.24f), new Vector3(0.36f, 0.2f, 0.5f), Palette.Plain(Color.Lerp(color, new Color(0.8f, 0.2f, 0.25f), 0.5f)));
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.38f, 0.26f), Vector3.one * 0.42f, color * 1.15f);
            Eyes(body, new Vector3(0f, 0.46f, 0.45f), 0.08f, 0.55f, new Color(1f, 0.25f, 0.3f), glowing: true);
            Eyes(body, new Vector3(0f, 0.53f, 0.41f), 0.15f, 0.45f, new Color(1f, 0.25f, 0.3f), glowing: true);
            foreach (float s in new[] { -1f, 1f })
                Part(MeshLibrary.Cone(6), body, new Vector3(0.06f * s, 0.24f, 0.44f), new Vector3(0.05f, 0.12f, 0.05f), Palette.Plain(Bone), new Vector3(160f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float z = 0.22f - i * 0.17f;
                foreach (float s in new[] { -1f, 1f })
                {
                    var leg = Pivot(root, "Leg", new Vector3(0.22f * s, 0.42f, z), new Vector3(0f, (i - 1.5f) * 18f * s, 0f));
                    Part(PrimitiveType.Capsule, leg, new Vector3(0.2f * s, 0.08f, 0f), new Vector3(0.07f, 0.2f, 0.07f), color * 0.7f, new Vector3(0f, 0f, -60f * s));
                    Part(PrimitiveType.Capsule, leg, new Vector3(0.42f * s, -0.12f, 0f), new Vector3(0.06f, 0.2f, 0.06f), color * 0.6f, new Vector3(0f, 0f, 25f * s));
                    Wiggle.Add(leg.gameObject, Wiggle.Mode.Swing, Vector3.up, 10f, 9f, i * 1.3f + (s > 0 ? 0f : 3.1f));
                }
            }
        }

        static void Skeleton(Transform root, Color color)
        {
            var bone = Palette.Toon(color);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Capsule, root, new Vector3(0.1f * s, 0.21f, 0f), new Vector3(0.08f, 0.2f, 0.08f), bone);
                Part(Round(0.3f), root, new Vector3(0.1f * s, 0.03f, 0.05f), new Vector3(0.12f, 0.06f, 0.2f), bone);
            }
            Part(Round(0.3f), root, new Vector3(0f, 0.43f, 0f), new Vector3(0.32f, 0.1f, 0.18f), bone);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.65f, 0f), new Vector3(0.07f, 0.2f, 0.07f), bone);
            for (int i = 0; i < 3; i++)
                Part(Round(0.4f), root, new Vector3(0f, 0.58f + i * 0.12f, 0.02f), new Vector3(0.44f - i * 0.04f, 0.045f, 0.24f), bone);

            var head = Pivot(root, "Head", new Vector3(0f, 0.93f, 0f));
            Wiggle.Add(head.gameObject, Wiggle.Mode.Swing, Vector3.forward, 6f, 2.5f);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.22f, 0f), new Vector3(0.48f, 0.46f, 0.48f), bone);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, head, new Vector3(0.1f * s, 0.24f, 0.19f), new Vector3(0.13f, 0.14f, 0.06f), Palette.Plain(Dark));
                Part(PrimitiveType.Sphere, head, new Vector3(0.1f * s, 0.24f, 0.22f), Vector3.one * 0.045f, Palette.Emissive(new Color(0.4f, 0.95f, 1f), 2.5f));
            }
            var jaw = Pivot(head, "Jaw", new Vector3(0f, 0.06f, 0.02f));
            Part(Round(0.35f), jaw, new Vector3(0f, -0.02f, 0.06f), new Vector3(0.3f, 0.09f, 0.26f), bone);
            Wiggle.Add(jaw.gameObject, Wiggle.Mode.Swing, Vector3.right, 10f, 9f);

            var arm = Pivot(root, "Arm", new Vector3(0.24f, 0.8f, 0f), new Vector3(0f, 0f, 12f));
            Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.16f, 0f), new Vector3(0.07f, 0.18f, 0.07f), bone);
            var sword = Pivot(arm, "Sword", new Vector3(0f, -0.34f, 0.03f), new Vector3(70f, 0f, 0f));
            Part(Round(0.3f), sword, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.04f, 0.06f), Palette.Glossy(new Color(0.5f, 0.45f, 0.35f)));
            Part(Round(0.4f), sword, new Vector3(0f, 0.36f, 0f), new Vector3(0.07f, 0.6f, 0.025f), Palette.Glossy(new Color(0.72f, 0.74f, 0.8f)));
            Wiggle.Add(arm.gameObject, Wiggle.Mode.Swing, Vector3.right, 10f, 3f);
            var other = Pivot(root, "Arm", new Vector3(-0.24f, 0.8f, 0f), new Vector3(0f, 0f, -12f));
            Part(PrimitiveType.Capsule, other, new Vector3(0f, -0.16f, 0f), new Vector3(0.07f, 0.18f, 0.07f), bone);
            Wiggle.Add(other.gameObject, Wiggle.Mode.Swing, Vector3.right, 10f, 3f, 3f);
        }

        static void Ghost(Transform root, Color color)
        {
            var body = Pivot(root, "Body", Vector3.zero);
            Wiggle.Add(body.gameObject, Wiggle.Mode.Bob, Vector3.up, 0.06f, 2.5f);
            var mist = Palette.Ghost(color, 0.72f);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.18f, 0f), new Vector3(0.76f, 0.82f, 0.72f), mist);
            var tail = Pivot(body, "Tail", new Vector3(0f, -0.15f, -0.05f));
            Wiggle.Add(tail.gameObject, Wiggle.Mode.Swing, Vector3.forward, 14f, 3f);
            Part(PrimitiveType.Sphere, tail, new Vector3(0f, -0.08f, -0.02f), new Vector3(0.55f, 0.42f, 0.5f), mist);
            Part(PrimitiveType.Sphere, tail, new Vector3(0.06f, -0.3f, -0.08f), new Vector3(0.34f, 0.3f, 0.32f), mist);
            Part(PrimitiveType.Sphere, tail, new Vector3(0.12f, -0.46f, -0.14f), new Vector3(0.18f, 0.18f, 0.18f), mist);
            foreach (float s in new[] { -1f, 1f })
            {
                var hand = Part(PrimitiveType.Sphere, body, new Vector3(0.4f * s, 0.05f, 0.08f), new Vector3(0.16f, 0.22f, 0.16f), mist);
                Wiggle.Add(hand, Wiggle.Mode.Bob, Vector3.up, 0.05f, 3.5f, s);
                Part(PrimitiveType.Sphere, body, new Vector3(0.13f * s, 0.28f, 0.33f), new Vector3(0.12f, 0.17f, 0.05f), Palette.Plain(new Color(0.15f, 0.1f, 0.3f)));
            }
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.07f, 0.35f), new Vector3(0.1f, 0.14f, 0.04f), Palette.Plain(new Color(0.15f, 0.1f, 0.3f)));
            var aura = GlowQuad(body, new Vector3(0f, 0.1f, -0.1f), 1.8f, Palette.Glow(new Color(color.r, color.g, color.b, 0.35f)), Vector3.zero);
            aura.name = "Aura";
            aura.AddComponent<Billboard>();
        }

        static void Elemental(Transform root, Color color)
        {
            var core = Part(MeshLibrary.Icosphere(1), root, new Vector3(0f, 0.78f, 0f), Vector3.one * 0.52f, Palette.Emissive(Color.Lerp(color, Color.white, 0.25f), 2.4f));
            Wiggle.Add(core, Wiggle.Mode.Pulse, Vector3.up, 0.06f, 4f);
            var orbit = Pivot(root, "Orbit", new Vector3(0f, 0.78f, 0f));
            Wiggle.Add(orbit.gameObject, Wiggle.Mode.Spin, Vector3.up, 70f, 1f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Part(MeshLibrary.Icosphere(1, true, 0.18f, 10 + i), orbit, new Vector3(Mathf.Cos(a) * 0.52f, Mathf.Sin(a * 2f) * 0.18f, Mathf.Sin(a) * 0.52f),
                    Vector3.one * (0.26f + (i % 2) * 0.08f), color * 0.65f);
            }
            var head = Part(MeshLibrary.Icosphere(1, true, 0.12f, 20), root, new Vector3(0f, 1.32f, 0f), Vector3.one * 0.4f, color * 0.75f);
            Wiggle.Add(head, Wiggle.Mode.Bob, Vector3.up, 0.05f, 3f);
            Eyes(head.transform, new Vector3(0f, 0.05f, 0.42f), 0.22f, 1.4f, Color.Lerp(color, Color.white, 0.5f), glowing: true);
            Part(MeshLibrary.Cone(6), root, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.42f, 0.36f), color * 0.6f, new Vector3(180f, 0f, 0f));
        }

        static void Knight(Transform root, Color color)
        {
            var armor = Palette.Glossy(color);
            var trim = Palette.Glossy(Palette.Gold, 0f);
            var cloth = new Color(0.5f, 0.2f, 0.7f);
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Capsule, root, new Vector3(0.14f * s, 0.2f, 0f), new Vector3(0.2f, 0.18f, 0.2f), armor);
                Part(Round(0.3f), root, new Vector3(0.14f * s, 0.05f, 0.05f), new Vector3(0.22f, 0.1f, 0.3f), color * 0.7f);
            }
            Part(PrimitiveType.Capsule, root, new Vector3(0f, 0.64f, 0f), new Vector3(0.66f, 0.36f, 0.56f), armor);
            Part(Round(0.3f), root, new Vector3(0f, 0.55f, 0.27f), new Vector3(0.34f, 0.42f, 0.05f), cloth);
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.42f, 0f), new Vector3(0.68f, 0.03f, 0.58f), trim);

            var head = Pivot(root, "Head", new Vector3(0f, 1.04f, 0f));
            Part(Round(0.22f), head, new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.52f, 0.5f), Palette.Glossy(color * 0.85f));
            Part(Round(0.45f), head, new Vector3(0f, 0.24f, 0.25f), new Vector3(0.36f, 0.06f, 0.03f), Palette.Emissive(new Color(0.45f, 0.9f, 1f), 2.4f));
            var plume = Pivot(head, "Plume", new Vector3(0f, 0.48f, -0.04f));
            Part(MeshLibrary.Drop(), plume, new Vector3(0f, 0.12f, -0.08f), new Vector3(0.16f, 0.36f, 0.34f), cloth, new Vector3(-30f, 0f, 0f));
            Wiggle.Add(plume.gameObject, Wiggle.Mode.Swing, Vector3.right, 8f, 2.5f);

            foreach (float s in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, root, new Vector3(0.36f * s, 0.9f, 0f), new Vector3(0.32f, 0.24f, 0.32f), armor);

            var arm = Pivot(root, "Arm", new Vector3(0.4f, 0.86f, 0.05f), new Vector3(0f, 0f, 10f));
            Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.16f, 0f), new Vector3(0.16f, 0.18f, 0.16f), armor);
            var sword = Pivot(arm, "Sword", new Vector3(0f, -0.34f, 0.04f), new Vector3(65f, 0f, 0f));
            Part(Round(0.3f), sword, new Vector3(0f, 0.06f, 0f), new Vector3(0.26f, 0.05f, 0.07f), trim);
            Part(Round(0.4f), sword, new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 0.85f, 0.03f), Palette.Glossy(new Color(0.85f, 0.88f, 0.95f)));
            Wiggle.Add(arm.gameObject, Wiggle.Mode.Swing, Vector3.right, 6f, 2f);

            var shield = Pivot(root, "Shield", new Vector3(-0.44f, 0.62f, 0.12f), new Vector3(0f, -12f, 0f));
            Part(Round(0.3f), shield, Vector3.zero, new Vector3(0.08f, 0.62f, 0.48f), color * 0.7f);
            Part(MeshLibrary.Icosphere(0), shield, new Vector3(-0.05f, 0.02f, 0f), new Vector3(0.05f, 0.2f, 0.2f), trim);
        }
    }
}
