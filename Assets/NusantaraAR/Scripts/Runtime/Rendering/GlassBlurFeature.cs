using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace NusantaraAR.Rendering
{
    /// <summary>
    /// Status bersama efek kaca: jumlah elemen kaca aktif (blur hanya dihitung bila ada), material UI kaca,
    /// dan tekstur global <c>_GlassBlurTex</c> yang dibaca shader "NusantaraAR/UI/Glass".
    /// </summary>
    public static class GlassBlur
    {
        public static readonly int TextureId = Shader.PropertyToID("_GlassBlurTex");

        static int active;
        static Material uiMaterial;
        static bool uiMaterialLoaded;
        static Texture2D fallback;

        public static bool Active => active > 0;

        public static void Register()
        {
            active++;
            if (fallback != null) return;
            // Sebelum blur pertama tersedia, kaca menampilkan warna latar gading.
            fallback = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, name = "GlassBlurFallback" };
            fallback.SetPixel(0, 0, UI.Theme.Bg);
            fallback.Apply(false, true);
            Shader.SetGlobalTexture(TextureId, fallback);
        }

        public static void Unregister() => active = Mathf.Max(0, active - 1);

        /// <summary>Material bersama untuk semua Image kaca (satu batch). Null = kaca tampil sebagai panel biasa.</summary>
        public static Material UIMaterial
        {
            get
            {
                if (uiMaterialLoaded) return uiMaterial;
                uiMaterialLoaded = true;
                uiMaterial = Resources.Load<Material>("UIGlass"); // Resources: shader ikut ter-build
                if (uiMaterial == null)
                {
                    var shader = Shader.Find("NusantaraAR/UI/Glass");
                    if (shader != null) uiMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                return uiMaterial;
            }
        }

        internal static void Publish(RTHandle blurred) => Shader.SetGlobalTexture(TextureId, blurred.rt);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active = 0;
    }

    /// <summary>
    /// Kaca buram (glassmorphism) untuk uGUI di URP Render Graph: setelah transparan (sebelum UI), warna kamera
    /// diturunkan resolusinya dan di-blur dual-Kawase, lalu disimpan di RTHandle persisten ¼ resolusi agar bisa dibaca
    /// kanvas Screen Space-Overlay yang digambar sesudahnya. Isi blur: model 3D, latar kamera Scan QR (kanvas
    /// Screen Space-Camera), dan gambar kamera AR. Tidak berjalan bila tidak ada elemen kaca aktif.
    /// </summary>
    public class GlassBlurFeature : ScriptableRendererFeature
    {
        public Shader shader;
        [Tooltip("Jumlah langkah turun (1/2, 1/4, 1/8, ...). Hasil selalu 1/4 resolusi; makin banyak = makin buram.")]
        [Range(2, 5)] public int downPasses = 3;
        [Range(0.5f, 3f)] public float offset = 1.25f;
        [Tooltip("Saturasi hasil blur (>1 = warna lebih hidup, khas kaca).")]
        [Range(0f, 2f)] public float saturation = 1.15f;

        Material material;
        GlassBlurPass pass;

        public override void Create()
        {
            pass = new GlassBlurPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GlassBlur.Active || shader == null || renderingData.cameraData.cameraType != CameraType.Game) return;
            if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
            material.SetFloat(GlassBlurPass.OffsetId, offset);
            material.SetFloat(GlassBlurPass.SaturationId, saturation);
            pass.Setup(material, downPasses);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class GlassBlurPass : ScriptableRenderPass
        {
            public static readonly int OffsetId = Shader.PropertyToID("_BlurOffset");
            public static readonly int SaturationId = Shader.PropertyToID("_Saturation");
            const int PassDown = 0, PassUp = 1, PassFinal = 2;

            Material material;
            int downPasses;
            RTHandle output;
            readonly List<TextureHandle> chain = new List<TextureHandle>();

            public GlassBlurPass()
            {
                profilingSampler = new ProfilingSampler("GlassBlur");
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, int downs)
            {
                material = mat;
                downPasses = Mathf.Clamp(downs, 2, 5);
            }

            public void Dispose()
            {
                output?.Release();
                output = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || material == null) return;
                var camera = frameData.Get<UniversalCameraData>();
                var src = resources.activeColorTexture;
                int width = camera.cameraTargetDescriptor.width, height = camera.cameraTargetDescriptor.height;
                if (width < 16 || height < 16) return;

                // Hasil: RTHandle persisten (tidak dilepas Render Graph), ¼ resolusi, sRGB 8-bit.
                var outDesc = new RenderTextureDescriptor(width / 4, height / 4, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None)
                {
                    msaaSamples = 1,
                    useMipMap = false
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref output, outDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GlassBlurTex");
                var dst = renderGraph.ImportTexture(output);

                // Turun: 1/2, 1/4, 1/8, ... (chain[1] = 1/4 = ukuran hasil).
                chain.Clear();
                var cur = src;
                int w = width, h = height;
                for (int i = 0; i < downPasses; i++)
                {
                    w = Mathf.Max(1, w / 2);
                    h = Mathf.Max(1, h / 2);
                    var desc = new TextureDesc(w, h)
                    {
                        format = GraphicsFormat.R8G8B8A8_SRGB,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        name = "GlassBlurDown" + i
                    };
                    var t = renderGraph.CreateTexture(desc);
                    renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(cur, t, material, PassDown), "GlassBlur Down");
                    chain.Add(t);
                    cur = t;
                }
                // Naik kembali ke 1/4.
                for (int i = chain.Count - 2; i >= 1; i--)
                {
                    renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(cur, chain[i], material, PassUp), "GlassBlur Up");
                    cur = chain[i];
                }
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(cur, dst, material, PassFinal), "GlassBlur Final");

                GlassBlur.Publish(output);
            }
        }
    }
}
