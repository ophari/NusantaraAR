using UnityEngine;

namespace NusantaraAR.Marker
{
    /// <summary>
    /// Marker persegi gaya ArUco: grid 6x6 sel = bingkai hitam 1 sel + 4x4 bit data (1 = hitam).
    /// Kode dipilih agar keempat rotasinya berbeda jauh (jarak Hamming >= 10), sehingga orientasi kartu selalu jelas.
    /// </summary>
    public static class MarkerPattern
    {
        public const int Grid = 6;
        public const int DataBits = 4;

        /// <summary>Kode marker untuk Keris Bali (pola #.## / .#.# / ..## / ..#.).</summary>
        public const int KerisBaliCode = 0xB532;

        /// <summary>
        /// Kode marker untuk Keris Sumatra (pola #### / .... / ###. / .#..); keempat rotasinya berjarak >= 10,
        /// jarak ke semua rotasi 0xB532 >= 8, dan >= 6 dari kartu lama yang sudah ditarik (0xEEC1, 0xDA26).
        /// </summary>
        public const int KerisSumatraCode = 0xF0E4;

        /// <summary>
        /// Kode marker untuk Candi Borobudur (pola ###. / ..## / #.## / ...#); keempat rotasinya berjarak >= 10,
        /// jarak ke semua rotasi kedua kode keris >= 7, dan >= 6 dari kartu yang sudah ditarik (0xEEC1, 0xDA26).
        /// </summary>
        public const int BorobudurCode = 0xE3B1;

        /// <summary>True bila sel (kolom u, baris v; 0..5, dari kiri-atas) berwarna hitam.</summary>
        public static bool IsBlack(int code, int u, int v)
        {
            if (u == 0 || v == 0 || u == Grid - 1 || v == Grid - 1) return true;
            int bit = (v - 1) * DataBits + (u - 1);
            return ((code >> (15 - bit)) & 1) == 1;
        }

        public static int HammingDistance(int a, int b)
        {
            int x = (a ^ b) & 0xFFFF, n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }

        /// <summary>Gambar marker (dengan zona putih di sekeliling = <paramref name="quietCells"/> sel).</summary>
        public static Texture2D CreateTexture(int code, int cellPixels = 64, int quietCells = 1)
        {
            int size = (Grid + quietCells * 2) * cellPixels;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, name = "Marker_" + code.ToString("X4") };
            var px = new Color32[size * size];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int u = x / cellPixels - quietCells;
                int v = (size - 1 - y) / cellPixels - quietCells; // baris 0 = atas
                bool inMarker = u >= 0 && v >= 0 && u < Grid && v < Grid;
                px[y * size + x] = inMarker && IsBlack(code, u, v) ? black : white;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            return tex;
        }
    }
}
