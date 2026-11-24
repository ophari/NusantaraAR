using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Build Android. APK untuk uji di perangkat, AAB untuk Play Store (PRD §8 langkah 7).
    /// Batch: -executeMethod NusantaraAR.EditorTools.BuildScript.BuildAndroidApk
    /// AAB rilis membutuhkan keystore rilis (atur di Player Settings > Publishing Settings).
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Nusantara AR/Build/Android APK (uji perangkat)")]
        public static void BuildAndroidApk() => Build(false, "Builds/Android/NusantaraAR.apk");

        [MenuItem("Nusantara AR/Build/Android App Bundle (.aab)")]
        public static void BuildAndroidAab() => Build(true, "Builds/Android/NusantaraAR.aab");

        /// <summary>Build Windows QA dengan DevCapture (tangkapan layar otomatis alur utama).</summary>
        public static void BuildWindowsCapture()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = "Builds/QA/NusantaraAR.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                extraScriptingDefines = new[] { "NUSANTARA_CAPTURE" }
            };
            // BuildGuard menolak build bila platform aktif != target; platform semula dikembalikan di akhir.
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
            if (previousTarget != BuildTarget.StandaloneWindows64 &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new Exception("Gagal pindah platform aktif ke Windows.");

            // Pakai profil kualitas "Mobile" (sama dengan Android) selama build QA, lalu kembalikan.
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qs.FindProperty("m_QualitySettings");
            var mobileExcluded = levels.GetArrayElementAtIndex(0).FindPropertyRelative("excludedTargetPlatforms");
            int excludedIndex = -1;
            for (int i = 0; i < mobileExcluded.arraySize; i++)
                if (mobileExcluded.GetArrayElementAtIndex(i).stringValue == "Standalone") excludedIndex = i;
            if (excludedIndex >= 0) mobileExcluded.DeleteArrayElementAtIndex(excludedIndex);
            qs.ApplyModifiedPropertiesWithoutUndo();
            int previousDefault = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(0, false);
            SetStandaloneDefaultQuality(0);

            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally
            {
                qs.Update();
                if (excludedIndex >= 0)
                {
                    mobileExcluded.InsertArrayElementAtIndex(0);
                    mobileExcluded.GetArrayElementAtIndex(0).stringValue = "Standalone";
                    qs.ApplyModifiedPropertiesWithoutUndo();
                }
                SetStandaloneDefaultQuality(1);
                QualitySettings.SetQualityLevel(previousDefault, false);
                // Kembalikan platform aktif (biasanya Android) agar build APK berikutnya tidak ikut setelan Standalone.
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
            }
            Debug.Log($"[NusantaraAR] QA build {report.summary.result}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void SetStandaloneDefaultQuality(int level)
        {
            var qs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var map = qs.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < map.arraySize; i++)
            {
                var entry = map.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue != "Standalone") continue;
                entry.FindPropertyRelative("second").intValue = level;
            }
            qs.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Build(bool appBundle, string path)
        {
            // Wajib: URP menghitung stripping shader (termasuk varian XR/ARCore) dari platform AKTIF, bukan target build.
            // Kalau masih Standalone (mis. sisa build QA), varian XR ikut dibuang dan model 3D tak tampil di HP.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("Gagal pindah platform aktif ke Android.");
            EditorUserBuildSettings.buildAppBundle = appBundle;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // LZ4HC: data aplikasi terkompres lebih rapat daripada LZ4 biasa (APK lebih kecil, build sedikit lebih lama).
                options = BuildOptions.CompressWithLz4HC
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            float mb = File.Exists(path) ? new FileInfo(path).Length / (1024f * 1024f) : 0f;
            Debug.Log($"[NusantaraAR] Build {summary.result}: {path} ({mb:F1} MB, {summary.totalTime})");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
