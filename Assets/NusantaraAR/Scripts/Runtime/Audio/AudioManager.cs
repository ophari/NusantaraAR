using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Dua AudioSource terpisah (PRD §9.2): narasi (play/pause/seek, progress) dan SFX (PlayOneShot).
    /// Volume SFX diturunkan (ducking) selama narasi berbunyi.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const float DuckFactor = 0.35f;
        static AudioManager instance;

        AudioSource narration;
        AudioSource sfx;
        AudioClip click;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("AudioManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        public AudioClip CurrentNarration => narration.clip;
        public bool IsNarrationPlaying => narration.isPlaying;
        public float NarrationProgress =>
            narration.clip != null && narration.clip.length > 0f ? narration.time / narration.clip.length : 0f;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            narration = gameObject.AddComponent<AudioSource>();
            narration.playOnAwake = false;
            narration.spatialBlend = 0f;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            click = CreateClick();
        }

        void Update()
        {
            narration.volume = AppSettings.NarrationVolume;
            sfx.volume = AppSettings.SfxVolume * (narration.isPlaying ? DuckFactor : 1f);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PauseNarration();
        }

        public void PlayNarration(AudioClip clip)
        {
            if (clip == null) return;
            if (narration.clip != clip)
            {
                narration.clip = clip;
                narration.time = 0f;
            }
            narration.Play();
        }

        public void PauseNarration()
        {
            if (narration.isPlaying) narration.Pause();
        }

        public void StopNarration()
        {
            narration.Stop();
            narration.clip = null;
        }

        /// <summary>Lompat ke posisi narasi (0-1).</summary>
        public void Seek(float normalized)
        {
            if (narration.clip == null) return;
            narration.time = Mathf.Clamp01(normalized) * Mathf.Max(0f, narration.clip.length - 0.01f);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null) sfx.PlayOneShot(clip);
        }

        public void Click() => PlaySfx(click);

        static AudioClip CreateClick()
        {
            const int rate = 44100;
            const float seconds = 0.045f;
            int n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 90f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1850f * t) * env * 0.25f;
            }
            var clip = AudioClip.Create("ui_click", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
