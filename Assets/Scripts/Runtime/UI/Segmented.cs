using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Kontrol bersegmen (tab kartu info, pilihan bahasa): segmen terpilih terakota, lainnya transparan.</summary>
    public class Segmented : MonoBehaviour
    {
        readonly List<(Image fill, TextMeshProUGUI label)> items = new List<(Image, TextMeshProUGUI)>();
        int selected;

        public event Action<int> Changed;
        public int Selected => selected;

        /// <param name="labels">Teks segmen, atau kunci <see cref="Locale"/> bila <paramref name="localized"/>.</param>
        public static Segmented Create(Transform parent, string name, string[] labels, bool localized, int selected,
            Action<int> onSelect, float fontSize = 26f)
        {
            var track = UIKit.Panel(parent, name, Theme.WithAlpha(Theme.Line, 0.7f), true, true, 40);
            var seg = track.gameObject.AddComponent<Segmented>();
            var row = UIKit.HRow(track.rectTransform, 6f, new RectOffset(6, 6, 6, 6));
            row.childForceExpandWidth = true;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var fill = UIKit.Panel(track.transform, "Segment" + i, Color.clear, true, true, 36);
                var label = UIKit.Text(fill.transform, "Label", localized ? Locale.T(labels[i]) : labels[i], fontSize, Theme.Ink,
                    TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.enableAutoSizing = true;
                label.fontSizeMin = fontSize * 0.7f;
                label.fontSizeMax = fontSize;
                UIKit.Stretch(label.rectTransform, 10, 10, 0, 0);
                if (localized) LocalizedLabel.Attach(label, labels[i]);
                var button = fill.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    AudioManager.Instance.Click();
                    seg.SetSelected(index);
                    seg.Changed?.Invoke(index);
                });
                seg.items.Add((fill, label));
            }
            if (onSelect != null) seg.Changed += onSelect;
            seg.SetSelected(selected);
            return seg;
        }

        public void SetSelected(int index)
        {
            selected = index;
            for (int i = 0; i < items.Count; i++)
            {
                bool on = i == index;
                items[i].fill.color = on ? Theme.Accent : new Color(1f, 1f, 1f, 0f);
                items[i].label.color = on ? Theme.OnAccent : Theme.Ink;
                items[i].label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
