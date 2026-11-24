using System;
using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>Awal sebuah kalimat di klip narasi (detik) - subtitle mengikuti suara.</summary>
    [Serializable]
    public class StoryCue
    {
        public float time;
        public string text;
    }

    /// <summary>Satu bab mode Kisah: narasi + tahap exploded view + bagian yang disorot selama bab berjalan.</summary>
    [Serializable]
    public class StoryChapter
    {
        public string key;
        public LocalizedString title;
        public LocalizedString text;
        [Tooltip("Tahap exploded view saat bab dimulai; -1 = tidak diubah")] public int stage = -1;
        [Tooltip("hotspotId yang disorot selama bab; kosong = tidak ada")] public string focusHotspot;

        public AudioClip voiceID;
        public AudioClip voiceEN;
        public List<StoryCue> cuesID = new List<StoryCue>();
        public List<StoryCue> cuesEN = new List<StoryCue>();

        // Suara dan subtitle selalu dari bahasa yang sama; tanpa klip, subtitle berjalan dengan pewaktu.
        public AudioClip Voice => Locale.Current == Language.EN ? voiceEN : voiceID;
        public List<StoryCue> Cues => Locale.Current == Language.EN ? cuesEN : cuesID;
    }

    /// <summary>
    /// Mode Kisah artefak: narasi bercerita (sejarah & cara pembuatan) yang menggerakkan model - bilah dihunus,
    /// bagian dibongkar, label disorot - sambil subtitle tampil per kalimat. Dibangun dari Tools/narasi/kisah.json.
    /// </summary>
    [CreateAssetMenu(menuName = "Nusantara AR/Artifact Story", fileName = "Story")]
    public class ArtifactStory : ScriptableObject
    {
        public List<StoryChapter> chapters = new List<StoryChapter>();
        [Tooltip("Suara narasi (untuk kredit)")] public string voiceCredit;

        [Header("Validasi (gerbang rilis PRD §6.4)")]
        public bool curatorValidated;
    }
}
