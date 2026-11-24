using TMPro;
using UnityEngine;

namespace NusantaraAR.UI
{
    /// <summary>Label yang otomatis mengikuti bahasa aktif.</summary>
    public class LocalizedLabel : MonoBehaviour
    {
        public string key;
        TextMeshProUGUI text;

        public static TextMeshProUGUI Attach(TextMeshProUGUI t, string key)
        {
            var l = t.gameObject.AddComponent<LocalizedLabel>();
            l.key = key;
            l.text = t;
            l.Refresh();
            return t;
        }

        void OnEnable()
        {
            Locale.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Locale.Changed -= Refresh;

        void Refresh()
        {
            if (text == null) text = GetComponent<TextMeshProUGUI>();
            if (text != null && !string.IsNullOrEmpty(key)) text.text = Locale.T(key);
        }
    }
}
