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
    /// Detektor kartu penanda persegi 6x6 pada citra grayscale (CPU, tanpa ARCore):
    /// komponen gelap (<see cref="DarkRegions"/>) -> segi empat -> baca grid 6x6 di keempat orientasi.
    /// Buffer: gray[y * width + x], baris 0 = atas.
    /// </summary>
    public class MarkerDetector
    {
        public int minSidePixels = 24;
        public int maxBitErrors = 1;

        readonly DarkRegions own = new DarkRegions();
        readonly List<MarkerDetection> results = new List<MarkerDetection>();
        readonly float[] cellValues = new float[MarkerPattern.Grid * MarkerPattern.Grid];

        public List<MarkerDetection> Detect(byte[] gray, int width, int height, IList<int> codes)
        {
            results.Clear();
            if (gray == null || width < 32 || height < 32 || codes == null || codes.Count == 0) return results;
            own.Analyze(gray, width, height);
            return Detect(own, codes);
        }

        /// <summary>Deteksi pada frame yang sudah dianalisis (dipakai bersama detektor QR).</summary>
        public List<MarkerDetection> Detect(DarkRegions regions, IList<int> codes)
        {
            results.Clear();
            if (regions.Gray == null || regions.Width < 32 || regions.Height < 32 || codes == null || codes.Count == 0) return results;
            foreach (var r in regions.Regions)
            {
                if (r.Width < minSidePixels || r.Height < minSidePixels || r.pixels < minSidePixels * 3) continue;
                if (!regions.TryQuad(r, minSidePixels * 0.6f, out var quad)) continue;
                if (TryDecode(regions, quad, codes, out var det)) AddUnique(det);
            }
            return results;
        }

        void AddUnique(MarkerDetection d)
        {
            var c = DarkRegions.Center(d.corners);
            foreach (var r in results)
                if (r.code == d.code && (DarkRegions.Center(r.corners) - c).sqrMagnitude < 100f) return;
            results.Add(d);
        }

        bool TryDecode(DarkRegions regions, Vector2[] quad, IList<int> codes, out MarkerDetection det)
        {
            det = default;
            var ordered = new Vector2[4];
            for (int rot = 0; rot < 4; rot++)
            {
                for (int k = 0; k < 4; k++) ordered[k] = quad[(k + rot) % 4];
                if (!Homography.FromUnitSquare(ordered, out var H)) return false;
                if (!ReadCells(regions, H, out int code, out int borderErrors) || borderErrors > maxBitErrors) continue;
                foreach (var target in codes)
                {
                    if (MarkerPattern.HammingDistance(code, target) > maxBitErrors) continue;
                    det = new MarkerDetection { code = target, corners = (Vector2[])ordered.Clone() };
                    return true;
                }
            }
            return false;
        }

        bool ReadCells(DarkRegions regions, double[] H, out int code, out int borderErrors)
        {
            const int n = MarkerPattern.Grid;
            code = 0;
            borderErrors = 0;
            float min = float.MaxValue, max = float.MinValue;
            for (int v = 0; v < n; v++)
            for (int u = 0; u < n; u++)
            {
                float val = regions.SampleCell(H, u, v, n);
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

        /// <summary>
        /// Homografi kuadrat terkecil dari >= 4 pasangan titik (src -> dst), dengan normalisasi Hartley agar stabil.
        /// weights (opsional) memberi bobot per pasangan.
        /// </summary>
        public static bool FromPointsLeastSquares(IList<Vector2> src, IList<Vector2> dst, IList<float> weights, out double[] H)
        {
            H = null;
            int n = src.Count;
            if (n < 4 || dst.Count != n) return false;
            Normalization(src, out double sx, out double smx, out double smy);
            Normalization(dst, out double dx, out double dmx, out double dmy);
            var ata = new double[8, 9]; // kolom 8 = A^T b
            var row = new double[8];
            for (int i = 0; i < n; i++)
            {
                double x = (src[i].x - smx) * sx, y = (src[i].y - smy) * sx;
                double u = (dst[i].x - dmx) * dx, v = (dst[i].y - dmy) * dx;
                double wgt = weights != null ? weights[i] : 1.0;
                for (int pass = 0; pass < 2; pass++)
                {
                    System.Array.Clear(row, 0, 8);
                    double rhs;
                    if (pass == 0) { row[0] = x; row[1] = y; row[2] = 1; row[6] = -u * x; row[7] = -u * y; rhs = u; }
                    else { row[3] = x; row[4] = y; row[5] = 1; row[6] = -v * x; row[7] = -v * y; rhs = v; }
                    for (int a = 0; a < 8; a++)
                    {
                        if (row[a] == 0) continue;
                        for (int b = 0; b < 8; b++) ata[a, b] += wgt * row[a] * row[b];
                        ata[a, 8] += wgt * row[a] * rhs;
                    }
                }
            }
            if (!Solve8(ata, out var h)) return false;
            // H = Td^-1 * Hn * Ts
            double[] Hn = { h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1 };
            double[] Ts = { sx, 0, -sx * smx, 0, sx, -sx * smy, 0, 0, 1 };
            double[] TdInv = { 1 / dx, 0, dmx, 0, 1 / dx, dmy, 0, 0, 1 };
            var M = Multiply(TdInv, Multiply(Hn, Ts));
            if (System.Math.Abs(M[8]) < 1e-12) return false;
            H = new double[9];
            for (int i = 0; i < 9; i++) H[i] = M[i] / M[8];
            return true;
        }

        static void Normalization(IList<Vector2> pts, out double scale, out double mx, out double my)
        {
            mx = my = 0;
            foreach (var p in pts) { mx += p.x; my += p.y; }
            mx /= pts.Count;
            my /= pts.Count;
            double d = 0;
            foreach (var p in pts) d += System.Math.Sqrt((p.x - mx) * (p.x - mx) + (p.y - my) * (p.y - my));
            d /= pts.Count;
            scale = d > 1e-9 ? System.Math.Sqrt(2) / d : 1;
        }

        static bool Solve8(double[,] A, out double[] x)
        {
            x = new double[8];
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
            for (int i = 0; i < 8; i++) x[i] = A[i, 8] / A[i, i];
            return true;
        }

        static double[] Multiply(double[] a, double[] b)
        {
            var r = new double[9];
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
            return r;
        }

        public static Vector2 Apply(double[] H, double x, double y)
        {
            double z = H[6] * x + H[7] * y + H[8];
            return new Vector2((float)((H[0] * x + H[1] * y + H[2]) / z), (float)((H[3] * x + H[4] * y + H[5]) / z));
        }
    }
}
