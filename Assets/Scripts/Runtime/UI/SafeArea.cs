using UnityEngine;

namespace NusantaraAR.UI
{
    /// <summary>Menyesuaikan rect ke Screen.safeArea (notch/punch-hole).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int screen;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            var safe = Screen.safeArea;
            applied = safe;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
