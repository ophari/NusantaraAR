using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>Saklar geser (track + knob) bergaya iOS; aktif = terakota.</summary>
    public class SwitchToggle : MonoBehaviour, IPointerClickHandler
    {
        const float Width = 104f, Height = 60f, Knob = 48f;

        Image track;
        RectTransform knob;
        CanvasGroup group;
        bool isOn, interactable = true;
        float t;

        public event Action<bool> Changed;

        public bool IsOn
        {
            get => isOn;
            set => isOn = value;
        }

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                group.alpha = value ? 1f : 0.45f;
            }
        }

        public static SwitchToggle Create(Transform parent, string name, bool value, Action<bool> onChanged)
        {
            var root = UIKit.Rect(name, parent);
            root.sizeDelta = new Vector2(Width, Height);
            var s = root.gameObject.AddComponent<SwitchToggle>();
            s.group = root.gameObject.AddComponent<CanvasGroup>();
            s.track = UIKit.Panel(root, "Track", Theme.Line, true, true, 30);
            UIKit.Stretch(s.track.rectTransform);
            var k = UIKit.Surface(root, "Knob", SurfaceStyle.Solid, 24, false);
            s.knob = k.rectTransform;
            s.knob.anchorMin = s.knob.anchorMax = s.knob.pivot = new Vector2(0.5f, 0.5f);
            s.knob.sizeDelta = new Vector2(Knob, Knob);
            s.isOn = value;
            s.t = value ? 1f : 0f;
            s.Apply();
            if (onChanged != null) s.Changed += onChanged;
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Width;
            le.preferredHeight = Height;
            return s;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            isOn = !isOn;
            AudioManager.Instance.Click();
            Changed?.Invoke(isOn);
        }

        void Update()
        {
            float target = isOn ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        void Apply()
        {
            float e = t * t * (3f - 2f * t);
            float travel = (Width - Knob) * 0.5f - 6f;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, e), 0f);
            track.color = Color.Lerp(Theme.Line, Theme.Accent, e);
        }
    }
}
