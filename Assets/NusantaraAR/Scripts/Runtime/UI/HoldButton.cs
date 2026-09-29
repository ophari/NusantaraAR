using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Tombol tahan-tekan (putar/miringkan objek dengan tombol, alternatif gestur): selama ditekan memanggil
    /// <see cref="Held"/> tiap frame dengan waktu terskala (awal lebih pelan agar ketukan singkat = langkah kecil).
    /// Sentuhan di atas UI tidak diteruskan ke gestur (<see cref="TouchGestures.IsOverUI"/>).
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float RampSeconds = 0.25f;

        public event Action<float> Held;
        public event Action Pressed;

        Graphic target;
        bool pressed;
        float heldTime;

        public static HoldButton Attach(Graphic hitArea, Graphic tintTarget)
        {
            var h = hitArea.gameObject.AddComponent<HoldButton>();
            h.target = tintTarget;
            return h;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            heldTime = 0f;
            AudioManager.Instance.Click();
            Pressed?.Invoke();
            SetLook(true);
        }

        public void OnPointerUp(PointerEventData e) => Release();

        public void OnPointerExit(PointerEventData e) => Release();

        void OnDisable() => Release();

        void Release()
        {
            if (!pressed) return;
            pressed = false;
            SetLook(false);
        }

        void SetLook(bool down)
        {
            if (target != null) target.CrossFadeColor(down ? new Color(0.82f, 0.8f, 0.78f, 1f) : Color.white, 0.08f, true, true);
            transform.localScale = Vector3.one * (down ? 0.94f : 1f);
        }

        void Update()
        {
            if (!pressed) return;
            float dt = Time.unscaledDeltaTime;
            heldTime += dt;
            Held?.Invoke(dt * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(heldTime / RampSeconds)));
        }
    }
}
