using System;
using UnityEngine;

namespace NusantaraAR
{
    public enum Language { ID, EN }

    /// <summary>Teks dua bahasa. Bahasa Inggris jatuh ke Bahasa Indonesia bila kosong.</summary>
    [Serializable]
    public struct LocalizedString
    {
        [TextArea(1, 8)] public string id;
        [TextArea(1, 8)] public string en;

        public LocalizedString(string id, string en)
        {
            this.id = id;
            this.en = en;
        }

        public bool IsEmpty => string.IsNullOrEmpty(id) && string.IsNullOrEmpty(en);

        public string Get() => Locale.Current == Language.EN && !string.IsNullOrEmpty(en) ? en : id ?? string.Empty;

        public override string ToString() => Get();
    }

    /// <summary>
    /// Bahasa aktif (ID/EN), disimpan di PlayerPrefs. Implementasi ringan untuk MVP;
    /// dapat dimigrasikan ke Unity Localization package bila konten bertambah.
    /// </summary>
    public static class Locale
    {
        const string PrefKey = "nusantaraar.lang";
        static bool loaded;
        static Language current;

        public static event Action Changed;

        public static Language Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
            set
            {
                EnsureLoaded();
                if (current == value) return;
                current = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Teks antarmuka berdasarkan kunci (lihat <see cref="UIStrings"/>).</summary>
        public static string T(string key) => UIStrings.Get(key, Current);

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            current = (Language)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 1);
        }
    }
}
