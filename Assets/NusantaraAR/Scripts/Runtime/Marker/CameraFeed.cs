using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.Marker
{
    /// <summary>
    /// Kamera belakang lewat WebCamTexture (tidak butuh ARCore). Menyediakan:
    /// (1) buffer grayscale tegak (sesuai orientasi layar, baris 0 = atas) untuk deteksi marker,
    /// (2) pengaturan RawImage latar agar gambar kamera memenuhi layar (crop tengah),
    /// (3) intrinsik perkiraan (FOV sisi panjang) yang juga dipakai untuk FOV kamera 3D, sehingga overlay selalu sejajar.
    /// </summary>
    public class CameraFeed : MonoBehaviour
    {
        [Tooltip("Perkiraan FOV sisi panjang kamera belakang HP (derajat)")]
        public float assumedLongSideFov = 64f;
        [Tooltip("Sisi pendek buffer deteksi (piksel)")]
        public int targetShortSide = 360;
        public int requestedWidth = 1280;
        public int requestedHeight = 720;

        WebCamTexture texture;
        Color32[] pixels;
        int step = 1;

        public byte[] Gray { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Angle { get; private set; }
        public bool Mirrored { get; private set; }
        public bool IsRunning => texture != null && texture.isPlaying;
        public Texture Texture => texture;

        /// <summary>Panjang fokus dalam piksel buffer deteksi.</summary>
        public float FocalPixels => Mathf.Max(Width, Height) * 0.5f / Mathf.Tan(assumedLongSideFov * 0.5f * Mathf.Deg2Rad);

        public static bool HasCamera => WebCamTexture.devices != null && WebCamTexture.devices.Length > 0;

        public bool StartFeed()
        {
            try
            {
                var devices = WebCamTexture.devices;
                if (devices == null || devices.Length == 0) return false;
                var device = devices[0];
                foreach (var d in devices)
                    if (!d.isFrontFacing) { device = d; break; }
                texture = new WebCamTexture(device.name, requestedWidth, requestedHeight, 30);
                texture.Play();
                // Fokus ke tengah layar (tempat kartu biasanya diarahkan) bila perangkat mendukung.
                if (device.isAutoFocusPointSupported) texture.autoFocusPoint = new Vector2(0.5f, 0.5f);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[NusantaraAR] Gagal memulai WebCamTexture: " + e.Message);
                return false;
            }
        }

        public void StopFeed()
        {
            if (texture != null && texture.isPlaying) texture.Stop();
        }

        void OnDestroy() => StopFeed();

        void OnApplicationPause(bool paused)
        {
            if (texture == null) return;
            if (paused) texture.Pause();
            else texture.Play();
        }

        /// <summary>Ambil frame baru ke buffer grayscale. False bila belum ada frame baru.</summary>
        public bool Grab()
        {
            if (texture == null || !texture.isPlaying || !texture.didUpdateThisFrame || texture.width <= 16) return false;
            int tw = texture.width, th = texture.height;
            if (pixels == null || pixels.Length != tw * th) pixels = new Color32[tw * th];
            texture.GetPixels32(pixels);
            Angle = ((texture.videoRotationAngle % 360) + 360) % 360;
            Mirrored = texture.videoVerticallyMirrored;
            FillGray(pixels, tw, th, Angle, Mirrored);
            return true;
        }

        /// <summary>Isi buffer tegak dari piksel tekstur (baris 0 tekstur = bawah). Publik untuk pengujian.</summary>
        public void FillGray(Color32[] px, int tw, int th, int angle, bool mirrored)
        {
            bool swap = angle == 90 || angle == 270;
            int uw = swap ? th : tw, uh = swap ? tw : th;
            step = Mathf.Max(1, Mathf.Min(uw, uh) / Mathf.Max(64, targetShortSide));
            int w = uw / step, h = uh / step;
            if (Gray == null || Gray.Length != w * h) Gray = new byte[w * h];
            Width = w;
            Height = h;
            int half = step / 2;
            for (int oy = 0; oy < h; oy++)
            {
                int uy = uh - 1 - (oy * step + half); // tegak, sumbu y ke atas
                for (int ox = 0; ox < w; ox++)
                {
                    int ux = ox * step + half;
                    MapUprightToTexture(ux, uy, tw, th, angle, mirrored, out int tx, out int ty);
                    var c = px[ty * tw + tx];
                    Gray[oy * w + ox] = (byte)((c.r * 77 + c.g * 150 + c.b * 29) >> 8);
                }
            }
        }

        /// <summary>Koordinat tegak (y ke atas) -> koordinat tekstur, kebalikan dari rotasi searah jarum jam sebesar angle.</summary>
        public static void MapUprightToTexture(int ux, int uy, int tw, int th, int angle, bool mirrored, out int tx, out int ty)
        {
            switch (angle)
            {
                case 90: tx = tw - 1 - uy; ty = ux; break;
                case 180: tx = tw - 1 - ux; ty = th - 1 - uy; break;
                case 270: tx = uy; ty = th - 1 - ux; break;
                default: tx = ux; ty = uy; break;
            }
            if (mirrored) ty = th - 1 - ty;
        }

        /// <summary>
        /// Mengatur RawImage latar (di dalam kanvas <paramref name="canvasRect"/>) agar memenuhi layar dengan orientasi benar,
        /// lalu menyamakan FOV vertikal kamera 3D dengan bagian gambar yang terlihat.
        /// </summary>
        public void ConfigureDisplay(RawImage image, RectTransform canvasRect, Camera cam)
        {
            if (texture == null || texture.width <= 16) return;
            int tw = texture.width, th = texture.height;
            bool swap = Angle == 90 || Angle == 270;
            float uw = swap ? th : tw, uh = swap ? tw : th;
            var size = canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float s = Mathf.Max(size.x / uw, size.y / uh);

            image.texture = texture;
            image.uvRect = Mirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(tw * s, th * s);
            rt.localEulerAngles = new Vector3(0f, 0f, -Angle);

            // Tinggi gambar tegak yang terlihat (dalam piksel buffer deteksi).
            float visibleHeight = size.y / s / step;
            cam.fieldOfView = 2f * Mathf.Atan(visibleHeight * 0.5f / FocalPixels) * Mathf.Rad2Deg;
        }
    }
}
