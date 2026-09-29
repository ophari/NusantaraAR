using NusantaraAR.Marker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Menampilkan kode QR artefak layar penuh (latar putih) agar bisa dipindai dari HP lain atau difoto untuk dicetak.</summary>
    public class MarkerCardScreen : MonoBehaviour
    {
        RawImage image;
        TextMeshProUGUI title;
        string shownText;

        public static MarkerCardScreen Create(RectTransform fullRoot)
        {
            var bg = UIKit.Panel(fullRoot, "MarkerCard", Color.white, false, true);
            UIKit.Stretch(bg.rectTransform);
            var area = UIKit.Rect("Safe", bg.transform);
            area.gameObject.AddComponent<SafeArea>();
            var s = bg.gameObject.AddComponent<MarkerCardScreen>();
            s.Build(area);
            bg.gameObject.SetActive(false);
            return s;
        }

        void Build(RectTransform area)
        {
            title = UIKit.Text(area, "Title", "", Theme.Title, Theme.Ink, TextAlignmentOptions.Top, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1000f, 160f));

            image = UIKit.Rect("Marker", area).gameObject.AddComponent<RawImage>();
            UIKit.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(860f, 860f));
            image.raycastTarget = false;

            var hint = UIKit.Text(area, "Hint", Locale.T("marker.cardHint"), Theme.Small, Theme.InkMuted, TextAlignmentOptions.Top);
            LocalizedLabel.Attach(hint, "marker.cardHint");
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(960f, 200f));

            var close = UIKit.Button(area, "Close", Locale.T("common.close"), ButtonStyle.Primary, Hide, out var cl);
            LocalizedLabel.Attach(cl, "common.close");
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(400f, 104f));
        }

        public void Show(ArtifactData data)
        {
            if (data == null || string.IsNullOrEmpty(data.artifactId)) return;
            var text = data.QrText;
            if (shownText != text)
            {
                var modules = QrCode.Encode(text);
                if (modules == null) return;
                if (image.texture != null) Destroy(image.texture);
                image.texture = QrCode.CreateTexture(modules, 16, 4);
                shownText = text;
            }
            title.text = Locale.T("marker.cardTitle") + "\n<size=40><color=" + Theme.HexOf(Theme.AccentText) + ">" + data.displayName.Get() + "</color></size>";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Screen.brightness = 1f;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
