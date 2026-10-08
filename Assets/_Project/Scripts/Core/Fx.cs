using UnityEngine;
using UnityEngine.Rendering;

namespace Oiram.Core
{
    /// <summary>
    /// Partículas criadas por código (sem prefabs): faíscas, estrelas, fumaça, poeira, brilhos subindo, brasas e rastros.
    /// Usam o shader Oiram/Glow (círculo suave); a cor de cada partícula tinge o material.
    /// </summary>
    public static class Fx
    {
        static Material Additive => Palette.Glow(new Color(1.8f, 1.8f, 1.8f, 1f), GlowShape.Circle, additive: true, softness: 0.85f);
        static Material Smoke => Palette.Glow(Color.white, GlowShape.Circle, additive: false, softness: 0.9f);

        static ParticleSystem Create(Transform parent, Vector3 position, Material material, string name)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        static void Fade(ParticleSystem ps, bool shrink)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            if (!shrink) return;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
        }

        /// <summary>Explosão única: <paramref name="count"/> partículas saindo de uma esfera pequena.</summary>
        public static ParticleSystem Burst(Vector3 at, Color color, int count, float speed, float size, float life,
            float gravity = 0f, bool additive = true, float radius = 0.1f, Transform parent = null)
        {
            if (!Application.isPlaying) return null;
            var ps = Create(parent, at, additive ? Additive : Smoke, "FX Burst");
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            Fade(ps, shrink: true);
            ps.Play();
            Object.Destroy(ps.gameObject, life + 0.5f);
            return ps;
        }

        /// <summary>Faíscas do golpe.</summary>
        public static void Sparks(Vector3 at, Color color) => Burst(at, color, 14, 4.5f, 0.14f, 0.32f, gravity: 0.6f);

        /// <summary>Estrelas grandes (perfeito, crítico, baú).</summary>
        public static void Stars(Vector3 at, Color color, int count = 10) => Burst(at, color, count, 3.2f, 0.32f, 0.6f, gravity: 0.3f, radius: 0.2f);

        /// <summary>Nuvem de fumaça (inimigo derrotado).</summary>
        public static void Poof(Vector3 at, Color color) => Burst(at, color, 12, 1.4f, 0.75f, 0.7f, gravity: -0.15f, additive: false, radius: 0.35f);

        /// <summary>Poeirinha ao cair de um pulo.</summary>
        public static void Dust(Vector3 at) => Burst(at, new Color(0.9f, 0.85f, 0.75f, 0.8f), 8, 1.3f, 0.3f, 0.4f, gravity: -0.1f, additive: false, radius: 0.15f);

        /// <summary>Brilhos subindo (cura, loot, subir de nível).</summary>
        public static void Rising(Vector3 at, Color color, float radius = 0.45f, int count = 18, float life = 0.9f)
        {
            if (!Application.isPlaying) return;
            var ps = Create(null, at, Additive, "FX Rising");
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = count / 0.35f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            Fade(ps, shrink: true);
            ps.Play();
            Object.Destroy(ps.gameObject, life + 0.8f);
        }

        /// <summary>Emissor contínuo de brasas (fogueira, tocha). Funciona também em cenas "assadas" no editor.</summary>
        public static ParticleSystem Embers(Transform parent, Vector3 localPos, Color color, float rate = 6f, float spread = 0.15f)
        {
            var ps = Create(parent, parent.position, Additive, "Embers");
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = spread;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            Fade(ps, shrink: true);
            if (Application.isPlaying) ps.Play();
            return ps;
        }

        /// <summary>Rastro brilhante (projéteis de magia).</summary>
        public static TrailRenderer Trail(GameObject target, Color color, float width = 0.25f, float time = 0.25f)
        {
            var trail = target.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Palette.Glow(Color.white, GlowShape.Solid, additive: true);
            trail.time = time;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, width, 1f, 0f);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }
    }
}
