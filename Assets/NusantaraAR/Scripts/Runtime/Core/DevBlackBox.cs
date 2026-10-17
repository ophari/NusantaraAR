#if NUSANTARA_CAPTURE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NusantaraAR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NusantaraAR
{
    /// <summary>
    /// Hanya untuk build QA (NUSANTARA_CAPTURE), dijalankan dengan argumen -blackbox (fase 1) atau -blackbox2
    /// (fase 2: buka ulang aplikasi tanpa menghapus PlayerPrefs). Menjalankan skenario uji black box lewat
    /// tombol dan sentuhan virtual, menyimpan tangkapan layar ke Captures/BlackBox, dan mencatat pengamatan
    /// ke blackbox_log.txt. Skenario kamera (Scan QR pada kartu) dan ARCore tidak termasuk.
    /// </summary>
    public static class DevBlackBox
    {
        public static bool Requested(out int phase)
        {
            var args = Environment.GetCommandLineArgs();
            phase = args.Contains("-blackbox2") ? 2 : args.Contains("-blackbox") ? 1 : 0;
            return phase > 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Requested(out int phase) || Object.FindAnyObjectByType<MainController>() == null) return;
            int mobile = Array.IndexOf(QualitySettings.names, "Mobile");
            if (mobile >= 0) QualitySettings.SetQualityLevel(mobile, true);
            if (phase == 1)
            {
                // Sebelum MainController.Start: keadaan pemasangan baru.
                PlayerPrefs.DeleteAll();
                Locale.Current = Language.ID;
            }
            var go = new GameObject("DevBlackBox");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>().phase = phase;
        }

        class Runner : MonoBehaviour
        {
            public int phase;
            string dir;
            StreamWriter log;
            Touchscreen touch;

            IEnumerator Start()
            {
                dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Captures", "BlackBox");
                Directory.CreateDirectory(dir);
                log = new StreamWriter(Path.Combine(dir, "blackbox_log.txt"), phase == 2) { AutoFlush = true };
                Log($"===== FASE {phase} | {DateTime.Now:yyyy-MM-dd HH:mm:ss} | layar {Screen.width}x{Screen.height} | v{Application.version}");
                touch = InputSystem.AddDevice<Touchscreen>("BlackBoxTouch");
                yield return new WaitForSeconds(2f);
                if (phase == 1) yield return Phase1();
                else yield return Phase2();
                Log("===== selesai");
                log.Close();
                Application.Quit();
            }

            // ============================================================ fase 1

            IEnumerator Phase1()
            {
                // BB-01 onboarding
                Log("BB-01 onboarding tampil: " + IsShown("Onboarding"));
                yield return Shot("BB01_1_halaman1");
                Press("Next", "Onboarding"); yield return Wait(0.6f); yield return Shot("BB01_2_halaman2");
                Press("Next", "Onboarding"); yield return Wait(0.6f); yield return Shot("BB01_3_halaman3");
                Log("BB-01 label tombol halaman 3: " + ButtonText("Next", "Onboarding"));
                Press("Next", "Onboarding"); yield return Wait(0.8f); yield return Shot("BB01_4_setelah_mulai");
                Log("BB-01 onboarding tampil setelah Mulai: " + IsShown("Onboarding"));

                // BB-05 katalog
                Log("BB-05 kartu katalog: " + string.Join(", ", ActiveCards()));
                yield return Shot("BB05_1_katalog_atas");
                SetScroll("List", 0f); yield return Wait(0.5f); yield return Shot("BB05_2_katalog_bawah");
                SetScroll("List", 1f); yield return Wait(0.3f);

                // BB-06 filter kategori
                foreach (var chip in new[] { "Keris & Senjata", "Candi", "Satwa Endemik", "Semua" })
                {
                    PressText(chip, "Categories"); yield return Wait(0.5f);
                    Log($"BB-06 filter '{chip}': " + string.Join(", ", ActiveCards()));
                    yield return Shot("BB06_" + chip.Replace(" & ", "_").Replace(" ", "_"));
                }

                // BB-04 navigasi (Scan QR diuji di akhir karena berpindah scene)
                Press("Settings", "BottomNav"); yield return Wait(0.6f);
                Log("BB-04 tab Pengaturan -> layar Pengaturan tampil: " + IsShown("SettingsScreen"));
                yield return Shot("BB04_1_tab_pengaturan");
                Press("Collection", "BottomNav"); yield return Wait(0.6f);
                Log("BB-04 tab Koleksi -> Pengaturan tertutup: " + !IsShown("SettingsScreen") + ", kartu katalog: " + ActiveCards().Count);
                yield return Shot("BB04_2_tab_koleksi");

                // BB-23 / BB-24 pengaturan
                Press("Settings", "BottomNav"); yield return Wait(0.6f);
                SetSlider("settings.narrationVol", 0.3f);
                SetSlider("settings.musicVol", 0.2f);
                SetSlider("settings.sfxVol", 0.9f);
                Log($"BB-23 slider diubah -> AppSettings narasi={AppSettings.NarrationVolume:0.00} musik={AppSettings.MusicVolume:0.00} efek={AppSettings.SfxVolume:0.00}");
                yield return Shot("BB23_1_slider_diubah");
                SetScroll("Content", 0.45f, "SettingsScreen"); yield return Wait(0.4f); yield return Shot("BB24_1_pengaturan_tengah");
                SetScroll("Content", 0f, "SettingsScreen"); yield return Wait(0.4f); yield return Shot("BB24_2_tentang_kredit_musik");
                Log("BB-24 teks kredit musik: " + Oneline(TextOf("MusicCredits")));

                // BB-03 ulangi tutorial
                Press("Tutorial", "SettingsScreen"); yield return Wait(0.8f);
                Log("BB-03 onboarding tampil setelah Ulangi tutorial: " + IsShown("Onboarding"));
                yield return Shot("BB03_1_tutorial_diulang");
                Press("Skip", "Onboarding"); yield return Wait(0.8f);
                Log("BB-03 onboarding tertutup setelah Lewati: " + !IsShown("Onboarding"));
                yield return Shot("BB03_2_setelah_lewati");

                // BB-07, BB-12, BB-13, BB-14 untuk tiap artefak
                var artifacts = AppSession.Catalog.artifacts.Where(a => a != null).ToList();
                foreach (var a in artifacts)
                {
                    yield return OpenFromCatalog(a);
                    yield return ExplodeAssemble(a);
                    yield return DrawSheathe(a);
                }

                // Pengujian interaksi pada artefak pertama
                var first = artifacts[0];
                yield return OpenFromCatalog(first, false);
                yield return Gestures();
                yield return Hotspots();
                yield return Story();

                // BB-29 Tampilkan QR
                Press("ShowCard", "DetailSheet"); yield return Wait(0.8f);
                Log("BB-29 layar kode QR tampil: " + IsShown("MarkerCard"));
                yield return Shot("BB29_1_tampilkan_qr");
                Press("Close", "MarkerCard"); yield return Wait(0.5f);

                // BB-30 tanpa ARCore
                var place = FindAll("PlaceOnTable", "DetailSheet").FirstOrDefault();
                Log("BB-30 tombol Letakkan di Meja tampil di detail: " + (place != null && place.gameObject.activeInHierarchy));
                yield return Shot("BB30_1_detail_tanpa_tombol_meja");
                AppSession.OpenAR(first);
                yield return Wait(3f);
                Log("BB-30 dialog di scene AR: " + Oneline(DialogText()));
                yield return Shot("BB30_2_dialog_ar_tidak_tersedia");
                bool back = PressText("Buka di 3D Viewer");
                yield return Wait(2.5f);
                Log("BB-30 tombol 'Buka di 3D Viewer' ditekan: " + back + ", kembali ke 3D Viewer: " + (Object.FindAnyObjectByType<MainController>() != null));
                yield return Shot("BB30_3_kembali_ke_3d_viewer");

                // BB-21 bahasa (dibiarkan English untuk diperiksa di fase 2)
                Press("Settings", "BottomNav"); yield return Wait(0.6f);
                PressText("English", "SettingsScreen"); yield return Wait(0.6f);
                Log("BB-21 Locale setelah memilih English: " + Locale.Current);
                yield return Shot("BB21_1_pengaturan_english");
                Press("Collection", "BottomNav"); yield return Wait(0.6f);
                yield return Shot("BB21_2_katalog_english");
                yield return OpenFromCatalog(first, false);
                yield return Shot("BB21_3_detail_english");
                if (PressFirstVisibleLabel(out string hid))
                {
                    yield return Wait(1f);
                    Log("BB-21 kartu info (English) bagian " + hid + ": " + Oneline(CardTitle()));
                    yield return Shot("BB21_4_kartu_info_english");
                    Hud()?.CloseInfo(); yield return Wait(0.4f);
                }
                Press("Story", "Rail"); yield return Wait(5f);
                Log("BB-21 subtitle Kisah (English): " + Oneline(StorySubtitle()));
                yield return Shot("BB21_5_kisah_english");
                Hud()?.CloseInfo(); yield return Wait(0.5f);

                // BB-04 tab Scan QR (kamera laptop menyala; scan kartu tidak diuji di sini)
                Press("Scan", "BottomNav"); yield return Wait(4f);
                Log("BB-04 tab Scan QR -> scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                yield return Shot("BB04_3_tab_scan_qr");
            }

            IEnumerator OpenFromCatalog(ArtifactData a, bool record = true)
            {
                Hud()?.CloseInfo();
                Press("Collection", "BottomNav"); yield return Wait(0.6f);
                Press("Card_" + a.artifactId, "List"); yield return Wait(1.5f);
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                if (!record) yield break;
                var music = AudioManager.Instance != null ? AudioManager.Instance.CurrentMusic : null;
                Log($"BB-07 {a.artifactId}: model tampil={(inst != null && inst.Data == a)}, musik={(music != null ? music.name : "-")}, sedang diputar={MusicPlaying()}");
                yield return Shot($"BB07_{a.artifactId}");
            }

            IEnumerator ExplodeAssemble(ArtifactData a)
            {
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                var ex = inst != null ? inst.exploded : null;
                if (ex == null) { Log($"BB-12 {a.artifactId}: tidak ada exploded view"); yield break; }
                Log($"BB-12 {a.artifactId}: jumlah tahap (termasuk utuh) = {ex.StageCount}");
                Press("Explode", "Rail");
                int last = ex.CurrentStage;
                for (int guard = 0; guard < 12; guard++)
                {
                    float t = 0f;
                    while (ex.CurrentStage == last && t < 5f) { t += Time.deltaTime; yield return null; }
                    if (ex.CurrentStage == last) break;
                    last = ex.CurrentStage;
                    yield return Wait(0.15f);
                    Log($"BB-12 {a.artifactId}: tahap {last} '{ex.CurrentStageLabel}'");
                    yield return Shot($"BB12_{a.artifactId}_tahap{last}");
                    if (last >= ex.StageCount - 1) break;
                }
                yield return Wait(0.6f);
                Log($"BB-12 {a.artifactId}: tahap akhir {ex.CurrentStage}/{ex.StageCount - 1}, label tombol: {ButtonText("Explode", "Rail")}");

                Press("Explode", "Rail");
                float w = 0f;
                while ((ex.CurrentStage != 0 || ex.IsAnimating) && w < 12f) { w += Time.deltaTime; yield return null; }
                yield return Wait(0.5f);
                Log($"BB-13 {a.artifactId}: setelah Gabung tahap={ex.CurrentStage}, utuh={ex.IsAssembled}");
                yield return Shot($"BB13_{a.artifactId}_gabung");
            }

            IEnumerator DrawSheathe(ArtifactData a)
            {
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                var ex = inst != null ? inst.exploded : null;
                var drawButton = FindAll("Draw", "Rail").FirstOrDefault();
                bool visible = drawButton != null && drawButton.gameObject.activeInHierarchy;
                Log($"BB-14 {a.artifactId}: tombol Hunus tampil={visible}");
                if (!visible || ex == null) yield break;
                Press("Draw", "Rail");
                yield return Wait(ex.secondsPerDraw + 0.5f);
                Log($"BB-14 {a.artifactId}: setelah Hunus tahap={ex.CurrentStage}, label tombol={ButtonText("Draw", "Rail")}");
                yield return Shot($"BB14_{a.artifactId}_hunus");
                Press("Draw", "Rail");
                yield return Wait(ex.secondsPerDraw + 0.5f);
                Log($"BB-14 {a.artifactId}: setelah Sarungkan tahap={ex.CurrentStage}, label tombol={ButtonText("Draw", "Rail")}");
                yield return Shot($"BB14_{a.artifactId}_sarungkan");
            }

            IEnumerator Gestures()
            {
                var cam = Object.FindAnyObjectByType<MainController>().viewerCamera;
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                // Pengguna menggeser di area kosong: titik mulai tidak boleh di atas label/titik hotspot (UI).
                var c = FreePoint(ModelScreenCenter(cam, inst), Screen.width * 0.12f);
                Log($"BB-08 titik mulai sentuhan ({c.x:0},{c.y:0}), di atas UI={TouchGestures.IsOverUI(c)}");
                ViewPose start = Snap(cam, inst);
                yield return Shot("BB08_1_sebelum_geser");
                yield return Drag(c, c + new Vector2(Screen.width * 0.35f, 0f));
                yield return Wait(0.8f);
                ViewPose afterDrag = Snap(cam, inst);
                Log($"BB-08 geser 1 jari: kamera {start} -> {afterDrag}; kartu info terbuka tanpa sengaja={CardOpen()}");
                yield return Shot("BB08_2_sesudah_geser");

                // Setelah diputar, label berpindah: cari ulang titik kosong untuk kedua jari.
                Press("Reset", "Rail"); yield return Wait(1f);
                c = FreePoint(ModelScreenCenter(cam, inst), Screen.width * 0.10f);
                Log($"BB-09 titik tengah cubit ({c.x:0},{c.y:0}), jari di atas UI={TouchGestures.IsOverUI(c - Vector2.right * Screen.width * 0.10f) || TouchGestures.IsOverUI(c + Vector2.right * Screen.width * 0.10f)}");
                float d0 = Vector3.Distance(cam.transform.position, inst.transform.position);
                yield return Pinch(c, Screen.width * 0.10f, Screen.width * 0.32f);
                yield return Wait(0.8f);
                float d1 = Vector3.Distance(cam.transform.position, inst.transform.position);
                Log($"BB-09 cubit membuka: jarak kamera ke model {d0:0.000} -> {d1:0.000} (lebih kecil = diperbesar)");
                yield return Shot("BB09_1_cubit_perbesar");
                c = FreePoint(ModelScreenCenter(cam, inst), Screen.width * 0.10f);
                yield return Pinch(c, Screen.width * 0.10f, Screen.width * 0.025f);
                yield return Wait(0.8f);
                float d2 = Vector3.Distance(cam.transform.position, inst.transform.position);
                Log($"BB-09 cubit menutup: jarak kamera ke model {d1:0.000} -> {d2:0.000} (lebih besar = diperkecil); kartu info terbuka tanpa sengaja={CardOpen()}");
                yield return Shot("BB09_2_cubit_perkecil");
                Hud()?.CloseInfo(); yield return Wait(0.4f);

                Press("Reset", "Rail"); yield return Wait(1.2f);
                ViewPose afterReset = Snap(cam, inst);
                Log($"BB-11 Reset: {afterReset} (awal {start})");
                yield return Shot("BB11_1_setelah_reset");

                float yaw0 = inst.transform.eulerAngles.y + cam.transform.eulerAngles.y;
                Press("AutoRotate", "Rail"); yield return Wait(0.2f);
                yield return Shot("BB10_1_putar360_mulai");
                yield return Wait(1.5f);
                float yaw1 = inst.transform.eulerAngles.y + cam.transform.eulerAngles.y;
                Log($"BB-10 Putar 360: aktif={(inst.autoRotate != null && inst.autoRotate.Active)}, sudut berubah {Mathf.DeltaAngle(yaw0, yaw1):0.0} derajat dalam 1,5 dtk");
                yield return Shot("BB10_2_putar360_1_5dtk");
                Press("AutoRotate", "Rail"); yield return Wait(0.3f);
                Press("Reset", "Rail"); yield return Wait(1f);
            }

            IEnumerator Hotspots()
            {
                Log("BB-15 label tampil (awal): " + VisibleLabels());
                yield return Shot("BB15_1_label_awal");
                Press("Labels", "Rail"); yield return Wait(0.6f);
                Log("BB-15 label tampil setelah tombol Label: " + VisibleLabels());
                yield return Shot("BB15_2_label_ditekan");
                Press("Labels", "Rail"); yield return Wait(0.6f);
                Log("BB-15 label tampil setelah ditekan lagi: " + VisibleLabels());
                yield return Shot("BB15_3_label_ditekan_lagi");

                if (PressFirstVisibleLabel(out string id))
                {
                    yield return Wait(1f);
                    Log($"BB-16 ketuk label '{id}': kartu terbuka={CardOpen()}, judul={Oneline(CardTitle())}");
                    yield return Shot("BB16_1_kartu_tab_kriya");
                    foreach (var tab in new[] { "Filosofi", "Sejarah" })
                    {
                        PressText(tab, "InfoCard"); yield return Wait(0.5f);
                        yield return Shot("BB16_2_kartu_tab_" + tab.ToLower());
                    }
                    Log("BB-16 teks 'Sumber rujukan' ada di kartu: " + TextsUnder("InfoCard").Any(t => t.Contains("Sumber rujukan")));

                    var before = Hud().Overlay.SelectedId;
                    Press("Next", "InfoCard"); yield return Wait(0.8f);
                    var mid = Hud().Overlay.SelectedId;
                    Log($"BB-17 panah kanan: bagian {before} -> {mid}");
                    yield return Shot("BB17_1_panah_kanan");
                    Press("Prev", "InfoCard"); yield return Wait(0.8f);
                    Log($"BB-17 panah kiri: bagian {mid} -> {Hud().Overlay.SelectedId}");
                    yield return Shot("BB17_2_panah_kiri");
                    Hud().CloseInfo(); yield return Wait(0.5f);
                }
                else Log("BB-16 tidak ada label yang terlihat untuk diketuk");

                // Ketuk langsung pada bagian model (sentuhan virtual, bukan tombol)
                var cam = Object.FindAnyObjectByType<MainController>().viewerCamera;
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                bool hit = false;
                var points = new List<Vector3>();
                foreach (var part in inst.GetComponentsInChildren<Collider>())
                {
                    var b = part.bounds;
                    for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++)
                        points.Add(cam.WorldToScreenPoint(b.center + Vector3.Scale(b.extents, new Vector3(i / 2f - 1f, j / 2f - 1f, 0f)) * 0.8f));
                }
                foreach (var p in points)
                {
                    if (p.z <= 0 || Hud().Overlay.PickAt(p) == null || TouchGestures.IsOverUI(p)) continue;
                    yield return Tap(p);
                    yield return Wait(1f);
                    Log($"BB-16 ketuk bagian model di layar ({p.x:0},{p.y:0}): kartu terbuka={CardOpen()}, judul={Oneline(CardTitle())}");
                    yield return Shot("BB16_3_ketuk_bagian_model");
                    hit = true;
                    break;
                }
                if (!hit) Log("BB-16 tidak menemukan titik bagian model untuk diketuk");
                Hud().CloseInfo(); yield return Wait(0.5f);
            }

            IEnumerator Story()
            {
                float musicBefore = MusicVolume();
                Press("Story", "Rail"); yield return Wait(4f);
                var am = AudioManager.Instance;
                Log($"BB-18 Kisah: panel aktif={Hud().Story.IsActive}, narasi berbunyi={am.IsNarrationPlaying}, klip={(am.CurrentNarration != null ? am.CurrentNarration.name : "-")}, volume musik {musicBefore:0.000} -> {MusicVolume():0.000}");
                Log("BB-18 subtitle: " + Oneline(StorySubtitle()));
                yield return Shot("BB18_1_kisah_bab1");
                yield return Wait(4f);
                Log("BB-18 subtitle 4 dtk kemudian: " + Oneline(StorySubtitle()));
                yield return Shot("BB18_2_kisah_bab1_lanjut");

                Press("Pause", "StoryPanel"); yield return Wait(1.5f);
                Log($"BB-19 setelah Jeda: narasi berbunyi={am.IsNarrationPlaying}");
                yield return Shot("BB19_1_jeda");
                Press("Pause", "StoryPanel"); yield return Wait(1.5f);
                Log($"BB-19 setelah Lanjut: narasi berbunyi={am.IsNarrationPlaying}");
                yield return Shot("BB19_2_lanjut");

                var clipBefore = am.CurrentNarration != null ? am.CurrentNarration.name : "-";
                Press("Next", "StoryPanel"); yield return Wait(3f);
                var clipAfter = am.CurrentNarration != null ? am.CurrentNarration.name : "-";
                Log($"BB-20 bab berikutnya: klip {clipBefore} -> {clipAfter}");
                yield return Shot("BB20_1_bab_berikutnya");
                Press("Next", "StoryPanel"); Press("Next", "StoryPanel"); yield return Wait(5f);
                Log($"BB-18 bab ke-4: klip {(am.CurrentNarration != null ? am.CurrentNarration.name : "-")}, subtitle: {Oneline(StorySubtitle())}");
                yield return Shot("BB18_3_kisah_bab4");
                Press("Close", "StoryPanel"); yield return Wait(1f);
                Log($"BB-18 Kisah ditutup: panel aktif={Hud().Story.IsActive}");
                var inst = Object.FindAnyObjectByType<ArtifactInstance>();
                if (inst != null && inst.exploded != null && !inst.exploded.IsAssembled)
                {
                    inst.exploded.Assemble();
                    yield return Wait(4f);
                }
            }

            // ============================================================ fase 2

            IEnumerator Phase2()
            {
                Log("BB-02 onboarding tampil saat dibuka ulang: " + IsShown("Onboarding") + ", kartu katalog: " + ActiveCards().Count);
                Log("BB-22 Locale saat dibuka ulang: " + Locale.Current);
                yield return Shot("BB02_BB22_1_dibuka_ulang");
                Press("Settings", "BottomNav"); yield return Wait(0.6f);
                Log($"BB-23 volume saat dibuka ulang: narasi={SliderValue("settings.narrationVol"):0.00} musik={SliderValue("settings.musicVol"):0.00} efek={SliderValue("settings.sfxVol"):0.00} (diatur 0,30 / 0,20 / 0,90)");
                yield return Shot("BB22_BB23_2_pengaturan_dibuka_ulang");
                // Bersihkan keadaan uji.
                Locale.Current = Language.ID;
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            // ============================================================ sentuhan virtual

            readonly Dictionary<int, Vector2> lastTouch = new Dictionary<int, Vector2>();

            // Seperti HP nyata, setiap kejadian membawa delta posisi sejak kejadian sebelumnya.
            void TouchEvent(int id, UnityEngine.InputSystem.TouchPhase ph, Vector2 pos)
            {
                var delta = ph == UnityEngine.InputSystem.TouchPhase.Began || !lastTouch.TryGetValue(id, out var prev) ? Vector2.zero : pos - prev;
                lastTouch[id] = pos;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = ph, position = pos, delta = delta, pressure = 1f });
            }

            IEnumerator Drag(Vector2 from, Vector2 to, int steps = 24)
            {
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, from); yield return null; yield return null;
                for (int i = 1; i <= steps; i++)
                {
                    TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Moved, Vector2.Lerp(from, to, i / (float)steps));
                    yield return null;
                }
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, to); yield return null; yield return null;
            }

            IEnumerator Pinch(Vector2 c, float r0, float r1, int steps = 60)
            {
                Vector2 dir = Vector2.right;
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, c - dir * r0);
                TouchEvent(2, UnityEngine.InputSystem.TouchPhase.Began, c + dir * r0);
                yield return null; yield return null;
                for (int i = 1; i <= steps; i++)
                {
                    float r = Mathf.Lerp(r0, r1, i / (float)steps);
                    TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Moved, c - dir * r);
                    TouchEvent(2, UnityEngine.InputSystem.TouchPhase.Moved, c + dir * r);
                    yield return null;
                }
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, c - dir * r1);
                TouchEvent(2, UnityEngine.InputSystem.TouchPhase.Ended, c + dir * r1);
                yield return null; yield return null;
            }

            IEnumerator Tap(Vector2 p)
            {
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Began, p); yield return null; yield return null;
                TouchEvent(1, UnityEngine.InputSystem.TouchPhase.Ended, p); yield return null; yield return null;
            }

            /// <summary>Titik terdekat dari <paramref name="near"/> yang bebas UI, termasuk dua titik ±spread untuk cubit.</summary>
            static Vector2 FreePoint(Vector2 near, float spread)
            {
                bool Free(Vector2 p) => p.x > 20 && p.x < Screen.width - 20 && p.y > 20 && p.y < Screen.height - 20 && !TouchGestures.IsOverUI(p);
                for (float r = 0; r < Screen.height * 0.4f; r += 8f)
                    for (int a = 0; a < 36; a++)
                    {
                        var p = near + new Vector2(Mathf.Cos(a * 10f * Mathf.Deg2Rad), Mathf.Sin(a * 10f * Mathf.Deg2Rad)) * r;
                        if (Free(p) && Free(p + Vector2.right * spread) && Free(p - Vector2.right * spread)) return p;
                        if (r == 0) break;
                    }
                return near;
            }

            static Vector2 ModelScreenCenter(Camera cam, ArtifactInstance inst)
            {
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return new Vector2(Screen.width / 2f, Screen.height / 2f);
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return cam.WorldToScreenPoint(b.center);
            }

            struct ViewPose
            {
                public Vector3 camPos, camEuler, modelEuler;
                public override string ToString() => $"[kamera pos {camPos:F3} rot {camEuler:F1}; model rot {modelEuler:F1}]";
            }

            static ViewPose Snap(Camera cam, ArtifactInstance inst) => new ViewPose
            {
                camPos = cam.transform.position, camEuler = cam.transform.eulerAngles, modelEuler = inst.transform.eulerAngles
            };

            // ============================================================ UI

            static List<Button> FindAll(string name, string ancestor = null) =>
                Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(b => b.name == name && (ancestor == null || HasAncestor(b.transform, ancestor)))
                    .OrderByDescending(b => b.gameObject.activeInHierarchy).ToList();

            static bool HasAncestor(Transform t, string name)
            {
                for (var p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
                return false;
            }

            bool Press(string name, string ancestor = null)
            {
                var b = FindAll(name, ancestor).FirstOrDefault(x => x.gameObject.activeInHierarchy && x.interactable);
                if (b == null) { Log($"   ! tombol '{name}' ({ancestor}) tidak ditemukan/aktif"); return false; }
                b.onClick.Invoke();
                return true;
            }

            bool PressText(string text, string ancestor = null)
            {
                foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (ancestor != null && !HasAncestor(b.transform, ancestor)) continue;
                    if (b.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Trim() == text)) { b.onClick.Invoke(); return true; }
                }
                Log($"   ! tombol berlabel '{text}' ({ancestor}) tidak ditemukan");
                return false;
            }

            static string ButtonText(string name, string ancestor)
            {
                var b = FindAll(name, ancestor).FirstOrDefault();
                return b != null ? string.Join(" ", b.GetComponentsInChildren<TMP_Text>().Select(t => t.text)) : "-";
            }

            static bool IsShown(string name)
            {
                foreach (var t in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (t.name != name) continue;
                    var g = t.GetComponent<CanvasGroup>();
                    if (g == null || g.alpha > 0.5f) return true;
                }
                return false;
            }

            static List<string> ActiveCards() =>
                Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(b => b.name.StartsWith("Card_") && HasAncestor(b.transform, "List"))
                    .OrderByDescending(b => b.transform.position.y).Select(b => b.name.Substring(5)).ToList();

            static void SetScroll(string name, float v, string ancestor = null)
            {
                foreach (var s in Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (s.name == name && (ancestor == null || HasAncestor(s.transform, ancestor))) s.verticalNormalizedPosition = v;
            }

            void SetSlider(string name, float v)
            {
                var s = Object.FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.name == name);
                if (s == null) { Log($"   ! slider '{name}' tidak ditemukan"); return; }
                s.value = v;
            }

            static float SliderValue(string name) =>
                Object.FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(x => x.name == name)?.value ?? -1f;

            static string TextOf(string name) =>
                Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name)?.text ?? "-";

            static IEnumerable<string> TextsUnder(string ancestor) =>
                Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(t => HasAncestor(t.transform, ancestor)).Select(t => t.text);

            static ArtifactHud Hud() => Object.FindAnyObjectByType<ArtifactHud>();

            static bool CardOpen() => Hud() != null && Hud().Overlay.Card.IsOpen;

            static string CardTitle() => Hud() != null && Hud().Overlay.Card.Current != null ? Hud().Overlay.Card.Current.title.Get() : "-";

            static string StorySubtitle() => string.Join(" | ", TextsUnder("StoryPanel").Where(t => t.Length > 20));

            static string DialogText() => string.Join(" | ", TextsUnder("Message"));

            static string VisibleLabels()
            {
                var shown = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(b => b.name.StartsWith("Label_") && b.GetComponent<CanvasGroup>() is CanvasGroup g && g.alpha > 0.5f)
                    .Select(b => b.name.Substring(6)).ToList();
                return shown.Count + " (" + string.Join(", ", shown) + ")";
            }

            static bool PressFirstVisibleLabel(out string id)
            {
                var b = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(x => x.name.StartsWith("Label_") && x.GetComponent<CanvasGroup>() is CanvasGroup g && g.alpha > 0.5f && g.blocksRaycasts)
                    .OrderByDescending(x => x.transform.position.y).FirstOrDefault();
                id = b != null ? b.name.Substring(6) : null;
                if (b == null) return false;
                b.onClick.Invoke();
                return true;
            }

            static AudioSource MusicSource()
            {
                var clip = AudioManager.Instance != null ? AudioManager.Instance.CurrentMusic : null;
                return clip == null ? null : Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).FirstOrDefault(s => s.clip == clip);
            }

            static bool MusicPlaying() => MusicSource() is AudioSource s && s.isPlaying;

            static float MusicVolume() => MusicSource() is AudioSource s ? s.volume : -1f;

            static string Oneline(string s) => string.IsNullOrEmpty(s) ? "-" : s.Replace("\n", " / ").Replace("\r", "");

            // ============================================================ bukti

            void Log(string line)
            {
                log.WriteLine(line);
                Debug.Log("[BlackBox] " + line);
            }

            static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

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
