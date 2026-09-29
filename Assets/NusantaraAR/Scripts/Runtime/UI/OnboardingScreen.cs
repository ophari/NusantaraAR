using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Onboarding (PRD FR-13): tutorial gestur + keselamatan AR. Bisa dilewati.
    /// Persetujuan analitik hanya di Pengaturan (default nonaktif).
    /// </summary>
    public class OnboardingScreen : MonoBehaviour
    {
        static readonly string[] Pages = { "onb.1", "onb.2", "onb.3" };
        static readonly Icon[] PageIcons = { Icon.Book, Icon.Spin360, Icon.Shield };

        int page;
        TextMeshProUGUI title, body, nextLabel;
        Image icon;
        Image[] dots;
        Action onDone;

        public static OnboardingScreen Create(RectTransform fullRoot, Action onDone)
        {
            var scrim = UIKit.Panel(fullRoot, "Onboarding", Theme.Scrim, false, true);
            UIKit.Stretch(scrim.rectTransform);
            var s = scrim.gameObject.AddComponent<OnboardingScreen>();
            s.onDone = onDone;
            s.Build(scrim.rectTransform);
            scrim.gameObject.SetActive(false);
            return s;
        }

        void Build(RectTransform root)
        {
            var card = UIKit.Surface(root, "Card", SurfaceStyle.Glass, 48);
            card.GetComponent<Surface>().fill.GetComponent<GlassSurface>().Strength = 0.82f;
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1120f));

            var badge = UIKit.Panel(card.transform, "Badge", Theme.WithAlpha(Theme.AccentSoft, 0.22f), false, false);
            badge.sprite = SpriteFactory.Circle();
            UIKit.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(176f, 176f));
            icon = UIKit.IconImage(badge.transform, Icon.Book, 92f, Theme.Accent);
            UIKit.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));

            title = UIKit.Text(card.transform, "Title", "", 56f, Theme.Ink, TextAlignmentOptions.Top, FontStyles.Bold);
            UIKit.Stretch(title.rectTransform, 56, 56, 272, 760);
            body = UIKit.Text(card.transform, "Body", "", Theme.Body, Theme.Ink);
            body.lineSpacing = 12f;
            UIKit.Stretch(body.rectTransform, 64, 64, 380, 220);

            var dotsRow = UIKit.Rect("Dots", card.transform);
            UIKit.Place(dotsRow, new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(120f, 18f));
            UIKit.HRow(dotsRow, 16f);
            dots = new Image[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                dots[i] = UIKit.Panel(dotsRow, "Dot", Theme.Line, false, false);
                dots[i].sprite = SpriteFactory.Circle();
            }

            var skip = UIKit.Button(card.transform, "Skip", Locale.T("onb.skip"), ButtonStyle.Ghost, Finish, out var sl);
            LocalizedLabel.Attach(sl, "onb.skip");
            UIKit.Place((RectTransform)skip.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(300f, 110f));
            var next = UIKit.Button(card.transform, "Next", "", ButtonStyle.Primary, Next, out nextLabel);
            UIKit.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(380f, 110f));

            Locale.Changed += Refresh;
        }

        void OnDestroy() => Locale.Changed -= Refresh;

        public void Show()
        {
            page = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        void Refresh()
        {
            title.text = Locale.T(Pages[page] + ".title");
            body.text = Locale.T(Pages[page] + ".body");
            icon.sprite = IconFactory.Get(PageIcons[page]);
            nextLabel.text = Locale.T(page == Pages.Length - 1 ? "onb.start" : "onb.next");
            for (int i = 0; i < dots.Length; i++) dots[i].color = i == page ? Theme.Accent : Theme.Line;
        }

        void Next()
        {
            if (page < Pages.Length - 1)
            {
                page++;
                Refresh();
            }
            else Finish();
        }

        void Finish()
        {
            AppSettings.OnboardingDone = true;
            gameObject.SetActive(false);
            onDone?.Invoke();
        }
    }
}
