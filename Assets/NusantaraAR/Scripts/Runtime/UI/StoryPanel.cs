using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Mode Kisah: narator bercerita tentang sejarah dan cara pembuatan artefak, bab demi bab. Setiap bab
    /// menggerakkan model (hunus / bongkar / rakit lewat <see cref="ExplodedViewController"/>), menyorot label bagian
    /// yang sedang diceritakan, dan menampilkan subtitle per kalimat yang mengikuti suara. Panel ini menggantikan dock
    /// selama kisah berjalan; model tetap bisa diputar/di-zoom. Data: <see cref="ArtifactData.story"/>.
    /// </summary>
    public class StoryPanel : MonoBehaviour
    {
        public const float Height = 236f;
        const float GapBetweenChapters = 0.8f;
        const float FallbackCharsPerSecond = 14f; // bab tanpa audio: subtitle berjalan dengan pewaktu

        RectTransform rt;
        TextMeshProUGUI chapterLabel, caption;
        Image pauseIcon;
        RectTransform progressFill;
        HotspotOverlay overlay;

        ArtifactInstance artifact;
        ArtifactStory story;
        int chapter = -1;
        AudioClip clip;
        List<StoryCue> cues = new List<StoryCue>();
        float clock, duration, gap;
        bool paused, loading, suspended, resumeOnShow;

        public bool IsActive => chapter >= 0;
        public bool HasStory => story != null && story.chapters.Count > 0;
        public event Action<bool> ActiveChanged;

        public static StoryPanel Create(RectTransform safeRoot, HotspotOverlay overlay)
        {
            var bg = UIKit.Surface(safeRoot, "StoryPanel", SurfaceStyle.Glass, 40);
            var panel = bg.gameObject.AddComponent<StoryPanel>();
            panel.rt = bg.rectTransform;
            panel.overlay = overlay;
            panel.Build();
            bg.gameObject.SetActive(false);
            return panel;
        }

        void Build()
        {
            UIKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(1020f, Height));
            UIKit.VColumn(rt, 6f, new RectOffset(32, 20, 14, 20));

            var header = UIKit.Rect("Header", rt);
            UIKit.HRow(header, 10f, null, false);
            UIKit.Layout(header, 68f);
            chapterLabel = UIKit.Text(header, "Chapter", "", 26f, Theme.AccentText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            chapterLabel.textWrappingMode = TextWrappingModes.NoWrap;
            chapterLabel.overflowMode = TextOverflowModes.Ellipsis;
            chapterLabel.richText = true;
            UIKit.Layout(chapterLabel, -1, -1, 1f);
            var pause = UIKit.IconButton(header, "Pause", Icon.Pause, TogglePause, 68f, SurfaceStyle.Outline);
            pauseIcon = pause.transform.Find("Icon").GetComponent<Image>();
            UIKit.Layout(pause, -1, 68f);
            var next = UIKit.IconButton(header, "Next", Icon.SkipNext, Next, 68f, SurfaceStyle.Outline);
            UIKit.Layout(next, -1, 68f);
            var close = UIKit.IconButton(header, "Close", Icon.Close, Stop, 68f, SurfaceStyle.Outline);
            UIKit.Layout(close, -1, 68f);

            caption = UIKit.Text(rt, "Caption", "", 30f, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 22f;
            caption.fontSizeMax = 30f;
            caption.lineSpacing = 4f;
            var capLayout = UIKit.Layout(caption, 100f);
            capLayout.flexibleHeight = 1f;

            var bar = UIKit.Panel(rt, "Progress", Theme.Line, true, false, 4);
            UIKit.Layout(bar, 8f);
            var fill = UIKit.Panel(bar.transform, "Fill", Theme.Accent, true, false, 4);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;

            Locale.Changed += OnLocaleChanged;
        }

        void OnDestroy()
        {
            Locale.Changed -= OnLocaleChanged;
            // Pindah scene saat kisah berjalan: AudioManager bertahan antarscene, jadi suara dihentikan di sini.
            if (IsActive && AudioManager.Exists && AudioManager.Instance.CurrentNarration == clip) AudioManager.Instance.StopNarration();
        }

        // ------------------------------------------------------------------ API

        /// <summary>Jarak panel dari dasar area aman (di atas bar navigasi di 3D Viewer).</summary>
        public void SetBottom(float y) => rt.anchoredPosition = new Vector2(0f, y);

        public void Bind(ArtifactInstance instance)
        {
            Stop();
            artifact = instance;
            story = instance != null && instance.Data != null ? instance.Data.story : null;
        }

        public void Play()
        {
            if (!HasStory || artifact == null) return;
            overlay.Card.Close();
            if (artifact.autoRotate != null) artifact.autoRotate.Active = false;
            bool wasActive = IsActive;
            suspended = resumeOnShow = false;
            UIKit.SetVisible(rt, true);
            StartChapter(0);
            Analytics.Log("story_start", ("artifact", artifact.Data.artifactId));
            if (!wasActive) ActiveChanged?.Invoke(true);
        }

        public void Stop()
        {
            if (!IsActive) return;
            if (AudioManager.Instance.CurrentNarration == clip) AudioManager.Instance.StopNarration();
            chapter = -1;
            clip = null;
            overlay.FocusId = null;
            UIKit.SetVisible(rt, false);
            ActiveChanged?.Invoke(false);
        }

        /// <summary>
        /// Kontrol HUD disembunyikan sementara (QR hilang dari kamera, mode Pindahkan): suara dijeda dan dilanjutkan
        /// otomatis saat kontrol tampil lagi.
        /// </summary>
        public void SetSuspended(bool value)
        {
            if (!IsActive || suspended == value) return;
            suspended = value;
            UIKit.SetVisible(rt, !value);
            if (value)
            {
                resumeOnShow = !paused;
                SetPaused(true);
            }
            else if (resumeOnShow) SetPaused(false);
        }

        // ------------------------------------------------------------------ bab

        void StartChapter(int index)
        {
            if (AudioManager.Instance.CurrentNarration == clip) AudioManager.Instance.StopNarration();
            chapter = index;
            var ch = story.chapters[index];
            clip = ch.Voice;
            duration = clip != null ? clip.length : Mathf.Max(3f, ch.text.Get().Length / FallbackCharsPerSecond);
            cues = clip != null && ch.Cues != null && ch.Cues.Count > 0 ? ch.Cues : SplitSentences(ch.text.Get(), duration);
            clock = 0f;
            gap = 0f;
            paused = false;

            var ex = artifact != null ? artifact.exploded : null;
            if (ch.stage >= 0 && ex != null && ch.stage < ex.StageCount && ch.stage != ex.TargetStage) ex.GoTo(ch.stage);
            overlay.FocusId = string.IsNullOrEmpty(ch.focusHotspot) ? null : ch.focusHotspot;

            loading = clip != null && clip.loadState != AudioDataLoadState.Loaded;
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (!loading && clip != null) AudioManager.Instance.PlayNarration(clip);
            PreloadNext();
            RefreshTexts();
        }

        void PreloadNext()
        {
            if (chapter + 1 >= story.chapters.Count) return;
            var next = story.chapters[chapter + 1].Voice;
            if (next != null && next.loadState == AudioDataLoadState.Unloaded) next.LoadAudioData();
        }

        void Next()
        {
            if (!IsActive) return;
            if (chapter + 1 < story.chapters.Count) StartChapter(chapter + 1);
            else Stop();
        }

        void TogglePause()
        {
            if (IsActive) SetPaused(!paused);
        }

        void SetPaused(bool value)
        {
            paused = value;
            var am = AudioManager.Instance;
            if (clip != null && !loading && gap <= 0f)
            {
                if (value) am.PauseNarration();
                else if (am.CurrentNarration == clip) am.ResumeNarration();
                else am.PlayNarration(clip);
            }
            RefreshTexts();
        }

        void OnApplicationPause(bool pausedApp)
        {
            // AudioManager sudah menjeda suaranya; kisah ikut dijeda agar tidak dianggap bab selesai.
            if (pausedApp && IsActive && !paused) SetPaused(true);
        }

        void OnLocaleChanged()
        {
            if (!IsActive) return;
            bool wasPaused = paused;
            StartChapter(chapter); // suara & subtitle bahasa baru dari awal bab
            if (wasPaused) SetPaused(true);
        }

        void Update()
        {
            if (!IsActive || suspended) return;
            var am = AudioManager.Instance;

            if (loading)
            {
                if (clip.loadState == AudioDataLoadState.Loading) return;
                loading = false;
                if (clip.loadState != AudioDataLoadState.Loaded) clip = null; // gagal dimuat: lanjut dengan pewaktu
                else if (!paused) am.PlayNarration(clip);
            }

            if (gap > 0f)
            {
                if (paused) return;
                gap -= Time.unscaledDeltaTime;
                if (gap <= 0f) Next();
                return;
            }

            if (clip != null)
            {
                if (am.CurrentNarration != clip)
                {
                    if (am.CurrentNarration != null)
                    {
                        Stop(); // narasi lain mengambil alih (mis. tombol Putar di kartu info)
                        return;
                    }
                    if (!paused) am.PlayNarration(clip); // selesai dimuat saat dijeda, lalu dilanjutkan
                }
                else
                {
                    clock = am.NarrationTime;
                    if (!paused && !am.IsNarrationPlaying)
                    {
                        clock = duration;
                        gap = GapBetweenChapters;
                    }
                }
            }
            else if (!paused)
            {
                clock += Time.unscaledDeltaTime;
                if (clock >= duration) gap = GapBetweenChapters;
            }

            UpdateCaption();
        }

        // ------------------------------------------------------------------ tampilan

        void RefreshTexts()
        {
            if (!IsActive) return;
            var ch = story.chapters[chapter];
            string draft = story.curatorValidated ? "" : "  <color=" + Theme.HexOf(Theme.InkMuted) + "><size=22>" + Locale.T("story.draft") + "</size></color>";
            chapterLabel.text = Locale.T("story.title") + " " + (chapter + 1) + "/" + story.chapters.Count + "  -  " + ch.title.Get() + draft;
            pauseIcon.sprite = IconFactory.Get(paused ? Icon.Play : Icon.Pause);
            UpdateCaption();
        }

        void UpdateCaption()
        {
            int line = 0;
            for (int i = 1; i < cues.Count; i++)
                if (clock >= cues[i].time - 0.05f) line = i;
            string text = cues.Count > 0 ? cues[line].text : "";
            if (caption.text != text) caption.text = text;

            float chapterProgress = duration > 0f ? Mathf.Clamp01(clock / duration) : 0f;
            float total = (chapter + chapterProgress) / Mathf.Max(1, story.chapters.Count);
            progressFill.anchorMax = new Vector2(total, 1f);
        }

        static readonly Regex SentenceEnd = new Regex(@"(?<=[.!?])\s+");

        /// <summary>Tanpa waktu kalimat dari TTS: pecah per kalimat, waktunya sebanding panjang teks.</summary>
        public static List<StoryCue> SplitSentences(string text, float totalSeconds)
        {
            var list = new List<StoryCue>();
            text = text ?? "";
            float at = 0f;
            foreach (var p in SentenceEnd.Split(text))
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                list.Add(new StoryCue { time = at / Mathf.Max(1, text.Length) * totalSeconds, text = p.Trim() });
                at += p.Length + 1;
            }
            return list;
        }
    }
}
