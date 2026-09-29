using NusantaraAR.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Menjadikan Image kaca buram: material bersama "NusantaraAR/UI/Glass" + kekuatan tint di UV1
    /// (warna Image = warna tint, alpha Image/CanvasGroup = pudar). Selama aktif, GlassBlurFeature menghitung blur.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class GlassSurface : BaseMeshEffect
    {
        float strength = Theme.GlassStrength;
        bool registered;

        public float Strength
        {
            get => strength;
            set
            {
                strength = Mathf.Clamp01(value);
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            var material = GlassBlur.UIMaterial;
            if (material != null) graphic.material = material;
            if (registered) return;
            GlassBlur.Register();
            registered = true;
        }

        protected override void OnDisable()
        {
            if (registered)
            {
                GlassBlur.Unregister();
                registered = false;
            }
            base.OnDisable();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var vertex = new UIVertex();
            var uv1 = new Vector4(strength, 0f, 0f, 0f);
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = uv1;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
