using UnityEngine;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Tekstur prosedural bernuansa Nusantara: latar gading bergumpal warna (untuk kaca), motif kawung samar,
    /// cahaya radial di bawah objek, dan grid bidang AR. Dipakai saat runtime (latar) dan oleh editor (aset glow & grid).
    /// </summary>
    public static class ProceduralTextures
    {
        /// <summary>Latar tegak: gradasi gading + gumpalan lembut terakota & nila. <paramref name="blobs"/> 0..1 (redup di detail 3D).</summary>
        public static Texture2D Backdrop(float blobs, int width = 256, int height = 512)
        {
            var tex = New(width, height, false, TextureWrapMode.Clamp);
            var px = new Color32[width * height];
            var top = Theme.Bg;
            var bottom = Color.Lerp(Theme.Bg, Theme.Stage, 0.9f);
            var terracotta = Theme.AccentSoft;
            var nila = Color.Lerp(Theme.Nav, Theme.Bg, 0.35f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
                var c = Color.Lerp(bottom, top, v);
                c = Color.Lerp(c, terracotta, Blob(u, v, 0.88f, 0.84f, 0.5f) * 0.42f * blobs);
                c = Color.Lerp(c, nila, Blob(u, v, 0.06f, 0.5f, 0.42f) * 0.22f * blobs);
                c = Color.Lerp(c, terracotta, Blob(u, v, 0.28f, 0.06f, 0.38f) * 0.26f * blobs);
                c = Color.Lerp(c, Theme.Gold, Blob(u, v, 0.7f, 0.3f, 0.3f) * 0.12f * blobs);
                px[y * width + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static float Blob(float u, float v, float cx, float cy, float r)
        {
            // Aspek potret: jarak tegak dipendekkan agar gumpalan tetap bulat di layar 9:19,5.
            float dx = (u - cx), dy = (v - cy) * 2f;
            float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Satu petak motif kawung (empat kelopak lonjong mengelilingi titik), putih + alpha, bisa diulang.</summary>
        public static Texture2D KawungTile(int size = 128)
        {
            var tex = New(size, size, true, TextureWrapMode.Repeat);
            var px = new Color32[size * size];
            const float a = 0.235f, b = 0.13f, stroke = 0.012f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Min(
                    Mathf.Min(Ellipse(u - 0.25f, v, a, b), Ellipse(u + 0.25f, v, a, b)),
                    Mathf.Min(Ellipse(u, v - 0.25f, b, a), Ellipse(u, v + 0.25f, b, a)));
                float dot = Mathf.Sqrt(u * u + v * v) - 0.03f;
                float alpha = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(d) / stroke), Mathf.Clamp01(1f - dot * size));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static float Ellipse(float x, float y, float a, float b)
        {
            float k = Mathf.Sqrt(x * x / (a * a) + y * y / (b * b));
            return (k - 1f) * Mathf.Min(a, b);
        }

        /// <summary>Cahaya radial putih (alpha memudar ke tepi) untuk glow di bawah objek.</summary>
        public static Texture2D RadialGlow(int size = 128, bool readable = false)
        {
            var tex = New(size, size, true, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c);
                float a = Mathf.Pow(1f - r, 2.2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, !readable);
            return tex;
        }

        /// <summary>Satu petak grid bidang AR: garis gading tegas di tepi petak, isi terakota tipis.</summary>
        public static Texture2D PlaneGrid(int size = 256, bool readable = false)
        {
            var tex = New(size, size, true, TextureWrapMode.Repeat);
            var px = new Color32[size * size];
            var line = Theme.Bg;
            var fill = Theme.AccentSoft;
            const float half = 1.6f; // setengah tebal garis (piksel)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x + 0.5f, size - x - 0.5f), dy = Mathf.Min(y + 0.5f, size - y - 0.5f);
                float l = Mathf.Clamp01(half + 0.5f - Mathf.Min(dx, dy));
                var col = Color.Lerp(fill, line, l);
                col.a = Mathf.Lerp(0.1f, 0.8f, l);
                px[y * size + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply(true, !readable);
            return tex;
        }

        static Texture2D New(int w, int h, bool mips, TextureWrapMode wrap) =>
            new Texture2D(w, h, TextureFormat.RGBA32, mips)
            {
                wrapMode = wrap,
                filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
    }
}
