using System.Collections.Generic;
using Oiram.Battle;
using UnityEngine;

namespace Oiram.Field
{
    /// <summary>Mantém a sombra-blob no chão, logo abaixo do dono, encolhendo com a altura.</summary>
    public sealed class BlobShadowFollower : MonoBehaviour
    {
        public Transform shadow;
        public float maxDistance = 12f;

        Vector3 baseScale;

        void Awake()
        {
            if (shadow) baseScale = shadow.localScale;
        }

        void LateUpdate()
        {
            if (!shadow) return;
            var origin = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                shadow.gameObject.SetActive(true);
                shadow.position = hit.point + Vector3.up * 0.02f;
                shadow.rotation = Quaternion.Euler(90f, 0f, 0f); // quad deitado no chão
                float height = transform.position.y - hit.point.y;
                float k = Mathf.Clamp(1f - height * 0.12f, 0.45f, 1f);
                shadow.localScale = new Vector3(baseScale.x * k, baseScale.y * k, baseScale.z);
            }
            else shadow.gameObject.SetActive(false);
        }
    }
}
