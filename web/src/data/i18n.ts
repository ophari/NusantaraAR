import type { Lang } from './artifacts';

export const COPY = {
  id: {
    nav: {
      collection: 'Koleksi 3D',
      features: 'Fitur',
      arMode: 'Mode AR',
      download: 'Unduh',
      cta: 'Unduh APK',
    },
    hero: {
      eyebrow: 'Museum Saku Interaktif · Android AR',
      titleLine1: 'Warisan Nusantara,',
      titleLine2: 'Hidup di Tangan Anda.',
      subtitle:
        'Jelajahi enam mahakarya budaya dan satwa endemik Indonesia dalam 3D interaktif & Augmented Reality — bongkar tiap lapisan konstruksinya, dengarkan kisah sejarahnya, dan pindai kartu QR langsung dari kamera ponsel.',
      ctaDownload: 'Unduh APK Android',
      ctaExplore: 'Jelajahi Model 3D',
      dragHint: 'Geser untuk memutar 360°',
      explode: 'Bongkar 3D',
      assemble: 'Gabung',
      autoSpin: 'Putar',
      reset: 'Reset',
      loading3d: 'Memuat model 3D',
      stats: [
        { value: '6', label: 'Artefak 3D Detail' },
        { value: '59+', label: 'Hotspot Kuratorial' },
        { value: '2', label: 'Mode AR (QR & Meja)' },
        { value: 'ID/EN', label: 'Narasi & Subtitle' },
      ],
    },
    gallery: {
      eyebrow: 'Studio Koleksi 3D Interaktif',
      title: 'Sentuh, Putar, dan Bongkar Setiap Bagian.',
      subtitle:
        'Pilih artefak di bawah ini untuk memuat model 3D asli dari aplikasi Nusantara AR secara langsung di browser Anda — terkompresi Meshopt + WebP agar tetap mulus 60 FPS.',
      filters: {
        all: 'Semua (6)',
        weapons: 'Keris & Senjata',
        temple: 'Candi',
        wildlife: 'Satwa Endemik',
      },
      originLabel: 'Asal',
      eraLabel: 'Era & Status',
      hotspotsUnit: 'Hotspot Info',
      stagesUnit: 'Tahap Bongkar',
      drawBadge: 'Animasi Hunus',
      downloadQr: 'Unduh Kartu QR (A5 PDF)',
      inspectHint: 'Klik tombol Bongkar 3D untuk memisahkan komponen artefak secara real-time.',
    },
    features: {
      eyebrow: 'Dirancang untuk Pengalaman Belajar Imersif',
      title: 'Bukan Sekadar Melihat, Tapi Memahami Maknanya.',
      subtitle:
        'Seluruh antarmuka Nusantara AR dibangun dengan tema kaca buram gading-terakota-nila dan kontras aksesibel WCAG AA, menghadirkan kurasi budaya yang nyaman di berbagai perangkat Android.',
      items: [
        {
          id: 'detail',
          title: '3D Viewer & Hotspot Emas',
          desc: 'Ketuk label emas langsung pada bagian artefak untuk membuka kartu penjelasan 3 tab: Filosofi, Kriya, dan Sejarah lengkap dengan sumber rujukan.',
          screen: '/screens/app_detail_golok.png',
          badge: '59+ Titik Info',
        },
        {
          id: 'hunus',
          title: 'Animasi Hunus & Sarungkan',
          desc: 'Tarik bilah Golok Betawi, Keris Bali, dan Keris Sumatra keluar dari sarungnya secara mulus mengikuti lintasan fisik asli tanpa menembus dinding sarung.',
          screen: '/screens/app_hunus_golok.png',
          badge: 'Lintasan Fisik',
        },
        {
          id: 'explode',
          title: 'Exploded View Bertahap',
          desc: 'Bongkar artefak selapis demi selapis — dari hulu, paku pengunci, salut, hingga 14 simpay kuningan atau tingkatan candi — lalu gabungkan kembali kapan saja.',
          screen: '/screens/app_explode_golok.png',
          badge: '4–5 Tahap Bongkar',
        },
        {
          id: 'catalog',
          title: 'Mode Kisah, Audio & Bilingual',
          desc: 'Dengarkan narasi Mode Kisah berbahasa Indonesia & Inggris dengan subtitle per kalimat yang otomatis membongkar dan menyorot bagian terkait, diiringi musik latar tradisi.',
          screen: '/screens/app_catalog.png',
          badge: 'ID / EN · Audio',
        },
      ],
    },
    ar: {
      eyebrow: 'Dua Jalur Augmented Reality',
      title: 'Jalan di Semua HP Android Berkamera.',
      subtitle:
        'Tidak perlu HP flagship mahal. Nusantara AR menyediakan detektor QR mandiri bawaan yang berjalan ringan di ponsel tanpa ARCore sekalipun.',
      mode1Title: 'Scan QR (Utama — Semua HP)',
      mode1Desc:
        'Cukup arahkan kamera ke kartu QR cetak A5 atau layar laptop. Artefak 3D muncul di atas kode QR lengkap dengan tombol Kunci Posisi agar Anda bebas memutar dan mendekat.',
      mode1Tag: 'Tanpa ARCore · Kamera Standar',
      mode2Title: 'Letakkan di Meja (ARCore)',
      mode2Desc:
        'Untuk perangkat yang mendukung Google Play Services for AR, deteksi permukaan meja atau lantai secara real-time lalu tempatkan artefak berskala nyata dengan jangkar spasial.',
      mode2Tag: 'AR Foundation · Plane Detection',
      cardsTitle: 'Unduh Kartu QR Cetak A5',
      cardsSub: 'Cetak tanpa diperkecil (sisi QR 8 cm) atau tampilkan di layar perangkat lain untuk langsung memindai.',
    },
    download: {
      eyebrow: 'Rilis Android · Gratis & Offline-Ready',
      title: 'Siap Menjelajah Nusantara dalam AR?',
      subtitle:
        'Pasang Nusantara AR langsung di ponsel Android Anda. Seluruh model 3D, narasi Mode Kisah, dan musik latar sudah tertanam sehingga dapat dinikmati tanpa koneksi internet.',
      btn: 'Unduh NusantaraAR.apk',
      specs: {
        version: 'Versi',
        size: 'Ukuran APK',
        os: 'Minimum OS',
        arch: 'Arsitektur',
      },
      stepsTitle: 'Cara Pemasangan (3 Langkah)',
      steps: [
        'Tekan tombol Unduh NusantaraAR.apk di atas dan tunggu berkas selesai tersimpan.',
        'Buka berkas APK di ponsel Android Anda, lalu izinkan pemasangan dari sumber ini jika diminta.',
        'Jalankan Nusantara AR, jelajahi 3D Viewer atau cetak Kartu QR untuk mencoba mode Scan QR AR.',
      ],
    },
    footer: {
      tagline: 'Museum saku benda budaya dan satwa endemik Indonesia berbasis Unity 6 & WebGL 3D.',
      rights: 'Nusantara AR · Edukasi Budaya Indonesia',
    },
  },
  en: {
    nav: {
      collection: '3D Collection',
      features: 'Features',
      arMode: 'AR Modes',
      download: 'Download',
      cta: 'Download APK',
    },
    hero: {
      eyebrow: 'Interactive Pocket Museum · Android AR',
      titleLine1: 'Indonesian Heritage,',
      titleLine2: 'Alive in Your Hands.',
      subtitle:
        'Explore six iconic Indonesian cultural artifacts and endemic wildlife in interactive 3D & Augmented Reality — inspect every exploded layer, listen to narrated stories, and scan printable QR cards directly from your phone camera.',
      ctaDownload: 'Download Android APK',
      ctaExplore: 'Explore 3D Models',
      dragHint: 'Drag to orbit 360°',
      explode: 'Explode 3D',
      assemble: 'Assemble',
      autoSpin: 'Spin',
      reset: 'Reset',
      loading3d: 'Loading 3D model',
      stats: [
        { value: '6', label: 'Detailed 3D Artifacts' },
        { value: '59+', label: 'Curatorial Hotspots' },
        { value: '2', label: 'AR Modes (QR & Table)' },
        { value: 'ID/EN', label: 'Narrated Subtitles' },
      ],
    },
    gallery: {
      eyebrow: 'Interactive 3D Collection Studio',
      title: 'Touch, Orbit, and Disassemble Every Layer.',
      subtitle:
        'Select any artifact below to inspect the real 3D model from Nusantara AR right in your browser — optimized with Meshopt + WebP for smooth 60 FPS interaction.',
      filters: {
        all: 'All (6)',
        weapons: 'Keris & Weapons',
        temple: 'Temples',
        wildlife: 'Endemic Wildlife',
      },
      originLabel: 'Origin',
      eraLabel: 'Era & Recognition',
      hotspotsUnit: 'Info Hotspots',
      stagesUnit: 'Explode Stages',
      drawBadge: 'Draw Animation',
      downloadQr: 'Download QR Card (A5 PDF)',
      inspectHint: 'Toggle Explode 3D to separate the artifact’s modular craftsmanship in real time.',
    },
    features: {
      eyebrow: 'Crafted for Immersive Cultural Learning',
      title: 'Not Just Viewing — Understanding Every Story.',
      subtitle:
        'Designed with warm ivory-terracotta-indigo glassmorphism and WCAG AA typography contrast, delivering museum-grade learning across mid-range and flagship Android phones.',
      items: [
        {
          id: 'detail',
          title: '3D Viewer & Gold Hotspots',
          desc: 'Tap gold callouts directly on the 3D object to open 3-tab curatorial cards covering Philosophy, Craftsmanship, and History with citations.',
          screen: '/screens/app_detail_golok.png',
          badge: '59+ Info Points',
        },
        {
          id: 'hunus',
          title: 'Authentic Draw & Sheathe Motion',
          desc: 'Smoothly draw the Betawi Golok, Balinese Keris, and Sumatran Keris blades along real physical clearance paths without clipping the scabbard.',
          screen: '/screens/app_hunus_golok.png',
          badge: 'Physical Clearance',
        },
        {
          id: 'explode',
          title: 'Step-by-Step Exploded View',
          desc: 'Disassemble each artifact layer by layer — from hilt, brass pin, guard, and 14 simpay bands to temple cosmology tiers — and reassemble seamlessly.',
          screen: '/screens/app_explode_golok.png',
          badge: '4–5 Explode Stages',
        },
        {
          id: 'catalog',
          title: 'Story Mode, Audio & Bilingual',
          desc: 'Follow narrated Indonesian & English Story Mode chapters with sentence-synced subtitles that automatically animate and highlight each part.',
          screen: '/screens/app_catalog.png',
          badge: 'ID / EN · Audio',
        },
      ],
    },
    ar: {
      eyebrow: 'Dual Augmented Reality Modes',
      title: 'Runs on Any Android Phone With a Camera.',
      subtitle:
        'No flagship required. Nusantara AR ships with a custom built-in QR detector & pose tracker that runs smoothly even on devices without ARCore.',
      mode1Title: 'Scan QR (Primary — All Phones)',
      mode1Desc:
        'Point your camera at a printed A5 QR card or another screen. The 3D artifact anchors right above the code with a Lock Position toggle for hands-free inspection.',
      mode1Tag: 'No ARCore Needed · Any Camera',
      mode2Title: 'Place on Table (ARCore)',
      mode2Desc:
        'On ARCore-supported devices, detect real tables or floors and place true-scale artifacts anchored in your room with intuitive touch gestures.',
      mode2Tag: 'AR Foundation · Plane Detection',
      cardsTitle: 'Download Printable A5 QR Cards',
      cardsSub: 'Print at 100% scale (8 cm QR side) or display on another screen to scan right away.',
    },
    download: {
      eyebrow: 'Android Release · Free & Offline-Ready',
      title: 'Ready to Explore Indonesia in AR?',
      subtitle:
        'Install Nusantara AR directly on your Android phone. All 3D models, narrated stories, and traditional music are bundled offline so you can explore anywhere.',
      btn: 'Download NusantaraAR.apk',
      specs: {
        version: 'Version',
        size: 'APK Size',
        os: 'Minimum OS',
        arch: 'Architecture',
      },
      stepsTitle: 'How to Install (3 Steps)',
      steps: [
        'Tap Download NusantaraAR.apk above and wait for the installer file to finish downloading.',
        'Open the APK file on your Android device and allow installing from this source if prompted.',
        'Launch Nusantara AR to explore the 3D Viewer or scan any printed A5 QR Card in AR mode.',
      ],
    },
    footer: {
      tagline: 'Pocket museum of Indonesian heritage & endemic wildlife powered by Unity 6 & WebGL 3D.',
      rights: 'Nusantara AR · Indonesian Cultural Education',
    },
  },
} as const;

export function t(lang: Lang) {
  return COPY[lang];
}
