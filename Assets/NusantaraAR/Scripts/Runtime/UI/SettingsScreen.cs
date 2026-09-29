using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Layar 6: bahasa, volume, persetujuan analitik, ulangi tutorial, tentang (PRD §5.2). Kartu kaca di atas latar
    /// kamera (<see cref="Backdrop"/>); tab Pengaturan di <see cref="BottomNav"/> tetap tampil di bawahnya.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        Action onReplayTutorial;
        RectTransform content;
        Segmented language;
        SwitchToggle analytics;
        TextMeshProUGUI musicCredits;

        public event Action VisibilityChanged;

        public static SettingsScreen Create(RectTransform fullRoot, Action onReplayTutorial)
        {
            var bg = UIKit.Panel(fullRoot, "SettingsScreen", new Color(1f, 1f, 1f, 0f), false, true);
            bg.canvasRenderer.cullTransparentMesh = true;
            UIKit.Stretch(bg.rectTransform);
            var area = UIKit.Rect("Safe", bg.transform);
            area.gameObject.AddComponent<SafeArea>();
            var s = bg.gameObject.AddComponent<SettingsScreen>();
            s.onReplayTutorial = onReplayTutorial;
            s.Build(area);
            bg.gameObject.SetActive(false);
            return s;
        }

        void Build(RectTransform area)
        {
            var bar = TopBar.Create(area, Hide, false);
            bar.SetTitle(Locale.T("common.settings"));
            LocalizedLabel.Attach(bar.GetComponentInChildren<TextMeshProUGUI>(), "common.settings");

            var scroll = UIKit.VerticalScroll(area, "Content", out content, 28f, new RectOffset(40, 40, 8, 60));
            UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 20f + TopBar.Height + 8f, BottomNav.Height);

            var lang = Card("settings.language");
            language = Segmented.Create(lang, "Language", new[] { "Bahasa Indonesia", "English" }, false,
                Locale.Current == Language.ID ? 0 : 1, i => Locale.Current = i == 0 ? Language.ID : Language.EN, 28f);
            UIKit.Layout(language, 84f);

            var sound = Card("settings.sound");
            VolumeRow(sound, "settings.narrationVol", AppSettings.NarrationVolume, v => AppSettings.NarrationVolume = v);
            VolumeRow(sound, "settings.musicVol", AppSettings.MusicVolume, v => AppSettings.MusicVolume = v);
            VolumeRow(sound, "settings.sfxVol", AppSettings.SfxVolume, v => AppSettings.SfxVolume = v);

            var privacy = Card("settings.privacy");
            var row = UIKit.Rect("Analytics", privacy);
            var h = UIKit.HRow(row, 20f, null, false);
            h.childForceExpandHeight = false;
            UIKit.Layout(row, 84f);
            var label = UIKit.Text(row, "Label", Locale.T("settings.analytics"), 28f, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Attach(label, "settings.analytics");
            UIKit.Layout(label, 84f, -1, 1f);
            analytics = SwitchToggle.Create(row, "Switch", AppSettings.AnalyticsConsent, on => AppSettings.AnalyticsConsent = on);
            var note = UIKit.Text(privacy, "AnalyticsNote", Locale.T("settings.analyticsNote"), 26f, Theme.InkMuted);
            LocalizedLabel.Attach(note, "settings.analyticsNote");

            var tutorial = UIKit.IconTextButton(content, "Tutorial", Icon.Book, Locale.T("settings.tutorial"), ButtonStyle.Secondary, () =>
            {
                Hide();
                onReplayTutorial?.Invoke();
            }, out var tl, 32f);
            LocalizedLabel.Attach(tl, "settings.tutorial");
            UIKit.Layout(tutorial, 100);

            var about = Card("settings.about");
            var aboutText = UIKit.Text(about, "About", Locale.T("settings.aboutBody"), 28f, Theme.Ink);
            aboutText.lineSpacing = 6f;
            LocalizedLabel.Attach(aboutText, "settings.aboutBody");

            // Kredit musik latar dari data artefak (satu baris per artefak yang punya musik).
            if (MusicArtifacts().Count > 0)
            {
                var credits = Card("settings.musicCredits");
                musicCredits = UIKit.Text(credits, "MusicCredits", "", 26f, Theme.Ink);
            }

            var version = UIKit.Text(content, "Version", "v" + Application.version, 26f, Theme.InkMuted, TextAlignmentOptions.Center);
            UIKit.Layout(version, 50);

            RefreshLocalized();
        }

        /// <summary>Kartu kaca berjudul; isi ditumpuk vertikal (tinggi mengikuti isinya).</summary>
        RectTransform Card(string titleKey)
        {
            var card = UIKit.Surface(content, "Card_" + titleKey, SurfaceStyle.Glass, 36);
            var col = UIKit.VColumn(card.rectTransform, 16f, new RectOffset(36, 36, 28, 34));
            col.childForceExpandHeight = false;
            var t = UIKit.Text(card.transform, "Section", Locale.T(titleKey), 32f, Theme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            LocalizedLabel.Attach(t, titleKey);
            UIKit.Layout(t, 48);
            return card.rectTransform;
        }

        static void VolumeRow(RectTransform card, string key, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var label = UIKit.Text(card, "Label", Locale.T(key), 28f, Theme.InkMuted, TextAlignmentOptions.BottomLeft);
            LocalizedLabel.Attach(label, key);
            UIKit.Layout(label, 40);
            UIKit.Layout(UIKit.Slider(card, key, value, onChanged), 64);
        }

        static List<ArtifactData> MusicArtifacts()
        {
            var list = new List<ArtifactData>();
            var catalog = AppSession.Catalog;
            if (catalog == null) return list;
            foreach (var a in catalog.artifacts)
                if (a != null && a.backgroundMusic != null && !string.IsNullOrEmpty(a.musicCredit)) list.Add(a);
            return list;
        }

        void RefreshLocalized()
        {
            language.SetSelected(Locale.Current == Language.ID ? 0 : 1);
            analytics.IsOn = AppSettings.AnalyticsConsent;
            if (musicCredits == null) return;
            var lines = new List<string>();
            foreach (var a in MusicArtifacts()) lines.Add(a.displayName.Get() + ": " + a.musicCredit);
            musicCredits.text = string.Join("\n", lines);
        }

        void OnEnable()
        {
            Locale.Changed += RefreshLocalized;
            if (language != null) RefreshLocalized();
        }

        void OnDisable() => Locale.Changed -= RefreshLocalized;

        public void Show()
        {
            gameObject.SetActive(true);
            VisibilityChanged?.Invoke();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            VisibilityChanged?.Invoke();
        }
    }
}
