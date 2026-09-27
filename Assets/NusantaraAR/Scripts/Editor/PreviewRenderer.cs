using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Merender setiap tahap exploded view setiap artefak (dengan titik hotspot yang aktif) ke Previews/{id}_stage_N.png,
    /// untuk review visual cepat tanpa perangkat. Menu: Nusantara AR / Render Stage Previews.
    /// </summary>
    public static class PreviewRenderer
    {
        [MenuItem("Nusantara AR/Render Stage Previews")]
        public static void Render()
        {
            var catalog = ContentCatalog.Load();
            if (catalog == null || catalog.First == null) throw new System.Exception("Katalog kosong - jalankan Setup Everything dulu.");
            // Per ID + muat ulang katalog: ArtifactData bisa ter-unload setelah scene preview ditutup.
            var ids = catalog.artifacts.Where(a => a != null).Select(a => a.artifactId).ToList();
            foreach (var id in ids)
                Render(ContentCatalog.Load().Find(id));
            Debug.Log("[NusantaraAR] Preview tahap ditulis ke " + Path.GetFullPath("Previews"));
        }

        static void Render(ArtifactData data)
        {

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.3f;
            key.color = new Color(1f, 0.9f, 0.76f);
            key.transform.rotation = Quaternion.Euler(38f, 28f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.37f, 0.33f);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab);
            var inst = go.GetComponent<ArtifactInstance>();
            inst.Init(data);

            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.11f, 0.1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            const int w = 900, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Directory.CreateDirectory("Previews");

            inst.exploded.SnapTo(inst.exploded.StageCount - 1);
            var b = inst.GetWorldBounds();
            inst.exploded.SnapTo(0);
            b.Encapsulate(inst.GetWorldBounds());
            float dist = b.extents.magnitude / Mathf.Sin(Mathf.Deg2Rad * cam.fieldOfView * 0.5f);
            cam.transform.position = b.center + Vector3.back * dist;
            cam.transform.LookAt(b.center);

            cam.targetTexture = rt;
            cam.Render(); // pemanasan: tekstur/shader pertama kali dimuat
            for (int s = 0; s < inst.exploded.StageCount; s++)
            {
                inst.exploded.SnapTo(s);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                foreach (var hs in data.hotspots)
                {
                    if (!inst.IsHotspotAvailable(hs) || !inst.TryGetHotspotWorldPosition(hs, out var p)) continue;
                    var sp = cam.WorldToScreenPoint(p);
                    DrawDot(tex, (int)sp.x, (int)sp.y, hs.visibleFrom == HotspotStage.Utuh ? new Color(0.83f, 0.69f, 0.22f) : new Color(0.3f, 0.8f, 1f));
                }
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes($"Previews/{data.artifactId}_stage_{s}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null;
            rt.Release();
            EditorSceneManager.CloseScene(scene, true);
        }

        static void DrawDot(Texture2D t, int cx, int cy, Color c)
        {
            for (int y = -9; y <= 9; y++)
            for (int x = -9; x <= 9; x++)
            {
                int d2 = x * x + y * y;
                if (d2 > 81) continue;
                int px = cx + x, py = cy + y;
                if (px < 0 || py < 0 || px >= t.width || py >= t.height) continue;
                t.SetPixel(px, py, d2 > 49 ? Color.black : c);
            }
        }

        public static void RenderBatch()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
