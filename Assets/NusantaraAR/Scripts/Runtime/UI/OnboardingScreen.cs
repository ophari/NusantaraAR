using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Onboarding (PRD FR-13): tutorial gestur + keselamatan AR + persetujuan analitik. Bisa dilewati.</summary>
    public class OnboardingScreen : MonoBehaviour
    {
        static readonly string[] Pages = { "onb.1", "onb.2", "onb.3" };

        int page;
        TextMeshProUGUI title, body, nextLabel, consentLabel;
        Button consent;
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
            var card = UIKit.Panel(root, "Card", Theme.TextPanel, true, true, 40);
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960f, 1080f));

            title = UIKit.Text(card.transform, "Title", "", Theme.Title, Theme.Gold, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            UIKit.Stretch(title.rectTransform, 56, 56, 64, 900);
            body = UIKit.Text(card.transform, "Body", "", Theme.Body, Theme.Parchment);
            body.lineSpacing = 12f;
            UIKit.Stretch(body.rectTransform, 56, 56, 200, 360);

            consent = UIKit.Button(card.transform, "Consent", "", ButtonStyle.Chip, ToggleConsent, out consentLabel, 30f);
            consentLabel.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Place((RectTransform)consent.transform, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(848f, 110f));

            var dotsRow = UIKit.Rect("Dots", card.transform);
            UIKit.Place(dotsRow, new Vector2(0.5f, 0f), new Vector2(0f, 176f), new Vector2(120f, 20f));
            UIKit.HRow(dotsRow, 16f);
            dots = new Image[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
            {
                dots[i] = UIKit.Panel(dotsRow, "Dot", Theme.Stone, false, false);
                dots[i].sprite = SpriteFactory.Circle();
            }

            var skip = UIKit.Button(card.transform, "Skip", Locale.T("onb.skip"), ButtonStyle.Ghost, Finish, out var sl);
            LocalizedLabel.Attach(sl, "onb.skip");
            sl.color = Theme.Stone;
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
            nextLabel.text = Locale.T(page == Pages.Length - 1 ? "onb.start" : "onb.next");
            for (int i = 0; i < dots.Length; i++) dots[i].color = i == page ? Theme.Gold : Theme.Stone;
            bool last = page == Pages.Length - 1;
            consent.gameObject.SetActive(last);
            consentLabel.text = (AppSettings.AnalyticsConsent ? "[x]  " : "[  ]  ") + Locale.T("settings.analytics");
        }

        void ToggleConsent()
        {
            AppSettings.AnalyticsConsent = !AppSettings.AnalyticsConsent;
            Refresh();
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
