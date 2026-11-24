using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Layar 6: bahasa, volume, ulangi tutorial. Kartu kaca di atas latar
    /// kamera (<see cref="Backdrop"/>); tab Pengaturan di <see cref="BottomNav"/> tetap tampil di bawahnya.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        Action onReplayTutorial;
        RectTransform content;
        Segmented language;
        Slider narrationSlider, musicSlider, sfxSlider;

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
            narrationSlider = VolumeRow(sound, "settings.narrationVol", AppSettings.NarrationVolume, v => AppSettings.NarrationVolume = v);
            musicSlider = VolumeRow(sound, "settings.musicVol", AppSettings.MusicVolume, v => AppSettings.MusicVolume = v);
            sfxSlider = VolumeRow(sound, "settings.sfxVol", AppSettings.SfxVolume, v => AppSettings.SfxVolume = v);

            var tutorial = UIKit.IconTextButton(content, "Tutorial", Icon.Book, Locale.T("settings.tutorial"), ButtonStyle.Secondary, () =>
            {
                Hide();
                onReplayTutorial?.Invoke();
            }, out var tl, 32f);
            LocalizedLabel.Attach(tl, "settings.tutorial");
            UIKit.Layout(tutorial, 100);

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

        static Slider VolumeRow(RectTransform card, string key, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var label = UIKit.Text(card, "Label", Locale.T(key), 28f, Theme.InkMuted, TextAlignmentOptions.BottomLeft);
            LocalizedLabel.Attach(label, key);
            UIKit.Layout(label, 40);
            var slider = UIKit.Slider(card, key, value, onChanged);
            UIKit.Layout(slider, 64);
            return slider;
        }

        void RefreshLocalized()
        {
            language.SetSelected(Locale.Current == Language.ID ? 0 : 1);
            if (narrationSlider != null) narrationSlider.SetValueWithoutNotify(AppSettings.NarrationVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(AppSettings.MusicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AppSettings.SfxVolume);
        }

        void OnEnable()
        {
            Locale.Changed += RefreshLocalized;
            if (language != null) RefreshLocalized();
        }

        void OnDisable()
        {
            Locale.Changed -= RefreshLocalized;
            PlayerPrefs.Save();
        }

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
