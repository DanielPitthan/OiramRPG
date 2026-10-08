using System.Collections.Generic;
using Oiram.Battle;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Câmera isométrica ortográfica que segue o alvo suavemente.</summary>
    public sealed class IsoCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 30f;
        public float smoothTime = 0.12f;
        public Vector3 offset = new(0f, 0.6f, 0f);

        Vector3 velocity;

        void Start() => Snap();

        public void Snap()
        {
            if (target) transform.position = Desired();
        }

        Vector3 Desired() => target.position + offset - transform.forward * distance;

        void LateUpdate()
        {
            if (!target) return;
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
