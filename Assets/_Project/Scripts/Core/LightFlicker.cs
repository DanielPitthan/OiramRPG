using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Luz de fogo tremulando (tochas, fogueiras).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        public float amount = 0.25f;
        public float speed = 7f;

        Light target;
        float baseIntensity;
        float seed;

        void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            target.intensity = baseIntensity * (1f + n * amount * 2f);
        }
    }
}
