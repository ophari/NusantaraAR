using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Kartu info yang mengembang di samping bagian artefak (menggantikan bottom sheet, PRD FR-09/10):
    /// judul + istilah daerah + bahan, tab Teknik Kriya | Makna Filosofis | Sejarah Asal, narasi & pelafalan,
    /// sumber rujukan, dan navigasi sebelum/berikutnya antar bagian. Posisinya diatur <see cref="HotspotOverlay"/>.
    /// </summary>
    public class HotspotCard : MonoBehaviour
    {
        public const float Width = 640f;
        const float Pad = 30f;
        const float MaxBodyHeight = 330f;

        static readonly string[] TabKeys = { "sheet.craft", "sheet.philosophy", "sheet.history" };

        RectTransform rt;
        TextMeshProUGUI title, subtitle, meta, draft, body, counter, listenLabel;
        ScrollRect bodyScroll;
        LayoutElement bodyLayout;
        Button listenButton, pronounceButton, prevButton, nextButton;
        readonly List<(Image bg, TextMeshProUGUI label)> tabs = new List<(Image, TextMeshProUGUI)>();

        HotspotData hotspot;
        int tabIndex;
        float pop;

        public RectTransform Rect => rt;
        public bool IsOpen => hotspot != null;
        public HotspotData Current => hotspot;
        public event Action Closed;
        public event Action<int> Stepped;

        public static HotspotCard Create(RectTransform parent)
        {
            // Bingkai emas tipis: panel emas + isi gelap di dalamnya (Outline membuat warna panel keruh).
            var bg = UIKit.Panel(parent, "InfoCard", Theme.Gold, true, true, 36);
            var fill = UIKit.Panel(bg.transform, "Fill", Theme.Teak, true, false, 34);
            UIKit.Stretch(fill.rectTransform, 3, 3, 3, 3);
            fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var card = bg.gameObject.AddComponent<HotspotCard>();
            card.rt = bg.rectTransform;
            card.Build();
            bg.gameObject.SetActive(false);
            return card;
        }

        void Build()
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Width, 400f);
            UIKit.VColumn(rt, 10f, new RectOffset((int)Pad, (int)Pad, 22, 24));
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UIKit.Rect("Header", rt);
            UIKit.HRow(header, 12f, null, false).childAlignment = TextAnchor.UpperLeft;
            title = UIKit.Text(header, "Title", "", 38f, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Layout(title, -1, -1, 1f);
            var close = UIKit.Button(header, "Close", "X", ButtonStyle.Chip, Close, out var closeLabel, 28f);
            closeLabel.fontStyle = FontStyles.Bold;
            UIKit.Layout(close, 72f, 72f);

            subtitle = UIKit.Text(rt, "Subtitle", "", 26f, Theme.Parchment, TextAlignmentOptions.TopLeft, FontStyles.Italic);
            meta = UIKit.Text(rt, "Meta", "", 26f, Theme.Stone);
            draft = UIKit.Text(rt, "Draft", "", 24f, Theme.Gold);
            LocalizedLabel.Attach(draft, "sheet.draft");

            var tabsRow = UIKit.Rect("Tabs", rt);
            UIKit.HRow(tabsRow, 10f);
            UIKit.Layout(tabsRow, 64f);
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int index = i;
                var b = UIKit.Button(tabsRow, "Tab" + i, Locale.T(TabKeys[i]), ButtonStyle.Chip, () => SelectTab(index), out var l, 25f);
                LocalizedLabel.Attach(l, TabKeys[i]);
                tabs.Add((b.GetComponent<Image>(), l));
            }

            bodyScroll = UIKit.VerticalScroll(rt, "Body", out var content, 10f, new RectOffset(0, 0, 4, 4));
            bodyLayout = UIKit.Layout(bodyScroll, 200f);
            body = UIKit.Text(content, "Text", "", 30f, Theme.Parchment);
            body.lineSpacing = 6f;
            body.richText = true;

            var footer = UIKit.Rect("Footer", rt);
            UIKit.HRow(footer, 10f, null, false);
            UIKit.Layout(footer, 72f);
            prevButton = UIKit.Button(footer, "Prev", "<", ButtonStyle.Chip, () => Stepped?.Invoke(-1), out var prevLabel, 32f);
            prevLabel.richText = false;
            UIKit.Layout(prevButton, -1, 84f);
            counter = UIKit.Text(footer, "Counter", "", 26f, Theme.Stone, TextAlignmentOptions.Center);
            UIKit.Layout(counter, -1, -1, 1f);
            listenButton = UIKit.Button(footer, "Listen", Locale.T("sheet.play"), ButtonStyle.Primary, ToggleNarration, out listenLabel, 26f);
            UIKit.Layout(listenButton, -1, 150f);
            pronounceButton = UIKit.Button(footer, "Pronounce", Locale.T("sheet.pronounce"), ButtonStyle.Chip, PlayPronunciation, out var pl, 24f);
            LocalizedLabel.Attach(pl, "sheet.pronounce");
            UIKit.Layout(pronounceButton, -1, 170f);
            nextButton = UIKit.Button(footer, "Next", ">", ButtonStyle.Chip, () => Stepped?.Invoke(1), out var nextLabel, 32f);
            nextLabel.richText = false;
            UIKit.Layout(nextButton, -1, 84f);

            Locale.Changed += Refresh;
        }

        void OnDestroy() => Locale.Changed -= Refresh;

        // ------------------------------------------------------------------ API

        public void Show(ArtifactData data, HotspotData h, int index, int count)
        {
            bool changed = hotspot != h;
            hotspot = h;
            if (changed)
            {
                tabIndex = 0;
                if (AudioManager.Instance.CurrentNarration != h.Narration) AudioManager.Instance.StopNarration();
                pop = 0f;
                Analytics.Log("hotspot_open", ("artifact", data != null ? data.artifactId : ""), ("hotspot", h.hotspotId));
            }
            counter.text = index >= 0 && count > 0 ? (index + 1) + " / " + count : string.Empty;
            prevButton.interactable = nextButton.interactable = count > 1;
            Refresh();
        }

        public void Close()
        {
            if (hotspot == null) return;
            hotspot = null;
            AudioManager.Instance.StopNarration();
            UIKit.SetVisible(rt, false);
            Closed?.Invoke();
        }

        /// <summary>Ditampilkan/disembunyikan sementara (bagian keluar layar atau sedang animasi bongkar) tanpa menutup.</summary>
        public void SetShown(bool shown)
        {
            if (shown && !rt.gameObject.activeSelf) pop = 0f;
            UIKit.SetVisible(rt, shown && hotspot != null);
        }

        // ------------------------------------------------------------------ isi

        void Refresh()
        {
            if (hotspot == null) return;
            var h = hotspot;
            title.text = h.title.Get();
            SetOptional(subtitle, h.regionalTerm);
            SetOptional(meta, h.material.IsEmpty ? null : Locale.T("sheet.material") + ": " + h.material.Get());
            UIKit.SetVisible(draft, !h.curatorValidated);

            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == tabIndex;
                tabs[i].bg.color = on ? Theme.Gold : Theme.WithAlpha(Theme.Border, 0.95f);
                tabs[i].label.color = on ? Theme.Teak : Theme.Parchment;
                tabs[i].label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }

            var text = new StringBuilder(Tab(h, tabIndex).Get());
            if (h.sources != null && h.sources.Count > 0)
            {
                text.Append("\n\n<size=24><color=#A8A29E><b>").Append(Locale.T("sheet.sources")).Append("</b>");
                foreach (var s in h.sources) text.Append("\n- ").Append(s);
                text.Append("</color></size>");
            }
            body.text = text.ToString();
            float bodyHeight = body.GetPreferredValues(body.text, Width - Pad * 2f, 0f).y + 12f;
            bodyLayout.preferredHeight = Mathf.Min(bodyHeight, MaxBodyHeight);
            bodyScroll.verticalNormalizedPosition = 1f;

            UIKit.SetVisible(listenButton, h.Narration != null);
            UIKit.SetVisible(pronounceButton, h.pronunciation != null);
        }

        static LocalizedString Tab(HotspotData h, int i) => i == 0 ? h.craft : i == 1 ? h.philosophy : h.history;

        static void SetOptional(TextMeshProUGUI t, string value)
        {
            bool has = !string.IsNullOrEmpty(value);
            UIKit.SetVisible(t, has);
            if (has) t.text = value;
        }

        void SelectTab(int i)
        {
            tabIndex = i;
            Refresh();
        }

        void ToggleNarration()
        {
            var clip = hotspot?.Narration;
            if (clip == null) return;
            var am = AudioManager.Instance;
            if (am.IsNarrationPlaying && am.CurrentNarration == clip) am.PauseNarration();
            else am.PlayNarration(clip);
        }

        void PlayPronunciation()
        {
            if (hotspot?.pronunciation != null) AudioManager.Instance.PlaySfx(hotspot.pronunciation);
        }

        void Update()
        {
            // Animasi "muncul": sedikit membesar dari 0,85 dengan easing.
            pop = Mathf.MoveTowards(pop, 1f, Time.unscaledDeltaTime * 5f);
            float s = Mathf.Lerp(0.85f, 1f, 1f - (1f - pop) * (1f - pop));
            rt.localScale = new Vector3(s, s, 1f);

            if (hotspot != null && hotspot.Narration != null)
            {
                var am = AudioManager.Instance;
                bool mine = am.CurrentNarration == hotspot.Narration && am.IsNarrationPlaying;
                listenLabel.text = Locale.T(mine ? "sheet.pause" : "sheet.play");
            }
        }
    }
}
