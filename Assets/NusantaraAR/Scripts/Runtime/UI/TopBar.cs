using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Bar atas: tombol kembali (lingkaran kaca) + judul di tengah. Di atas kamera judul diberi pil kaca agar tetap
    /// terbaca di latar apa pun.
    /// </summary>
    public class TopBar : MonoBehaviour
    {
        public const float Height = 112f;
        const float MaxTitleWidth = 720f;

        TextMeshProUGUI title;
        RectTransform pill;

        public Button Back { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        public static TopBar Create(RectTransform safeRoot, UnityAction onBack, bool titlePill)
        {
            var rt = UIKit.Rect("TopBar", safeRoot);
            UIKit.Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1032f, Height));
            var bar = rt.gameObject.AddComponent<TopBar>();

            if (titlePill)
            {
                var p = UIKit.Surface(rt, "TitlePill", SurfaceStyle.Glass, 44, false);
                bar.pill = p.rectTransform;
                UIKit.Place(bar.pill, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 88f));
            }
            bar.title = UIKit.Text(bar.pill != null ? bar.pill : rt, "Title", "", 36f, Theme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            bar.title.textWrappingMode = TextWrappingModes.NoWrap;
            bar.title.overflowMode = TextOverflowModes.Ellipsis;
            if (bar.pill != null) UIKit.Stretch(bar.title.rectTransform, 36, 36, 0, 0);
            else UIKit.Place(bar.title.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MaxTitleWidth, Height));

            if (onBack != null)
            {
                bar.Back = UIKit.IconButton(rt, "Back", Icon.Back, onBack, 96f);
                UIKit.Place((RectTransform)bar.Back.transform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(96f, 96f));
            }
            return bar;
        }

        public void SetTitle(string text)
        {
            title.text = text ?? string.Empty;
            if (pill == null) return;
            bool has = !string.IsNullOrEmpty(text);
            UIKit.SetVisible(pill, has);
            if (has)
            {
                float w = title.GetPreferredValues(title.text, 10000f, 0f).x + 72f;
                pill.sizeDelta = new Vector2(Mathf.Clamp(w, 200f, MaxTitleWidth), pill.sizeDelta.y);
            }
        }
    }
}
