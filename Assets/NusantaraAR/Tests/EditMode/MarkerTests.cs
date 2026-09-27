using NUnit.Framework;
using NusantaraAR.Marker;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>Uji mode Scan Kartu dengan gambar sintetis (tanpa kamera): deteksi, orientasi, pose, pemetaan rotasi kamera.</summary>
    public class MarkerTests
    {
        const int W = 360, H = 640;
        static readonly int[] Codes = { MarkerPattern.KerisBaliCode };

        /// <summary>Render marker (+ zona putih 1 sel) ke buffer abu-abu; corners = TL, TR, BR, BL marker di gambar.</summary>
        static byte[] Render(Vector2[] corners, int seed = 1, byte background = 150, int code = MarkerPattern.KerisBaliCode)
        {
            var img = new byte[W * H];
            var rnd = new System.Random(seed);
            Assert.IsTrue(Homography.FromPoints(corners, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, out var inv));
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var m = Homography.Apply(inv, x + 0.5, y + 0.5);
                float cell = 1f / MarkerPattern.Grid;
                byte v = (byte)(background + rnd.Next(-12, 12));
                if (m.x > -cell && m.y > -cell && m.x < 1 + cell && m.y < 1 + cell)
                {
                    v = 235;
                    if (m.x >= 0 && m.y >= 0 && m.x < 1 && m.y < 1)
                    {
                        int u = Mathf.Min(MarkerPattern.Grid - 1, (int)(m.x * MarkerPattern.Grid));
                        int vv = Mathf.Min(MarkerPattern.Grid - 1, (int)(m.y * MarkerPattern.Grid));
                        if (MarkerPattern.IsBlack(code, u, vv)) v = 25;
                    }
                    v = (byte)Mathf.Clamp(v + rnd.Next(-10, 10), 0, 255);
                }
                img[y * W + x] = v;
            }
            return img;
        }

        [Test]
        public void Detects_PerspectiveMarker_InAllOrientations()
        {
            var baseCorners = new[] { new Vector2(90, 210), new Vector2(262, 196), new Vector2(285, 390), new Vector2(76, 372) };
            for (int rot = 0; rot < 4; rot++)
            {
                var c = new Vector2[4];
                for (int k = 0; k < 4; k++) c[k] = baseCorners[(k + rot) % 4];
                var img = Render(c, rot + 3);
                var found = new MarkerDetector().Detect(img, W, H, Codes);
                Assert.AreEqual(1, found.Count, "rotasi " + rot);
                Assert.AreEqual(MarkerPattern.KerisBaliCode, found[0].code);
                for (int k = 0; k < 4; k++)
                    Assert.Less((found[0].corners[k] - c[k]).magnitude, 2.5f, $"rotasi {rot}, sudut {k}");
            }
        }

        [Test]
        public void NoFalsePositive_OnPlainNoise()
        {
            var img = new byte[W * H];
            var rnd = new System.Random(5);
            for (int i = 0; i < img.Length; i++) img[i] = (byte)rnd.Next(60, 200);
            Assert.AreEqual(0, new MarkerDetector().Detect(img, W, H, Codes).Count);
        }

        [Test]
        public void Pose_MatchesKnownTransform()
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
            var found = new MarkerDetector().Detect(Render(px, 9), W, H, Codes);
            Assert.AreEqual(1, found.Count);
            Assert.IsTrue(MarkerPose.TryEstimate(found[0].corners, size, f, cx, cy, out var cam));
            var center = (cam[0] + cam[1] + cam[2] + cam[3]) * 0.25f;
            var expected = new Vector3(t.x, -t.y, t.z);
            Assert.Less((center - expected).magnitude, 0.01f, $"pusat {center} vs {expected}");
            var pose = MarkerPose.ArtifactPose(cam);
            Assert.Greater(Vector3.Dot(pose.up, -center.normalized), 0.3f, "normal kartu harus menghadap kamera");
        }

        /// <summary>Sudut QR (TL, TR, BR, BL) di ruang kamera Unity dari pusat, arah kanan, dan arah tepi atas QR.</summary>
        static Vector3[] QrCorners(Vector3 center, Vector3 right, Vector3 top, float size = 0.08f)
        {
            right = right.normalized * size * 0.5f;
            top = top.normalized * size * 0.5f;
            return new[] { center - right + top, center + right + top, center + right - top, center - right - top };
        }

        static void AssertDir(Vector3 expected, Vector3 actual, string what) =>
            Assert.Greater(Vector3.Dot(expected.normalized, actual.normalized), 0.99f, $"{what}: {actual} vs {expected}");

        [Test]
        public void UprightPose_QrOnScreen_StandsUpFacingViewer()
        {
            // QR di monitor tepat di depan kamera: dulu artefak terbaring dan terlihat dari atas.
            var up = Vector3.up;
            var pose = MarkerPose.UprightPose(QrCorners(new Vector3(0f, 0f, 0.4f), Vector3.right, Vector3.up), up, out float wall, out float yaw);
            AssertDir(up, pose.up, "atas");
            AssertDir(Vector3.forward, pose.forward, "depan (muka -Z ke kamera)");
            Assert.Greater(wall, 0.99f);
            Assert.AreEqual(0f, yaw);
        }

        [Test]
        public void UprightPose_QrOnTable_StandsOnQr()
        {
            // Kamera menunduk 45° ke QR di meja; tepi atas QR menjauhi pengguna.
            var up = new Vector3(0f, 1f, -1f).normalized;
            var away = new Vector3(0f, 1f, 1f).normalized;
            var pose = MarkerPose.UprightPose(QrCorners(new Vector3(0f, 0f, 0.4f), Vector3.right, away), up, out float wall, out float yaw);
            AssertDir(up, pose.up, "atas");
            AssertDir(away, pose.forward, "depan");
            Assert.Less(wall, 0.01f);
            Assert.AreEqual(0f, yaw);
        }

        [Test]
        public void UprightPose_UpsideDownQrOnTable_TurnsToViewer()
        {
            var up = new Vector3(0f, 1f, -1f).normalized;
            var away = new Vector3(0f, 1f, 1f).normalized;
            var pose = MarkerPose.UprightPose(QrCorners(new Vector3(0f, 0f, 0.4f), Vector3.left, -away), up, out _, out float yaw);
            Assert.AreEqual(180f, Mathf.Abs(yaw));
            AssertDir(away, Quaternion.AngleAxis(yaw, up) * pose.forward, "depan setelah yawToViewer");
        }

        [Test]
        public void UprightPose_QrLeaningBack_StaysUpright()
        {
            // Layar laptop condong 45° ke belakang, kamera mendatar.
            var up = Vector3.up;
            var top = new Vector3(0f, 1f, 1f).normalized;
            var pose = MarkerPose.UprightPose(QrCorners(new Vector3(0f, 0.05f, 0.4f), Vector3.right, top), up, out float wall, out _);
            AssertDir(up, pose.up, "atas");
            AssertDir(Vector3.forward, pose.forward, "depan");
            Assert.That(wall, Is.InRange(0.2f, 0.8f));
        }

        [Test]
        public void UprightBuffer_RotatesLikeTheScreen()
        {
            // Tekstur 8x4, satu piksel terang di pojok kanan-bawah tekstur (tx=7, ty=0).
            var px = new Color32[8 * 4];
            px[0 * 8 + 7] = new Color32(255, 255, 255, 255);
            var go = new GameObject("feed");
            try
            {
                var feed = go.AddComponent<CameraFeed>();
                feed.targetShortSide = 4;
                // Diputar 90° searah jarum jam: kanan-bawah -> kiri-bawah. Buffer tegak 4x8, baris 0 = atas.
                feed.FillGray(px, 8, 4, 90, false);
                Assert.AreEqual(4, feed.Width);
                Assert.AreEqual(8, feed.Height);
                Assert.AreEqual(255, feed.Gray[7 * 4 + 0]);
                // Tanpa rotasi: kanan-bawah tetap kanan-bawah.
                feed.FillGray(px, 8, 4, 0, false);
                Assert.AreEqual(255, feed.Gray[3 * 8 + 7]);
                // 270°: kanan-bawah -> kanan-atas.
                feed.FillGray(px, 8, 4, 270, false);
                Assert.AreEqual(255, feed.Gray[0 * 4 + 3]);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static int[] Rotations(int code)
        {
            var grids = new int[4];
            for (int r = 0; r < 4; r++)
            {
                int c = 0;
                for (int v = 1; v <= 4; v++)
                for (int u = 1; u <= 4; u++)
                {
                    int su = u, sv = v;
                    for (int k = 0; k < r; k++) { int tmp = su; su = 5 - sv; sv = tmp; }
                    if (MarkerPattern.IsBlack(code, su, sv)) c |= 1 << (15 - ((v - 1) * 4 + (u - 1)));
                }
                grids[r] = c;
            }
            return grids;
        }

        [TestCase(MarkerPattern.KerisBaliCode)]
        [TestCase(MarkerPattern.KerisSumatraCode)]
        public void MarkerCode_RotationsAreDistinct(int code)
        {
            var grids = Rotations(code);
            for (int a = 0; a < 4; a++)
            for (int b = a + 1; b < 4; b++)
                Assert.GreaterOrEqual(MarkerPattern.HammingDistance(grids[a], grids[b]), 4);
        }

        [Test]
        public void MarkerCodes_AreFarFromEachOther_InEveryRotation()
        {
            // Detektor menerima 1 bit salah, jadi jarak antarkode (semua rotasi) harus >= 3 agar tidak tertukar.
            foreach (int a in AllCodes)
            foreach (int b in AllCodes)
            {
                if (a == b) continue;
                foreach (int r in Rotations(a))
                    Assert.GreaterOrEqual(MarkerPattern.HammingDistance(r, b), 3, a.ToString("X4") + " vs " + b.ToString("X4") + " rotasi " + r.ToString("X4"));
            }
        }

        static readonly int[] AllCodes = { MarkerPattern.KerisBaliCode, MarkerPattern.KerisSumatraCode };

        [Test]
        public void Detects_TheRightArtifact_WhenAllCodesAreKnown()
        {
            var corners = new[] { new Vector2(90, 210), new Vector2(262, 196), new Vector2(285, 390), new Vector2(76, 372) };
            foreach (int code in AllCodes)
            {
                var found = new MarkerDetector().Detect(Render(corners, 11, 150, code), W, H, AllCodes);
                Assert.AreEqual(1, found.Count, code.ToString("X4"));
                Assert.AreEqual(code, found[0].code);
            }
        }

        [TestCase("Docs/KartuPenanda_KerisBali_A5.png", MarkerPattern.KerisBaliCode)]
        [TestCase("Docs/KartuPenanda_KerisSumatra_A5.png", MarkerPattern.KerisSumatraCode)]
        public void Detects_PrintableCard_AndIsFastEnough(string path, int code)
        {
            var codes = AllCodes;
            if (!System.IO.File.Exists(path)) Assert.Ignore("Kartu belum dibuat");
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(System.IO.File.ReadAllBytes(path)));
            // Simulasikan frame kamera 360x640 (kartu memenuhi ~70% lebar, baris 0 = atas).
            int w = 360, h = 640;
            var gray = new byte[w * h];
            float scale = tex.width / (w * 0.7f);
            float ox = (w - tex.width / scale) * 0.5f, oy = (h - tex.height / scale) * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x - ox) * scale, v = (y - oy) * scale;
                byte g = 90;
                if (u >= 0 && v >= 0 && u < tex.width && v < tex.height)
                {
                    var c = tex.GetPixel((int)u, tex.height - 1 - (int)v);
                    g = (byte)(c.grayscale * 255f);
                }
                gray[y * w + x] = g;
            }
            Object.DestroyImmediate(tex);
            var detector = new MarkerDetector();
            detector.Detect(gray, w, h, codes); // pemanasan
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int runs = 10;
            System.Collections.Generic.List<MarkerDetection> found = null;
            for (int i = 0; i < runs; i++) found = detector.Detect(gray, w, h, codes);
            sw.Stop();
            Debug.Log($"[MarkerTests] rata-rata deteksi {sw.Elapsed.TotalMilliseconds / runs:F1} ms (Editor/Mono)");
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(code, found[0].code);
            Assert.Less(sw.Elapsed.TotalMilliseconds / runs, 120.0);
        }
    }
}
