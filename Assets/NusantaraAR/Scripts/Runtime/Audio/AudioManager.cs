using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Tiga AudioSource terpisah (PRD §9.2): narasi (play/pause/seek, progress), SFX (PlayOneShot) dan musik latar
    /// artefak (berulang). Volume SFX dan musik diturunkan (ducking) selama narasi berbunyi.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const float DuckFactor = 0.35f;
        const float MusicDuckFactor = 0.3f;
        const float MusicDuckSeconds = 0.4f;
        const float MusicFadeSeconds = 0.8f;
        const float MusicLoopFadeSeconds = 2f;
        static AudioManager instance;

        AudioSource narration;
        AudioSource sfx;
        AudioSource music;
        AudioClip click;

        // Ganti/henti musik: yang lama fade-out dulu, baru klip tertunda (null = berhenti) dimulai.
        AudioClip pendingMusic;
        bool musicChanging;
        float musicGain, musicDuck = 1f;

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

        /// <summary>Sudah dibuat (tanpa membuatnya, mis. dari OnDestroy saat aplikasi ditutup).</summary>
        public static bool Exists => instance != null;

        public AudioClip CurrentNarration => narration.clip;
        public bool IsNarrationPlaying => narration.isPlaying;
        /// <summary>Posisi narasi (detik).</summary>
        public float NarrationTime => narration.time;
        public float NarrationProgress =>
            narration.clip != null && narration.clip.length > 0f ? narration.time / narration.clip.length : 0f;

        /// <summary>Musik latar yang sedang/akan diputar (null = tidak ada).</summary>
        public AudioClip CurrentMusic => musicChanging ? pendingMusic : music.clip;

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
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
            music.volume = 0f;
            click = CreateClick();
        }

        void Update()
        {
            narration.volume = AppSettings.NarrationVolume;
            sfx.volume = AppSettings.SfxVolume * (narration.isPlaying ? DuckFactor : 1f);
            UpdateMusic(Time.unscaledDeltaTime);
        }

        /// <summary>Fade saat ganti/henti, redam selama narasi, dan fade di batas loop (tanpa bunyi klik saat berulang).</summary>
        void UpdateMusic(float dt)
        {
            musicGain = Mathf.MoveTowards(musicGain, musicChanging ? 0f : 1f, dt / MusicFadeSeconds);
            if (musicChanging && (musicGain <= 0f || !music.isPlaying))
            {
                musicChanging = false;
                music.Stop();
                music.clip = pendingMusic;
                pendingMusic = null;
                musicGain = 0f;
                if (music.clip != null) music.Play();
            }
            musicDuck = Mathf.MoveTowards(musicDuck, narration.isPlaying ? MusicDuckFactor : 1f, dt / MusicDuckSeconds);
            float loop = music.clip != null ? LoopEnvelope(music.time, music.clip.length, MusicLoopFadeSeconds) : 0f;
            music.volume = AppSettings.MusicVolume * musicGain * musicDuck * loop;
        }

        /// <summary>Penguat 0-1 di batas loop: naik selama <paramref name="fade"/> detik pertama, turun di detik-detik terakhir.</summary>
        public static float LoopEnvelope(float time, float length, float fade)
        {
            if (fade <= 0f || length <= 2f * fade) return 1f;
            return Mathf.Clamp01(Mathf.Min(time / fade, (length - time) / fade));
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

        /// <summary>Melanjutkan narasi yang dijeda dari posisi terakhir.</summary>
        public void ResumeNarration()
        {
            if (narration.clip != null && !narration.isPlaying) narration.UnPause();
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

        /// <summary>
        /// Musik latar artefak (berulang). Klip yang sama tidak dimulai ulang (mis. detail -> Scan QR -> kembali);
        /// klip lain menggantikan dengan fade. null = hentikan.
        /// </summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip != null && music.clip == clip && music.isPlaying)
            {
                // Lanjutkan; bila sedang fade-out (mis. baru kembali ke katalog lalu membuka keris yang sama), batalkan.
                musicChanging = false;
                pendingMusic = null;
                return;
            }
            if (musicChanging && pendingMusic == clip) return;
            pendingMusic = clip;
            musicChanging = true;
        }

        public void StopMusic() => PlayMusic(null);

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
