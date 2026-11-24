using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Kartu panduan kamera (Scan QR & AR Meja): versi besar dengan ikon beranimasi ("Mendeteksi permukaan…",
    /// "Arahkan kamera ke kode QR") dan versi pil ringkas untuk petunjuk yang bisa pudar sendiri.
    /// </summary>
    public class CoachCard : MonoBehaviour
    {
        // Lebar pil dibatasi agar tidak menabrak rel kanan & klaster kiri HUD yang tampil bersamaan.
        const float BigWidth = 780f, PillHeight = 84f, MaxPillWidth = 740f;

        RectTransform rt, halo, iconRt;
        CanvasGroup group;
        Image iconImage;
        TextMeshProUGUI title, subtitle;

        Icon icon;
        bool big, visible;
        string shown;
        float hideAt, alpha;

        public static CoachCard Create(RectTransform safeRoot, float top)
        {
            var root = UIKit.Surface(safeRoot, "Coach", SurfaceStyle.Glass, 42, false);
            var c = root.gameObject.AddComponent<CoachCard>();
            c.rt = root.rectTransform;
            UIKit.Place(c.rt, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(BigWidth, 240f));
            c.group = root.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = false;

            var h = UIKit.Panel(c.rt, "Halo", Theme.WithAlpha(Theme.AccentSoft, 0.2f), false, false);
            h.sprite = SpriteFactory.Circle();
            c.halo = h.rectTransform;
            c.iconImage = UIKit.IconImage(c.rt, Icon.Compass, 56f, Theme.Accent);
            c.iconRt = c.iconImage.rectTransform;

            c.title = UIKit.Text(c.rt, "Title", "", 32f, Theme.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            c.title.textWrappingMode = TextWrappingModes.NoWrap;
            c.title.overflowMode = TextOverflowModes.Ellipsis;
            c.subtitle = UIKit.Text(c.rt, "Subtitle", "", 26f, Theme.InkMuted, TextAlignmentOptions.Top);
            c.subtitle.lineSpacing = 4f;
            root.gameObject.SetActive(false);
            return c;
        }

        /// <param name="autoHide">Detik sampai pudar sendiri (0 = tetap).</param>
        public void Show(Icon iconValue, string text, string sub = null, bool bigCard = false, float autoHide = 0f)
        {
            string key = iconValue + "|" + text + "|" + sub + "|" + bigCard;
            visible = true;
            gameObject.SetActive(true);
            if (key == shown) return; // isi sama: jangan ulang animasi / timer
            shown = key;
            icon = iconValue;
            big = bigCard;
            hideAt = autoHide > 0f ? Time.unscaledTime + autoHide : 0f;
            iconImage.sprite = IconFactory.Get(icon);
            title.text = text ?? string.Empty;
            subtitle.text = sub ?? string.Empty;
            LayoutCard(!string.IsNullOrEmpty(sub));
        }

        public void Hide()
        {
            visible = false;
            shown = null;
        }

        void LayoutCard(bool hasSub)
        {
            UIKit.SetVisible(halo, big);
            UIKit.SetVisible(subtitle, big && hasSub);
            if (big)
            {
                float h = hasSub ? 330f : 236f;
                rt.sizeDelta = new Vector2(BigWidth, h);
                UIKit.Place(halo, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(104f, 104f));
                UIKit.Place(iconRt, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(56f, 56f));
                title.alignment = TextAlignmentOptions.Center;
                title.enableAutoSizing = false;
                title.fontSize = 32f;
                UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(BigWidth - 60f, 52f));
                UIKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(BigWidth - 80f, 110f));
                subtitle.alignment = TextAlignmentOptions.Top;
            }
            else
            {
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.enableAutoSizing = true;
                title.fontSizeMin = 20f;
                title.fontSizeMax = 28f;
                title.fontSize = 28f;
                float textW = title.GetPreferredValues(title.text, 10000f, 0f).x;
                float w = Mathf.Clamp(textW + 30f + 40f + 16f + 36f, 360f, MaxPillWidth);
                rt.sizeDelta = new Vector2(w, PillHeight);
                UIKit.Place(iconRt, new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(40f, 40f));
                UIKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(w - 86f - 30f, PillHeight));
            }
        }

        void Update()
        {
            if (visible && hideAt > 0f && Time.unscaledTime >= hideAt) visible = false;
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            group.alpha = alpha;
            if (alpha <= 0f && !visible)
            {
                gameObject.SetActive(false);
                return;
            }

            // Animasi ikon: kompas bergoyang mencari arah, bingkai scan & target berdenyut.
            float time = Time.unscaledTime;
            iconRt.localRotation = icon == Icon.Compass ? Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.2f) * 28f) : Quaternion.identity;
            float pulse = icon == Icon.ScanFrame || icon == Icon.Target ? 1f + Mathf.Sin(time * 3.2f) * 0.08f : 1f;
            iconRt.localScale = Vector3.one * pulse;
            if (big) halo.localScale = Vector3.one * (1f + Mathf.Repeat(time * 0.8f, 1f) * 0.18f);
        }
    }
}
