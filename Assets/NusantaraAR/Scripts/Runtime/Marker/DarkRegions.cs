using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.Marker
{
    /// <summary>Satu komponen gelap terhubung: kotak batas, jumlah piksel, dan titik berat.</summary>
    public struct DarkRegion
    {
        public int id, minX, minY, maxX, maxY, pixels;
        public long sumX, sumY;

        public int Width => maxX - minX + 1;
        public int Height => maxY - minY + 1;

        /// <summary>Titik berat dalam koordinat piksel kontinu (pusat piksel = +0,5).</summary>
        public Vector2 Centroid => new Vector2(sumX / (float)pixels + 0.5f, sumY / (float)pixels + 0.5f);
    }

    /// <summary>
    /// Analisis citra grayscale bersama untuk semua detektor (CPU, tanpa ARCore):
    /// threshold adaptif (integral image) -> komponen gelap 8-tetangga -> segi empat dari convex hull.
    /// Dipakai <see cref="MarkerDetector"/> (kartu 6x6) dan <see cref="QrDetector"/> (pola finder QR),
    /// sehingga satu frame kamera cukup dilabeli sekali. Buffer: gray[y * width + x], baris 0 = atas.
    /// </summary>
    public class DarkRegions
    {
        public int adaptiveRadius = 12;
        public int thresholdOffset = 8;

        public byte[] Gray { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public readonly List<DarkRegion> Regions = new List<DarkRegion>();

        int w, h;
        int[] integral = new int[0];
        int[] labels = new int[0];
        int[] stack = new int[0];
        readonly List<Vector2> boundary = new List<Vector2>();
        readonly List<Vector2> hull = new List<Vector2>();

        public void Analyze(byte[] gray, int width, int height)
        {
            Regions.Clear();
            Gray = gray;
            Width = w = width;
            Height = h = height;
            if (gray == null || width < 32 || height < 32) return;
            if (integral.Length != (w + 1) * (h + 1)) integral = new int[(w + 1) * (h + 1)];
            if (labels.Length != w * h)
            {
                labels = new int[w * h];
                stack = new int[w * h];
            }
            System.Array.Clear(labels, 0, labels.Length);
            BuildIntegral(gray);
            Label(gray);
        }

        /// <summary>Komponen menyentuh (atau hampir menyentuh) tepi gambar: bentuknya terpotong.</summary>
        public bool TouchesBorder(in DarkRegion r) => r.minX <= 1 || r.minY <= 1 || r.maxX >= w - 2 || r.maxY >= h - 2;

        void BuildIntegral(byte[] g)
        {
            int stride = w + 1;
            for (int x = 0; x <= w; x++) integral[x] = 0;
            for (int y = 0; y < h; y++)
            {
                int rowSum = 0;
                integral[(y + 1) * stride] = 0;
                for (int x = 0; x < w; x++)
                {
                    rowSum += g[y * w + x];
                    integral[(y + 1) * stride + x + 1] = integral[y * stride + x + 1] + rowSum;
                }
            }
        }

        bool IsDark(byte[] g, int x, int y)
        {
            int r = adaptiveRadius, stride = w + 1;
            int x0 = Mathf.Max(0, x - r), y0 = Mathf.Max(0, y - r);
            int x1 = Mathf.Min(w, x + r + 1), y1 = Mathf.Min(h, y + r + 1);
            int area = (x1 - x0) * (y1 - y0);
            int sum = integral[y1 * stride + x1] - integral[y0 * stride + x1] - integral[y1 * stride + x0] + integral[y0 * stride + x0];
            return g[y * w + x] * area < sum - thresholdOffset * area;
        }

        /// <summary>Pelabelan komponen gelap (8-tetangga). labels: 0 = belum/terang, -1 = terang, >0 = id.</summary>
        void Label(byte[] g)
        {
            // Tandai piksel terang sebagai -1 sekali jalan agar IsDark tidak dihitung berulang.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (!IsDark(g, x, y)) labels[y * w + x] = -1;

            int next = 0;
            for (int start = 0; start < w * h; start++)
            {
                if (labels[start] != 0) continue;
                next++;
                int sp = 0, n = 0;
                int bx0 = int.MaxValue, bx1 = -1, by0 = int.MaxValue, by1 = -1;
                long sx = 0, sy = 0;
                stack[sp++] = start;
                labels[start] = next;
                while (sp > 0)
                {
                    int p = stack[--sp];
                    int px = p % w, py = p / w;
                    n++;
                    sx += px;
                    sy += py;
                    if (px < bx0) bx0 = px;
                    if (px > bx1) bx1 = px;
                    if (py < by0) by0 = py;
                    if (py > by1) by1 = py;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = py + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = px + dx;
                            if (nx < 0 || nx >= w) continue;
                            int q = ny * w + nx;
                            if (labels[q] != 0) continue;
                            labels[q] = next;
                            stack[sp++] = q;
                        }
                    }
                }
                Regions.Add(new DarkRegion { id = next, minX = bx0, maxX = bx1, minY = by0, maxY = by1, pixels = n, sumX = sx, sumY = sy });
            }
        }

        /// <summary>
        /// Segi empat yang membungkus komponen (sudut searah jarum jam di layar), atau false bila bentuknya bukan segi empat.
        /// Komponen yang menyentuh tepi gambar selalu ditolak. minFill = luas segi empat / luas hull minimum;
        /// refineEdges = sudut subpiksel dari fit garis sisi (untuk objek kecil yang sudutnya membulat).
        /// </summary>
        public bool TryQuad(in DarkRegion r, float minSide, out Vector2[] quad, float minFill = 0.88f, bool refineEdges = false)
        {
            quad = null;
            if (TouchesBorder(r)) return false;
            int id = r.id;
            boundary.Clear();
            for (int y = r.minY; y <= r.maxY; y++)
            for (int x = r.minX; x <= r.maxX; x++)
            {
                int p = y * w + x;
                if (labels[p] != id) continue;
                if (labels[p - 1] != id || labels[p + 1] != id || labels[p - w] != id || labels[p + w] != id)
                    boundary.Add(new Vector2(x + 0.5f, y + 0.5f));
            }
            if (boundary.Count < 8) return false;
            ConvexHull(boundary, hull);
            if (hull.Count < 4) return false;

            // Diagonal = pasangan titik terjauh; dua sudut lain = titik terjauh dari diagonal di tiap sisi.
            int step = Mathf.Max(1, hull.Count / 200);
            int a = 0, c = 0;
            float best = -1f;
            for (int p = 0; p < hull.Count; p += step)
            for (int q = p + 1; q < hull.Count; q += step)
            {
                float d = (hull[p] - hull[q]).sqrMagnitude;
                if (d > best) { best = d; a = p; c = q; }
            }
            Vector2 A = hull[a], C = hull[c];
            int b = -1, dIdx = -1;
            float maxPos = 0f, maxNeg = 0f;
            for (int p = 0; p < hull.Count; p++)
            {
                float s = Cross(C - A, hull[p] - A);
                if (s > maxPos) { maxPos = s; b = p; }
                if (s < maxNeg) { maxNeg = s; dIdx = p; }
            }
            if (b < 0 || dIdx < 0) return false;
            quad = new[] { A, hull[b], C, hull[dIdx] };
            if (SignedArea(quad) < 0f) System.Array.Reverse(quad);

            float quadArea = SignedArea(quad);
            float hullArea = Mathf.Abs(SignedArea(hull));
            if (quadArea <= 0f || hullArea <= 0f || quadArea / hullArea < minFill) return false;
            for (int k = 0; k < 4; k++)
                if ((quad[k] - quad[(k + 1) % 4]).magnitude < minSide) return false;

            if (refineEdges && TryRefineEdges(quad, out var refined))
            {
                quad = refined;
                return true;
            }
            // Titik hull = pusat piksel gelap terluar; geser 0,5 px ke tepi sebenarnya.
            var center = Center(quad);
            for (int k = 0; k < 4; k++) quad[k] += (quad[k] - center).normalized * 0.7f;
            return true;
        }

        /// <summary>
        /// Sudut subpiksel: garis tiap sisi di-fit (PCA) ke titik batas di bagian tengah sisi, lalu sisi yang berdampingan
        /// dipotongkan. Tahan terhadap sudut yang membulat karena blur pada objek kecil.
        /// </summary>
        bool TryRefineEdges(Vector2[] q, out Vector2[] refined)
        {
            refined = null;
            var center = Center(q);
            var points = new Vector2[4];
            var dirs = new Vector2[4];
            float minLen = float.MaxValue;
            for (int k = 0; k < 4; k++)
            {
                Vector2 a = q[k], b = q[(k + 1) % 4];
                float len = (b - a).magnitude;
                minLen = Mathf.Min(minLen, len);
                var dir = (b - a) / len;
                var normal = new Vector2(dir.y, -dir.x);
                if (Vector2.Dot(normal, (a + b) * 0.5f - center) < 0f) normal = -normal;
                // Lintasan 1: dekat sisi awal (sisi awal bisa sedikit masuk ke dalam bila sudutnya terpotong).
                if (!FitLine(a, dir, normal, len, -1f, 2.5f, out var p, out var d)) return false;
                // Lintasan 2: hanya titik yang benar-benar di garis hasil lintasan 1.
                var n2 = new Vector2(d.y, -d.x);
                if (Vector2.Dot(n2, normal) < 0f) n2 = -n2;
                if (!FitLine(p, d, n2, len, -0.8f, 0.8f, out p, out d, a)) return false;
                var outward = new Vector2(d.y, -d.x);
                if (Vector2.Dot(outward, normal) < 0f) outward = -outward;
                points[k] = p + outward * 0.5f; // titik batas = pusat piksel, tepi sebenarnya 0,5 px di luarnya
                dirs[k] = d;
            }
            refined = new Vector2[4];
            float maxShift = Mathf.Max(3f, minLen * 0.2f);
            for (int k = 0; k < 4; k++)
            {
                int prev = (k + 3) % 4;
                float cross = Cross(dirs[prev], dirs[k]);
                if (Mathf.Abs(cross) < 1e-3f) return false;
                float t = Cross(points[k] - points[prev], dirs[k]) / cross;
                refined[k] = points[prev] + dirs[prev] * t;
                if ((refined[k] - q[k]).magnitude > maxShift) return false;
            }
            return true;
        }

        /// <summary>
        /// Fit garis ke titik batas yang proyeksinya di 15-85% sisi (dari <paramref name="edgeStart"/>) dan
        /// jarak bertandanya ke garis (origin, dir) di [minD, maxD].
        /// </summary>
        bool FitLine(Vector2 origin, Vector2 dir, Vector2 normal, float len, float minD, float maxD, out Vector2 mean, out Vector2 lineDir, Vector2? edgeStart = null)
        {
            var start = edgeStart ?? origin;
            mean = default;
            lineDir = dir;
            double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
            int n = 0;
            foreach (var p in boundary)
            {
                float t = Vector2.Dot(p - start, dir) / len;
                if (t < 0.15f || t > 0.85f) continue;
                float d = Vector2.Dot(p - origin, normal);
                if (d < minD || d > maxD) continue;
                sx += p.x; sy += p.y; sxx += p.x * p.x; sxy += p.x * p.y; syy += p.y * p.y;
                n++;
            }
            if (n < 3) return false;
            double mx = sx / n, my = sy / n;
            double cxx = sxx / n - mx * mx, cxy = sxy / n - mx * my, cyy = syy / n - my * my;
            double angle = 0.5 * System.Math.Atan2(2 * cxy, cxx - cyy);
            mean = new Vector2((float)mx, (float)my);
            lineDir = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            if (Vector2.Dot(lineDir, dir) < 0f) lineDir = -lineDir;
            return true;
        }

        /// <summary>Nilai abu-abu bilinear di koordinat piksel kontinu.</summary>
        public float Sample(float x, float y)
        {
            var g = Gray;
            x -= 0.5f;
            y -= 0.5f;
            int x0 = Mathf.Clamp((int)Mathf.Floor(x), 0, w - 1), y0 = Mathf.Clamp((int)Mathf.Floor(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            float top = Mathf.Lerp(g[y0 * w + x0], g[y0 * w + x1], fx);
            float bottom = Mathf.Lerp(g[y1 * w + x0], g[y1 * w + x1], fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        /// <summary>Rata-rata 3x3 titik di sekitar pusat sel (u, v) grid n x n pada homografi persegi satuan H.</summary>
        public float SampleCell(double[] H, int u, int v, int n)
        {
            float sum = 0f;
            for (int sy = -1; sy <= 1; sy++)
            for (int sx = -1; sx <= 1; sx++)
            {
                var p = Homography.Apply(H, (u + 0.5f + sx * 0.2f) / n, (v + 0.5f + sy * 0.2f) / n);
                sum += Sample(p.x, p.y);
            }
            return sum / 9f;
        }

        public static Vector2 Center(Vector2[] q) => (q[0] + q[1] + q[2] + q[3]) * 0.25f;

        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Luas bertanda (positif = searah jarum jam di layar, karena sumbu y ke bawah).</summary>
        public static float SignedArea(IList<Vector2> poly)
        {
            float s = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % poly.Count];
                s += p.x * q.y - q.x * p.y;
            }
            return s * 0.5f;
        }

        static void ConvexHull(List<Vector2> pts, List<Vector2> result)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            result.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = result.Count;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = pass == 0 ? pts[i] : pts[pts.Count - 1 - i];
                    while (result.Count >= start + 2 && Cross(result[result.Count - 1] - result[result.Count - 2], p - result[result.Count - 2]) <= 0f)
                        result.RemoveAt(result.Count - 1);
                    result.Add(p);
                }
                result.RemoveAt(result.Count - 1);
            }
        }
    }
}
