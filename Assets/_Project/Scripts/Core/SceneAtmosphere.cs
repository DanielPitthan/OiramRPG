using UnityEngine;
using UnityEngine.Rendering;

namespace Oiram.Core
{
    /// <summary>Cores do céu (degradê atrás da câmera) e da luz ambiente em hemisfério do shader toon.</summary>
    [System.Serializable]
    public struct AtmosphereColors
    {
        public Color skyTop;
        public Color skyHorizon;
        public Color skyBottom;
        public Color ambientSky;
        public Color ambientGround;

        public static AtmosphereColors Day => new()
        {
            skyTop = new Color(0.33f, 0.6f, 0.95f),
            skyHorizon = new Color(0.8f, 0.92f, 1f),
            skyBottom = new Color(0.58f, 0.8f, 0.98f),
            ambientSky = new Color(0.78f, 0.86f, 1f),
            ambientGround = new Color(0.62f, 0.55f, 0.46f),
        };

        public static AtmosphereColors Golden => new()
        {
            skyTop = new Color(0.36f, 0.5f, 0.9f),
            skyHorizon = new Color(1f, 0.84f, 0.66f),
            skyBottom = new Color(0.72f, 0.74f, 0.95f),
            ambientSky = new Color(0.86f, 0.82f, 0.95f),
            ambientGround = new Color(0.66f, 0.52f, 0.42f),
        };

        /// <summary>Subterrâneo: degradê do "céu" da dungeon até o abismo.</summary>
        public static AtmosphereColors Underground(Color sky, Color abyss, Color ambient) => new()
        {
            skyTop = sky * 0.6f,
            skyHorizon = sky,
            skyBottom = abyss,
            ambientSky = Color.Lerp(ambient, Color.white, 0.25f),
            ambientGround = ambient * 0.6f,
        };
    }

    /// <summary>
    /// Atmosfera da cena: céu em degradê num quad preso ao fundo da câmera e luz ambiente global do toon.
    /// Reaplica ao ser reativado (o campo é desligado durante a batalha e religado depois).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SceneAtmosphere : MonoBehaviour
    {
        static readonly int AmbientSkyId = Shader.PropertyToID("_OiramAmbientSky");
        static readonly int AmbientGroundId = Shader.PropertyToID("_OiramAmbientGround");

        public Camera targetCamera;
        public AtmosphereColors colors = AtmosphereColors.Day;

        Transform backdrop;
        MeshRenderer backdropRenderer;

        public static void ApplyAmbient(Color sky, Color ground)
        {
            Shader.SetGlobalColor(AmbientSkyId, sky);
            Shader.SetGlobalColor(AmbientGroundId, ground);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, ground, 0.5f);
            RenderSettings.ambientGroundColor = ground;
        }

        void OnEnable() => Apply();

        public void SetColors(AtmosphereColors value)
        {
            colors = value;
            Apply();
        }

        public void Apply()
        {
            ApplyAmbient(colors.ambientSky, colors.ambientGround);
            if (targetCamera == null) return;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = colors.skyHorizon;
            if (backdrop == null)
            {
                var go = Shapes.Part(MeshLibrary.Quad, targetCamera.transform, Vector3.zero, Vector3.one, Palette.Sky(colors.skyTop, colors.skyHorizon, colors.skyBottom));
                go.name = "SkyBackdrop";
                backdrop = go.transform;
                backdropRenderer = go.GetComponent<MeshRenderer>();
                backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
                backdropRenderer.receiveShadows = false;
            }
            backdropRenderer.sharedMaterial = Palette.Sky(colors.skyTop, colors.skyHorizon, colors.skyBottom);
            Fit();
        }

        void LateUpdate() => Fit();

        void Fit()
        {
            if (backdrop == null || targetCamera == null) return;
            float distance = targetCamera.farClipPlane * 0.95f;
            float height = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            backdrop.localPosition = new Vector3(0f, 0f, distance);
            backdrop.localRotation = Quaternion.identity;
            backdrop.localScale = new Vector3(height * targetCamera.aspect * 1.1f, height * 1.1f, 1f);
        }
    }
}
