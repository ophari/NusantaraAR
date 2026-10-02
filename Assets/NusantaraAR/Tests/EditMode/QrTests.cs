using System.Collections.Generic;
using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Uji mode Scan QR tanpa kamera: encoder/decoder QR dibandingkan dengan pustaka Python qrcode, koreksi galat,
    /// deteksi di gambar sintetis (perspektif, keempat orientasi, jauh/dekat, gradasi cahaya), pose, dan kartu cetak.
    /// </summary>
    public class QrTests
    {
        const int W = 360, H = 640;
        const string Bali = "NUSANTARA:KERIS_BALI_01";
        const string Sumatra = "NUSANTARA:KERIS_SUMATRA_01";
        const string Borobudur = "NUSANTARA:BOROBUDUR_01";

        static readonly Vector2[] BaseCorners = { new Vector2(80, 200), new Vector2(270, 188), new Vector2(292, 395), new Vector2(66, 380) };

        // python: qrcode.QRCode(version=2, error_correction=M, mask_pattern=3), data mode byte "NUSANTARA:KERIS_BALI_01"
        static readonly string[] PythonByteV2Mask3 =
        {
            "#######.#..#...##.#######", "#.....#.####..#...#.....#", "#.###.#..########.#.###.#", "#.###.#.##.#..##..#.###.#",
            "#.###.#..##..##.#.#.###.#", "#.....#...#####...#.....#", "#######.#.#.#.#.#.#######", "........####..###........",
            "#.##.###..######..#..#.##", "#.#....#.###.#.###...#.##", ".###..#.#.###.#.#######..", "#.#....#....#####.##.###.",
            ".####.###.#.#.#####..#.##", ".#.##..#...###..##.##....", ".##.#.#.#.##..#...######.", "#..###.###.#..##.#.......",
            "....###...#.#########...#", "........#.#..##.#...##...", "#######.##.....##.#.#####", "#.....#.#.##..###...##..#",
            "#.###.#.........########.", "#.###.#.#.#...######...##", "#.###.#.#.#...#..#.##..#.", "#.....#....#######...##..",
            "#######.#####....##..#.##",
        };

        // python: ECC Q, add_data(optimize=1) -> segmen alfanumerik + byte + numerik, versi 3
        static readonly string[] PythonMixedV3Q =
        {
            "#######.#..###........#######", "#.....#....#...##...#.#.....#", "#.###.#....#.####.#...#.###.#", "#.###.#..#...#....##..#.###.#",
            "#.###.#.#..#......#.#.#.###.#", "#.....#.###....#..###.#.....#", "#######.#.#.#.#.#.#.#.#######", ".........#...#....#.#........",
            ".#######...#.#.##.#....##...#", "#.##....###.#.##...##...##...", "#####.#......##...#..###...##", ".#.###..#..##..##......#.####",
            "..##..##.#.###..#.....#..#.#.", ".##.....###.##.##...####..#..", "##....##.....#....#.##...#.#.", "#.#..#.###...##..###.#.#.##..",
            "...####...#.##.......#.##.#.#", "#####...##...#.####..#.##..#.", "#..#.####.#......#..#.###...#", "#.#..#....#.##..#....##.#.#.#",
            "#..##.###..#..#..########.#..", "........#.###..#.####...###.#", "#######.####.#.####.#.#.##...", "#.....#.#.#..####.###...#..##",
            "#.###.#.#.#..#...#..#####..##", "#.###.#.##....##..#.#...##.#.", "#.###.#.##..#.##..#...#.#..#.", "#.....#.#.#..###..#..##....##",
            "#######....##..####..#.....#.",
        };

        static bool[,] Parse(string[] rows)
        {
            int n = rows.Length;
            var m = new bool[n, n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                m[y, x] = rows[y][x] == '#';
            return m;
        }

        /// <summary>Render QR (+ zona sunyi 4 modul) ke buffer abu-abu dengan supersampling 2x2, derau, dan blur 3x3.</summary>
        static byte[] Render(bool[,] qr, Vector2[] corners, int seed, byte background = 150)
        {
            int n = qr.GetLength(0);
            var img = new byte[W * H];
            var rnd = new System.Random(seed);
            Assert.IsTrue(Homography.FromPoints(corners, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, out var inv));
            float q = 4f / n;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    var m = Homography.Apply(inv, x + 0.25 + sx * 0.5, y + 0.25 + sy * 0.5);
                    float v = background;
                    if (m.x > -q && m.y > -q && m.x < 1 + q && m.y < 1 + q)
                    {
                        v = 225;
                        if (m.x >= 0 && m.y >= 0 && m.x < 1 && m.y < 1)
                        {
                            int u = Mathf.Min(n - 1, (int)(m.x * n)), vv = Mathf.Min(n - 1, (int)(m.y * n));
                            if (qr[vv, u]) v = 30;
                        }
                    }
                    sum += v;
                }
                img[y * W + x] = (byte)Mathf.Clamp(sum / 4f + rnd.Next(-10, 10), 0, 255);
            }
            var blurred = new byte[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int s = 0, c = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                    s += img[yy * W + xx];
                    c++;
                }
                blurred[y * W + x] = (byte)(s / c);
            }
            return blurred;
        }

        static Vector2[] Scaled(float scale)
        {
            var c = new Vector2[4];
            for (int k = 0; k < 4; k++) c[k] = new Vector2(180, 320) + (BaseCorners[k] - new Vector2(180, 290)) * scale;
            return c;
        }

        // ------------------------------------------------------------------ encoder / decoder

        [Test]
        public void Encoder_MatchesPythonQrcode_BitForBit()
        {
            var ours = QrCode.Encode(Bali, QrEcc.M, 2, 3);
            var reference = Parse(PythonByteV2Mask3);
            Assert.AreEqual(25, ours.GetLength(0));
            for (int y = 0; y < 25; y++)
            for (int x = 0; x < 25; x++)
                Assert.AreEqual(reference[y, x], ours[y, x], $"modul ({x},{y})");
        }

        [Test]
        public void Decoder_ReadsPythonQrcode_ByteAndMixedModes()
        {
            Assert.IsTrue(QrCode.TryDecode(Parse(PythonByteV2Mask3), out var a));
            Assert.AreEqual(Bali, a);
            Assert.IsTrue(QrCode.TryDecode(Parse(PythonMixedV3Q), out var b));
            Assert.AreEqual("NUSANTARA:KERIS_SUMATRA_01-2026", b);
        }

        [Test]
        public void RoundTrip_AllEccLevels_AllVersions()
        {
            var rnd = new System.Random(7);
            int versions = 0;
            foreach (QrEcc ecc in System.Enum.GetValues(typeof(QrEcc)))
            for (int len = 0; len < 220; len += 7)
            {
                var chars = new char[len];
                for (int i = 0; i < len; i++) chars[i] = (char)rnd.Next(32, 127);
                var s = new string(chars);
                var m = QrCode.Encode(s, ecc, 1);
                if (m == null) continue; // melebihi versi 10
                versions |= 1 << ((m.GetLength(0) - 17) / 4);
                Assert.IsTrue(QrCode.TryDecode(m, out var back), $"{ecc} panjang {len}");
                Assert.AreEqual(s, back);
            }
            Assert.AreEqual(0b11111111110, versions, "semua versi 1-10 harus teruji");
        }

        [Test]
        public void ReedSolomon_FixesDamage_AndRejectsTooMuch()
        {
            var m = QrCode.Encode(Sumatra);
            int n = m.GetLength(0);
            for (int y = n - 4; y < n; y++)
            for (int x = n - 3; x < n; x++)
                m[y, x] = !m[y, x];
            Assert.IsTrue(QrCode.TryDecode(m, out var back), "kerusakan kecil harus terkoreksi");
            Assert.AreEqual(Sumatra, back);

            for (int y = 9; y < n - 9; y++)
            for (int x = 9; x < n; x++)
                m[y, x] = !m[y, x];
            Assert.IsFalse(QrCode.TryDecode(m, out back) && back == Sumatra, "kerusakan besar tidak boleh terbaca sebagai isi asli");
        }

        [Test]
        public void ReedSolomon_RandomErrorsWithinCapacity()
        {
            var rnd = new System.Random(3);
            for (int trial = 0; trial < 200; trial++)
            {
                var text = "RS" + trial;
                var m = QrCode.Encode(text, QrEcc.H, 1 + trial % 10);
                int n = m.GetLength(0);
                for (int k = 0; k < 1 + trial % 6; k++)
                {
                    int x = rnd.Next(9, n - 9), y = rnd.Next(9, n - 9);
                    if (x != 6 && y != 6) m[y, x] = !m[y, x];
                }
                Assert.IsTrue(QrCode.TryDecode(m, out var back), "percobaan " + trial);
                Assert.AreEqual(text, back);
            }
        }

        // ------------------------------------------------------------------ detektor

        [Test]
        public void Detects_PerspectiveQr_InAllOrientations_WithAccurateCorners()
        {
            foreach (var text in new[] { Bali, Sumatra, Borobudur })
            for (int rot = 0; rot < 4; rot++)
            {
                var c = new Vector2[4];
                for (int k = 0; k < 4; k++) c[k] = BaseCorners[(k + rot) % 4];
                var found = new QrDetector().Detect(Render(QrCode.Encode(text), c, rot + 3), W, H);
                Assert.AreEqual(1, found.Count, $"{text} rotasi {rot}");
                Assert.AreEqual(text, found[0].text);
                for (int k = 0; k < 4; k++)
                    Assert.Less((found[0].corners[k] - c[k]).magnitude, 2.5f, $"{text} rotasi {rot}, sudut {k}");
            }
        }

        [TestCase(0.35f)] // modul ~2,7 px (jauh)
        [TestCase(0.6f)]
        [TestCase(1.5f)]  // dekat
        public void Detects_AtDifferentDistances(float scale)
        {
            var found = new QrDetector().Detect(Render(QrCode.Encode(Sumatra), Scaled(scale), 11), W, H);
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(Sumatra, found[0].text);
        }

        [Test]
        public void Detects_StrongTilt_AndUnevenLight()
        {
            var tilt = new[] { new Vector2(110, 250), new Vector2(250, 250), new Vector2(320, 420), new Vector2(40, 420) };
            var found = new QrDetector().Detect(Render(QrCode.Encode(Sumatra), tilt, 12), W, H);
            Assert.AreEqual(1, found.Count, "miring");
            Assert.AreEqual(Sumatra, found[0].text);

            var img = Render(QrCode.Encode(Bali), BaseCorners, 13);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                img[y * W + x] = (byte)Mathf.Clamp(img[y * W + x] * (0.35f + 0.65f * x / W), 0, 255);
            found = new QrDetector().Detect(img, W, H);
            Assert.AreEqual(1, found.Count, "gradasi cahaya");
            Assert.AreEqual(Bali, found[0].text);
        }

        [Test]
        public void UnreadableQr_StillGivesPose()
        {
            var qr = QrCode.Encode(Bali);
            int n = qr.GetLength(0);
            for (int y = 9; y < n - 9; y++)
            for (int x = 9; x < n; x++)
                qr[y, x] = !qr[y, x];
            var found = new QrDetector().Detect(Render(qr, BaseCorners, 22), W, H);
            Assert.AreEqual(1, found.Count);
            Assert.IsFalse(found[0].Decoded);
            for (int k = 0; k < 4; k++) Assert.Less((found[0].corners[k] - BaseCorners[k]).magnitude, 3f, "sudut " + k);
        }

        [Test]
        public void NoFalsePositive_OnNoise_OrOnLegacyCard()
        {
            var img = new byte[W * H];
            var rnd = new System.Random(5);
            for (int i = 0; i < img.Length; i++) img[i] = (byte)rnd.Next(60, 200);
            Assert.AreEqual(0, new QrDetector().Detect(img, W, H).Count);

            // Kartu penanda lama (6x6) bukan QR, dan QR bukan kartu - keduanya dari satu analisis frame.
            var card = new bool[8, 8];
            for (int v = 0; v < 6; v++)
            for (int u = 0; u < 6; u++)
                card[v + 1, u + 1] = MarkerPattern.IsBlack(MarkerPattern.KerisBaliCode, u, v);
            var regions = new DarkRegions();
            regions.Analyze(Render(card, BaseCorners, 4), W, H);
            Assert.AreEqual(0, new QrDetector().Detect(regions).Count);

            regions.Analyze(Render(QrCode.Encode(Bali), BaseCorners, 6), W, H);
            Assert.AreEqual(1, new QrDetector().Detect(regions).Count);
            Assert.AreEqual(0, new MarkerDetector().Detect(regions, new[] { MarkerPattern.KerisBaliCode, MarkerPattern.KerisSumatraCode, MarkerPattern.BorobudurCode }).Count);
        }

        [Test]
        public void Pose_FromQrCorners_MatchesKnownTransform()
        {
            const float size = 0.08f, f = 520f, cx = W / 2f, cy = H / 2f;
            var rot = Quaternion.Euler(-35f, 20f, 10f); // ruang kamera CV
            var t = new Vector3(0.02f, 0.03f, 0.35f);
            float s = size / 2f;
            var model = new[] { new Vector3(-s, -s, 0), new Vector3(s, -s, 0), new Vector3(s, s, 0), new Vector3(-s, s, 0) };
            var px = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                var p = rot * model[i] + t;
                px[i] = new Vector2(f * p.x / p.z + cx, f * p.y / p.z + cy);
            }
            var found = new QrDetector().Detect(Render(QrCode.Encode(Bali), px, 9), W, H);
            Assert.AreEqual(1, found.Count);
            Assert.IsTrue(MarkerPose.TryEstimate(found[0].corners, size, f, cx, cy, out var cam));
            var center = (cam[0] + cam[1] + cam[2] + cam[3]) * 0.25f;
            var expected = new Vector3(t.x, -t.y, t.z);
            Assert.Less((center - expected).magnitude, 0.01f, $"pusat {center} vs {expected}");
            var pose = MarkerPose.ArtifactPose(cam);
            Assert.Greater(Vector3.Dot(pose.up, -center.normalized), 0.3f, "normal QR harus menghadap kamera");
        }

        [TestCase("Docs/KartuQR_KerisBali_A5.png", Bali)]
        [TestCase("Docs/KartuQR_KerisSumatra_A5.png", Sumatra)]
        [TestCase("Docs/KartuQR_CandiBorobudur_A5.png", Borobudur)]
        public void Detects_PrintableQrCard_AndIsFastEnough(string path, string text)
        {
            if (!System.IO.File.Exists(path)) Assert.Ignore("Kartu QR belum dibuat (python Tools/kartu_qr.py)");
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(path)));
            // Simulasikan frame kamera 360x640 (kartu memenuhi ~70% lebar, baris 0 = atas).
            var gray = new byte[W * H];
            float scale = tex.width / (W * 0.7f);
            float ox = (W - tex.width / scale) * 0.5f, oy = (H - tex.height / scale) * 0.5f;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x - ox) * scale, v = (y - oy) * scale;
                byte g = 90;
                if (u >= 0 && v >= 0 && u < tex.width && v < tex.height)
                    g = (byte)(tex.GetPixel((int)u, tex.height - 1 - (int)v).grayscale * 255f);
                gray[y * W + x] = g;
            }
            Object.DestroyImmediate(tex);

            // Alur per frame di MarkerController: satu analisis, lalu QR (+ kartu lama sebagai cadangan).
            var regions = new DarkRegions();
            var qr = new QrDetector();
            var cards = new MarkerDetector();
            var codes = new[] { MarkerPattern.KerisBaliCode, MarkerPattern.KerisSumatraCode, MarkerPattern.BorobudurCode };
            List<QrDetection> found = null;
            regions.Analyze(gray, W, H);
            qr.Detect(regions); // pemanasan
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int runs = 10;
            for (int i = 0; i < runs; i++)
            {
                regions.Analyze(gray, W, H);
                found = qr.Detect(regions);
                cards.Detect(regions, codes);
            }
            sw.Stop();
            Debug.Log($"[QrTests] rata-rata analisis + QR + kartu {sw.Elapsed.TotalMilliseconds / runs:F1} ms (Editor/Mono)");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(text, found[0].text);
            Assert.Less(sw.Elapsed.TotalMilliseconds / runs, 150.0);
        }
    }
}
