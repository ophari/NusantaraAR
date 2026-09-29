#if NUSANTARA_CAPTURE
using System.Collections;
using System.IO;
using System.Reflection;
using NusantaraAR.UI;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Hanya untuk build QA dengan define NUSANTARA_CAPTURE: menelusuri alur utama secara otomatis
    /// dan menyimpan tangkapan layar ke folder Captures di samping executable, lalu keluar.
    /// </summary>
    public static class DevCapture
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<MainController>() == null) return;
            // QA memakai profil kualitas yang sama dengan Android (Mobile_RPAsset), bukan "PC".
            int mobile = System.Array.IndexOf(QualitySettings.names, "Mobile");
            if (mobile >= 0) QualitySettings.SetQualityLevel(mobile, true);
            var go = new GameObject("DevCapture");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        class Runner : MonoBehaviour
        {
            string dir;

            IEnumerator Start()
            {
                dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Captures");
                Directory.CreateDirectory(dir);
                PlayerPrefs.DeleteAll();
                yield return new WaitForSeconds(1.5f);
                yield return Shot("01_onboarding");
                Click("Onboarding/Card/Next"); yield return Shot("02_onboarding_gestures");
                Click("Onboarding/Card/Next"); yield return Shot("03_onboarding_safety");
                Click("Onboarding/Card/Next"); yield return Shot("04_catalog");
                Click("NavLayer/BottomNav/Items/Settings"); yield return new WaitForSeconds(0.5f);
                yield return Shot("04b_settings");
                Click("NavLayer/BottomNav/Items/Settings"); yield return new WaitForSeconds(0.3f);

                var main = Object.FindAnyObjectByType<MainController>();
                Invoke(main, "OpenDetail", AppSession.Catalog.First);
                yield return new WaitForSeconds(1f);
                yield return Shot("05_detail_assembled");
                Click("Safe/DetailSheet/Handle"); yield return new WaitForSeconds(0.5f);
                yield return Shot("05b_detail_sheet_expanded");
                Click("Safe/DetailSheet/Handle"); yield return new WaitForSeconds(0.3f);

                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                var hud = Object.FindAnyObjectByType<ArtifactHud>();
                yield return DrawFrames(inst, "05");
                inst.exploded.Explode();
                yield return new WaitForSeconds(3.5f);
                yield return Shot("06_detail_exploded");

                var data = inst.Data;
                hud.OpenHotspot("pamor");
                yield return new WaitForSeconds(1f);
                yield return Shot("07_card_pamor");

                hud.OpenHotspot("gandar");
                yield return new WaitForSeconds(1f);
                yield return Shot("08_card_gandar");
                hud.CloseInfo();

                // Mode Kisah: bab 1 (utuh), bab hulu (sorotan), bab hunus (animasi + subtitle), lalu dihentikan.
                inst.exploded.Assemble();
                yield return new WaitForSeconds(3.5f);
                hud.PlayStory();
                yield return new WaitForSeconds(4f);
                yield return Shot("08b_story_intro");
                var next = GameObject.Find("UI/Full/Safe/StoryPanel/Header/Next")?.GetComponent<UnityEngine.UI.Button>();
                next?.onClick.Invoke(); next?.onClick.Invoke();
                yield return new WaitForSeconds(3f);
                yield return Shot("08c_story_focus_hulu");
                next?.onClick.Invoke(); next?.onClick.Invoke();
                yield return new WaitForSeconds(6f);
                yield return Shot("08d_story_drawn");
                hud.CloseInfo();

                Locale.Current = Language.EN;
                inst.exploded.Assemble();
                yield return new WaitForSeconds(3.5f);
                yield return Shot("09_detail_english");
                Locale.Current = Language.ID;

                // Animasi hunus keris kedua (keris berdiri).
                foreach (var other in AppSession.Catalog.artifacts)
                {
                    if (other == null || other == data) continue;
                    Invoke(main, "OpenDetail", other);
                    yield return new WaitForSeconds(1f);
                    yield return DrawFrames(Object.FindAnyObjectByType<ArtifactInstance>(), "09b");

                    // Probe kaca: panel kaca tanpa tint selayar = blur murni. Bentuknya harus sama letaknya dengan
                    // tangkapan referensi (tidak terbalik/bergeser) di D3D maupun GLES.
                    yield return Shot("12_glass_probe_reference");
                    var probe = UIKit.Panel(GameObject.Find("UI/Full").transform, "GlassProbe", Theme.Surface, false, false);
                    UIKit.Stretch(probe.rectTransform);
                    probe.gameObject.AddComponent<GlassSurface>().Strength = 0f;
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("12_glass_probe");
                    Destroy(probe.gameObject);
                    break;
                }

                AppSession.OpenAR(data);
                yield return new WaitForSeconds(2.5f);
                yield return Shot("10_ar_unsupported_fallback");

                // Scan QR: tanpa webcam = dialog "Kamera tidak ditemukan"; dengan webcam = kartu panduan scan.
                AppSession.OpenMarker(data);
                yield return new WaitForSeconds(4f);
                yield return Shot("11_marker");
                Application.Quit();
            }

            /// <summary>Beberapa frame animasi hunus, lalu disarungkan kembali.</summary>
            IEnumerator DrawFrames(ArtifactInstance inst, string prefix)
            {
                if (inst == null || inst.exploded == null || !inst.exploded.CanDraw) yield break;
                float total = inst.exploded.secondsPerDraw;
                inst.exploded.ToggleDraw();
                float start = Time.time;
                foreach (float f in new[] { 0.2f, 0.4f, 0.55f, 0.75f })
                {
                    while (Time.time - start < total * f) yield return null;
                    yield return Shot($"{prefix}_draw_{Mathf.RoundToInt(f * 100):00}");
                }
                yield return new WaitForSeconds(total);
                yield return Shot($"{prefix}_draw_done");
                inst.exploded.ToggleDraw();
                yield return new WaitForSeconds(total + 0.3f);
            }

            static void Invoke(object target, string method, params object[] args) =>
                target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.Invoke(target, args);

            static void Click(string path)
            {
                var t = GameObject.Find("UI/Full/" + path);
                var b = t != null ? t.GetComponent<UnityEngine.UI.Button>() : null;
                if (b != null) b.onClick.Invoke();
                else Debug.LogWarning("[DevCapture] tombol tidak ditemukan: " + path);
            }

            IEnumerator Shot(string name)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
        }
    }
}
#endif
