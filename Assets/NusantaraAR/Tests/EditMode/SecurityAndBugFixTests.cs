using System.Collections.Generic;
using NusantaraAR.Marker;
using NusantaraAR.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Uji regresi keamanan (OWASP Mobile / CWE) dan perbaikan bug stabilitas runtime.
    /// </summary>
    public class SecurityAndBugFixTests
    {
        [Test]
        public void AndroidTapjackingProtection_IsEnabled()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            Assert.IsNotEmpty(assets);
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("AndroidFilterTouchesWhenObscured");
            Assert.IsNotNull(prop);
            Assert.IsTrue(prop.boolValue,
                "AndroidFilterTouchesWhenObscured wajib aktif untuk mencegah serangan overlay tapjacking (CWE-1021)");
        }

        [Test]
        public void RichTextSanitizer_BlocksMarkupAndNoparseBreakout()
        {
            Assert.AreEqual("Warangka Sesrengatan", UIKit.EscapeRichText("Warangka Sesrengatan"));
            Assert.AreEqual(string.Empty, UIKit.EscapeRichText(null));

            string injected = "Judul <color=#ff0000>Palsu</color> & </noparse><size=999>Rusak";
            string escaped = UIKit.EscapeRichText(injected);
            StringAssert.StartsWith("<noparse>", escaped);
            StringAssert.EndsWith("</noparse>", escaped);
            // Pastikan tag penutup </noparse> di dalam teks asli sudah dinetralkan agar tidak bisa keluar blok noparse.
            string inner = escaped.Substring("<noparse>".Length, escaped.Length - "<noparse></noparse>".Length);
            StringAssert.DoesNotContain("</noparse>", inner);

            var go = new GameObject("TmpCheck", typeof(RectTransform));
            try
            {
                var label = UIKit.Text(go.transform, "Plain", "<color=red>Test</color>", 24f, Color.black);
                Assert.IsFalse(label.richText, "UIKit.Text harus menonaktifkan richText secara bawaan");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DarkRegions_RejectsUndersizedBuffer_AndGuardsNonFiniteSample()
        {
            var regions = new DarkRegions();
            var tiny = new byte[10];
            Assert.DoesNotThrow(() => regions.Analyze(tiny, 64, 64));
            Assert.IsNull(regions.Gray, "Buffer yang lebih kecil dari width*height harus ditolak");
            Assert.AreEqual(0, regions.Regions.Count);

            var valid = new byte[64 * 64];
            for (int i = 0; i < valid.Length; i++) valid[i] = 128;
            regions.Analyze(valid, 64, 64);
            Assert.AreEqual(0f, regions.Sample(float.NaN, 10f));
            Assert.AreEqual(0f, regions.Sample(10f, float.PositiveInfinity));
            Assert.AreEqual(0f, regions.Sample(float.NegativeInfinity, 10f));
        }

        [Test]
        public void QrCode_GuardsNullOutOfRangeAndMalformedInputs()
        {
            Assert.IsFalse(QrCode.TryDecode(null, out _));
            Assert.IsFalse(QrCode.RsCorrect(null, 4));
            Assert.IsFalse(QrCode.RsCorrect(new byte[4], 0));
            Assert.IsFalse(QrCode.RsCorrect(new byte[4], 4));
            Assert.IsNull(QrCode.CreateTexture(null));

            // Mask di luar 0..7 harus jatuh kembali ke pemilihan mask otomatis yang valid, bukan merusak bit ECC.
            var encoded = QrCode.Encode("NUSANTARA:KERIS_BALI_01", QrEcc.M, 2, 99);
            Assert.IsNotNull(encoded);
            Assert.IsTrue(QrCode.TryDecode(encoded, out var decoded));
            Assert.AreEqual("NUSANTARA:KERIS_BALI_01", decoded);
        }

        [Test]
        public void ContentCatalog_SanitizesQrLookup_AndSkipsNullEntries()
        {
            var cat = ScriptableObject.CreateInstance<ContentCatalog>();
            var item = ScriptableObject.CreateInstance<ArtifactData>();
            try
            {
                item.artifactId = "KERIS_BALI_01";
                cat.artifacts = new List<ArtifactData> { null, item };
                Assert.AreSame(item, cat.First, "First harus melewati elemen null di awal daftar");
                Assert.AreSame(item, cat.FindByQr("NUSANTARA:  KERIS_BALI_01  "));
                Assert.IsNull(cat.FindByQr(new string('A', 512)), "Payload QR terlalu panjang harus langsung ditolak");
                Assert.IsNull(cat.FindByQr("   "));
            }
            finally
            {
                Object.DestroyImmediate(item);
                Object.DestroyImmediate(cat);
            }
        }

        [Test]
        public void AppSettings_SanitizesNonFiniteVolumes_AndReloads()
        {
            float origNar = AppSettings.NarrationVolume;
            float origMus = AppSettings.MusicVolume;
            float origSfx = AppSettings.SfxVolume;
            try
            {
                AppSettings.NarrationVolume = float.NaN;
                Assert.IsFalse(float.IsNaN(AppSettings.NarrationVolume));
                Assert.That(AppSettings.NarrationVolume, Is.InRange(0f, 1f));

                AppSettings.MusicVolume = float.PositiveInfinity;
                Assert.IsFalse(float.IsInfinity(AppSettings.MusicVolume));
                Assert.That(AppSettings.MusicVolume, Is.InRange(0f, 1f));

                AppSettings.SfxVolume = 0.25f;
                PlayerPrefs.SetFloat("nusantaraar.vol.sfx", 0.85f);
                AppSettings.Reload();
                Assert.AreEqual(0.85f, AppSettings.SfxVolume, 1e-4f);
            }
            finally
            {
                AppSettings.NarrationVolume = origNar;
                AppSettings.MusicVolume = origMus;
                AppSettings.SfxVolume = origSfx;
            }
        }

        [Test]
        public void ScaleAndSlider_IgnoreNaNAndInfinity()
        {
            Assert.AreEqual(1f, ControlPanel.ScaleFromSlider(float.NaN), 1e-4f);
            Assert.AreEqual(1f, ControlPanel.ScaleFromSlider(float.PositiveInfinity), 1e-4f);
            Assert.AreEqual(0f, ControlPanel.SliderFromScale(float.NaN), 1e-4f);
            Assert.AreEqual(0f, ControlPanel.SliderFromScale(float.NegativeInfinity), 1e-4f);

            var go = new GameObject("InstTest");
            try
            {
                var inst = go.AddComponent<ArtifactInstance>();
                inst.Init(null);
                inst.SetRelativeScale(float.NaN);
                Assert.AreEqual(1f, inst.RelativeScale, 1e-4f);
                inst.MultiplyScale(float.PositiveInfinity);
                Assert.AreEqual(1f, inst.RelativeScale, 1e-4f);
                inst.MultiplyScale(-2f);
                Assert.AreEqual(1f, inst.RelativeScale, 1e-4f);
                Assert.IsFalse(inst.IsHotspotAvailable(null));
                Assert.IsFalse(inst.TryGetHotspotWorldPosition(null, out _));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DegenerateMarkerPose_ReturnsFinitePose()
        {
            var zeroCorners = new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
            var pose = MarkerPose.UprightPose(zeroCorners, Vector3.zero, out float wall, out float yaw);
            Assert.IsFalse(float.IsNaN(pose.rotation.x) || float.IsNaN(pose.rotation.y) || float.IsNaN(pose.rotation.z) || float.IsNaN(pose.rotation.w));
            Assert.IsFalse(float.IsNaN(wall) || float.IsNaN(yaw));
        }
    }
}
