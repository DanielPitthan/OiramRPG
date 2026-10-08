using System.Threading;
using Oiram.Core;
using UnityEngine;

namespace Oiram.Battle
{
    /// <summary>Representação visual de uma <see cref="BattleUnit"/> (primitivas por enquanto).</summary>
    public sealed class BattleUnitView : MonoBehaviour
    {
        Renderer[] renderers;
        Material[] originalMaterials;
        Vector3 visualBase;
        Vector3 visualScale;
        float bobPhase;
        bool flying;

        CharacterRig rig;

        public BattleUnit Unit { get; private set; }
        public Transform Visual { get; private set; }
        public Vector3 Home { get; set; }
        public Vector3 HomeFacing { get; set; }
        public float Height { get; private set; } = 1.6f;
        public float Radius { get; private set; } = 0.5f;
        public bool IsDown { get; private set; }

        public Vector3 Top => transform.position + Vector3.up * (Height + 0.2f);
        public Vector3 Center => transform.position + Vector3.up * (Height * 0.5f);

        public static BattleUnitView Create(Transform parent, BattleUnit unit, Vector3 position, Vector3 facing)
        {
            var go = new GameObject(unit.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var view = go.AddComponent<BattleUnitView>();
            view.Unit = unit;
            view.Home = position;
            view.HomeFacing = facing;

            if (unit.Member != null)
                view.Visual = Shapes.Hero(go.transform, unit.Member.Definition.color, unit.Member.Job.color, unit.Member.Job.id);
            else
            {
                view.Visual = Shapes.EnemyFor(go.transform, unit.Enemy);
                view.flying = unit.Enemy.flying;
                if (view.flying) view.Visual.localPosition = Vector3.up * 1.0f;
            }

            Shapes.BlobShadow(go.transform, unit.Member != null ? 0.38f : 0.45f * (unit.Enemy?.scale ?? 1f)).localPosition = Vector3.up * 0.02f;
            view.Init();
            view.Face(facing);
            return view;
        }

        void Init()
        {
            rig = Visual.GetComponent<CharacterRig>();
            renderers = Visual.GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterial;

            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Height = Mathf.Max(0.6f, bounds.max.y - transform.position.y);
            Radius = Mathf.Max(0.35f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            visualBase = Visual.localPosition;
            visualScale = Visual.localScale;
            bobPhase = Random.value * 10f;
        }

        void Update()
        {
            if (IsDown || Visual == null) return;
            float amplitude = flying ? 0.12f : 0.025f;
            float speed = flying ? 5f : 3f;
            Visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * speed + bobPhase) * amplitude + amplitude);
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public void FaceHome() => Face(HomeFacing);

        public async Awaitable Flash(CancellationToken ct)
        {
            var white = Palette.Flash;
            foreach (var r in renderers) if (r) r.sharedMaterial = white;
            await Tween.Delay(0.07f, ct);
            for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].sharedMaterial = originalMaterials[i];
        }

        public async Awaitable HitReaction(CancellationToken ct)
        {
            _ = Flash(ct);
            await Tween.Shake(Visual, 0.25f, 0.12f, ct);
        }

        /// <summary>Inclina para trás (preparação do golpe) durante <paramref name="duration"/>.</summary>
        public Awaitable WindUp(float duration, CancellationToken ct)
        {
            Vector3 baseScale = visualScale;
            return Tween.Run(duration, k =>
            {
                if (!Visual) return;
                float s = Tween.EaseOutQuad(k) * 0.18f;
                Visual.localScale = new Vector3(baseScale.x * (1f + s), baseScale.y * (1f - s), baseScale.z * (1f + s));
                if (rig) rig.armRaise = Tween.EaseOutQuad(k);
            }, ct);
        }

        /// <summary>Estica para frente no impacto e volta à escala normal.</summary>
        public async Awaitable Strike(CancellationToken ct)
        {
            Vector3 baseScale = visualScale;
            await Tween.Run(0.12f, k =>
            {
                if (!Visual) return;
                float s = Mathf.Sin(k * Mathf.PI) * 0.25f;
                Visual.localScale = new Vector3(baseScale.x * (1f - s * 0.5f), baseScale.y * (1f + s), baseScale.z * (1f + s));
                if (rig) rig.armRaise = Mathf.Lerp(1f, -1f, Tween.EaseOutQuad(k));
            }, ct);
            if (Visual) Visual.localScale = baseScale;
            if (rig) _ = Tween.Run(0.25f, k => { if (rig) rig.armRaise = Mathf.Lerp(-1f, 0f, k); }, ct);
        }

        public Awaitable Hop(CancellationToken ct, float height = 0.5f, float duration = 0.3f) =>
            Tween.Arc(transform, transform.position, height, duration, ct);

        public async Awaitable Die(CancellationToken ct)
        {
            IsDown = true;
            if (Unit.Side == Side.Party)
            {
                // Herói cai deitado; continua na tela para poder ser revivido.
                await Tween.Run(0.3f, k => { if (Visual) Visual.localEulerAngles = new Vector3(-90f * k, 0, 0); }, ct);
                return;
            }
            _ = Flash(ct);
            await Tween.Run(0.45f, k =>
            {
                if (!Visual) return;
                Visual.localScale = visualScale * (1f - k);
                Visual.localPosition = visualBase + Vector3.up * (k * 0.6f);
            }, ct);
            gameObject.SetActive(false);
        }

        public void Revive()
        {
            IsDown = false;
            gameObject.SetActive(true);
            Visual.localEulerAngles = Vector3.zero;
            Visual.localScale = visualScale;
            Visual.localPosition = visualBase;
        }
    }
}
