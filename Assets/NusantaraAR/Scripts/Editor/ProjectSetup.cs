using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARKit;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Setup proyek sekali jalan (idempoten): pengaturan Android/iOS, XR (ARCore/ARKit Optional),
    /// URP (AR Background Renderer Feature), artefak dari model Blender (GLB via glTFast: Keris Bali, Keris Sumatra)
    /// beserta prefab modular + exploded view + hotspot draf, dan scene Main + AR + Marker.
    /// Menu: Nusantara AR / Setup Everything. Batch: -executeMethod NusantaraAR.EditorTools.ProjectSetup.RunBatch
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/NusantaraAR";
        const string CommonDir = Root + "/Art/Common";
        const string CatalogPath = Root + "/Resources/ContentCatalog.asset";
        const string PlanePrefabPath = Root + "/Prefabs/ARPlane.prefab";
        const string MainScenePath = Root + "/Scenes/Main.unity";
        const string ARScenePath = Root + "/Scenes/AR.unity";
        const string MarkerScenePath = Root + "/Scenes/Marker.unity";

        [MenuItem("Nusantara AR/Setup Everything")]
        public static void RunAll()
        {
            EnsureFolders();
            ImportTmpEssentials();
            ConfigurePlayer();
            ConfigureXR();
            ConfigureURP();
            KerisBaliBuilder.Build();
            KerisSumatraBuilder.Build();
            StoryBuilder.Build();
            MusicBuilder.Build();
            PruneCatalog();
            BuildCommonAssets(out var reticleMat, out var planePrefab);
            BuildMainScene();
            BuildARScene(reticleMat, planePrefab);
            BuildMarkerScene();
            RenderThumbnail(KerisBaliBuilder.DataPath, KerisBaliBuilder.ThumbPath);
            RenderThumbnail(KerisSumatraBuilder.DataPath, KerisSumatraBuilder.ThumbPath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(ARScenePath, true),
                new EditorBuildSettingsScene(MarkerScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[NusantaraAR] Setup selesai.");
        }

        public static void RunBatch()
        {
            try
            {
                RunAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolders()
        {
            foreach (var d in new[] { CommonDir, Root + "/Content", Root + "/Resources", Root + "/Prefabs", Root + "/Scenes" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ TextMeshPro

        static void ImportTmpEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            const string pkg = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(Path.GetFullPath(pkg), false);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ Player

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Nusantara AR";
            PlayerSettings.productName = "Nusantara AR";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "id.nusantaraar.app");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "id.nusantaraar.app");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Android - PRD v1.1 §3 & §8
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            // iOS (P1)
            PlayerSettings.iOS.cameraUsageDescription =
                "Kamera dipakai untuk menampilkan artefak di ruangan secara langsung (AR). Tidak ada gambar yang direkam atau diunggah.";

            // Input System saja (EnhancedTouch)
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var so = new SerializedObject(playerSettings);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue != 1)
            {
                handler.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ XR

        static void ConfigureXR()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            AssignLoader(perTarget, BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
            AssignLoader(perTarget, BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");

            // Optional: app tetap tersedia di perangkat tanpa ARCore/ARKit (mereka memakai 3D Viewer).
            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Optional;
            arcore.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);
            var arkit = ARKitSettings.GetOrCreateSettings();
            arkit.requirement = ARKitSettings.Requirement.Optional;
            EditorUtility.SetDirty(arkit);
            AssetDatabase.SaveAssets();
        }

        static void AssignLoader(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, string loader)
        {
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var settings = perTarget.SettingsForBuildTarget(group);
            settings.InitManagerOnStart = true;
            if (!settings.AssignedSettings.activeLoaders.Any(l => l != null && l.GetType().FullName == loader))
                XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, loader, group);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(perTarget);
        }

        // ------------------------------------------------------------------ URP

        static void ConfigureURP()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                RemoveRendererFeatures(data, f => f is ScreenSpaceAmbientOcclusion); // mahal di mobile, tak dipakai
                if (!data.rendererFeatures.Any(f => f is ARBackgroundRendererFeature))
                    AddRendererFeature(data, typeof(ARBackgroundRendererFeature));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || !asset.name.StartsWith("Mobile")) continue;
                asset.supportsHDR = false; // hemat bandwidth di perangkat kelas menengah
                EditorUtility.SetDirty(asset);
            }
        }

        static void RemoveRendererFeatures(ScriptableRendererData data, Func<ScriptableRendererFeature, bool> match)
        {
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            bool changed = false;
            for (int i = features.arraySize - 1; i >= 0; i--)
            {
                var f = features.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererFeature;
                if (f == null || !match(f)) continue;
                features.GetArrayElementAtIndex(i).objectReferenceValue = null;
                features.DeleteArrayElementAtIndex(i);
                if (i < map.arraySize) map.DeleteArrayElementAtIndex(i);
                UnityEngine.Object.DestroyImmediate(f, true);
                changed = true;
            }
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Sama seperti tombol "Add Renderer Feature" di inspector URP (sub-asset + feature map).</summary>
        static void AddRendererFeature(ScriptableRendererData data, Type type)
        {
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = type.Name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Katalog

        /// <summary>Hanya artefak dari model Blender yang dipakai; entri lain (atau yang asetnya sudah dihapus) dibuang.</summary>
        static void PruneCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            var keep = new[] { KerisBaliBuilder.Id, KerisSumatraBuilder.Id };
            catalog.artifacts.RemoveAll(a => a == null || Array.IndexOf(keep, a.artifactId) < 0);
            catalog.artifacts.Sort((x, y) => Array.IndexOf(keep, x.artifactId).CompareTo(Array.IndexOf(keep, y.artifactId)));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        internal static Material GetOrCreateMaterial(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ Aset umum AR

        static void BuildCommonAssets(out Material reticleMat, out GameObject planePrefab)
        {
            reticleMat = GetOrCreateMaterial(CommonDir + "/M_Reticle.mat", "Universal Render Pipeline/Unlit");
            reticleMat.SetColor("_BaseColor", UI.Theme.Gold);
            reticleMat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(reticleMat);

            var planeMat = GetOrCreateMaterial(CommonDir + "/M_ARPlane.mat", "Universal Render Pipeline/Unlit");
            planeMat.SetFloat("_Surface", 1f);
            planeMat.SetFloat("_Blend", 0f);
            planeMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            planeMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            planeMat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            planeMat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            planeMat.SetFloat("_ZWrite", 0f);
            planeMat.SetFloat("_Cull", 0f);
            planeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            planeMat.renderQueue = (int)RenderQueue.Transparent;
            planeMat.SetColor("_BaseColor", new Color(UI.Theme.Gold.r, UI.Theme.Gold.g, UI.Theme.Gold.b, 0.16f));
            EditorUtility.SetDirty(planeMat);

            var go = new GameObject("ARPlane", typeof(ARPlane), typeof(ARPlaneMeshVisualizer), typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshRenderer>().sharedMaterial = planeMat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            planePrefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Scene

        static void AddLights()
        {
            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.9f, 0.76f);
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38f, 28f, 0f);
            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.7f, 0.78f, 1f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20f, -140f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.38f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.23f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.09f, 0.08f);
        }

        static void AddEventSystem()
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetAsLastSibling();
        }

        static void BuildMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLights();
            RenderSettings.skybox = null;

            var app = new GameObject("App");
            var gestures = app.AddComponent<TouchGestures>();

            var camGo = new GameObject("ViewerCamera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.08f, 0.07f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 30f;
            var orbit = camGo.AddComponent<OrbitCameraController>();
            orbit.gestures = gestures;

            var stage = new GameObject("Stage").transform;

            var main = app.AddComponent<MainController>();
            main.viewerCamera = cam;
            main.orbit = orbit;
            main.gestures = gestures;
            main.stageRoot = stage;

            AddEventSystem();
            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        static void BuildARScene(Material reticleMat, GameObject planePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.43f, 0.4f);

            var sessionGo = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            var session = sessionGo.GetComponent<ARSession>();

            // XR Origin (Mobile AR) - setara menu GameObject/XR/XR Origin (Mobile AR)
            var originGo = new GameObject("XR Origin", typeof(XROrigin));
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager),
                typeof(ARCameraBackground), typeof(TrackedPoseDriver));
            camGo.transform.SetParent(offset.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            var tpd = camGo.GetComponent<TrackedPoseDriver>();
            var pos = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            pos.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rot = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
            tpd.positionInput = new InputActionProperty(pos);
            tpd.rotationInput = new InputActionProperty(rot);
            var origin = originGo.GetComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offset;
            origin.Camera = cam;

            var planes = originGo.AddComponent<ARPlaneManager>();
            planes.planePrefab = planePrefab;
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            var raycasts = originGo.AddComponent<ARRaycastManager>();
            originGo.AddComponent<ARAnchorManager>();

            var reticleGo = new GameObject("Reticle", typeof(MeshFilter), typeof(MeshRenderer));
            var mr = reticleGo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = reticleMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var reticle = reticleGo.AddComponent<ReticleView>();

            var app = new GameObject("App");
            var gestures = app.AddComponent<TouchGestures>();
            var placement = app.AddComponent<PlacementController>();
            placement.raycastManager = raycasts;
            placement.planeManager = planes;
            placement.arCamera = cam;
            placement.reticle = reticle;
            var ar = app.AddComponent<ARController>();
            ar.session = session;
            ar.placement = placement;
            ar.gestures = gestures;
            ar.arCamera = cam;

            AddEventSystem();
            EditorSceneManager.SaveScene(scene, ARScenePath);
        }

        /// <summary>Scene "Scan QR": kamera biasa (WebCamTexture) di latar + deteksi kode QR (dan kartu penanda lama), tanpa ARCore.</summary>
        static void BuildMarkerScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.47f, 0.43f);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.fieldOfView = 60f;

            // Latar kamera: kanvas Screen Space - Camera jauh di belakang objek 3D (tanpa GraphicRaycaster).
            var bgGo = new GameObject("CameraBackground", typeof(RectTransform), typeof(Canvas));
            var bgCanvas = bgGo.GetComponent<Canvas>();
            bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            bgCanvas.worldCamera = cam;
            bgCanvas.planeDistance = 15f;
            bgCanvas.sortingOrder = -100;
            var imgGo = new GameObject("CameraImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            imgGo.transform.SetParent(bgGo.transform, false);
            var raw = imgGo.GetComponent<UnityEngine.UI.RawImage>();
            raw.raycastTarget = false;
            raw.color = Color.white;

            var app = new GameObject("App");
            var gestures = app.AddComponent<TouchGestures>();
            var feed = app.AddComponent<Marker.CameraFeed>();
            var controller = app.AddComponent<Marker.MarkerController>();
            controller.cam = cam;
            controller.feed = feed;
            controller.background = raw;
            controller.backgroundCanvas = (RectTransform)bgGo.transform;
            controller.gestures = gestures;

            AddEventSystem();
            EditorSceneManager.SaveScene(scene, MarkerScenePath);
        }

        // ------------------------------------------------------------------ Thumbnail katalog

        /// <summary>Dipanggil dengan path, bukan instance: instance ArtifactData bisa sudah di-unload saat scene lain dibangun.</summary>
        internal static void RenderThumbnail(string dataPath, string thumbPath)
        {
            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(dataPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLights();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab);
            var inst = go.GetComponent<ArtifactInstance>();
            inst.Init(data);
            if (inst.exploded != null) inst.exploded.SnapTo(0);
            var b = inst.GetWorldBounds();
            float radius = b.extents.magnitude;
            if (radius < 1e-4f) throw new Exception("Thumbnail " + data.artifactId + ": bounds artefak kosong.");

            var camGo = new GameObject("ThumbCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.14f, 0.12f);
            cam.fieldOfView = 30f;
            const int w = 600, h = 740;
            cam.aspect = (float)w / h;
            // FOV horizontal sebenarnya (bukan fov vertikal * aspect) agar artefak memanjang tetap muat lebarnya.
            float halfH = Mathf.Atan(Mathf.Tan(Mathf.Deg2Rad * cam.fieldOfView * 0.5f) * cam.aspect);
            float dist = radius / Mathf.Sin(halfH) * 0.95f;
            cam.nearClipPlane = Mathf.Max(0.001f, (dist - radius) * 0.5f);
            cam.farClipPlane = dist + radius * 2f;
            var dir = Quaternion.Euler(10f, -18f, 0f) * Vector3.back;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render(); // pemanasan: render pertama di scene baru bisa kosong (shader/tekstur belum siap)
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            bool blank = IsUniform(tex);
            if (!blank) File.WriteAllBytes(thumbPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            EditorSceneManager.CloseScene(scene, true);
            if (blank) throw new Exception("Thumbnail " + data.artifactId + " ter-render polos (satu warna); file lama tidak ditimpa.");

            AssetDatabase.ImportAsset(thumbPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(thumbPath);
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None; // 600x740 jangan dipaksa jadi 512x512 (gepeng + pita kosong)
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            data = AssetDatabase.LoadAssetAtPath<ArtifactData>(dataPath); // instance lama bisa ter-unload saat reimport
            data.thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(thumbPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MainScenePath);
        }

        static bool IsUniform(Texture2D tex)
        {
            var px = tex.GetPixels32();
            var first = px[0];
            foreach (var p in px)
                if (Mathf.Abs(p.r - first.r) > 4 || Mathf.Abs(p.g - first.g) > 4 || Mathf.Abs(p.b - first.b) > 4) return false;
            return true;
        }

        [MenuItem("Nusantara AR/Render Thumbnails")]
        public static void RenderThumbnails()
        {
            RenderThumbnail(KerisBaliBuilder.DataPath, KerisBaliBuilder.ThumbPath);
            RenderThumbnail(KerisSumatraBuilder.DataPath, KerisSumatraBuilder.ThumbPath);
            Debug.Log("[NusantaraAR] Thumbnail katalog diperbarui.");
        }

        public static void RenderThumbnailsBatch()
        {
            try { RenderThumbnails(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
