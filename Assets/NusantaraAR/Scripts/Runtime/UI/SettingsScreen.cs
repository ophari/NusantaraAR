using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Layar 6: bahasa, volume, persetujuan analitik, ulangi tutorial, tentang (PRD §5.2).</summary>
    public class SettingsScreen : MonoBehaviour
    {
        Action onReplayTutorial;
        RectTransform content;
        TextMeshProUGUI analyticsState, musicCredits;

        public static SettingsScreen Create(RectTransform fullRoot, Action onReplayTutorial)
        {
            var bg = UIKit.Panel(fullRoot, "SettingsScreen", Theme.Teak, false, true);
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
            var title = UIKit.Text(area, "Title", Locale.T("common.settings"), Theme.Title, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            LocalizedLabel.Attach(title, "common.settings");
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -48f), new Vector2(700f, 80f));
            var close = UIKit.Button(area, "Close", Locale.T("common.close"), ButtonStyle.Chip, Hide, out var cl, 30f);
            LocalizedLabel.Attach(cl, "common.close");
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-40f, -52f), new Vector2(220f, 84f));

            var scroll = UIKit.VerticalScroll(area, "Content", out content, 24f, new RectOffset(48, 48, 0, 80));
            UIKit.Stretch((RectTransform)scroll.transform, 0, 0, 170, 0);

            Section("settings.language");
            var langRow = UIKit.Rect("Language", content);
            UIKit.Layout(langRow, 96);
            UIKit.HRow(langRow, 16f);
            UIKit.Button(langRow, "ID", "Bahasa Indonesia", ButtonStyle.Chip, () => SetLanguage(Language.ID));
            UIKit.Button(langRow, "EN", "English", ButtonStyle.Chip, () => SetLanguage(Language.EN));

            Section("settings.narrationVol");
            UIKit.Layout(UIKit.Slider(content, "Narration", AppSettings.NarrationVolume, v => AppSettings.NarrationVolume = v), 80);
            Section("settings.musicVol");
            UIKit.Layout(UIKit.Slider(content, "Music", AppSettings.MusicVolume, v => AppSettings.MusicVolume = v), 80);
            Section("settings.sfxVol");
            UIKit.Layout(UIKit.Slider(content, "Sfx", AppSettings.SfxVolume, v => AppSettings.SfxVolume = v), 80);

            Section("settings.analytics");
            var analytics = UIKit.Button(content, "Analytics", "", ButtonStyle.Chip, ToggleAnalytics, out analyticsState, 32f);
            UIKit.Layout(analytics, 96);
            var note = UIKit.Text(content, "AnalyticsNote", Locale.T("settings.analyticsNote"), Theme.Small, Theme.Stone);
            LocalizedLabel.Attach(note, "settings.analyticsNote");
            UIKit.Layout(note, 90);

            var tutorial = UIKit.Button(content, "Tutorial", Locale.T("settings.tutorial"), ButtonStyle.Secondary, () =>
            {
                Hide();
                onReplayTutorial?.Invoke();
            }, out var tl, 32f);
            LocalizedLabel.Attach(tl, "settings.tutorial");
            UIKit.Layout(tutorial, 100);

            Section("settings.about");
            var about = UIKit.Text(content, "About", Locale.T("settings.aboutBody"), Theme.Small, Theme.Parchment);
            LocalizedLabel.Attach(about, "settings.aboutBody");
            UIKit.Layout(about, 440);

            // Kredit musik latar dari data artefak (satu baris per artefak yang punya musik).
            int musicCount = MusicArtifacts().Count;
            if (musicCount > 0)
            {
                Section("settings.musicCredits");
                musicCredits = UIKit.Text(content, "MusicCredits", "", Theme.Small, Theme.Parchment);
                UIKit.Layout(musicCredits, 90f * musicCount);
            }

            var version = UIKit.Text(content, "Version", "v" + Application.version, 26f, Theme.Stone);
            UIKit.Layout(version, 50);

            RefreshLocalized();
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

        void RefreshMusicCredits()
        {
            if (musicCredits == null) return;
            var lines = new List<string>();
            foreach (var a in MusicArtifacts()) lines.Add(a.displayName.Get() + ": " + a.musicCredit);
            musicCredits.text = string.Join("\n", lines);
        }

        void RefreshLocalized()
        {
            RefreshAnalytics();
            RefreshMusicCredits();
        }

        void Section(string key)
        {
            var t = UIKit.Text(content, "Section", Locale.T(key), Theme.Body, Theme.Gold, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            LocalizedLabel.Attach(t, key);
            UIKit.Layout(t, 84);
        }

        static void SetLanguage(Language lang) => Locale.Current = lang;

        void ToggleAnalytics()
        {
            AppSettings.AnalyticsConsent = !AppSettings.AnalyticsConsent;
            RefreshAnalytics();
        }

        void RefreshAnalytics()
        {
            analyticsState.text = Locale.T(AppSettings.AnalyticsConsent ? "settings.on" : "settings.off");
            analyticsState.color = AppSettings.AnalyticsConsent ? Theme.Gold : Theme.Parchment;
        }

        void OnEnable()
        {
            Locale.Changed += RefreshLocalized;
            if (analyticsState != null) RefreshLocalized();
        }

        void OnDisable() => Locale.Changed -= RefreshLocalized;

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
