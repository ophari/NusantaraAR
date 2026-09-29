using System;
using System.Linq;
using NusantaraAR.Rendering;
using NusantaraAR.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace NusantaraAR.Tests
{
    /// <summary>
    /// Token desain (kontras WCAG AA, termasuk teks di atas kaca pada latar kamera terburuk), ikon prosedural,
    /// pemetaan slider skala, dan aset efek kaca/AR (hasil menu Nusantara AR/Build AR Visuals).
    /// </summary>
    public class UiThemeTests
    {
        const float TextAA = 4.5f, NonTextAA = 3f;
        static readonly Color Black = Color.black, White = Color.white;

        static void AssertContrast(Color fg, Color bg, float min, string what)
        {
            float ratio = Theme.Contrast(fg, bg);
            Assert.GreaterOrEqual(ratio, min, $"{what}: kontras {ratio:0.00} < {min}");
        }

        [Test]
        public void SolidColors_MeetWcagAA()
        {
            AssertContrast(Theme.Ink, Theme.Surface, TextAA, "Ink/Surface");
            AssertContrast(Theme.Ink, Theme.Bg, TextAA, "Ink/Bg");
            AssertContrast(Theme.Ink, Theme.Stage, TextAA, "Ink/Stage");
            AssertContrast(Theme.InkMuted, Theme.Bg, TextAA, "InkMuted/Bg");
            AssertContrast(Theme.InkMuted, Theme.Surface, TextAA, "InkMuted/Surface");
            AssertContrast(Theme.AccentText, Theme.Surface, TextAA, "AccentText/Surface");
            AssertContrast(Theme.AccentText, Theme.Bg, TextAA, "AccentText/Bg");
            AssertContrast(Theme.OnAccent, Theme.Accent, TextAA, "OnAccent/Accent (tombol utama)");
            AssertContrast(Theme.NavIcon, Theme.Nav, TextAA, "NavIcon/Nav");
            AssertContrast(Theme.NavActive, Theme.Nav, NonTextAA, "NavActive (ikon)/Nav");
        }

        /// <summary>Kaca di atas feed kamera: latar terburuk hitam pekat atau putih terang.</summary>
        [Test]
        public void TextOnGlass_MeetsWcagAA_OverBlackAndWhite()
        {
            foreach (var behind in new[] { Black, White })
            {
                var glass = Theme.GlassOver(Theme.Surface, Theme.GlassStrength, behind);
                AssertContrast(Theme.Ink, glass, TextAA, "Ink/kaca di atas " + behind);
                AssertContrast(Theme.InkMuted, glass, TextAA, "InkMuted/kaca di atas " + behind);
                AssertContrast(Theme.AccentText, glass, TextAA, "AccentText/kaca di atas " + behind);
                AssertContrast(Theme.Accent, glass, NonTextAA, "Ikon Accent/kaca di atas " + behind);

                var chrome = Theme.GlassOver(Theme.Surface, Theme.GlassChromeStrength, behind);
                AssertContrast(Theme.Ink, chrome, NonTextAA, "Ikon Ink/kaca tombol di atas " + behind);

                var nav = Theme.GlassOver(Theme.Nav, Theme.NavGlassStrength, behind);
                AssertContrast(Theme.NavIcon, nav, TextAA, "NavIcon/kaca nila di atas " + behind);
                AssertContrast(Theme.NavActive, nav, NonTextAA, "NavActive/kaca nila di atas " + behind);
            }
        }

        [Test]
        public void EveryIcon_RendersVisibleShape()
        {
            foreach (Icon icon in Enum.GetValues(typeof(Icon)))
            {
                var tex = IconFactory.Render(icon, 64, true);
                try
                {
                    var px = tex.GetPixels32();
                    float coverage = px.Count(p => p.a > 127) / (float)px.Length;
                    Assert.Greater(coverage, 0.02f, icon + ": ikon (hampir) kosong");
                    Assert.Less(coverage, 0.6f, icon + ": ikon terlalu penuh");
                    // Tepi tekstur harus bersih (tidak ada garis terpotong di batas 24 unit).
                    int edge = 0;
                    for (int i = 0; i < 64; i++)
                        edge += (px[i].a > 127 ? 1 : 0) + (px[63 * 64 + i].a > 127 ? 1 : 0)
                                + (px[i * 64].a > 127 ? 1 : 0) + (px[i * 64 + 63].a > 127 ? 1 : 0);
                    Assert.AreEqual(0, edge, icon + ": ikon menyentuh tepi tekstur");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }

        [Test]
        public void ScaleSlider_IsLogarithmicAndRoundTrips()
        {
            Assert.AreEqual(ArtifactInstance.MinScale, ControlPanel.ScaleFromSlider(0f), 1e-4f);
            Assert.AreEqual(ArtifactInstance.MaxScale, ControlPanel.ScaleFromSlider(1f), 1e-4f);
            Assert.AreEqual(0f, ControlPanel.SliderFromScale(0.1f), 1e-4f, "di bawah minimum dijepit");
            Assert.AreEqual(1f, ControlPanel.SliderFromScale(10f), 1e-4f, "di atas maksimum dijepit");
            float one = ControlPanel.SliderFromScale(1f);
            Assert.That(one, Is.InRange(0.3f, 0.5f), "1x berada di sepertiga kiri slider, bukan di ujung");
            foreach (float s in new[] { 0.5f, 0.8f, 1f, 1.7f, 3f })
                Assert.AreEqual(s, ControlPanel.ScaleFromSlider(ControlPanel.SliderFromScale(s)), 1e-3f, "bolak-balik " + s);
        }

        [Test]
        public void GlassAndArVisualAssets_AreBuilt()
        {
            const string hint = " - jalankan Nusantara AR/Build AR Visuals";
            var uiGlass = Resources.Load<Material>("UIGlass");
            Assert.IsNotNull(uiGlass, "Resources/UIGlass.mat" + hint);
            Assert.AreEqual("NusantaraAR/UI/Glass", uiGlass.shader.name);
            var glow = Resources.Load<Material>("GroundGlow");
            Assert.IsNotNull(glow, "Resources/GroundGlow.mat" + hint);
            Assert.IsNotNull(glow.GetTexture("_BaseMap"), "tekstur glow" + hint);

            var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
            Assert.IsNotEmpty(guids);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature = data.rendererFeatures.OfType<GlassBlurFeature>().FirstOrDefault();
                Assert.IsNotNull(feature, path + ": GlassBlurFeature" + hint);
                Assert.IsNotNull(feature.shader, path + ": shader blur" + hint);
                Assert.IsTrue(feature.isActive, path + ": GlassBlurFeature nonaktif");
            }
        }
    }
}
