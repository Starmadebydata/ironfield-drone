using UnityEngine;

namespace Ironfield.Fx
{
    /// <summary>
    /// Lightweight camera grade (no post-processing package): ACES tonemap,
    /// gentle contrast / saturation, shadow tint and a vignette. Gives the
    /// Built-in pipeline a filmic look instead of flat sRGB.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Ironfield/Camera Post")]
    public class CameraPost : MonoBehaviour
    {
        [Range(0.5f, 2f)] public float exposure = 1.05f;
        [Range(0.8f, 1.5f)] public float contrast = 1.07f;
        [Range(0f, 2f)] public float saturation = 1.06f;
        [Range(0f, 1f)] public float vignette = 0.32f;
        public Color shadowTint = new Color(0.03f, 0.04f, 0.06f);

        Material _mat;

        void OnEnable() => EnsureMat();

        void EnsureMat()
        {
            if (_mat != null) return;
            var sh = Shader.Find("Ironfield/Post");
            if (sh != null) _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            EnsureMat();
            if (_mat == null) { Graphics.Blit(src, dst); return; }
            _mat.SetFloat("_Exposure", exposure);
            _mat.SetFloat("_Contrast", contrast);
            _mat.SetFloat("_Saturation", saturation);
            _mat.SetFloat("_Vignette", vignette);
            _mat.SetColor("_LiftShadow", shadowTint);
            Graphics.Blit(src, dst, _mat);
        }
    }
}
