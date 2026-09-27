using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.Marker
{
    /// <summary>Hasil deteksi: kode marker + 4 sudut (TL, TR, BR, BL) dalam piksel gambar, sumbu y ke bawah.</summary>
    public struct MarkerDetection
    {
        public int code;
        public Vector2[] corners;
    }

    /// <summary>
    /// Detektor marker persegi pada citra grayscale (CPU, tanpa ARCore):
    /// threshold adaptif -> komponen gelap (8-tetangga) -> convex hull -> segi empat -> baca grid 6x6
    /// di keempat orientasi. Buffer: gray[y * width + x], baris 0 = atas.
    /// </summary>
    public class MarkerDetector
    {
        public int adaptiveRadius = 12;
        public int thresholdOffset = 8;
        public int minSidePixels = 24;
        public int maxBitErrors = 1;

        int w, h;
        int[] integral = new int[0];
        int[] labels = new int[0];
        int[] stack = new int[0];
        readonly List<int> minX = new List<int>(), maxX = new List<int>(), minY = new List<int>(), maxY = new List<int>(), count = new List<int>();
        readonly List<Vector2> boundary = new List<Vector2>();
        readonly List<Vector2> hull = new List<Vector2>();
        readonly List<MarkerDetection> results = new List<MarkerDetection>();
        readonly float[] cellValues = new float[MarkerPattern.Grid * MarkerPattern.Grid];

        public List<MarkerDetection> Detect(byte[] gray, int width, int height, IList<int> codes)
        {
            results.Clear();
            if (gray == null || width < 32 || height < 32 || codes == null || codes.Count == 0) return results;
            Prepare(width, height);
            BuildIntegral(gray);
            int components = Label(gray);
            for (int id = 1; id <= components; id++)
            {
                int i = id - 1;
                int bw = maxX[i] - minX[i] + 1, bh = maxY[i] - minY[i] + 1;
                if (bw < minSidePixels || bh < minSidePixels || count[i] < minSidePixels * 3) continue;
                if (minX[i] <= 1 || minY[i] <= 1 || maxX[i] >= w - 2 || maxY[i] >= h - 2) continue;
                if (!QuadFromComponent(id, i, out var quad)) continue;
                if (TryDecode(gray, quad, codes, out var det)) AddUnique(det);
            }
            return results;
        }

        void AddUnique(MarkerDetection d)
        {
            var c = Center(d.corners);
            foreach (var r in results)
                if (r.code == d.code && (Center(r.corners) - c).sqrMagnitude < 100f) return;
            results.Add(d);
        }

        static Vector2 Center(Vector2[] q) => (q[0] + q[1] + q[2] + q[3]) * 0.25f;

        void Prepare(int width, int height)
        {
            w = width;
            h = height;
            if (integral.Length != (w + 1) * (h + 1)) integral = new int[(w + 1) * (h + 1)];
            if (labels.Length != w * h)
            {
                labels = new int[w * h];
                stack = new int[w * h];
            }
            System.Array.Clear(labels, 0, labels.Length);
            minX.Clear(); maxX.Clear(); minY.Clear(); maxY.Clear(); count.Clear();
        }

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
        int Label(byte[] g)
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
                stack[sp++] = start;
                labels[start] = next;
                while (sp > 0)
                {
                    int p = stack[--sp];
                    int px = p % w, py = p / w;
                    n++;
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
                minX.Add(bx0); maxX.Add(bx1); minY.Add(by0); maxY.Add(by1); count.Add(n);
            }
            return next;
        }

        bool QuadFromComponent(int id, int i, out Vector2[] quad)
        {
            quad = null;
            boundary.Clear();
            for (int y = minY[i]; y <= maxY[i]; y++)
            for (int x = minX[i]; x <= maxX[i]; x++)
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
            if (quadArea <= 0f || hullArea <= 0f || quadArea / hullArea < 0.88f) return false;
            for (int k = 0; k < 4; k++)
                if ((quad[k] - quad[(k + 1) % 4]).magnitude < minSidePixels * 0.6f) return false;

            // Titik hull = pusat piksel gelap terluar; geser 0,5 px ke tepi sebenarnya.
            var center = Center(quad);
            for (int k = 0; k < 4; k++) quad[k] += (quad[k] - center).normalized * 0.7f;
            return true;
        }

        bool TryDecode(byte[] g, Vector2[] quad, IList<int> codes, out MarkerDetection det)
        {
            det = default;
            var ordered = new Vector2[4];
            for (int rot = 0; rot < 4; rot++)
            {
                for (int k = 0; k < 4; k++) ordered[k] = quad[(k + rot) % 4];
                if (!Homography.FromUnitSquare(ordered, out var H)) return false;
                if (!ReadCells(g, H, out int code, out int borderErrors) || borderErrors > maxBitErrors) continue;
                foreach (var target in codes)
                {
                    if (MarkerPattern.HammingDistance(code, target) > maxBitErrors) continue;
                    det = new MarkerDetection { code = target, corners = (Vector2[])ordered.Clone() };
                    return true;
                }
            }
            return false;
        }

        bool ReadCells(byte[] g, double[] H, out int code, out int borderErrors)
        {
            const int n = MarkerPattern.Grid;
            code = 0;
            borderErrors = 0;
            float min = float.MaxValue, max = float.MinValue;
            for (int v = 0; v < n; v++)
            for (int u = 0; u < n; u++)
            {
                float sum = 0f;
                int samples = 0;
                for (int sy = -1; sy <= 1; sy++)
                for (int sx = -1; sx <= 1; sx++)
                {
                    var p = Homography.Apply(H, (u + 0.5f + sx * 0.2f) / n, (v + 0.5f + sy * 0.2f) / n);
                    sum += Sample(g, p.x, p.y);
                    samples++;
                }
                float val = sum / samples;
                cellValues[v * n + u] = val;
                if (val < min) min = val;
                if (val > max) max = val;
            }
            if (max - min < 40f) return false;
            float t = (min + max) * 0.5f;
            for (int v = 0; v < n; v++)
            for (int u = 0; u < n; u++)
            {
                bool black = cellValues[v * n + u] < t;
                bool border = u == 0 || v == 0 || u == n - 1 || v == n - 1;
                if (border) { if (!black) borderErrors++; continue; }
                if (black) code |= 1 << (15 - ((v - 1) * MarkerPattern.DataBits + (u - 1)));
            }
            return true;
        }

        float Sample(byte[] g, float x, float y)
        {
            x -= 0.5f;
            y -= 0.5f;
            int x0 = Mathf.Clamp((int)Mathf.Floor(x), 0, w - 1), y0 = Mathf.Clamp((int)Mathf.Floor(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            float top = Mathf.Lerp(g[y0 * w + x0], g[y0 * w + x1], fx);
            float bottom = Mathf.Lerp(g[y1 * w + x0], g[y1 * w + x1], fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>Luas bertanda (positif = searah jarum jam di layar, karena sumbu y ke bawah).</summary>
        static float SignedArea(IList<Vector2> poly)
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

    /// <summary>Homografi 3x3 (h33 = 1) disimpan row-major dalam double[9].</summary>
    public static class Homography
    {
        static readonly Vector2[] Unit = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

        public static bool FromUnitSquare(Vector2[] dst, out double[] H) => FromPoints(Unit, dst, out H);

        /// <summary>DLT 4 titik: src -> dst.</summary>
        public static bool FromPoints(IList<Vector2> src, IList<Vector2> dst, out double[] H)
        {
            var A = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double x = src[i].x, y = src[i].y, u = dst[i].x, v = dst[i].y;
                int r = i * 2;
                A[r, 0] = x; A[r, 1] = y; A[r, 2] = 1; A[r, 6] = -u * x; A[r, 7] = -u * y; A[r, 8] = u;
                A[r + 1, 3] = x; A[r + 1, 4] = y; A[r + 1, 5] = 1; A[r + 1, 6] = -v * x; A[r + 1, 7] = -v * y; A[r + 1, 8] = v;
            }
            H = new double[9];
            // Eliminasi Gauss dengan pivot parsial.
            for (int col = 0; col < 8; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 8; r++)
                    if (System.Math.Abs(A[r, col]) > System.Math.Abs(A[pivot, col])) pivot = r;
                if (System.Math.Abs(A[pivot, col]) < 1e-12) return false;
                if (pivot != col)
                    for (int k = 0; k < 9; k++) { var tmp = A[col, k]; A[col, k] = A[pivot, k]; A[pivot, k] = tmp; }
                for (int r = 0; r < 8; r++)
                {
                    if (r == col) continue;
                    double f = A[r, col] / A[col, col];
                    if (f == 0) continue;
                    for (int k = col; k < 9; k++) A[r, k] -= f * A[col, k];
                }
            }
            for (int i = 0; i < 8; i++) H[i] = A[i, 8] / A[i, i];
            H[8] = 1;
            return true;
        }

        public static Vector2 Apply(double[] H, double x, double y)
        {
            double z = H[6] * x + H[7] * y + H[8];
            return new Vector2((float)((H[0] * x + H[1] * y + H[2]) / z), (float)((H[3] * x + H[4] * y + H[5]) / z));
        }
    }
}
