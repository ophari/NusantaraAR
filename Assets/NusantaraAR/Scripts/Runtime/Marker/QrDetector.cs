using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.Marker
{
    /// <summary>Hasil deteksi QR: isi teks + 4 sudut luar simbol (TL, TR, BR, BL; tanpa zona sunyi) dalam piksel, y ke bawah.</summary>
    public struct QrDetection
    {
        /// <summary>Null = pola QR terlihat (pose tetap valid) tetapi isinya belum terbaca, mis. karena buram.</summary>
        public string text;
        public Vector2[] corners;
        public int version;

        public bool Decoded => text != null;
    }

    /// <summary>
    /// Detektor kode QR pada citra kamera (CPU, tanpa ARCore/library luar):
    /// komponen gelap -> cincin luar pola finder 7x7 (segi empat + pola 1:1:3:1:1) -> tiga finder membentuk siku
    /// -> homografi kuadrat terkecil dari 12 sudut finder (+ pola alignment bila ada) -> baca grid modul -> <see cref="QrCode"/>.
    /// Sudut dari homografi ini juga dipakai untuk estimasi pose, sama seperti sudut kartu penanda.
    /// </summary>
    public class QrDetector
    {
        public int minFinderSide = 14;
        public int maxFinderErrors = 3;
        public float minContrast = 30f;

        struct Finder
        {
            public Vector2[] quad;
            public Vector2 center;
            public float module;
            public float threshold;
        }

        const int MaxFinders = 12;

        readonly DarkRegions own = new DarkRegions();
        readonly List<Finder> finders = new List<Finder>();
        readonly List<(float score, int tl, int tr, int bl)> triples = new List<(float, int, int, int)>();
        readonly List<QrDetection> results = new List<QrDetection>();
        readonly List<Vector2> src = new List<Vector2>(), dst = new List<Vector2>();
        readonly List<float> weights = new List<float>();
        readonly Dictionary<int, bool[,]> grids = new Dictionary<int, bool[,]>();

        public List<QrDetection> Detect(byte[] gray, int width, int height)
        {
            own.Analyze(gray, width, height);
            return Detect(own);
        }

        /// <summary>Deteksi pada frame yang sudah dianalisis (dipakai bersama detektor kartu). Hasil terbaca diurutkan lebih dulu.</summary>
        public List<QrDetection> Detect(DarkRegions regions)
        {
            results.Clear();
            finders.Clear();
            if (regions.Gray == null || regions.Width < 32 || regions.Height < 32) return results;

            int maxSide = Mathf.Min(regions.Width, regions.Height) / 2;
            foreach (var r in regions.Regions)
            {
                if (r.Width < minFinderSide || r.Height < minFinderSide || r.pixels < minFinderSide * 2) continue;
                if (r.Width > maxSide || r.Height > maxSide) continue;
                if (!regions.TryQuad(r, minFinderSide * 0.6f, out var quad, 0.8f, true)) continue;
                if (!TryFinder(regions, quad, out var f)) continue;
                bool duplicate = false;
                foreach (var e in finders)
                    if ((e.center - f.center).magnitude < e.module * 2f) { duplicate = true; break; }
                if (!duplicate) finders.Add(f);
            }
            if (finders.Count < 3) return results;
            if (finders.Count > MaxFinders)
            {
                finders.Sort((a, b) => b.module.CompareTo(a.module));
                finders.RemoveRange(MaxFinders, finders.Count - MaxFinders);
            }

            triples.Clear();
            for (int i = 0; i < finders.Count; i++)
            for (int j = i + 1; j < finders.Count; j++)
            for (int k = j + 1; k < finders.Count; k++)
                if (TryTriple(i, j, k, out var t)) triples.Add(t);
            triples.Sort((a, b) => a.score.CompareTo(b.score));

            var used = new bool[finders.Count];
            foreach (var t in triples)
            {
                if (used[t.tl] || used[t.tr] || used[t.bl]) continue;
                if (!TryRead(regions, finders[t.tl], finders[t.tr], finders[t.bl], out var det)) continue;
                used[t.tl] = used[t.tr] = used[t.bl] = true;
                results.Add(det);
            }
            results.Sort((a, b) => b.Decoded.CompareTo(a.Decoded));
            return results;
        }

        static bool IsFinderDark(int u, int v) => Mathf.Max(Mathf.Abs(u - 3), Mathf.Abs(v - 3)) != 2;

        /// <summary>Segi empat = cincin luar finder bila grid 7x7 di dalamnya berpola cincin gelap / cincin terang / inti 3x3 gelap.</summary>
        bool TryFinder(DarkRegions regions, Vector2[] quad, out Finder f)
        {
            f = default;
            if (!Homography.FromUnitSquare(quad, out var H)) return false;
            var values = new float[49];
            float darkSum = 0f, lightSum = 0f;
            int darkCount = 0, lightCount = 0;
            for (int v = 0; v < 7; v++)
            for (int u = 0; u < 7; u++)
            {
                float val = values[v * 7 + u] = regions.SampleCell(H, u, v, 7);
                if (IsFinderDark(u, v)) { darkSum += val; darkCount++; }
                else { lightSum += val; lightCount++; }
            }
            float dark = darkSum / darkCount, light = lightSum / lightCount;
            if (light - dark < minContrast) return false;
            float t = (dark + light) * 0.5f;
            int errors = 0;
            for (int v = 0; v < 7; v++)
            for (int u = 0; u < 7; u++)
                if (values[v * 7 + u] < t != IsFinderDark(u, v)) errors++;
            if (errors > maxFinderErrors) return false;
            float perimeter = 0f;
            for (int k = 0; k < 4; k++) perimeter += (quad[k] - quad[(k + 1) % 4]).magnitude;
            f = new Finder { quad = quad, center = Homography.Apply(H, 0.5, 0.5), module = perimeter / 28f, threshold = t };
            return true;
        }

        /// <summary>Tiga finder membentuk siku sama kaki: sudut siku = kiri-atas; kanan-atas/kiri-bawah dari arah putar.</summary>
        bool TryTriple(int i, int j, int k, out (float score, int tl, int tr, int bl) best)
        {
            best = (float.MaxValue, -1, -1, -1);
            float mMin = Mathf.Min(finders[i].module, Mathf.Min(finders[j].module, finders[k].module));
            float mMax = Mathf.Max(finders[i].module, Mathf.Max(finders[j].module, finders[k].module));
            if (mMax > mMin * 2.5f) return false; // perspektif kuat: finder dekat bisa 2x lebih besar
            float m = (finders[i].module + finders[j].module + finders[k].module) / 3f;
            int[] idx = { i, j, k };
            for (int s = 0; s < 3; s++)
            {
                int a = idx[s], b = idx[(s + 1) % 3], c = idx[(s + 2) % 3];
                var ab = finders[b].center - finders[a].center;
                var ac = finders[c].center - finders[a].center;
                float lb = ab.magnitude, lc = ac.magnitude;
                // Jarak antarpusat finder = N - 7 modul (versi 1-10: 14..50), beri kelonggaran untuk perspektif.
                if (lb < 10f * m || lc < 10f * m || lb > 60f * m || lc > 60f * m) continue;
                float cos = Vector2.Dot(ab, ac) / (lb * lc);
                float ratio = lb / lc;
                if (Mathf.Abs(cos) > 0.6f || ratio < 0.45f || ratio > 2.2f) continue;
                float score = Mathf.Abs(cos) + Mathf.Abs(Mathf.Log(ratio));
                if (score >= best.score) continue;
                best = DarkRegions.Cross(ab, ac) > 0f ? (score, a, b, c) : (score, a, c, b);
            }
            return best.tl >= 0;
        }

        bool TryRead(DarkRegions regions, Finder tl, Finder tr, Finder bl, out QrDetection det)
        {
            det = default;
            bool haveFallback = false;
            float m = (tl.module + tr.module + bl.module) / 3f;
            float n = ((tr.center - tl.center).magnitude + (bl.center - tl.center).magnitude) * 0.5f / m + 7f;
            int v0 = Mathf.Clamp(Mathf.RoundToInt((n - 17f) / 4f), QrCode.MinVersion, QrCode.MaxVersion);
            foreach (int v in new[] { v0, v0 + 1, v0 - 1 })
            {
                if (v < QrCode.MinVersion || v > QrCode.MaxVersion) continue;
                int size = QrCode.Size(v);
                if (!FitFinders(tl, tr, bl, size, out var H)) continue;
                if (!haveFallback)
                {
                    // Sudut luar sudah benar walau versi salah: tiga di antaranya adalah sudut luar finder.
                    det = MakeDetection(H, size, v, null);
                    haveFallback = true;
                }
                if (v >= 2 && RefineWithAlignment(regions, H, size, m, out var refined)
                    && ReadGrid(regions, refined, size, tl, tr, bl, out var text))
                {
                    det = MakeDetection(refined, size, v, text);
                    return true;
                }
                if (ReadGrid(regions, H, size, tl, tr, bl, out text))
                {
                    det = MakeDetection(H, size, v, text);
                    return true;
                }
            }
            return haveFallback;
        }

        static QrDetection MakeDetection(double[] H, int size, int version, string text) => new QrDetection
        {
            text = text,
            version = version,
            corners = new[] { Homography.Apply(H, 0, 0), Homography.Apply(H, size, 0), Homography.Apply(H, size, size), Homography.Apply(H, 0, size) },
        };

        /// <summary>Homografi koordinat modul -> piksel dari 12 sudut luar ketiga finder.</summary>
        bool FitFinders(Finder tl, Finder tr, Finder bl, int size, out double[] H)
        {
            src.Clear();
            dst.Clear();
            weights.Clear();
            var ex = (tr.center - tl.center) / (size - 7);
            var ey = (bl.center - tl.center) / (size - 7);
            AddFinder(tl, 0, 0);
            AddFinder(tr, size - 7, 0);
            AddFinder(bl, 0, size - 7);
            return Homography.FromPointsLeastSquares(src, dst, weights, out H);

            void AddFinder(Finder f, float x0, float y0)
            {
                var expected = new[] { new Vector2(x0, y0), new Vector2(x0 + 7, y0), new Vector2(x0 + 7, y0 + 7), new Vector2(x0, y0 + 7) };
                // Pasangkan sudut segi empat ke sudut modul lewat perkiraan afin dari pusat finder (urutan sama-sama searah jarum jam).
                int bestRot = 0;
                float bestErr = float.MaxValue;
                for (int rot = 0; rot < 4; rot++)
                {
                    float err = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        var guess = tl.center + (expected[k].x - 3.5f) * ex + (expected[k].y - 3.5f) * ey;
                        err += (guess - f.quad[(k + rot) % 4]).sqrMagnitude;
                    }
                    if (err < bestErr) { bestErr = err; bestRot = rot; }
                }
                for (int k = 0; k < 4; k++)
                {
                    src.Add(expected[k]);
                    dst.Add(f.quad[(k + bestRot) % 4]);
                    weights.Add(1f);
                }
            }
        }

        /// <summary>Tambahkan titik pusat pola alignment kanan-bawah (titik gelap 1 modul) agar perspektif sisi kanan-bawah akurat.</summary>
        bool RefineWithAlignment(DarkRegions regions, double[] H, int size, float m, out double[] refined)
        {
            refined = null;
            var target = new Vector2(size - 6.5f, size - 6.5f);
            var predicted = Homography.Apply(H, target.x, target.y);
            float bestDist = 2f * m;
            Vector2 best = default;
            bool found = false;
            foreach (var r in regions.Regions)
            {
                if (r.Width > 1.8f * m + 1f || r.Height > 1.8f * m + 1f || r.pixels < 0.15f * m * m) continue;
                if (r.maxX < predicted.x - bestDist || r.minX > predicted.x + bestDist) continue;
                if (r.maxY < predicted.y - bestDist || r.minY > predicted.y + bestDist) continue;
                var c = r.Centroid;
                float d = (c - predicted).magnitude;
                if (d < bestDist) { bestDist = d; best = c; found = true; }
            }
            if (!found) return false;
            src.Add(target);
            dst.Add(best);
            weights.Add(4f);
            bool ok = Homography.FromPointsLeastSquares(src, dst, weights, out refined);
            src.RemoveAt(src.Count - 1);
            dst.RemoveAt(dst.Count - 1);
            weights.RemoveAt(weights.Count - 1);
            return ok;
        }

        /// <summary>Sampel tiap modul; ambang = bidang yang melalui ambang ketiga finder (tahan gradasi cahaya).</summary>
        bool ReadGrid(DarkRegions regions, double[] H, int size, Finder tl, Finder tr, Finder bl, out string text)
        {
            if (!grids.TryGetValue(size, out var grid)) grids[size] = grid = new bool[size, size];
            float span = size - 7;
            for (int v = 0; v < size; v++)
            for (int u = 0; u < size; u++)
            {
                float sum = 0f;
                for (int sy = -1; sy <= 1; sy++)
                for (int sx = -1; sx <= 1; sx++)
                {
                    var p = Homography.Apply(H, u + 0.5f + sx * 0.2f, v + 0.5f + sy * 0.2f);
                    sum += regions.Sample(p.x, p.y);
                }
                float t = tl.threshold + (u - 3f) / span * (tr.threshold - tl.threshold) + (v - 3f) / span * (bl.threshold - tl.threshold);
                grid[v, u] = sum / 9f < t;
            }
            return QrCode.TryDecode(grid, out text);
        }
    }
}
