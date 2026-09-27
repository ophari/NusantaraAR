using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Pengaman build (berlaku untuk semua jalur: menu, Build Profiles, batch).
    /// ARCore/ARKit aktif sejak start (InitManagerOnStart), jadi SEMUA kamera dirender lewat jalur XR.
    /// URP menghitung stripping shader dari platform AKTIF; kalau build Android dijalankan saat platform aktif
    /// masih Standalone, varian XR dibuang dan model 3D tidak tampil di HP. Build dihentikan sebelum terjadi.
    /// </summary>
    class BuildGuard : IPreprocessBuildWithReport
    {
        // Setelah ShaderBuildPreprocessor URP (callbackOrder 0) mengisi data prefiltering.
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            var active = EditorUserBuildSettings.activeBuildTarget;
            if (target != active)
                throw new BuildFailedException(
                    $"[NusantaraAR] Build {target} dijalankan saat platform aktif {active}. " +
                    $"Pindah dulu ke {target} (File > Build Profiles > Switch Platform, atau batch -buildTarget).");

            if (!HasActiveXRLoader(report.summary.platformGroup)) return;
            // Hanya aset URP dari level kualitas yang dipakai platform ini (PC_RPAsset memang membuang XR, dan itu benar).
            var platformName = NamedBuildTarget.FromBuildTargetGroup(report.summary.platformGroup).TargetName;
            QualitySettings.GetRenderPipelineAssetsForPlatform<UniversalRenderPipelineAsset>(platformName, out var assets, out _);
            foreach (var urp in assets)
            {
                if (urp == null) continue;
                var so = new SerializedObject(urp);
                var prop = so.FindProperty("m_PrefilterXRKeywords");
                if (prop != null && prop.boolValue)
                    throw new BuildFailedException(
                        $"[NusantaraAR] {urp.name} membuang varian shader XR padahal {target} memakai loader XR " +
                        "(ARCore/ARKit): model 3D akan tak terlihat di perangkat. Pastikan platform aktif = target build.");
            }
        }

        static bool HasActiveXRLoader(BuildTargetGroup group)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            return settings != null && settings.Manager != null && settings.Manager.activeLoaders.Any(l => l != null);
        }
    }
}
