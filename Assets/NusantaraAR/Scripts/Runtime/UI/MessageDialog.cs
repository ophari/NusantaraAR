using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Dialog kaca di atas scrim untuk mode kamera (izin kamera, AR tidak tersedia, kamera tidak ditemukan, ...):
    /// judul, isi, tombol utama opsional, dan tombol sekunder tetap (kembali / buka 3D Viewer).
    /// </summary>
    public class MessageDialog : MonoBehaviour
    {
        TextMeshProUGUI title, body, primaryLabel;
        Button primary, secondary;

        public static MessageDialog Create(RectTransform fullRoot, string secondaryKey, UnityAction onSecondary)
        {
            var scrim = UIKit.Panel(fullRoot, "Message", Theme.Scrim, false, true);
            UIKit.Stretch(scrim.rectTransform);
            var d = scrim.gameObject.AddComponent<MessageDialog>();

            var card = UIKit.Surface(scrim.transform, "Card", SurfaceStyle.Glass, 44);
            card.GetComponent<Surface>().fill.GetComponent<GlassSurface>().Strength = 0.88f;
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 760f));
            d.title = UIKit.Text(card.transform, "Title", "", Theme.Heading, Theme.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Stretch(d.title.rectTransform, 56, 56, 56, 620);
            d.body = UIKit.Text(card.transform, "Body", "", Theme.Body, Theme.Ink);
            d.body.lineSpacing = 8f;
            UIKit.Stretch(d.body.rectTransform, 56, 56, 146, 290);
            d.primary = UIKit.Button(card.transform, "Primary", "", ButtonStyle.Primary, null, out d.primaryLabel);
            UIKit.Place((RectTransform)d.primary.transform, new Vector2(0.5f, 0f), new Vector2(0f, 156f), new Vector2(620f, 104f));
            d.secondary = UIKit.Button(card.transform, "Secondary", Locale.T(secondaryKey), ButtonStyle.Chip, onSecondary, out var sl);
            LocalizedLabel.Attach(sl, secondaryKey);
            UIKit.Place((RectTransform)d.secondary.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(620f, 100f));
            scrim.gameObject.SetActive(false);
            return d;
        }

        /// <param name="action">Aksi tombol utama; null = tombol utama disembunyikan.</param>
        public void Show(string titleText, string bodyText, string primaryText, UnityAction action, bool showSecondary = true)
        {
            title.text = titleText;
            body.text = bodyText;
            UIKit.SetVisible(primary, action != null);
            UIKit.SetVisible(secondary, showSecondary);
            primary.onClick.RemoveAllListeners();
            primary.onClick.AddListener(() => AudioManager.Instance.Click());
            if (action != null) primary.onClick.AddListener(action);
            primaryLabel.text = primaryText ?? string.Empty;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
