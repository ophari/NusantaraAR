using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Isi <see cref="ControlPanel"/> per mode kamera (Scan QR / AR Meja).</summary>
    public class ControlPanelConfig
    {
        public Func<float> getScale;
        public Action<float> setScale;
        public string toggleKey;
        public Func<bool> getToggle;
        public Action<bool> setToggle;
        public Func<bool> toggleEnabled;
        /// <summary>Tombol lebar opsional (mis. "Pindahkan" di AR Meja). Null = tanpa tombol.</summary>
        public string buttonKey;
        public Icon buttonIcon = Icon.Move;
        public Action onButton;
    }

    /// <summary>
    /// Panel kaca bawah di mode kamera: nama artefak + nilai skala, slider Skala 0,5–3× (logaritmik, mengikuti cubit),
    /// saklar (Kunci Posisi / Tampilkan Bidang), dan tombol lebar opsional.
    /// </summary>
    public class ControlPanel : MonoBehaviour
    {
        public const float Width = 1020f;

        ControlPanelConfig config;
        RectTransform rt;
        TextMeshProUGUI title, value;
        Slider slider;
        SwitchToggle toggle;

        public float Height => rt.sizeDelta.y;

        /// <summary>Posisi slider 0..1 → skala (logaritmik agar 1× berada di tengah-kiri, bukan di ujung).</summary>
        public static float ScaleFromSlider(float v) =>
            ArtifactInstance.MinScale * Mathf.Pow(ArtifactInstance.MaxScale / ArtifactInstance.MinScale, Mathf.Clamp01(v));

        public static float SliderFromScale(float scale) =>
            Mathf.Clamp01(Mathf.Log(Mathf.Max(1e-4f, scale) / ArtifactInstance.MinScale)
                          / Mathf.Log(ArtifactInstance.MaxScale / ArtifactInstance.MinScale));

        public static ControlPanel Create(RectTransform safeRoot, ControlPanelConfig config)
        {
            var root = UIKit.Surface(safeRoot, "ControlPanel", SurfaceStyle.Glass, 44);
            var panel = root.gameObject.AddComponent<ControlPanel>();
            panel.config = config;
            panel.rt = root.rectTransform;
            panel.Build();
            return panel;
        }

        void Build()
        {
            bool hasButton = config.onButton != null;
            float height = 24f + 52f + 8f + 76f + 8f + 72f + (hasButton ? 8f + 92f : 0f) + 28f;
            UIKit.Place(rt, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(Width, height));
            UIKit.VColumn(rt, 8f, new RectOffset(40, 40, 24, 28));

            var header = UIKit.Rect("Header", rt);
            UIKit.Layout(header, 52f);
            UIKit.HRow(header, 12f, null, false);
            title = UIKit.Text(header, "Title", "", 30f, Theme.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Layout(title, -1, -1, 1f);
            value = UIKit.Text(header, "Value", "", 28f, Theme.InkMuted, TextAlignmentOptions.MidlineRight);
            UIKit.Layout(value, -1, 150f);

            var scaleRow = UIKit.Rect("Scale", rt);
            UIKit.Layout(scaleRow, 76f);
            UIKit.HRow(scaleRow, 20f, null, false);
            var scaleLabel = UIKit.Text(scaleRow, "Label", Locale.T("ctrl.scale"), 28f, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Attach(scaleLabel, "ctrl.scale");
            UIKit.Layout(scaleLabel, -1, 150f);
            slider = UIKit.Slider(scaleRow, "Slider", SliderFromScale(config.getScale()), v => config.setScale?.Invoke(ScaleFromSlider(v)));
            UIKit.Layout(slider, 76f, -1, 1f);

            var toggleRow = UIKit.Rect("Toggle", rt);
            UIKit.Layout(toggleRow, 72f);
            var tr = UIKit.HRow(toggleRow, 20f, null, false);
            tr.childForceExpandHeight = false;
            var toggleLabel = UIKit.Text(toggleRow, "Label", Locale.T(config.toggleKey), 28f, Theme.Ink, TextAlignmentOptions.MidlineLeft);
            LocalizedLabel.Attach(toggleLabel, config.toggleKey);
            UIKit.Layout(toggleLabel, 72f, -1, 1f);
            toggle = SwitchToggle.Create(toggleRow, "Switch", config.getToggle(), on => config.setToggle?.Invoke(on));

            if (config.onButton != null)
            {
                var b = UIKit.IconTextButton(rt, "Action", config.buttonIcon, Locale.T(config.buttonKey), ButtonStyle.Chip,
                    () => config.onButton(), out var bl, 30f);
                LocalizedLabel.Attach(bl, config.buttonKey);
                UIKit.Layout(b, 92f);
            }
        }

        public void SetTitle(string text) => title.text = text ?? string.Empty;

        void Update()
        {
            float scale = config.getScale();
            float v = SliderFromScale(scale);
            if (Mathf.Abs(slider.value - v) > 0.002f) slider.SetValueWithoutNotify(v); // cubit mengubah skala
            string s = scale.ToString("0.0", CultureInfo.InvariantCulture);
            if (Locale.Current == Language.ID) s = s.Replace('.', ',');
            value.text = s + "×";

            toggle.IsOn = config.getToggle();
            if (config.toggleEnabled != null) toggle.Interactable = config.toggleEnabled();
        }
    }
}
