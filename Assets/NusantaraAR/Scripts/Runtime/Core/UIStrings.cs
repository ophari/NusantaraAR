using System.Collections.Generic;

namespace NusantaraAR
{
    /// <summary>Teks antarmuka (ID/EN). Konten kuratorial ada di aset ArtifactData, bukan di sini.</summary>
    public static class UIStrings
    {
        static readonly Dictionary<string, (string id, string en)> Table = new Dictionary<string, (string, string)>
        {
            ["app.title"] = ("Nusantara AR", "Nusantara AR"),
            ["app.tagline"] = ("Museum saku benda budaya nusantara", "A pocket museum of Indonesian heritage"),

            ["common.back"] = ("Kembali", "Back"),
            ["common.close"] = ("Tutup", "Close"),
            ["common.settings"] = ("Pengaturan", "Settings"),
            ["common.retry"] = ("Coba Lagi", "Try Again"),

            ["catalog.all"] = ("Semua", "All"),
            ["catalog.placeholder"] = ("Model sementara", "Temporary model"),
            ["catalog.empty"] = ("Belum ada koleksi.", "No items yet."),
            ["cat.KerisSenjata"] = ("Keris & Senjata", "Keris & Weapons"),
            ["cat.Arca"] = ("Arca", "Statues"),
            ["cat.KriaLogam"] = ("Kria Logam", "Metalcraft"),
            ["cat.Candi"] = ("Candi", "Temples"),

            ["detail.viewAR"] = ("Lihat di AR", "View in AR"),
            ["detail.arUnsupported"] = ("Perangkat ini belum mendukung AR", "This device does not support AR"),

            ["dock.explode"] = ("Bongkar", "Take apart"),
            ["dock.assemble"] = ("Gabung", "Assemble"),
            ["dock.autorotate"] = ("Putar Otomatis", "Auto Rotate"),
            ["dock.draw"] = ("Hunus", "Draw"),
            ["dock.sheathe"] = ("Sarungkan", "Sheathe"),
            ["dock.labels"] = ("Label", "Labels"),
            ["dock.story"] = ("Kisah", "Story"),
            ["story.title"] = ("Kisah", "Story"),
            ["story.resume"] = ("Lanjut", "Resume"),
            ["story.draft"] = ("Draf", "Draft"),
            ["ctrl.reset"] = ("Reset Tampilan", "Reset View"),
            ["ctrl.move"] = ("Pindahkan", "Move"),
            ["ctrl.scale"] = ("Skala", "Scale"),
            ["ctrl.planes"] = ("Tampilkan Bidang", "Show Surfaces"),
            ["rail.autorotate"] = ("Putar 360°", "Spin 360°"),
            ["rail.reset"] = ("Reset", "Reset"),

            ["nav.collection"] = ("Koleksi", "Collection"),
            ["nav.scan"] = ("Scan QR", "Scan QR"),
            ["detail.era"] = ("Era", "Era"),

            ["ar.checking"] = ("Memeriksa dukungan AR...", "Checking AR support..."),
            ["ar.detecting"] = ("Mendeteksi permukaan...", "Detecting surface..."),
            ["ar.scanning"] = ("Gerakkan HP perlahan ke arah meja atau lantai...", "Slowly move your phone toward a table or floor..."),
            ["ar.ready"] = ("Ketuk untuk meletakkan", "Tap to place"),
            ["ar.placed"] = ("1 jari: putar  |  cubit: zoom  |  2 jari: geser", "1 finger: rotate  |  pinch: zoom  |  2 fingers: move"),
            ["ar.moving"] = ("Arahkan ke lokasi baru, lalu ketuk", "Aim at the new spot, then tap"),
            ["ar.tipsTitle"] = ("Permukaan belum ditemukan", "No surface found yet"),
            ["ar.tipsBody"] = (
                "- Nyalakan lampu ruangan\n- Hindari lantai keramik polos atau permukaan mengilap\n- Coba meja kayu, karpet, atau permukaan bertekstur\n- Gerakkan HP perlahan ke kiri dan ke kanan",
                "- Turn on the room lights\n- Avoid plain tiles or glossy surfaces\n- Try a wooden table, a rug, or a textured surface\n- Move the phone slowly left and right"),
            ["ar.openViewer"] = ("Buka di 3D Viewer", "Open in 3D Viewer"),
            ["ar.unsupportedTitle"] = ("AR tidak tersedia", "AR not available"),
            ["ar.unsupportedBody"] = (
                "Perangkat ini tidak termasuk perangkat yang didukung ARCore. Semua fitur edukasi tetap tersedia di 3D Viewer.",
                "This device is not on the ARCore supported devices list. All learning features are still available in the 3D Viewer."),
            ["ar.installing"] = ("Menyiapkan Google Play Services for AR...", "Setting up Google Play Services for AR..."),
            ["ar.installFailed"] = (
                "Google Play Services for AR belum terpasang. Fitur edukasi tetap tersedia di 3D Viewer.",
                "Google Play Services for AR is not installed. Learning features are still available in the 3D Viewer."),
            ["ar.permTitle"] = ("Izin kamera diperlukan", "Camera permission needed"),
            ["ar.permBody"] = (
                "Kamera hanya dipakai untuk menampilkan artefak di ruangan secara langsung. Tidak ada gambar yang direkam atau diunggah.",
                "The camera is only used to show the artifact in your room live. No images are recorded or uploaded."),
            ["ar.permAllow"] = ("Izinkan Kamera", "Allow Camera"),
            ["ar.permDeniedBody"] = (
                "Izin kamera ditolak. Aktifkan lewat Pengaturan aplikasi, atau gunakan 3D Viewer.",
                "Camera permission was denied. Enable it in the app settings, or use the 3D Viewer."),
            ["ar.openSettings"] = ("Buka Pengaturan", "Open Settings"),
            ["ar.trackingLost"] = ("Arahkan kembali kamera ke area objek...", "Point the camera back at the object area..."),

            ["marker.scan"] = ("Scan QR (AR)", "Scan QR (AR)"),
            ["marker.placeOnTable"] = ("Letakkan di Meja", "Place on Table"),
            ["marker.showCard"] = ("Tampilkan QR", "Show QR"),
            ["marker.cardTitle"] = ("Kode QR", "QR Code"),
            ["marker.cardHint"] = (
                "Buka Nusantara AR di HP lain lalu pilih Scan QR, dan arahkan kamera ke kode ini. Bisa juga dicetak (sisi kode QR sekitar 8 cm, jangan terlipat).",
                "Open Nusantara AR on another phone, choose Scan QR, and point the camera at this code. It can also be printed (QR code about 8 cm wide, keep it flat)."),
            ["marker.searching"] = ("Arahkan kamera ke kode QR", "Point the camera at the QR code"),
            ["marker.tracking"] = ("QR terdeteksi - geser: putar & miringkan, cubit: zoom", "QR detected - drag: rotate & tilt, pinch: zoom"),
            ["marker.locked"] = ("Posisi dikunci - kode QR boleh dijauhkan", "Position locked - you can move the QR code away"),
            ["marker.lock"] = ("Kunci Posisi", "Lock"),
            ["marker.unlock"] = ("Lepas Kunci", "Unlock"),
            ["marker.noCameraTitle"] = ("Kamera tidak ditemukan", "No camera found"),
            ["marker.noCameraBody"] = ("Perangkat ini tidak memiliki kamera yang bisa dipakai.", "This device has no usable camera."),
            ["marker.stalledTitle"] = ("Kamera belum menampilkan gambar", "The camera shows no picture"),
            ["marker.stalledBody"] = (
                "Kamera mungkin sedang dipakai aplikasi lain. Tutup aplikasi yang memakai kamera, lalu coba lagi.",
                "The camera may be in use by another app. Close any app using the camera, then try again."),

            ["sheet.philosophy"] = ("Filosofi", "Meaning"),
            ["sheet.craft"] = ("Kriya", "Craft"),
            ["sheet.history"] = ("Sejarah", "History"),
            ["sheet.sources"] = ("Sumber rujukan", "References"),
            ["sheet.draft"] = ("Draf - belum divalidasi kurator", "Draft - not yet validated by a curator"),
            ["sheet.play"] = ("Putar", "Play"),
            ["sheet.pause"] = ("Jeda", "Pause"),
            ["sheet.pronounce"] = ("Pelafalan", "Pronunciation"),
            ["sheet.material"] = ("Bahan", "Material"),
            ["sheet.region"] = ("Asal", "Origin"),

            ["settings.language"] = ("Bahasa", "Language"),
            ["settings.sound"] = ("Suara", "Sound"),
            ["settings.privacy"] = ("Privasi", "Privacy"),
            ["settings.narrationVol"] = ("Volume narasi", "Narration volume"),
            ["settings.musicVol"] = ("Volume musik latar", "Background music volume"),
            ["settings.sfxVol"] = ("Volume efek suara", "Sound effects volume"),
            ["settings.analytics"] = ("Bagikan data penggunaan anonim", "Share anonymous usage data"),
            ["settings.analyticsNote"] = (
                "Hanya statistik pemakaian fitur, tanpa data pribadi dan tanpa gambar kamera.",
                "Feature usage statistics only - no personal data and no camera images."),
            ["settings.tutorial"] = ("Ulangi tutorial", "Replay tutorial"),
            ["settings.about"] = ("Tentang", "About"),
            ["settings.aboutBody"] = (
                "Nusantara AR - aplikasi edukasi benda budaya Indonesia.\nModel 3D saat ini adalah model Blender sementara: keris dibuat dari lembar acuan, karambit dari foto acuan museum dan data bersumber, Candi Borobudur dari data ukuran bersumber ditambah perkiraan (skala 1:200). Model akan diganti atau divalidasi kurator.\nSeluruh teks kuratorial masih draf.\nSuara narator mode Kisah adalah suara sintetis (Microsoft neural TTS).\nMusik latar dari Pixabay; tanda [AI] = dibuat dengan AI oleh pengunggahnya.",
                "Nusantara AR - a learning app for Indonesian cultural objects.\nThe current 3D models are temporary Blender models: the keris were made from reference sheets, the karambit from museum reference photos and sourced data, Borobudur temple from sourced dimensions plus estimates (1:200 scale). They will be replaced or validated by a curator.\nAll curatorial text is still a draft.\nThe Story narrator is a synthetic voice (Microsoft neural TTS).\nBackground music from Pixabay; [AI] = made with AI by its uploader."),
            ["settings.musicCredits"] = ("Musik latar", "Background music"),
            ["settings.on"] = ("Aktif", "On"),
            ["settings.off"] = ("Nonaktif", "Off"),

            ["onb.skip"] = ("Lewati", "Skip"),
            ["onb.next"] = ("Lanjut", "Next"),
            ["onb.start"] = ("Mulai", "Start"),
            ["onb.1.title"] = ("Selamat datang", "Welcome"),
            ["onb.1.body"] = (
                "Jelajahi benda budaya nusantara dalam 3D dan AR: bongkar bagiannya, ketuk label emas untuk membaca maknanya, atau ketuk Kisah untuk mendengarkan cerita sejarah dan cara pembuatannya.",
                "Explore Indonesian cultural objects in 3D and AR: take them apart, tap the gold labels to learn their meaning, or tap Story to hear the tale of their history and how they are made."),
            ["onb.2.title"] = ("Cara berinteraksi", "How to interact"),
            ["onb.2.body"] = (
                "Geser 1 jari: memutar objek\nCubit 2 jari: memperbesar / memperkecil\nGeser 2 jari: memindahkan objek (mode AR)\nKetuk label atau bagian objek: membuka penjelasan",
                "Drag 1 finger: rotate the object\nPinch: zoom in / out\nDrag 2 fingers: move the object (AR mode)\nTap a label or part of the object: open its description"),
            ["onb.3.title"] = ("Aman saat memakai AR", "Stay safe in AR"),
            ["onb.3.body"] = (
                "Perhatikan sekitar saat berjalan sambil memegang HP. Letakkan objek di meja atau lantai yang lapang.",
                "Watch your surroundings while walking with your phone. Place objects on a clear table or floor."),
        };

        public static string Get(string key, Language lang)
        {
            if (!Table.TryGetValue(key, out var v)) return key;
            return lang == Language.EN && !string.IsNullOrEmpty(v.en) ? v.en : v.id;
        }
    }
}
