using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Mantém um quad (halo, brilho) sempre de frente para a câmera ativa.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        Camera cam;

        void LateUpdate()
        {
            if (cam == null || !cam.isActiveAndEnabled)
            {
                cam = Camera.main;
                if (cam == null || !cam.isActiveAndEnabled)
                    foreach (var c in Camera.allCameras) { cam = c; break; }
            }
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
