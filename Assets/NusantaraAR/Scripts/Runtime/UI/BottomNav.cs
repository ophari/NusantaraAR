using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Bar navigasi bawah kaca nila (katalog & detail 3D): Koleksi · Scan QR · Pengaturan. Scan QR di tengah berupa
    /// lingkaran terakota yang menonjol (pintu masuk utama mode AR, PRD Layar 1). Latar bar diperpanjang ke bawah area
    /// aman agar menutupi inset gesture bar.
    /// </summary>
    public class BottomNav : MonoBehaviour
    {
        public enum Tab { Collection, Scan, Settings }

        public const float Height = 148f;
        /// <summary>Tinggi lingkaran Scan QR yang menonjol di atas bar; UI di atas nav harus memberi jarak ini.</summary>
        public const float Protrusion = 56f;

        readonly Dictionary<Tab, (Image icon, TextMeshProUGUI label, Image dot)> items =
            new Dictionary<Tab, (Image, TextMeshProUGUI, Image)>();

        public event Action<Tab> Tapped;

        public static BottomNav Create(RectTransform safeRoot)
        {
            var rt = UIKit.Rect("BottomNav", safeRoot);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, Height);
            var nav = rt.gameObject.AddComponent<BottomNav>();

            var bg = UIKit.Surface(rt, "Background", SurfaceStyle.NavGlass, 48);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, -220);

            var row = UIKit.Stretch(UIKit.Rect("Items", rt));
            UIKit.HRow(row, 0f);
            nav.AddItem(row, Tab.Collection, Icon.Grid, "nav.collection");
            nav.AddItem(row, Tab.Scan, Icon.ScanFrame, "nav.scan");
            nav.AddItem(row, Tab.Settings, Icon.Settings, "common.settings");
            return nav;
        }

        void AddItem(RectTransform row, Tab tab, Icon icon, string key)
        {
            var hit = UIKit.Panel(row, tab.ToString(), new Color(1f, 1f, 1f, 0f), false, true);
            hit.canvasRenderer.cullTransparentMesh = true;
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                AudioManager.Instance.Click();
                Tapped?.Invoke(tab);
            });

            var label = UIKit.Text(hit.transform, "Label", Locale.T(key), Theme.Caption, Theme.NavIcon, TextAlignmentOptions.Bottom);
            LocalizedLabel.Attach(label, key);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Stretch(label.rectTransform, 4, 4, 0, 26);

            Image iconImage;
            if (tab == Tab.Scan)
            {
                // Tombol tengah menonjol di atas bar.
                var circle = UIKit.Surface(hit.transform, "Circle", SurfaceStyle.Accent, 62); // bagian menonjol ikut bisa diketuk
                UIKit.Place(circle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, Protrusion - 4f), new Vector2(124f, 124f));
                iconImage = UIKit.IconImage(circle.transform, icon, 58f, Theme.OnAccent);
                label.color = Theme.NavIcon;
                items[tab] = (iconImage, label, null);
                return;
            }

            iconImage = UIKit.IconImage(hit.transform, icon, 52f, Theme.NavIcon);
            UIKit.Place(iconImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(52f, 52f));
            var dot = UIKit.Panel(hit.transform, "Dot", Theme.NavActive, false, false);
            dot.sprite = SpriteFactory.Circle();
            UIKit.Place(dot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(9f, 9f));
            items[tab] = (iconImage, label, dot);
        }

        public void SetActive(Tab tab)
        {
            foreach (var kv in items)
            {
                if (kv.Key == Tab.Scan) continue;
                bool on = kv.Key == tab;
                kv.Value.icon.color = on ? Theme.NavActive : Theme.NavIcon;
                kv.Value.label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                UIKit.SetVisible(kv.Value.dot, on);
            }
        }
    }
}
