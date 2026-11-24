using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace NusantaraAR
{
    /// <summary>
    /// Penerjemah sentuhan menjadi gestur sesuai aturan PRD §4.3:
    /// sentuhan di atas UI diabaikan; tap = sentuhan singkat tanpa gerak; drag 1 jari = rotasi;
    /// 2 jari = pinch ATAU geser, diputuskan dalam jendela singkat lalu dikunci sampai jari diangkat.
    /// Di Editor, mouse disimulasikan sebagai sentuhan dan scroll wheel sebagai pinch.
    /// </summary>
    public class TouchGestures : MonoBehaviour
    {
        enum TwoFingerMode { Undecided, Pinch, Pan }

        [Header("Tap")]
        public float tapMaxSeconds = 0.25f;
        public float tapMaxMoveDp = 10f;

        [Header("Dua jari")]
        public float twoFingerDecisionSeconds = 0.15f;
        public float twoFingerMinMoveDp = 4f;

        /// <summary>Tap di luar UI (posisi layar).</summary>
        public event Action<Vector2> Tapped;
        /// <summary>Geser 1 jari (delta piksel per frame).</summary>
        public event Action<Vector2> Dragged;
        /// <summary>Pinch: rasio jarak jari frame ini terhadap frame sebelumnya.</summary>
        public event Action<float> Pinched;
        /// <summary>Geser 2 jari: posisi titik tengah jari (layar).</summary>
        public event Action<Vector2> TwoFingerPanned;
        public event Action TwoFingerPanEnded;
        /// <summary>Pengguna mulai berinteraksi dengan objek (dipakai untuk menghentikan Putar Otomatis).</summary>
        public event Action InteractionStarted;

        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        // satu jari
        bool tracking;
        bool trackingOverUi;
        bool moved;
        float startTime;
        Vector2 startPos;

        // dua jari
        bool twoActive;
        bool twoBlocked;
        TwoFingerMode mode;
        float twoStartTime;
        float lastDistance;
        Vector2 lastCenter;
        float accumDistance;
        float accumCenter;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable();
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            TouchSimulation.Disable();
#endif
            EnhancedTouchSupport.Disable();
        }

        public static float DpToPixels(float dp)
        {
            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            return dp * dpi / 160f;
        }

        /// <summary>Apakah posisi layar berada di atas elemen UI yang menerima raycast.</summary>
        public static bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPosition };
            UiHits.Clear();
            es.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
        }

        void Update()
        {
            var touches = ETouch.activeTouches;

            if (touches.Count >= 2)
            {
                HandleTwoFingers(touches[0].screenPosition, touches[1].screenPosition);
                return;
            }

            if (twoActive)
            {
                if (mode == TwoFingerMode.Pan) TwoFingerPanEnded?.Invoke();
                twoActive = false;
                twoBlocked = false;
                tracking = false; // jari tersisa tidak memicu rotasi sampai diangkat
            }

            if (touches.Count == 1) HandleOneFinger(touches[0]);
            else tracking = false;

            HandleScrollWheel();
        }

        void HandleOneFinger(ETouch t)
        {
            switch (t.phase)
            {
                case ETouchPhase.Began:
                    tracking = true;
                    moved = false;
                    startTime = Time.unscaledTime;
                    startPos = t.screenPosition;
                    trackingOverUi = IsOverUI(startPos);
                    if (!trackingOverUi) InteractionStarted?.Invoke();
                    break;

                case ETouchPhase.Moved:
                case ETouchPhase.Stationary:
                    if (!tracking || trackingOverUi) break;
                    if (!moved && (t.screenPosition - startPos).magnitude > DpToPixels(tapMaxMoveDp)) moved = true;
                    if (moved && t.delta.sqrMagnitude > 0f) Dragged?.Invoke(t.delta);
                    break;

                case ETouchPhase.Ended:
                    if (tracking && !trackingOverUi && !moved && Time.unscaledTime - startTime <= tapMaxSeconds)
                        Tapped?.Invoke(t.screenPosition);
                    tracking = false;
                    break;

                case ETouchPhase.Canceled:
                    tracking = false;
                    break;
            }
        }

        void HandleTwoFingers(Vector2 a, Vector2 b)
        {
            float distance = Vector2.Distance(a, b);
            Vector2 center = (a + b) * 0.5f;

            if (!twoActive)
            {
                twoActive = true;
                tracking = false;
                twoBlocked = IsOverUI(a) || IsOverUI(b);
                mode = TwoFingerMode.Undecided;
                twoStartTime = Time.unscaledTime;
                lastDistance = distance;
                lastCenter = center;
                accumDistance = accumCenter = 0f;
                if (!twoBlocked) InteractionStarted?.Invoke();
                return;
            }
            if (twoBlocked) return;

            switch (mode)
            {
                case TwoFingerMode.Undecided:
                    accumDistance += Mathf.Abs(distance - lastDistance);
                    accumCenter += (center - lastCenter).magnitude;
                    if (Time.unscaledTime - twoStartTime >= twoFingerDecisionSeconds &&
                        accumDistance + accumCenter >= DpToPixels(twoFingerMinMoveDp))
                        mode = accumDistance >= accumCenter ? TwoFingerMode.Pinch : TwoFingerMode.Pan;
                    break;
                case TwoFingerMode.Pinch:
                    if (lastDistance > 1f) Pinched?.Invoke(distance / lastDistance);
                    break;
                case TwoFingerMode.Pan:
                    TwoFingerPanned?.Invoke(center);
                    break;
            }

            lastDistance = distance;
            lastCenter = center;
        }

        void HandleScrollWheel()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f || IsOverUI(mouse.position.ReadValue())) return;
            InteractionStarted?.Invoke();
            Pinched?.Invoke(1f + Mathf.Clamp(scroll, -240f, 240f) * 0.001f);
#endif
        }
    }
}
