using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Penyusun mesh prosedural sederhana (editor saja): sweep penampang di sepanjang tulang, lathe,
    /// elipsoid, tabung. Winding selalu dibetulkan agar normal menghadap keluar (volume bertanda positif).
    /// </summary>
    class ProcMesh
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public ProcMesh Append(ProcMesh o)
        {
            int b = v.Count;
            v.AddRange(o.v);
            uv.AddRange(o.uv);
            foreach (int i in o.t) t.Add(i + b);
            return this;
        }

        public ProcMesh Copy()
        {
            var m = new ProcMesh();
            m.Append(this);
            return m;
        }

        /// <summary>Transformasi titik; skala dengan hasil kali negatif (cermin) membalik winding.</summary>
        public ProcMesh Xf(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            for (int i = 0; i < v.Count; i++) v[i] = pos + rot * Vector3.Scale(v[i], scale);
            if (scale.x * scale.y * scale.z < 0f) FlipWinding();
            return this;
        }

        public ProcMesh Move(Vector3 d) => Xf(d, Quaternion.identity, Vector3.one);

        public ProcMesh Mirrored(Vector3 axisScale) => Copy().Xf(Vector3.zero, Quaternion.identity, axisScale);

        void FlipWinding()
        {
            for (int i = 0; i < t.Count; i += 3) { int a = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = a; }
        }

        double SignedVolume(int from, int to)
        {
            double s = 0;
            for (int i = from; i < to; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                s += Vector3.Dot(a, Vector3.Cross(b, c));
            }
            return s;
        }

        /// <summary>Pastikan permukaan tertutup menghadap keluar (dipanggil pada potongan baru saja).</summary>
        public ProcMesh Orient()
        {
            if (SignedVolume(0, t.Count) < 0) FlipWinding();
            return this;
        }

        // ------------------------------------------------------------------ Primitif

        /// <summary>
        /// Cincin-cincin (masing-masing segs+1 titik, titik terakhir = duplikat seam) menjadi tabung,
        /// opsional ditutup di ujung awal/akhir.
        /// </summary>
        public static ProcMesh FromRings(List<Vector3[]> rings, List<Vector2[]> uvs, bool capStart, bool capEnd)
        {
            var m = new ProcMesh();
            int n = rings[0].Length;
            for (int r = 0; r < rings.Count; r++)
                for (int j = 0; j < n; j++)
                {
                    m.v.Add(rings[r][j]);
                    m.uv.Add(uvs != null ? uvs[r][j] : new Vector2(j / (float)(n - 1), r / (float)(rings.Count - 1)));
                }
            for (int r = 0; r < rings.Count - 1; r++)
                for (int j = 0; j < n - 1; j++)
                {
                    int a = r * n + j, b = a + 1, c = a + n, d = c + 1;
                    m.t.Add(a); m.t.Add(c); m.t.Add(b);
                    m.t.Add(b); m.t.Add(c); m.t.Add(d);
                }
            if (capStart) Cap(m, rings[0], true);
            if (capEnd) Cap(m, rings[rings.Count - 1], false);
            // Tabung bisa terbuka, jadi arah dicek lokal: normal quad pada cincin tengah harus menjauhi pusat cincin.
            // Tutup dibuat dengan urutan yang konsisten dengan dinding, jadi ikut terbalik bersama.
            int mid = (rings.Count - 1) / 2;
            var centroid = Vector3.zero;
            for (int j = 0; j < n - 1; j++) centroid += rings[mid][j];
            centroid /= Mathf.Max(1, n - 1);
            double score = 0;
            for (int j = 0; j < n - 1; j++)
            {
                int a = mid * n + j, b = a + 1, c = a + n;
                var normal = Vector3.Cross(m.v[c] - m.v[a], m.v[b] - m.v[a]);
                score += Vector3.Dot(normal, (m.v[a] + m.v[b] + m.v[c]) / 3f - centroid);
            }
            if (score < 0) m.FlipWinding();
            return m;
        }

        static void Cap(ProcMesh m, Vector3[] ring, bool start)
        {
            var c = Vector3.zero;
            int n = ring.Length - 1;
            for (int j = 0; j < n; j++) c += ring[j];
            c /= n;
            float radius = 0f;
            for (int j = 0; j < n; j++) radius = Mathf.Max(radius, (ring[j] - c).magnitude);
            if (radius < 1e-5f) return;
            int ci = m.v.Count;
            m.v.Add(c);
            m.uv.Add(new Vector2(0.5f, 0.5f));
            int b = m.v.Count;
            for (int j = 0; j <= n; j++)
            {
                m.v.Add(ring[j]);
                float a = j / (float)n * Mathf.PI * 2f;
                m.uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int j = 0; j < n; j++)
            {
                if (start) { m.t.Add(ci); m.t.Add(b + j); m.t.Add(b + j + 1); }
                else { m.t.Add(ci); m.t.Add(b + j + 1); m.t.Add(b + j); }
            }
        }

        /// <summary>Penampang: sudut θ -> titik (cos-sisi, sin-sisi) pada lingkaran/elips satuan.</summary>
        public delegate Vector2 Profile(float theta);

        public static Vector2 Ellipse(float th) => new Vector2(Mathf.Cos(th), Mathf.Sin(th));

        /// <summary>Lensa (bilah): tepi tajam di ±x.</summary>
        public static Vector2 Lens(float th)
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            return new Vector2(c, Mathf.Sign(s) * Mathf.Pow(1f - Mathf.Abs(c), 0.75f));
        }

        /// <summary>Superelips: n = 2 elips, n besar mendekati persegi panjang membulat.</summary>
        public static Profile Super(float n) => th =>
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / n), Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / n));
        };

        /// <summary>
        /// Sweep sepanjang tulang spine(s), s = 0..1. size(s) = (setengah lebar sisi +N, setengah lebar sisi -N, setengah tebal).
        /// N adalah arah "lebar" (tegak lurus tulang, sebidang dengan forwardHint x T), B arah tebal.
        /// </summary>
        public static ProcMesh Sweep(int rows, int segs, Func<float, Vector3> spine, Func<float, Vector3> size, Profile profile,
            Func<float, float, float, Vector2, Vector2> uvFn = null, bool capStart = true, bool capEnd = true, Vector3? thicknessAxis = null)
        {
            var axis = thicknessAxis ?? Vector3.forward;
            var rings = new List<Vector3[]>();
            var uvs = new List<Vector2[]>();
            float arc = 0f;
            var prev = spine(0f);
            for (int i = 0; i <= rows; i++)
            {
                float s = i / (float)rows;
                var p = spine(s);
                arc += (p - prev).magnitude;
                prev = p;
                float h = 1f / rows * 0.5f;
                var T = (spine(Mathf.Min(1f, s + h)) - spine(Mathf.Max(0f, s - h)));
                T = T.sqrMagnitude > 1e-14f ? T.normalized : Vector3.up;
                var N = Vector3.Cross(axis, T);
                if (N.sqrMagnitude < 1e-8f) N = Vector3.Cross(Vector3.up, T);
                N.Normalize();
                var B = Vector3.Cross(T, N);
                var sz = size(s);
                var ring = new Vector3[segs + 1];
                var ruv = new Vector2[segs + 1];
                for (int j = 0; j <= segs; j++)
                {
                    float th = Mathf.PI * 2f * (j % segs) / segs;
                    var q = profile(th);
                    var off = new Vector2(q.x * (q.x >= 0 ? sz.x : sz.y), q.y * sz.z);
                    ring[j] = p + N * off.x + B * off.y;
                    ruv[j] = uvFn != null ? uvFn(s, arc, j / (float)segs, off) : new Vector2(j / (float)segs, arc / 0.1f);
                }
                rings.Add(ring);
                uvs.Add(ruv);
            }
            return FromRings(rings, uvs, capStart, capEnd);
        }

        /// <summary>Benda putar dari profil (radius, y), berurutan dari bawah ke atas. sx/sz meng-elipskan penampang.</summary>
        public static ProcMesh Lathe(List<Vector2> profile, int segs, float sx = 1f, float sz = 1f)
        {
            var rings = new List<Vector3[]>();
            var uvs = new List<Vector2[]>();
            float len = 0f;
            for (int i = 0; i < profile.Count; i++)
            {
                if (i > 0) len += (profile[i] - profile[i - 1]).magnitude;
                var ring = new Vector3[segs + 1];
                var ruv = new Vector2[segs + 1];
                for (int j = 0; j <= segs; j++)
                {
                    float a = Mathf.PI * 2f * (j % segs) / segs;
                    ring[j] = new Vector3(Mathf.Cos(a) * profile[i].x * sx, profile[i].y, Mathf.Sin(a) * profile[i].x * sz);
                    ruv[j] = new Vector2(j / (float)segs, len / 0.05f);
                }
                rings.Add(ring);
                uvs.Add(ruv);
            }
            return FromRings(rings, uvs, true, true);
        }

        public static ProcMesh Ellipsoid(Vector3 center, Vector3 radii, Quaternion rot, int segs = 14, int rows = 9)
        {
            var rings = new List<Vector3[]>();
            for (int i = 0; i <= rows; i++)
            {
                float phi = Mathf.PI * i / rows; // 0 = bawah
                float y = -Mathf.Cos(phi), r = Mathf.Sin(phi);
                var ring = new Vector3[segs + 1];
                for (int j = 0; j <= segs; j++)
                {
                    float a = Mathf.PI * 2f * (j % segs) / segs;
                    ring[j] = center + rot * Vector3.Scale(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), radii);
                }
                rings.Add(ring);
            }
            return FromRings(rings, null, false, false);
        }

        /// <summary>Tabung meruncing dari a ke b dengan bola di kedua ujung (lengan, kaki, tangkai).</summary>
        public static ProcMesh Limb(Vector3 a, Vector3 b, float r0, float r1, int segs = 10)
        {
            var m = Sweep(4, segs, s => Vector3.Lerp(a, b, s), s => { float r = Mathf.Lerp(r0, r1, s); return new Vector3(r, r, r); },
                Ellipse, null, false, false, PerpTo(b - a));
            m.Append(Ellipsoid(a, Vector3.one * r0, Quaternion.identity, segs, 6));
            m.Append(Ellipsoid(b, Vector3.one * r1, Quaternion.identity, segs, 6));
            return m;
        }

        /// <summary>Tabung sepanjang kurva (untuk belalai, sulur).</summary>
        public static ProcMesh Tube(IList<Vector3> pts, Func<float, float> radius, int rows = 24, int segs = 10, bool caps = true)
        {
            Func<float, Vector3> spine = s => CatmullRom(pts, s);
            var m = Sweep(rows, segs, spine, s => { float r = radius(s); return new Vector3(r, r, r); }, Ellipse, null, false, false,
                PerpTo(pts[pts.Count - 1] - pts[0]));
            if (caps)
            {
                m.Append(Ellipsoid(spine(0f), Vector3.one * radius(0f), Quaternion.identity, segs, 6));
                m.Append(Ellipsoid(spine(1f), Vector3.one * radius(1f), Quaternion.identity, segs, 6));
            }
            return m;
        }

        static Vector3 PerpTo(Vector3 d)
        {
            d.Normalize();
            var c = Vector3.Cross(d, Mathf.Abs(d.z) < 0.9f ? Vector3.forward : Vector3.right);
            return Vector3.Cross(c, d).normalized;
        }

        /// <summary>Catmull-Rom uniform melalui semua titik, s = 0..1 sepanjang indeks segmen.</summary>
        public static Vector3 CatmullRom(IList<Vector3> p, float s)
        {
            int n = p.Count - 1;
            float f = Mathf.Clamp01(s) * n;
            int i = Mathf.Min(n - 1, (int)f);
            float u = f - i;
            var p0 = p[Mathf.Max(0, i - 1)];
            var p1 = p[i];
            var p2 = p[i + 1];
            var p3 = p[Mathf.Min(n, i + 2)];
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
        }

        /// <summary>smoothstep GLSL (Mathf.SmoothStep berbeda: itu interpolasi, bukan ambang).</summary>
        public static float Smooth(float e0, float e1, float x)
        {
            float k = Mathf.Clamp01((x - e0) / (e1 - e0));
            return k * k * (3f - 2f * k);
        }

        // ------------------------------------------------------------------ Keluaran

        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            SmoothSeams(mesh);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        /// <summary>Ratakan normal pada titik yang posisinya sama (seam), selama sudutnya &lt; 60° (tepi tajam tetap tajam).</summary>
        static void SmoothSeams(Mesh mesh)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var groups = new Dictionary<(long, long, long), List<int>>();
            for (int i = 0; i < verts.Length; i++)
            {
                var k = ((long)Mathf.Round(verts[i].x * 1e5f), (long)Mathf.Round(verts[i].y * 1e5f), (long)Mathf.Round(verts[i].z * 1e5f));
                if (!groups.TryGetValue(k, out var list)) groups[k] = list = new List<int>();
                list.Add(i);
            }
            var result = (Vector3[])normals.Clone();
            const float cos60 = 0.5f;
            foreach (var list in groups.Values)
            {
                if (list.Count < 2) continue;
                foreach (int i in list)
                {
                    var sum = Vector3.zero;
                    foreach (int j in list)
                        if (Vector3.Dot(normals[i], normals[j]) > cos60) sum += normals[j];
                    if (sum.sqrMagnitude > 1e-12f) result[i] = sum.normalized;
                }
            }
            mesh.normals = result;
        }
    }
}
