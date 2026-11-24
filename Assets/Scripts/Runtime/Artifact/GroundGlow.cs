using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Cahaya terakota lembut di bawah artefak (3D Viewer, AR Meja, Scan QR di meja): quad prosedural tanpa collider
    /// dengan material <c>Resources/GroundGlow</c> (URP Unlit transparan). Sedikit lebih terang saat objek dimanipulasi.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundGlow : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static Mesh quad;

        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        Color tint;
        float visibility = 1f, boost;

        /// <summary>Null bila material belum dibuat (menu Nusantara AR → Build AR Visuals).</summary>
        public static GroundGlow Create(Transform parent = null)
        {
            var material = Resources.Load<Material>("GroundGlow");
            if (material == null) return null;
            var go = new GameObject("GroundGlow", typeof(MeshFilter), typeof(MeshRenderer));
            if (parent != null) go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = Quad();
            var glow = go.AddComponent<GroundGlow>();
            glow.meshRenderer = go.GetComponent<MeshRenderer>();
            glow.meshRenderer.sharedMaterial = material;
            glow.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.meshRenderer.receiveShadows = false;
            glow.block = new MaterialPropertyBlock();
            glow.tint = material.HasProperty(ColorId) ? material.GetColor(ColorId) : UI.Theme.AccentSoft;
            glow.Apply();
            return glow;
        }

        /// <param name="up">Normal bidang tempat artefak berdiri.</param>
        /// <param name="radius">Jari-jari cahaya (meter).</param>
        /// <param name="alpha">0..1, mis. dipudarkan saat QR berdiri di layar/dinding.</param>
        public void Place(Vector3 position, Vector3 up, float radius, float alpha = 1f)
        {
            if (up.sqrMagnitude < 1e-6f) up = Vector3.up;
            transform.SetPositionAndRotation(position + up.normalized * 0.001f, Quaternion.FromToRotation(Vector3.up, up));
            transform.localScale = Vector3.one * Mathf.Max(0.001f, radius * 2f);
            visibility = Mathf.Clamp01(alpha);
            Apply();
        }

        /// <summary>Sorotan sesaat saat objek diputar/digeser.</summary>
        public void Pulse() => boost = 1f;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        void Update()
        {
            if (boost <= 0f) return;
            boost = Mathf.MoveTowards(boost, 0f, Time.unscaledDeltaTime * 1.5f);
            Apply();
        }

        void Apply()
        {
            var c = tint;
            c.a = Mathf.Clamp01(tint.a * visibility * (1f + boost * 0.5f));
            block.SetColor(ColorId, c);
            meshRenderer.SetPropertyBlock(block);
            meshRenderer.enabled = c.a > 0.004f;
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "GroundGlowQuad", hideFlags = HideFlags.DontSave };
            quad.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
            };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
