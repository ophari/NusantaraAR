using System;
using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.UI
{
    public enum Icon
    {
        Back, Close, ChevronLeft, ChevronRight, ArrowUp, ArrowDown, RotateLeft, RotateRight, Reset,
        Book, Layers, Blade, Tag, Spin360, Move, Lock, Unlock, Qr, ScanFrame, Compass, Cube, Grid, Settings,
        Play, Pause, SkipNext, Speaker, Info, Shield, Target, Alert
    }

    /// <summary>
    /// Ikon garis prosedural (tanpa file gambar): digambar sebagai medan jarak di grid 24 unit (y ke atas),
    /// garis 2 unit berujung bulat, lalu dirasterisasi ke tekstur putih ber-mipmap dan diwarnai lewat <c>Image.color</c>.
    /// </summary>
    public static class IconFactory
    {
        public const int Size = 96;
        const float Grid = 24f;
        const float Stroke = 2f;

        static readonly Dictionary<Icon, Sprite> Cache = new Dictionary<Icon, Sprite>();

        public static Sprite Get(Icon icon)
        {
            if (Cache.TryGetValue(icon, out var s) && s != null) return s;
            var tex = Render(icon, Size);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "icon_" + icon;
            Cache[icon] = s;
            return s;
        }

        /// <summary>Tekstur ikon putih + alpha. <paramref name="readable"/> = piksel tetap bisa dibaca (tes).</summary>
        public static Texture2D Render(Icon icon, int size, bool readable = false)
        {
            var p = new Painter();
            Draw(icon, p);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
                name = "icon_" + icon
            };
            var px = new Color32[size * size];
            float unitsPerPixel = Grid / size, pixelsPerUnit = size / Grid;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var q = new Vector2((x + 0.5f) * unitsPerPixel, (y + 0.5f) * unitsPerPixel);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(p.Coverage(q, pixelsPerUnit) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, !readable);
            return tex;
        }

        static void Draw(Icon icon, Painter p)
        {
            switch (icon)
            {
                case Icon.Back:
                    p.Seg(19, 12, 5, 12); p.Seg(5, 12, 11, 18); p.Seg(5, 12, 11, 6);
                    break;
                case Icon.Close:
                    p.Seg(6, 6, 18, 18); p.Seg(6, 18, 18, 6);
                    break;
                case Icon.ChevronLeft:
                    p.Seg(15, 19, 8, 12); p.Seg(8, 12, 15, 5);
                    break;
                case Icon.ChevronRight:
                    p.Seg(9, 19, 16, 12); p.Seg(16, 12, 9, 5);
                    break;
                case Icon.ArrowUp:
                    p.Seg(12, 5, 12, 19); p.Seg(12, 19, 6, 13); p.Seg(12, 19, 18, 13);
                    break;
                case Icon.ArrowDown:
                    p.Seg(12, 19, 12, 5); p.Seg(12, 5, 6, 11); p.Seg(12, 5, 18, 11);
                    break;
                case Icon.RotateLeft:
                    p.ArcArrow(12, 12, 6.5f, -20f, 200f);
                    break;
                case Icon.RotateRight:
                    p.ArcArrow(12, 12, 6.5f, 200f, -20f);
                    break;
                case Icon.Reset:
                    p.ArcArrow(12, 12, 6.5f, 100f, -200f);
                    p.Disc(12, 12, 1.8f);
                    break;
                case Icon.Book:
                    p.RRect(7.5f, 12, 4.5f, 7, 1.2f); p.RRect(16.5f, 12, 4.5f, 7, 1.2f);
                    break;
                case Icon.Layers:
                    p.Poly(true, 12, 21, 21, 16.5f, 12, 12, 3, 16.5f);
                    p.Poly(false, 3, 12, 12, 7.5f, 21, 12);
                    p.Poly(false, 3, 7.5f, 12, 3, 21, 7.5f);
                    break;
                case Icon.Blade:
                    Keris(p);
                    break;
                case Icon.Tag:
                    p.Poly(true, 3, 12, 8, 17.5f, 20.5f, 17.5f, 20.5f, 6.5f, 8, 6.5f);
                    p.Disc(9, 12, 1.5f);
                    break;
                case Icon.Spin360:
                    p.EllipseArrow(12, 11, 9, 4.5f, -60f, 240f);
                    p.Seg(12, 3.5f, 12, 20.5f);
                    break;
                case Icon.Move:
                    p.Seg(12, 3, 12, 21); p.Seg(3, 12, 21, 12);
                    p.Arrowhead(new Vector2(12, 21), Vector2.up, 2.8f);
                    p.Arrowhead(new Vector2(12, 3), Vector2.down, 2.8f);
                    p.Arrowhead(new Vector2(21, 12), Vector2.right, 2.8f);
                    p.Arrowhead(new Vector2(3, 12), Vector2.left, 2.8f);
                    break;
                case Icon.Lock:
                    p.RRect(12, 8.5f, 7, 5.5f, 1.5f);
                    p.Arc(12, 14.5f, 4.5f, 0f, 180f);
                    p.Seg(7.5f, 14.5f, 7.5f, 14); p.Seg(16.5f, 14.5f, 16.5f, 14);
                    p.Disc(12, 8.5f, 1.4f);
                    break;
                case Icon.Unlock:
                    p.RRect(12, 8.5f, 7, 5.5f, 1.5f);
                    p.Arc(12, 17.5f, 4.5f, 0f, 180f);
                    p.Seg(7.5f, 17.5f, 7.5f, 14); p.Seg(16.5f, 17.5f, 16.5f, 16.8f);
                    p.Disc(12, 8.5f, 1.4f);
                    break;
                case Icon.Qr:
                    foreach (var (cx, cy) in new[] { (6.5f, 17.5f), (17.5f, 17.5f), (6.5f, 6.5f) })
                    {
                        p.RRect(cx, cy, 3.5f, 3.5f, 1f);
                        p.RRectFill(cx, cy, 1.1f, 1.1f, 0.4f);
                    }
                    foreach (var (cx, cy) in new[] { (14.5f, 4f), (20f, 4f), (17.25f, 6.75f), (14.5f, 9.5f), (20f, 9.5f) })
                        p.RRectFill(cx, cy, 1.1f, 1.1f, 0.4f);
                    break;
                case Icon.ScanFrame:
                    p.Seg(3, 15.5f, 3, 21); p.Seg(3, 21, 8.5f, 21);
                    p.Seg(15.5f, 21, 21, 21); p.Seg(21, 21, 21, 15.5f);
                    p.Seg(3, 8.5f, 3, 3); p.Seg(3, 3, 8.5f, 3);
                    p.Seg(15.5f, 3, 21, 3); p.Seg(21, 3, 21, 8.5f);
                    p.Seg(7, 12, 17, 12);
                    break;
                case Icon.Compass:
                    p.Circle(12, 12, 9);
                    p.PolyFill(16, 16, 13.4f, 10.6f, 8, 8, 10.6f, 13.4f);
                    break;
                case Icon.Cube:
                    p.Poly(true, 12, 21, 20, 16.5f, 20, 7.5f, 12, 3, 4, 7.5f, 4, 16.5f);
                    p.Seg(12, 12, 12, 3); p.Seg(12, 12, 4, 16.5f); p.Seg(12, 12, 20, 16.5f);
                    break;
                case Icon.Grid:
                    p.RRect(7, 17, 3.5f, 3.5f, 1.2f); p.RRect(17, 17, 3.5f, 3.5f, 1.2f);
                    p.RRect(7, 7, 3.5f, 3.5f, 1.2f); p.RRect(17, 7, 3.5f, 3.5f, 1.2f);
                    break;
                case Icon.Settings:
                    p.Circle(12, 12, 3);
                    p.Circle(12, 12, 7);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        p.Seg(new Vector2(12, 12) + d * 7.6f, new Vector2(12, 12) + d * 9.6f);
                    }
                    break;
                case Icon.Play:
                    p.PolyFill(8, 5, 8, 19, 19.5f, 12);
                    break;
                case Icon.Pause:
                    p.RRectFill(8.5f, 12, 1.8f, 7, 1); p.RRectFill(15.5f, 12, 1.8f, 7, 1);
                    break;
                case Icon.SkipNext:
                    p.PolyFill(5, 5, 5, 19, 15, 12);
                    p.RRectFill(18, 12, 1.5f, 7, 0.8f);
                    break;
                case Icon.Speaker:
                    p.PolyFill(3, 9, 7, 9, 12, 4.5f, 12, 19.5f, 7, 15, 3, 15);
                    p.Arc(12.5f, 12, 4, -45f, 45f);
                    p.Arc(12.5f, 12, 7.5f, -50f, 50f);
                    break;
                case Icon.Info:
                    p.Circle(12, 12, 9);
                    p.Seg(12, 7, 12, 12.5f);
                    p.Disc(12, 16, 1.4f);
                    break;
                case Icon.Shield:
                    p.Poly(true, 12, 21.5f, 19.5f, 18.5f, 19.5f, 11.5f, 18, 7.5f, 15.5f, 4.8f, 12, 2.5f, 8.5f, 4.8f, 6, 7.5f, 4.5f, 11.5f, 4.5f, 18.5f);
                    p.Seg(8.5f, 12, 11, 9.5f); p.Seg(11, 9.5f, 15.5f, 14);
                    break;
                case Icon.Target:
                    p.Circle(12, 12, 7);
                    p.Disc(12, 12, 2);
                    p.Seg(12, 2, 12, 5); p.Seg(12, 19, 12, 22); p.Seg(2, 12, 5, 12); p.Seg(19, 12, 22, 12);
                    break;
                case Icon.Alert:
                    p.Poly(true, 12, 21, 21.5f, 4, 2.5f, 4);
                    p.Seg(12, 15, 12, 10.5f);
                    p.Disc(12, 7.2f, 1.3f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(icon), icon, null);
            }
        }

        /// <summary>Keris miring: hulu, ganja melintang, dan bilah ber-luk yang meruncing ke kanan atas.</summary>
        static void Keris(Painter p)
        {
            var dir = new Vector2(1f, 1f).normalized;
            var n = new Vector2(-dir.y, dir.x);
            p.Seg(3.5f, 3.5f, 6.5f, 6.5f);
            var g = new Vector2(7.4f, 7.4f);
            p.Seg(g - n * 2.8f, g + n * 2.8f);
            float[] t = { 0f, 2.5f, 5f, 7.5f, 10f, 12.5f, 14.8f, 16.6f };
            float[] off = { 0f, 1.1f, -1.1f, 1.1f, -1.1f, 0.9f, -0.5f, 0f };
            var start = new Vector2(8.3f, 8.3f);
            for (int i = 0; i + 1 < t.Length; i++)
                p.Seg(start + dir * t[i] + n * off[i], start + dir * t[i + 1] + n * off[i + 1]);
        }

        // ------------------------------------------------------------------ rasterisasi

        sealed class Painter
        {
            readonly List<Func<Vector2, float>> strokes = new List<Func<Vector2, float>>(); // jarak ke garis tengah
            readonly List<Func<Vector2, float>> fills = new List<Func<Vector2, float>>();   // jarak bertanda (negatif = dalam)

            public void Seg(float ax, float ay, float bx, float by) => Seg(new Vector2(ax, ay), new Vector2(bx, by));

            public void Seg(Vector2 a, Vector2 b) => strokes.Add(q => DistSeg(q, a, b));

            public void Poly(bool closed, params float[] xy)
            {
                int n = xy.Length / 2;
                for (int i = 0; i + 1 < n; i++) Seg(xy[i * 2], xy[i * 2 + 1], xy[i * 2 + 2], xy[i * 2 + 3]);
                if (closed) Seg(xy[(n - 1) * 2], xy[(n - 1) * 2 + 1], xy[0], xy[1]);
            }

            public void Circle(float cx, float cy, float r)
            {
                var c = new Vector2(cx, cy);
                strokes.Add(q => Mathf.Abs((q - c).magnitude - r));
            }

            public void Disc(float cx, float cy, float r)
            {
                var c = new Vector2(cx, cy);
                fills.Add(q => (q - c).magnitude - r);
            }

            /// <summary>Busur dari sudut a0 ke a1 (derajat, berlawanan jarum jam dari +x bila a1 &gt; a0).</summary>
            public void Arc(float cx, float cy, float r, float a0, float a1)
            {
                var c = new Vector2(cx, cy);
                float lo = Mathf.Min(a0, a1), sweep = Mathf.Abs(a1 - a0);
                var e0 = c + Dir(lo) * r;
                var e1 = c + Dir(lo + sweep) * r;
                strokes.Add(q =>
                {
                    var d = q - c;
                    float ang = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - lo, 360f);
                    if (ang <= sweep) return Mathf.Abs(d.magnitude - r);
                    return Mathf.Min((q - e0).magnitude, (q - e1).magnitude);
                });
            }

            /// <summary>Busur + mata panah di ujung a1, searah gerak (a1 &gt; a0 = berlawanan jarum jam).</summary>
            public void ArcArrow(float cx, float cy, float r, float a0, float a1)
            {
                Arc(cx, cy, r, a0, a1);
                var tip = new Vector2(cx, cy) + Dir(a1) * r;
                var radial = Dir(a1);
                var tangent = a1 > a0 ? new Vector2(-radial.y, radial.x) : new Vector2(radial.y, -radial.x);
                Arrowhead(tip, tangent, 2.8f);
            }

            public void EllipseArrow(float cx, float cy, float rx, float ry, float a0, float a1)
            {
                const int steps = 24;
                Vector2 At(float deg) => new Vector2(cx + rx * Mathf.Cos(deg * Mathf.Deg2Rad), cy + ry * Mathf.Sin(deg * Mathf.Deg2Rad));
                for (int i = 0; i < steps; i++)
                    Seg(At(Mathf.Lerp(a0, a1, i / (float)steps)), At(Mathf.Lerp(a0, a1, (i + 1) / (float)steps)));
                float a = a1 * Mathf.Deg2Rad, sign = Mathf.Sign(a1 - a0);
                var tangent = new Vector2(-rx * Mathf.Sin(a), ry * Mathf.Cos(a)).normalized * sign;
                Arrowhead(At(a1), tangent, 3f);
            }

            public void Arrowhead(Vector2 tip, Vector2 dir, float len)
            {
                dir.Normalize();
                var n = new Vector2(-dir.y, dir.x);
                Seg(tip, tip - dir * len + n * len);
                Seg(tip, tip - dir * len - n * len);
            }

            public void RRect(float cx, float cy, float hw, float hh, float r)
            {
                var c = new Vector2(cx, cy);
                strokes.Add(q => Mathf.Abs(SpriteFactory.SdRoundBox(q.x - c.x, q.y - c.y, hw, hh, r)));
            }

            public void RRectFill(float cx, float cy, float hw, float hh, float r)
            {
                var c = new Vector2(cx, cy);
                fills.Add(q => SpriteFactory.SdRoundBox(q.x - c.x, q.y - c.y, hw, hh, r));
            }

            public void PolyFill(params float[] xy)
            {
                var pts = new Vector2[xy.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
                fills.Add(q => SdPolygon(q, pts));
            }

            public float Coverage(Vector2 q, float pixelsPerUnit)
            {
                float a = 0f;
                foreach (var s in strokes) a = Mathf.Max(a, Mathf.Clamp01((Stroke * 0.5f - s(q)) * pixelsPerUnit + 0.5f));
                foreach (var f in fills) a = Mathf.Max(a, Mathf.Clamp01(-f(q) * pixelsPerUnit + 0.5f));
                return a;
            }

            static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

            static float DistSeg(Vector2 q, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return (q - (a + ab * t)).magnitude;
            }

            /// <summary>Jarak bertanda ke poligon sembarang (negatif di dalam).</summary>
            static float SdPolygon(Vector2 q, Vector2[] v)
            {
                float d = Vector2.Dot(q - v[0], q - v[0]);
                float s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    var e = v[j] - v[i];
                    var w = q - v[i];
                    var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = q.y >= v[i].y, c2 = q.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            }
        }
    }
}
