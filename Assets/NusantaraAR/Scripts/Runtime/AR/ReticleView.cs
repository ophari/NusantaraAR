using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Reticle penempatan (PRD FR-03): cincin emas tipis dengan empat aksen ornamen,
    /// dibangun sebagai mesh prosedural agar tidak bergantung aset tambahan.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ReticleView : MonoBehaviour
    {
        public float radius = 0.08f;
        public float thickness = 0.006f;
        public int segments = 64;
        public float pulseAmount = 0.06f;
        public float pulseSpeed = 2.4f;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = BuildMesh();
            Hide();
        }

        public void Show(Pose pose)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(pose.position + pose.up * 0.002f, pose.rotation);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            transform.localScale = new Vector3(s, 1f, s);
        }

        Mesh BuildMesh()
        {
            var mesh = new Mesh { name = "Reticle" };
            int ringVerts = (segments + 1) * 2;
            var verts = new Vector3[ringVerts + 4 * 4];
            var tris = new int[segments * 6 + 4 * 6];
            float r0 = radius - thickness * 0.5f, r1 = radius + thickness * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * r0;
                verts[i * 2 + 1] = d * r1;
            }
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                tris[t++] = v + 1; tris[t++] = v + 2; tris[t++] = v + 3;
            }
            // Aksen ornamen: belah ketupat kecil di empat penjuru, di luar cincin.
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = new Vector3(-d.z, 0f, d.x);
                var c = d * (radius + thickness * 2.2f);
                int v = ringVerts + k * 4;
                verts[v] = c - d * thickness * 1.6f;
                verts[v + 1] = c + p * thickness * 0.9f;
                verts[v + 2] = c + d * thickness * 1.6f;
                verts[v + 3] = c - p * thickness * 0.9f;
                tris[t++] = v; tris[t++] = v + 1; tris[t++] = v + 2;
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 3;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
