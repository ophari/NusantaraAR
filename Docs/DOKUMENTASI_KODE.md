# Dokumentasi Kode — Nusantara AR

Dokumen ini menjelaskan cara kerja kode proyek: arsitektur, alur data, tanggung jawab setiap file, kontrak konten, algoritma Scan QR (dan kartu penanda lama), pipeline editor, dan pengujian. Tujuannya agar proyek bisa dianalisis, diaudit, atau dilanjutkan orang lain tanpa harus membaca semua kode dari awal.

> Cara menjalankan dan status fitur PRD ada di [README.md](../README.md). Dokumen ini fokus ke **isi kode**.

---

## 1. Ringkasan teknis

| Hal | Nilai |
|---|---|
| Engine | Unity 6000.3.25f1 (Unity 6.3 LTS), Universal Render Pipeline 17.3 |
| AR | AR Foundation 6.3 + ARCore (**Optional**, jadi aplikasi tetap terpasang di HP tanpa ARCore) |
| Input | Input System (bukan Input Manager lama) |
| Target | Android, IL2CPP ARM64, minSdk 26, targetSdk 36 |
| UI | uGUI + TextMeshPro, **dibuat seluruhnya dari kode** (`UIKit`), tanpa prefab UI dan tanpa layout di YAML scene. Gaya gading · terakota · nila dengan **kaca buram sungguhan** (blur kamera lewat `GlassBlurFeature` URP, lihat §5.1) |
| Bahasa kode | C#; komentar dan teks dalam Bahasa Indonesia |
| Jumlah kode | 70 file C# + 2 shader, ±12.550 baris (Runtime 53 file / 9.390 baris, Editor 10 / 1.960, Test 7 / 1.200) |
| Audio | Narasi mode Kisah (Microsoft neural TTS, ID/EN), musik latar per keris (Pixabay), SFX klik prosedural |

### Assembly dan namespace

| Assembly (`.asmdef`) | Folder | Namespace | Isi |
|---|---|---|---|
| `NusantaraAR.Runtime` | `Assets/NusantaraAR/Scripts/Runtime` | `NusantaraAR`, `NusantaraAR.UI`, `NusantaraAR.Marker`, `NusantaraAR.Rendering` | Semua kode yang ikut ke APK (merujuk URP Runtime untuk `GlassBlurFeature`) |
| `NusantaraAR.Editor` | `Assets/NusantaraAR/Scripts/Editor` | `NusantaraAR.EditorTools` | Generator proyek/konten/scene, build script. **Tidak ikut ke APK** |
| `NusantaraAR.Tests.EditMode` | `Assets/NusantaraAR/Tests/EditMode` | `NusantaraAR.Tests` | Uji NUnit (EditMode) |

Arah dependensi: `Editor → Runtime`, `Tests → Runtime (+ Editor)`. Runtime tidak pernah bergantung pada Editor.

---

## 2. Struktur folder

```
NusantaraAR/
├─ Assets/NusantaraAR/
│  ├─ Art/
│  │  ├─ Common/                  material & mesh bersama (reticle, bidang AR)
│  │  ├─ KerisBali/               keris_bali.glb (ekspor Blender; mesh/material/tekstur diimpor glTFast)
│  │  ├─ KerisSumatra/            keris_sumatra.glb (ekspor Blender)
│  │  └─ CandiBorobudur/          candi_borobudur.glb (ekspor Blender, skala asli 123 m)
│  ├─ Content/
│  │  ├─ KERIS_BALI_01/           .prefab + .asset (ArtifactData) + _thumb.png
│  │  │  ├─ Story/                <ID>_Story.asset (ArtifactStory) + 9 bab × 2 bahasa .mp3 (narasi Kisah)
│  │  │  └─ Music/                <ID>_music.mp3 (musik latar)
│  │  ├─ KERIS_SUMATRA_01/        .prefab + .asset + _thumb.png + Story/ + Music/
│  │  └─ BOROBUDUR_01/            .prefab + .asset + _thumb.png + Story/ + Music/
│  ├─ Prefabs/                    ARPlane.prefab
│  ├─ Resources/                  ContentCatalog.asset (dimuat saat runtime)
│  ├─ Scenes/                     Main.unity, AR.unity, Marker.unity (DIHASILKAN oleh ProjectSetup)
│  ├─ Scripts/
│  │  ├─ Runtime/  AR/ Artifact/ Audio/ Content/ Core/ Interaction/ Marker/ UI/
│  │  └─ Editor/
│  └─ Tests/EditMode/
├─ Docs/        kartu QR + kartu penanda lama (PNG/PDF), draf laporan, dokumen ini
├─ Tools/       compile_and_test.ps1, kartu_qr.py, kartu_penanda.py, blender/keris_bali.py, blender/keris_sumatra.py (+ .blend, *_textures/),
│               narasi/ (kisah.json, kisah_cues.json, kisah_tts.py), musik/ (musik.json, siapkan_musik.py, asli/ tidak di-commit)
├─ Builds/      output APK/AAB
└─ Logs/        setup.log, tests.log, tests.xml (dari Tools/compile_and_test.ps1)
```

**Penting:** scene, prefab, katalog, aset Kisah, dan GLB adalah **output generator** (GLB dari skrip Blender, MP3 narasi/musik dari skrip Python di `Tools/`, sisanya dari `Scripts/Editor/`). Jika diubah manual lewat Inspector, perubahan akan **tertimpa** saat `Nusantara AR/Setup Everything` dijalankan lagi. Sumber kebenarannya ada di kode `Scripts/Editor/`.

---

## 3. Arsitektur dan alur aplikasi

### 3.1 Tiga scene dan navigasi

```
                ┌───────────────────────────────── Scene "Main" (MainController) ─────────────────────────────────┐
  App start ──► │  Katalog (CatalogScreen) ──pilih──► Detail + 3D Viewer (OrbitCamera, ArtifactHud)               │
                │        │  tombol "Scan"                    │ Scan QR        │ Letakkan di Meja  │ Tampilkan QR    │
                └────────┼───────────────────────────────────┼────────────────┼───────────────────┼─────────────────┘
                         │                                   │                │                   └─► MarkerCardScreen (overlay)
                         ▼                                   ▼                ▼
              Scene "Marker" (MarkerController)   ◄──────────┘     Scene "AR" (ARController)
              kamera biasa + deteksi QR (+kartu lama)             ARCore: deteksi bidang + anchor
                         │  Kembali                                         │  Kembali / gagal
                         └──────────────► AppSession.OpenViewer() ◄─────────┘   (kembali ke Detail artefak)
```

- Perpindahan scene hanya lewat `AppSession` (`OpenAR`, `OpenMarker`, `OpenViewer`, `OpenCatalog`).
- Keadaan lintas scene cukup dua nilai statis, yaitu `AppSession.SelectedArtifactId` dan `AppSession.OpenDetailOnLoad`.
- Tombol "Letakkan di Meja" hanya tampil bila `ARSession.CheckAvailability()` menyatakan ARCore didukung. Di Galaxy A05 tombol ini tersembunyi, jadi yang dipakai adalah Scan QR.
- Tombol back Android dipetakan ke `Keyboard.escapeKey` oleh Input System. Setiap controller menutup overlay teratas lebih dulu, baru kemudian kembali ke layar sebelumnya.

### 3.2 Alur data konten

```
Resources/ContentCatalog.asset ──► AppSession.Catalog (lazy, cache statis)
      └─ artifacts: List<ArtifactData>
             ├─ prefab ──Instantiate──► ArtifactInstance.Init(data)
             │                              ├─ ArtifactPart[]  (dicari berdasarkan partName)
             │                              ├─ ExplodedViewController (tahap-tahap pose)
             │                              └─ AutoRotate
             ├─ hotspots ──► HotspotOverlay (titik + label di AR) ──tap──► HotspotCard (isi kuratorial + narasi, menempel di objek)
             ├─ artifactId ──► QrText "NUSANTARA:<id>" ──► MarkerController (isi QR terbaca) ──► FindByQr(text)
             ├─ markerCode ──► MarkerController (kartu lama, cadangan) ──► FindByMarker(code)
             ├─ story (ArtifactStory) ──► StoryPanel (bab: suara + subtitle + tahap exploded + sorotan hotspot)
             └─ backgroundMusic ──► AudioManager.PlayMusic (3D Viewer, Scan QR, AR; StopMusic saat kembali ke katalog)
```

Satu komponen UI, yaitu `ArtifactHud`, dipakai di ketiga scene. Tiap controller membuatnya dengan `HudOptions` (jarak atas, ruang bawah milik layar, tombol miring ya/tidak, `ControlPanelConfig` untuk mode kamera) lalu memanggil `hud.Bind(instance, camera, onReset, onNudge)`. `ArtifactHud` mengatur rel kaca kanan (Kisah, Bongkar/Gabung, Hunus/Sarungkan, Label, Putar 360°, Reset), klaster tahan-tekan kiri (putar/miring, diteruskan ke `onNudge`: + yaw = muka objek bergeser ke kiri, + tilt = sisi atas menjauh), panel skala + saklar di mode kamera, label bagian + kartu info, dan panel Kisah. Controller meneruskan ketukan di luar UI ke `hud.HandleTap` agar bagian model bisa diketuk langsung.

Scene Main memakai `BottomNav` (Koleksi · Scan QR · Pengaturan), `TopBar`, dan `DetailSheet`; latar katalog/pengaturan digambar kamera (`Backdrop`) supaya kartu kaca punya sesuatu untuk di-blur. Scene AR & Marker memakai `TopBar` (judul dalam pil kaca), `CoachCard` (panduan kamera), `ControlPanel`, dan `MessageDialog`.

### 3.3 Pola yang dipakai konsisten

| Pola | Contoh | Alasan |
|---|---|---|
| Factory statis `X.Create(parent, …)` untuk layar UI | `CatalogScreen.Create`, `HotspotCard.Create`, `ArtifactHud.Create` | UI dibangun dari kode, jadi tidak ada referensi Inspector yang bisa putus |
| State machine `enum State` + `SetState()` + `RefreshTexts()` | `ARController`, `MarkerController` | Semua teks dan tombol diturunkan dari satu state |
| Event C# (`event Action<…>`) | `TouchGestures.Tapped/Dragged/Pinched`, `ExplodedViewController.StageChanged`, `Locale.Changed` | Komponen tidak saling kenal secara langsung |
| Unsubscribe di `OnDestroy` | semua controller | Mencegah handler menempel ke objek yang sudah dihancurkan saat pindah scene |
| `LocalizedString {id, en}` + `Locale.T(key)` | konten dan teks UI | Dua bahasa; EN kosong jatuh ke ID |
| Generator idempoten di editor | `ProjectSetup.RunAll`, `KerisBaliBuilder.Build`, `KerisSumatraBuilder.Build`, `CandiBorobudurBuilder.Build` | Proyek bisa dibangun ulang dari nol secara deterministik |

---

## 4. Kontrak prefab artefak

Setiap artefak **wajib** mengikuti kontrak ini. Kontrak ini dicek oleh `ArtifactTests` (dijalankan untuk setiap artefak) dan `KerisBlenderTests`.

```
KERIS_xxx (root)                 ← ArtifactInstance, ExplodedViewController, AutoRotate
│                                   pivot di DASAR artefak, muka depan menghadap -Z, 1 unit = 1 meter
└─ Model                         ← ArtifactInstance.modelRoot
   ├─ <bagian> (ArtifactPart)    partName unik, renderers[] = renderer milik bagian ini
   │   └─ mesh/renderer …
   └─ …
```

| Komponen | Tanggung jawab |
|---|---|
| `ArtifactInstance` | Menyimpan rotasi/skala awal (`ResetView`), memberi skala relatif 0,5×–3× (`MinScale`/`MaxScale`), menghadapkan muka ke kamera (`FaceTowards`), mencari bagian (`GetPart`), menentukan apakah hotspot boleh tampil (`IsHotspotAvailable`), dan menghitung bounds dunia (`GetWorldBounds`) |
| `ArtifactPart` | Nama bagian + renderer-nya; `IsVisible` = ada renderer yang aktif |
| `ExplodedViewController` | `stages[0]` = utuh. Setiap `Stage` berisi label dua bahasa, pose lokal lengkap semua bagian yang bergerak, dan `hiddenRenderers` (mis. bilah disembunyikan saat tersarung). Menyediakan `Toggle/Explode/Assemble/GoTo` (animasi ≤ 1 detik per tahap) dan `SnapTo` (tanpa animasi) |
| `AutoRotate` | Turntable pelan; berhenti otomatis ketika `TouchGestures.InteractionStarted` terpicu |

**Aturan hotspot** (`HotspotData`):
- `partName` harus sama dengan salah satu `ArtifactPart.partName`.
- `localPosition` adalah posisi relatif terhadap transform bagian itu.
- `visibleFrom = Utuh` berarti hotspot tampil sejak keris utuh. `Bilah` berarti hotspot hanya tampil pada tahap ≥ 1 (bilah sudah dihunus) dan saat tidak sedang beranimasi.
- `curatorValidated` adalah gerbang rilis (PRD §6.4). Konten draf ditandai `false`.

### Artefak yang ada

| ID | Sumber model | Kode kartu | Tahap exploded | Catatan |
|---|---|---|---|---|
| `KERIS_BALI_01` | **Blender GLB** (`Tools/blender/keris_bali.py` → `KerisBaliBuilder`) | `0xB532` (46386) | 1 utuh + 5 | Bilah 40 cm + pesi 8 cm, luk 9, pamor banyu tetes, hulu (danganan) figur dewa emas, warangka sesrengatan khas Bali dari kayu timoho, di atas jagrak Karang Boma 45 cm. 11 hotspot |
| `KERIS_SUMATRA_01` | **Blender GLB** (`Tools/blender/keris_sumatra.py` → `KerisSumatraBuilder`) | `0xF0E4` (61668) | 1 utuh + 4 | Bilah 36 cm + pesi 7,2 cm, luk 7, pamor wos wutah, hulu burl berukir, sampir bulan sabit, berdiri di dudukan kayu. 9 hotspot |
| `BOROBUDUR_01` | **Blender GLB** (`Tools/blender/candi_borobudur.py` → `CandiBorobudurBuilder`) | `0xE3B1` (58289) | 1 utuh + 4 | Kategori `Candi`. Denah 123 × 123 m, tinggi 35 m (data & sumber: `Docs/Borobudur_Data.md`), prefab **1:200** (61,5 × 17,5 cm). 10 bagian = 10 tingkat (kaki, 5 teras persegi, 3 teras melingkar, stupa induk); langkan, 432 relung-arca, tangga, gapura, 72 stupa terawang + arca ikut tingkatnya. 10 hotspot, semua `Utuh` |

Tahap Keris Bali (keris terbaring mendatar di jagrak, hulu ke +X): utuh (tersarung) → bilah dihunus (terangkat di atas sarung) → hulu + selut dilepas → ganja dilepas → warangka dilepas dari gandar → pendok dilepas dari gandar.

Tahap Keris Sumatra (keris berdiri di dudukan): utuh (tersarung) → bilah dihunus (ke samping sarung) → hulu + mendak dilepas → ganja dilepas → warangka dilepas dari gandar.

Tahap Candi Borobudur (tingkat dipisah ke atas, tiap batas 6 m skala asli): utuh → stupa induk diangkat → tiga teras melingkar (Arupadhatu) dipisah → teras persegi 3-5 dipisah → teras persegi 1-2 dipisah dari kaki (Kamadhatu). Tanpa `drawOut`, jadi tombol Hunus tidak tampil.

Kode kartu lama `0xEEC1` (keris sementara) dan `0xDA26` (Keris Jawa) sudah ditarik; `0xF0E4` berjarak ≥ 6 bit dari keduanya sehingga kartu lama yang terlanjur dicetak tidak akan memunculkan Keris Sumatra. `0xE3B1` (Borobudur) dipilih dengan pencarian menyeluruh: keempat rotasinya berjarak ≥ 10, ≥ 7 dari semua rotasi kode keris, dan ≥ 6 dari kartu yang ditarik.

`ArtifactCategory` disimpan sebagai angka di aset, jadi kategori baru (mis. `Candi`) selalu ditambahkan di **akhir** enum, lengkap dengan label `cat.<Nama>` di `UIStrings`.

---

## 5. Referensi file — Runtime

### Core (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Core/AppSession.cs` | 131 | `AppSession` (navigasi scene + artefak terpilih + cache katalog), `AppSettings` (PlayerPrefs: volume narasi/musik/SFX dengan cache, default 1 / 0,5 / 0,7; persetujuan analitik; onboarding selesai), `Analytics.Log` (hanya ke `Debug.Log`, dan hanya bila pengguna setuju; belum ada penyedia analitik) |
| `Core/MainController.cs` | 327 | Controller scene Main: membangun UI, membuka katalog/detail, 3D Viewer (instantiate prefab di `stageRoot`, `orbit.Frame`), memulai musik latar artefak saat detail dibuka dan menghentikannya saat kembali ke katalog, cek dukungan ARCore, tombol back |
| `Core/Locale.cs` | 68 | `Language {ID, EN}`, `LocalizedString`, `Locale.Current`, event `Locale.Changed`, `Locale.T(key)` |
| `Core/UIStrings.cs` | 151 | Kamus teks antarmuka per kunci (mis. `"marker.scan"`, `"ctrl.reset"`, `"dock.story"`). Konten kuratorial **tidak** disimpan di sini |
| `Core/DevCapture.cs` | 164 | Mode QA: menyusuri alur utama (termasuk mode Kisah) dan menyimpan tangkapan layar otomatis (dipakai oleh `BuildScript.BuildWindowsCapture`) |

### Content (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Content/ArtifactData.cs` | 91 | `ScriptableObject` satu artefak: identitas, kategori, region/era/ringkasan, thumbnail, prefab, data spesimen, `markerCode` (kartu lama), `QrText` (isi QR, diturunkan dari `artifactId`), flag placeholder, `hotspots`, `story` (aset Kisah terpisah), `backgroundMusic` + `musicCredit`. Juga `HotspotData`, `HotspotStage`, `ArtifactCategory` |
| `Content/ArtifactStory.cs` | 48 | `ScriptableObject` mode Kisah: daftar `StoryChapter` (kunci, judul & teks dua bahasa, `stage` exploded saat bab mulai atau -1, `focusHotspot` yang disorot, klip `voiceID/EN`, `cuesID/EN` = waktu mulai tiap kalimat), `voiceCredit`, `curatorValidated`. Suara dan subtitle selalu dari bahasa yang sama |
| `Content/ContentCatalog.cs` | 52 | Daftar artefak di `Resources/ContentCatalog`; `Find(id)`, `FindByQr(text)` (awalan `QrPrefix = "NUSANTARA:"`), `FindByMarker(code)`, `NonEmptyCategories()` (kategori tanpa isi disembunyikan) |

### Artifact (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Artifact/ArtifactInstance.cs` | 120 | Lihat §4 |
| `Artifact/ArtifactPart.cs` | 26 | Lihat §4 |
| `Artifact/ExplodedViewController.cs` | 248 | Lihat §4; animasi dijalankan lewat coroutine, event `StageChanged`. Tahap dengan `drawOut` (tahap 1 keris) dianimasikan sebagai cabut dari sarung (`DrawPath`: tarik lurus sepanjang sumbu sarung sampai pucuk lolos, lalu lengkung Bezier kubik ke pose tercabut; ±1,6 dtk). `ToggleDraw` = tombol Hunus/Sarungkan (tahap 0 ↔ 1 saja), `TryGetDrawSweepBounds` untuk pembingkaian kamera |
| `Artifact/AutoRotate.cs` | 30 | Turntable, event `ActiveChanged` |
| `Artifact/GroundGlow.cs` | 91 | Cahaya terakota lembut di bawah artefak (3D Viewer, AR Meja, Scan QR di meja): quad prosedural dengan material `Resources/GroundGlow`, sedikit lebih terang saat objek dimanipulasi. `Create` mengembalikan null bila material belum dibangun (menu Build AR Visuals) |

### Interaction (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Interaction/TouchGestures.cs` | 203 | Satu sumber gestur untuk semua scene. Event: `Tapped`, `Dragged` (1 jari), `Pinched` (rasio jarak), `TwoFingerPanned/Ended`, `InteractionStarted`. Dua jari diputuskan sekali menjadi Pinch **atau** Pan. Sentuhan di atas UI diabaikan (`IsOverUI`). `DpToPixels` untuk ambang yang tidak bergantung DPI |
| `Interaction/OrbitCameraController.cs` | 166 | Kamera orbit 3D Viewer: geser untuk memutar, pinch/scroll untuk zoom, `Nudge` untuk tombol putar/miring. `Frame(bounds)` membingkai artefak dan menjadikannya posisi reset; `EaseTo` menggeser kamera halus (dipakai `MainController` untuk mundur selama animasi hunus lalu kembali). `SetCoveredScreen(atas, bawah)` menggeser pusat proyeksi (matriks off-center, `m12`) agar keris di tengah area yang tidak tertutup top bar / sheet / nav; `DistanceToFit` memakai tinggi area terlihat |

### AR — mode ARCore (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `AR/ARController.cs` | 370 | State machine: `CheckingAvailability → (Unsupported / Installing → InstallFailed) → NeedsPermission / PermissionDenied → Scanning ⇄ ReadyToPlace → Placed ⇄ Repositioning`, dengan `TrackingLost` bila sesi kehilangan tracking. Tips ditampilkan setelah 15 detik tanpa bidang. Gestur: tap untuk meletakkan, geser 1 jari untuk memutar, pinch untuk skala, geser 2 jari untuk memindahkan. Semua jalur gagal menawarkan kembali ke 3D Viewer |
| `AR/PlacementController.cs` | 133 | Raycast dari tengah layar ke bidang (`UpdateTarget`), `Place` (membuat anchor + menghadap kamera), `BeginReposition`, `DragTo`, `SetPlanesVisible` |
| `AR/ReticleView.cs` | 84 | Cincin penanda posisi letak (mesh prosedural) |
| `AR/CameraPermission.cs` | 69 | `IsGranted`, `Request(callback)`, `OpenAppSettings()` (intent Android ke halaman info aplikasi). Dipakai oleh AR dan Marker |

### Marker — mode Scan QR (+ kartu penanda lama), tanpa ARCore (`NusantaraAR.Marker`)
| File | Baris | Isi |
|---|---|---|
| `Marker/MarkerController.cs` | 510 | Controller scene Marker, lihat §6.4 |
| `Marker/DarkRegions.cs` | 336 | Analisis frame bersama: threshold adaptif, komponen gelap, segi empat (+ sudut subpiksel opsional), sampling. Dipakai detektor QR dan kartu, lihat §6.2 |
| `Marker/QrCode.cs` | 709 | QR versi 1-10 tanpa library luar: encoder mode byte (untuk "Tampilkan QR"), decoder matriks (numerik/alfanumerik/byte), Reed-Solomon GF(256), `CreateTexture`, lihat §6.5 |
| `Marker/QrDetector.cs` | 287 | Detektor QR di gambar kamera: pola finder → homografi → grid modul → `QrCode.TryDecode`, lihat §6.5 |
| `Marker/CameraFeed.cs` | 162 | `WebCamTexture` → buffer grayscale **tegak** (sudah memperhitungkan `videoRotationAngle` dan mirror) di `Gray/Width/Height`. `ConfigureDisplay` memasang tekstur kamera ke `RawImage` latar dengan rotasi/aspek yang benar. `FocalPixels` adalah perkiraan fokus dari FOV asumsi |
| `Marker/MarkerDetector.cs` | 239 | Detektor kartu penanda lama + `Homography` (DLT 4 titik dan kuadrat terkecil), lihat §6.2 |
| `Marker/MarkerPattern.cs` | 59 | Definisi pola 6×6, kode artefak, `IsBlack`, `HammingDistance`, `CreateTexture` |
| `Marker/MarkerPose.cs` | 204 | Pose dari homografi, `UprightPose` + `DeviceGravity`, dan `PoseSmoother` (filter One Euro), lihat §6.3 |

### UI (`NusantaraAR.UI`)
| File | Baris | Isi |
|---|---|---|
| `UI/UIKit.cs` | 569 | `Theme` (token gading/terakota/nila + `AccentText` untuk teks terakota; `Contrast`/`GlassOver` untuk uji WCAG), `SpriteFactory` (rounded-rect, garis tepi, bayangan lembut 9-slice, lingkaran), `Surface` (panel berelevasi: akar transparan + `Shadow` + `Fill` + `Highlight`; warna diganti lewat `fill`), `UIKit` (Canvas dengan UV1, Panel, `Surface`, Text, Button, `IconTextButton`, `IconButton`, `RailButton`, HRow/VColumn, VerticalScroll, Slider, …) |
| `UI/IconFactory.cs` | 352 | 31 ikon garis prosedural (`enum Icon`) digambar sebagai medan jarak di grid 24 unit, dirasterisasi ke tekstur putih ber-mipmap dan di-cache |
| `UI/GlassSurface.cs` | 60 | `BaseMeshEffect` yang menjadikan Image kaca buram: material bersama `Resources/UIGlass`, kekuatan tint di UV1, mendaftar ke `GlassBlur` selama aktif |
| `UI/ArtifactHud.cs` | 384 | HUD bersama (lihat §3.2): rel kaca kanan, klaster `HoldButton` kiri, `ControlPanel` di mode kamera, pil tahap, panel Kisah (menggantikan slot bawah), reserve area untuk `HotspotOverlay`. `HandleTap`, `CloseInfo` (tombol Kembali), navigasi sebelum/berikutnya |
| `UI/HotspotOverlay.cs` | 437 | Anotasi di AR setiap `LateUpdate`: titik emas (cincin putih + halo gelap) + garis penunjuk bersarung + label kaca "Nama (i)" di kiri/kanan objek (histeresis sisi, label bertabrakan diturunkan, posisi dihaluskan). `SetReserves` menjauhkan label & kartu dari top bar, rel, klaster kiri, dan slot bawah (plus inset notch/gesture bar). `PickAt` meraycast model untuk memilih hotspot bagian yang diketuk. Label bagian yang sedang diceritakan di mode Kisah disorot |
| `UI/HotspotCard.cs` | 214 | Kartu kaca yang mengembang di samping bagian: judul, istilah daerah, bahan, status draf, tab bersegmen Kriya/Filosofi/Sejarah, sumber, Putar/Jeda + Pelafalan (hanya bila ada audio), dan chevron `n / N` antar bagian yang tampil di layar |
| `UI/StoryPanel.cs` | 315 | Mode Kisah (tombol Kisah di rel): memutar bab demi bab dari `ArtifactData.story`. Tiap bab memindah tahap exploded (hunus/bongkar/rakit), menyorot label `focusHotspot`, dan menampilkan subtitle per kalimat sesuai `cues` (tanpa klip: `SplitSentences` membagi waktu sebanding panjang teks). Panel kaca dengan Jeda/Lanjut/Tutup + progress terakota menggantikan slot bawah; model tetap bisa diputar/di-zoom. `SetSuspended` menjeda suara saat HUD disembunyikan (QR hilang, mode Pindahkan); `SetBottom` mengikuti layar |
| `UI/CatalogScreen.cs` | 161 | Layar katalog per kategori (akar transparan di atas `Backdrop`), kartu kaca bergambar; Scan QR & Pengaturan di `BottomNav` |
| `UI/DetailSheet.cs` | 187 | Lembar kaca detail di 3D Viewer: nama, nama lokal, Asal/Era, ringkasan (ketuk pegangan), CTA Scan QR (AR), Letakkan di Meja (ARCore), Tampilkan QR; `HeightChanged` untuk framing kamera |
| `UI/BottomNav.cs` | 97 | Bar navigasi kaca nila: Koleksi · Scan QR (lingkaran terakota menonjol) · Pengaturan |
| `UI/TopBar.cs` | 62 | Tombol kembali bulat + judul tengah (dalam pil kaca di atas kamera) |
| `UI/CoachCard.cs` | 124 | Kartu panduan kamera: versi besar ber-ikon animasi (kompas, bingkai scan) dan pil petunjuk yang bisa pudar sendiri |
| `UI/ControlPanel.cs` | 117 | Panel skala (slider logaritmik 0,5–3×, sinkron dengan cubit) + saklar + tombol lebar opsional untuk Scan QR / AR Meja |
| `UI/MessageDialog.cs` | 56 | Dialog kaca (izin kamera, AR tidak tersedia, kamera tidak ditemukan) |
| `UI/Backdrop.cs` | 74 | Latar scene Main yang digambar kamera (Screen Space-Camera): gradasi gading + gumpalan terakota/nila + motif kawung samar |
| `UI/HoldButton.cs`, `UI/SwitchToggle.cs`, `UI/Segmented.cs` | 67 / 83 / 65 | Tombol tahan-tekan (putar/miring), saklar geser, kontrol bersegmen |
| `UI/ProceduralTextures.cs` | 120 | Tekstur latar, petak kawung, glow radial, grid bidang AR (runtime & editor) |
| `UI/MarkerCardScreen.cs` | 65 | Menampilkan kode QR artefak layar penuh (bisa di-scan dari HP lain) |
| `UI/SettingsScreen.cs` | 154 | Kartu kaca: Bahasa (bersegmen), Suara (slider narasi, musik latar, efek), Privasi (saklar analitik), Ulangi tutorial, Tentang, lalu Kredit musik (judul, pembuat, sumber, lisensi tiap artefak yang punya musik) |
| `UI/OnboardingScreen.cs` | 107 | Tutorial gestur + keselamatan AR dengan ikon per halaman (persetujuan analitik hanya di Pengaturan) |
| `UI/LocalizedLabel.cs` | 35 | Label TMP yang otomatis berganti saat `Locale.Changed` |
| `UI/SafeArea.cs` | 31 | Menyesuaikan rect ke `Screen.safeArea` (notch) |

### Rendering (`NusantaraAR.Rendering`) — kaca buram

| File | Baris | Isi |
|---|---|---|
| `Rendering/GlassBlurFeature.cs` | 181 | `GlassBlur` (hitungan elemen kaca aktif, material UI, global `_GlassBlurTex`) dan `GlassBlurFeature` (Render Graph, `AfterRenderingTransparents`): warna kamera → turun 1/2…1/8 → naik ke 1/4 (dual-Kawase) → RTHandle persisten. Hanya jalan bila ada kaca aktif |
| `Shaders/GlassBlur.shader` | — | Pass Down / Up / Final (saturasi) untuk Blitter |
| `Shaders/UIGlass.shader` | — | Turunan UI-Default: sampel `_GlassBlurTex` di koordinat layar dari posisi clip (`ComputeScreenPos`; vertex kanvas Overlay ada di ruang kanvas, bukan piksel), campur dengan warna vertex sesuai kekuatan tint UV1. Diverifikasi dengan tangkapan `12_glass_probe` di build QA |

Alur satu frame: kamera merender model 3D + latar kamera (`Backdrop` / feed Scan QR / gambar ARCore) → `GlassBlurFeature` mem-blur warna kamera ke RT persisten → kanvas Overlay digambar; setiap Image ber-`GlassSurface` menampilkan RT itu (bagian tepat di belakangnya) dicampur tint gading/nila. Blur hanya menangkap apa yang digambar kamera, bukan UI lain di bawah panel.

### Audio (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Audio/AudioManager.cs` | 193 | Singleton `DontDestroyOnLoad` (`AudioManager.Instance`) dengan tiga `AudioSource`: **narasi** (play/pause/resume/stop/seek/progress, dijeda saat aplikasi ke latar), **SFX** (`Click` prosedural, diredam ke 35% selama narasi), dan **musik latar** (loop). `PlayMusic(clip)`: klip yang sama tidak dimulai ulang antarscene (detail → Scan QR → kembali), klip lain menggantikan dengan fade 0,8 dtk, `null`/`StopMusic` = fade lalu berhenti. Musik diredam ke 30% selama narasi dan di-fade 2 dtk di batas loop (`LoopEnvelope`) agar tidak berbunyi klik. Volume dari `AppSettings` |

---

## 6. Algoritma Scan QR dan kartu penanda (tanpa ARCore)

Mode ini ada karena HP target (Samsung Galaxy A05) **tidak didukung ARCore**. Semuanya berjalan di CPU dengan kamera biasa dan tidak memakai library eksternal.

Sejak revisi "Scan QR", penanda utama adalah **kode QR** berisi `NUSANTARA:<artifactId>` (§6.5). Kode kartu penanda 6×6 (§6.1-6.2) **tetap disimpan** dan dipakai sebagai cadangan di scene yang sama: satu analisis frame (`DarkRegions`) dipakai kedua detektor, dan kartu hanya dicari bila tidak ada QR yang dikenali. Pose, penghalus, gestur, dan tombol Kunci (§6.3-6.4) sama untuk keduanya.

### 6.1 Format marker (`MarkerPattern`)
- Grid 6×6 sel. Sel tepi berupa **bingkai hitam** 1 sel, dan bagian dalamnya 4×4 = **16 bit data** (1 = hitam), dibaca baris demi baris dari kiri atas (bit 15 = sel data kiri atas).
- Kartu dicetak dengan zona putih (quiet zone) di sekelilingnya. Sisi kotak hitam dicetak 80 mm (`markerSizeMeters = 0.08`).
- Syarat kode yang dipilih:
  - Keempat rotasi satu kode saling berbeda jauh (Hamming ≥ 10), sehingga orientasi kartu tidak ambigu.
  - Antar kode artefak berjarak ≥ 5 di semua rotasi.
  - Kedua syarat dicek oleh `MarkerTests`.

### 6.2 Deteksi (`DarkRegions.Analyze` + `MarkerDetector.Detect`)
Langkah 1-4 ada di `DarkRegions` (dipakai bersama detektor QR); langkah 5-6 di `MarkerDetector`.
1. **Integral image** dari buffer abu-abu.
2. **Threshold adaptif**: piksel dianggap gelap bila nilainya < rata-rata jendela (2·12+1)² dikurangi 8.
3. **Pelabelan komponen** gelap dengan konektivitas 8 (flood fill memakai stack eksplisit), sambil mencatat bounding box dan titik berat. Komponen yang terlalu kecil atau menyentuh tepi gambar dibuang.
4. **Titik batas → convex hull** (monotone chain), lalu **segi empat**:
   - Diagonal diambil dari pasangan titik hull terjauh.
   - Dua sudut lainnya adalah titik terjauh dari diagonal di tiap sisi.
   - Kandidat ditolak bila luas segi empat / luas hull < 0,88 (bukan persegi) atau ada sisi yang terlalu pendek.
5. **Decode di 4 rotasi**:
   - Hitung homografi dari persegi satuan ke 4 sudut.
   - Ambil sampel 3×3 titik per sel (bilinear), lalu threshold sel = tengah antara min dan max.
   - Bingkai boleh salah ≤ 1 sel, dan kode cocok bila jarak Hamming ke kode yang dicari ≤ 1.
6. Hasil berupa `MarkerDetection { code, corners[TL,TR,BR,BL] }`. Duplikat di posisi yang sama dibuang.

Ukuran gambar minimum 32×32 px, dan sisi marker minimum 24 px.

### 6.3 Pose (`MarkerPose`)
- `TryEstimate`: homografi model (persegi `size` meter) → piksel. Dengan intrinsik pinhole (`f = feed.FocalPixels`, pusat = tengah gambar), didapat `K⁻¹H = [r1 r2 t]` (metode Zhang), dinormalisasi. Hasilnya posisi 4 sudut di ruang kamera Unity (y dibalik dari konvensi CV).
- `ArtifactPose` (bingkai QR/kartu mentah):
  - Sumbu atas = normal kartu yang menghadap kamera.
  - +Z = arah tepi atas kartu, sehingga muka artefak (-Z) menghadap pengguna.
- `UprightPose` (yang dipakai `MarkerController`): artefak **selalu tegak menurut gravitasi** (`DeviceGravity`: `GravitySensor`, atau `Accelerometer` yang diredam; tanpa sensor/Editor → atas layar = atas dunia). Menangani beberapa sudut scan:

  | QR di… | Kemiringan QR | Hasil |
  |---|---|---|
  | meja, HP miring ±45° | < 30° | Artefak berdiri di atas QR (alas di QR), muka ke pengguna |
  | meja, HP tegak lurus di atas | < 30° | Sama; terlihat dari atas → geser tegak untuk memiringkan dan melihat sisi depan |
  | layar monitor/HP lain, dinding, kartu dipegang tegak | > 65° | Artefak tegak, dipusatkan di depan QR, muka menghadap keluar dari QR |
  | layar laptop condong, buku di penyangga | 30°–65° | Peralihan halus (`wallWeight`) antara dua posisi di atas |

  - Arah depan (+Z) = proyeksi mendatar tepi atas QR **ditambah** proyeksi mendatar kebalikan normal QR. Keduanya searah pada QR yang condong ke belakang menghadap pengguna, jadi tidak ada lompatan di kemiringan mana pun.
  - `yawToViewer`: di meja, artefak diputar kelipatan 90° agar muka menghadap pengguna walau QR diletakkan miring/terbalik. Diambil sekali saat QR mulai terlihat (atau direbahkan kembali, dengan histeresis 0,3/0,7), lalu tetap menempel pada QR.
- `PoseSmoother`: filter **One Euro** untuk posisi dan rotasi. Pose tidak bergetar saat diam dan tidak tertinggal saat bergerak cepat.

### 6.4 Loop `MarkerController`
```
Start: kumpulkan markerCode dari katalog → latar kamera disembunyikan → izin kamera → StartCamera
       (dijaga cameraStarting: callback izin & OnApplicationFocus tidak membuka kamera dua kali)
       → tunggu kamera (≤1,5 s) → CameraFeed.StartFeed() (melepas WebCamTexture lama dulu)
Update:
  tidak ada frame > 3 s → buka ulang kamera (maks. 2 kali) → lalu pesan CameraStalled + Coba Lagi
  feed.Grab() ada frame baru → ConfigureDisplay (latar kamera baru ditampilkan) → ProcessFrame:
      regions.Analyze (sekali) → FindTarget:
          1) QrDetector.Detect → FindByQr(text)
             (QR terlihat tapi tak terbaca & QR terakhir terbaca < 2 s lalu → tetap artefak itu)
          2) bila tidak ada QR dikenali dan detectLegacyCards: MarkerDetector.Detect → FindByMarker(code)
      → EnsureArtifact(data, size)  (ganti prefab bila QR/kartu lain)
      → TryEstimate(sudut, qrSizeMeters | markerSizeMeters) → UprightPose(gravitasi) → yawToViewer → smoother.Filter → lastSeen = now
  State: Searching ⇄ Tracking (hilang bila > 0,6 s tak terlihat) | Locked (pose dibekukan)
  ApplyPose: rotasi = kamera ∘ poseMarker ∘ rotasi pengguna; posisi: pusat bounds artefak di Lerp(alas di QR, tepat di depan QR, wallWeight);
             skala = baseScale·userScale. Putar Otomatis disalurkan ke rotasi pengguna (pose ditimpa tiap frame).
```
- `baseScale` diatur agar lebar artefak kira-kira 2,2 kali sisi QR/kartu (`fitToMarker`), jadi keris tampil "di atas QR", bukan 1:1.
- Gestur: geser mendatar = putar pada sumbu tegak, geser tegak = miringkan pada sumbu kanan layar (ala trackball, berporos di pusat artefak), pinch untuk skala (0,5×–3×). Reset Tampilan mengembalikan rotasi & skala.
- Tombol **Kunci** menahan pose sehingga QR/kartu boleh dijauhkan dari kamera.
- `targetFrameRate = 30` untuk menghemat CPU dan panas pada HP entry-level.

### 6.5 Kode QR (`QrCode`, `QrDetector`)
- **Isi**: `ContentCatalog.QrPrefix + artifactId`, mis. `NUSANTARA:KERIS_BALI_01` (`ArtifactData.QrText`). `FindByQr` juga menerima artifactId tanpa awalan. Tidak ada field baru di aset, jadi artefak baru otomatis punya QR.
- **Cetak**: `Tools/kartu_qr.py` (pustaka Python `qrcode`, ECC M, versi 2 = 25 modul) → `Docs/KartuQR_*_A5.pdf/png`, sisi QR 8 cm (`qrSizeMeters = 0.08`) + zona putih 4 modul. Di aplikasi, tombol **Tampilkan QR** membuat QR dengan isi sama lewat `QrCode.Encode` (mode byte, ECC M, versi ≥ 2 agar ada pola alignment).
- **Encoder/decoder** (`QrCode`): versi 1-10, semua tingkat ECC, 8 mask. Format dibaca dari kedua salinan dengan pencocokan BCH terdekat (≤ 3 bit salah); versi ≥ 7 dicek dari blok versi. Tiap blok dikoreksi Reed-Solomon (sindrom → Berlekamp-Massey → Chien → Forney), lalu segmen numerik/alfanumerik/byte/ECI diurai. Encoder identik bit-per-bit dengan pustaka Python `qrcode` (`QrTests`).
- **Deteksi** (`QrDetector`):
  1. Dari komponen gelap `DarkRegions`, cari **cincin luar pola finder** (7×7): segi empat dengan sudut subpiksel (garis tiap sisi di-fit, tahan sudut membulat karena blur), lalu grid 7×7 di dalamnya harus berpola cincin gelap / cincin terang / inti 3×3 gelap (≤ 3 sel salah). Ambang finder = tengah rata-rata sel gelap dan terang.
  2. Pilih tiga finder yang membentuk **siku sama kaki** (ukuran modul mirip, kosinus sudut kecil, rasio kaki dekat 1). Sudut siku = kiri-atas; kanan-atas/kiri-bawah dari tanda perkalian silang.
  3. Perkirakan ukuran N modul dari jarak antarfinder; coba versi v, v+1, v-1.
  4. **Homografi kuadrat terkecil** (normalisasi Hartley) dari 12 sudut luar ketiga finder; untuk versi ≥ 2 ditambah pusat **pola alignment** kanan-bawah (titik gelap 1 modul) agar perspektif sudut kanan-bawah akurat.
  5. Sampel 3×3 titik per modul; ambang = bidang yang melalui ambang ketiga finder (tahan gradasi cahaya) → `QrCode.TryDecode`.
  6. Hasil `QrDetection { text, corners[TL,TR,BR,BL], version }`. Bila pola QR terlihat tetapi isinya tak terbaca, `text = null` dengan sudut tetap valid (pelacakan tidak putus saat frame buram).
- Batas yang teruji (gambar sintetis): modul ≥ ~2,7 px di buffer deteksi 360 px (kira-kira QR 8 cm dari jarak ±50 cm), kemiringan kuat (sisi atas setengah sisi bawah), gradasi cahaya 35-100%.

---

## 7. Pipeline editor (`NusantaraAR.EditorTools`)

### 7.1 Menu
| Menu | Method | Fungsi |
|---|---|---|
| Nusantara AR / Setup Everything | `ProjectSetup.RunAll` | Membangun seluruh proyek (lihat 7.2) |
| Nusantara AR / Build Keris Bali | `KerisBaliBuilder.BuildMenu` | Mengimpor ulang `keris_bali.glb`, membangun prefab + konten + thumbnail |
| Nusantara AR / Build Keris Sumatra | `KerisSumatraBuilder.BuildMenu` | Mengimpor ulang `keris_sumatra.glb`, membangun prefab + konten + thumbnail |
| Nusantara AR / Build Candi Borobudur | `CandiBorobudurBuilder.BuildMenu` | Mengimpor ulang `candi_borobudur.glb`, membangun prefab 1:200 + konten + thumbnail |
| Nusantara AR / Bangun Kisah | `StoryBuilder.BuildMenu` | Membangun `<ID>_Story.asset` dari `Tools/narasi/kisah.json` + MP3 + `kisah_cues.json`, lalu memasangnya ke `ArtifactData.story` (lihat 7.6) |
| Nusantara AR / Pasang Musik Latar | `MusicBuilder.BuildMenu` | Memasang `backgroundMusic` + `musicCredit` dari `Tools/musik/musik.json` (lihat 7.6) |
| Nusantara AR / Render Stage Previews | `PreviewRenderer.Render` | Merender PNG tiap tahap exploded setiap artefak ke `Previews/{id}_stage_N.png` (untuk QA visual) |
| Nusantara AR / Render Thumbnails | `ProjectSetup.RenderThumbnails` | Merender ulang thumbnail katalog semua artefak tanpa menjalankan Setup Everything |
| Nusantara AR / Build AR Visuals | `ProjectSetup.BuildVisualAssets` | Tanpa menyentuh scene/prefab: reticle terakota, grid bidang AR (`T_PlaneGrid`), glow (`T_Glow`, `Resources/GroundGlow`), material UI kaca (`Resources/UIGlass`), dan `GlassBlurFeature` di setiap renderer URP |
| Nusantara AR / Build / Android APK (uji perangkat) | `BuildScript.BuildAndroidApk` | `Builds/Android/NusantaraAR.apk` (pindah ke platform Android dulu bila perlu) |
| Nusantara AR / Build / Android App Bundle (.aab) | `BuildScript.BuildAndroidAab` | `Builds/Android/NusantaraAR.aab` |
| *(batch saja)* | `BuildScript.BuildWindowsCapture` | Build QA Windows `Builds/QA/NusantaraAR.exe` dengan `DevCapture`; platform aktif dikembalikan setelahnya |

Versi batch (tanpa GUI): `ProjectSetup.RunBatch`, `ProjectSetup.BuildVisualAssetsBatch`, `ProjectSetup.RenderThumbnailsBatch`, `PreviewRenderer.RenderBatch`, `StoryBuilder.BuildBatch`, `MusicBuilder.BuildBatch`, dan `BuildScript.*`. Semuanya dipanggil lewat `-executeMethod`. Render (thumbnail, preview, QA) butuh GPU, jadi **jangan** pakai `-nographics`.

### 7.2 Urutan `ProjectSetup.RunAll` (idempoten)
1. `EnsureFolders`: membuat folder Art/Content/Resources/Prefabs/Scenes.
2. `ImportTmpEssentials`: mengimpor TMP Essential Resources bila belum ada.
3. `ConfigurePlayer`: package id, IL2CPP ARM64, SDK, orientasi, izin kamera, dan setelan Android lainnya.
4. `ConfigureXR`: loader ARCore/ARKit diset **Optional**.
5. `ConfigureURP`: menambahkan `ARBackgroundRendererFeature` ke renderer URP.
6. `KerisBaliBuilder.Build()`, `KerisSumatraBuilder.Build()`, lalu `CandiBorobudurBuilder.Build()`: impor GLB Blender, prefab modular, tahap exploded, hotspot, dan konten draf, lalu mendaftarkannya ke katalog.
7. `StoryBuilder.Build()` lalu `MusicBuilder.Build()`: memasang aset Kisah dan musik latar ke `ArtifactData` (dijalankan **setelah** builder artefak karena `ArtifactData` dibuat ulang di langkah 6; lihat 7.6).
8. `PruneCatalog`: katalog hanya berisi artefak model Blender (Bali, Sumatra, Borobudur), berurutan. Keris tetap di depan (DevCapture memakai artefak pertama).
9. `BuildCommonAssets`: material reticle dan `ARPlane.prefab`.
10. `BuildMainScene`, `BuildARScene`, `BuildMarkerScene`: kamera, cahaya, EventSystem (Input System UI module), controller, dan referensinya.
11. `RenderThumbnail(dataPath, thumbPath)` untuk tiap artefak (600×740 px, `Content/<ID>/<ID>_thumb.png`).
    - Parameternya **path**, bukan instance, karena `ArtifactData` bisa sudah di-unload setelah pergantian scene.
    - Artefak di-`Init` dan di-`SnapTo(0)` (utuh). Jarak kamera memakai FOV horizontal sebenarnya, dan near/far clip dihitung dari bounds.
    - Kamera merender **dua kali**. Render pertama di scene baru URP bisa kosong karena shader/tekstur belum siap; dulu ini menghasilkan thumbnail abu-abu polos.
    - Bila hasilnya satu warna (`IsUniform`), file lama **tidak ditimpa** dan muncul exception. Bounds kosong juga memicu exception.
    - Importer diset `npotScale = None`. Tanpa itu Unity membulatkan 600×740 menjadi 512×512, sehingga gambar gepeng dan ada pita kosong di katalog.
12. Mengisi Build Settings dengan urutan `Main`, `AR`, `Marker`, lalu `SaveAssets`.

### 7.3 Model Blender → GLB → prefab (`GlbArtifact`, `KerisBaliBuilder`, `KerisSumatraBuilder`, `CandiBorobudurBuilder`)
- **Sumber model**: skrip Blender 5.2 tanpa GUI di `Tools/blender/` membangun geometri, tekstur PBR (warna, ORM, normal map), dan material, lalu mengekspor GLB (tekstur tertanam) ke `Art/KerisBali/keris_bali.glb` dan `Art/KerisSumatra/keris_sumatra.glb`:
  ```bash
  "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_bali.py
  "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_sumatra.py
  ```
  Tambahkan `-- --no-render` untuk melewati render pratinjau (`Docs/KerisBali_Blender_*.png`, `Docs/KerisSumatra_Blender_*.png`). `keris_bali.py` dan `candi_borobudur.py` memakai utilitas dari `keris_sumatra.py`.
  Skrip candi kebalikannya: bawaannya hanya menyimpan `.blend` (tekstur di-pack); `-- --export` menulis `Art/CandiBorobudur/candi_borobudur.glb`, `-- --render` merender pratinjau Cycles dari 4 kamera.
- **Impor**: paket **glTFast** (`com.unity.cloud.gltfast`) mengimpor GLB sebagai model beserta mesh, material (shader graph glTF PBR untuk URP), dan tekstur. Animasi GLB `Cabut_Keris` diabaikan; aplikasi memakai `ExplodedViewController`.
- **`GlbArtifact.Load`**: instansiasi GLB, kelompokkan node Blender ke `ArtifactPart` lewat tabel nama (`Warangka_Sampir` + `Warangka_Celah` → `Warangka`, `Permata_Selut` → `Selut`, dst.; node tanpa bagian memicu exception), pasang pivot bagian di titik sambung, tambah `MeshCollider` (oklusi hotspot), dan geser model agar dasar alas di y = 0 dan pusat alas di XZ = 0.
- **Sumbu**: Blender (x, y, z) → ruang Model Unity (x, z, y); muka depan Blender (-Y) menjadi -Z. Arah pencerminan glTF→Unity diperiksa dari sisi +X satu bagian acuan (hulu Bali / warangka Sumatra); bila terbalik, model diputar 180° di Y. Candi simetris empat arah memakai `rightPart = null` (pemeriksaan dilewati, hasil deterministik).
- **`GlbArtifact.SetStages` / `Move` / `Front`**: tahap exploded dari offset kumulatif per bagian; hotspot diletakkan `gap` (bawaan 3 mm) di depan permukaan terdepan (-Z) bagian pada koordinat (x, y) tertentu, dengan sinar dari z = −`reach` (bawaan 2 m). Keris dihitung pada pose "dihunus"; candi pada pose utuh dengan `reach` 100 m dan `gap` 0,6 m (= 3 mm setelah 1:200). Hotspot di luar bagian memicu exception.
- **Artefak besar**: `ArtifactData.prefab` berskala nyata, tetapi candi 123 m tidak muat di kamera mana pun (far clip viewer 30 m, AR 20 m). `CandiBorobudurBuilder` memperkecil **child `Model`** ke 1:200 setelah hotspot dihitung; root tetap skala 1 karena `MarkerController` menghitung skala kartu dari ukuran dunia prefab dan slider AR memakai skala root. Pose exploded dan hotspot relatif terhadap bagiannya, jadi ikut terskala. MeshCollider arca & kisi stupa (±220 ribu segitiga) dilepas agar pemuatan di HP ringan; oklusi hotspot cukup memakai collider teras.
- **`GlbArtifact.Save`**: prefab di `Content/<ID>/`, `ArtifactData` (+ katalog). Konten draf (`Fill`) ada di file builder masing-masing.
- Bila model Blender diubah: jalankan skrip Blender, lalu menu **Build Keris Bali / Build Keris Sumatra / Build Candi Borobudur** (atau `Tools/compile_and_test.ps1`).

### 7.4 Konten draf
`KerisBaliBuilder.Fill` dan `KerisSumatraBuilder.Fill` menulis judul, istilah daerah, bahan, teknik, filosofi, dan sejarah ke setiap hotspot lewat `GlbArtifact.Hotspot`. Isi teks sudah diperiksa terhadap sumber daring (UNESCO, Wikipedia, jurnal, media, situs perkerisan; 28-09-2026). Rujukannya dikumpulkan di `KerisRefs` dan tampil sebagai "Sumber rujukan" di kartu info. Tab Filosofi/Sejarah yang belum punya isi otomatis diberi penanda "[Draf] Diisi kurator…". Istilah Keris Sumatra memakai istilah Melayu/Palembang (mis. pendongkok untuk mendak, sampir untuk warangka). `CandiBorobudurBuilder.Fill` mengikuti pola yang sama dengan rujukan di `CandiRefs` (Wikipedia EN/ID, Kompas 2024, dll.; diperiksa 01-10-2026); ukuran yang tidak bersumber disebut "perkiraan". Semua hotspot tetap `curatorValidated = false` sampai divalidasi kurator.

### 7.5 Build dan pengaman platform (`BuildScript`, `BuildGuard`)
**Masalah yang dicegah:** ARCore diinisialisasi sejak aplikasi dibuka (`InitManagerOnStart = true` di `ConfigureXR`), jadi **semua** kamera, termasuk 3D Viewer di scene Main, dirender lewat jalur XR URP. URP menghitung *shader prefiltering* (varian mana yang dibuang) dari **platform aktif** editor, bukan dari target build. Bila APK di-build saat platform aktif masih Standalone (mis. sisa build QA Windows), `Mobile_RPAsset.m_PrefilterXRKeywords` menjadi `1`. Varian XR dibuang, dan model 3D **tidak tampil di HP** padahal build "sukses". Satu-satunya jejak di log: pesan ARCore *"Cannot get path to the Gradle launcher unless the active build platform is Android"*.

Pengaman berlapis:
- `BuildScript.Build` (APK/AAB) memanggil `SwitchActiveBuildTarget(Android)` bila platform aktif bukan Android.
- `BuildScript.BuildWindowsCapture` pindah ke Windows dulu, lalu mengembalikan platform semula di `finally`.
- `BuildGuard` (`IPreprocessBuildWithReport`, `callbackOrder` 1000, setelah URP mengisi data prefiltering) berjalan di **setiap** build, termasuk menu, Build Profiles, dan batch. Build dihentikan (`BuildFailedException`) bila:
  1. target build ≠ platform aktif, atau
  2. platform target punya loader XR aktif (ARCore/ARKit) tetapi aset URP miliknya (level kualitas platform itu, lewat `QualitySettings.GetRenderPipelineAssetsForPlatform`) membuang varian XR.
- `PC_RPAsset` memang membuang varian XR. Itu benar karena aset ini hanya dipakai level kualitas PC, jadi tidak ikut dicek untuk Android.

Build batch dari command line tetap disarankan memakai `-buildTarget Android` agar Unity langsung terbuka di platform yang benar (tanpa impor ulang aset di tengah build).

### 7.6 Narasi Kisah dan musik latar (`StoryBuilder`, `MusicBuilder`)
Aset audio dibuat oleh skrip Python di `Tools/`, lalu dipasang ke konten oleh builder editor. Keduanya menulis ke field/aset yang **tidak** disentuh builder keris, sehingga tetap terpasang saat model dibangun ulang.

**Mode Kisah**
1. Naskah: `Tools/narasi/kisah.json`, berisi suara (`voiceID` = `id-ID-GadisNeural`, `voiceEN` = `en-US-AvaNeural`) dan per artefak 9 bab (`key`, `titleID/EN`, teks `id`/`en`, `stage`, `focus` = hotspotId yang disorot).
2. Suara: `python Tools/narasi/kisah_tts.py [--force]` (paket `edge-tts`, perlu internet) menulis `Content/<ID>/Story/<ID>_<nn>_<key>_<id|en>.mp3` dan waktu mulai tiap kalimat ke `Tools/narasi/kisah_cues.json`. Bab yang tidak berubah dilewati (cache hash).
3. Unity: **Bangun Kisah** (atau Setup Everything) membuat `Content/<ID>/Story/<ID>_Story.asset` (`ArtifactStory`) dan memasangnya ke `ArtifactData.story`. Bila naskah tidak ada, builder hanya memberi peringatan.

**Musik latar**
1. Daftar trek: `Tools/musik/musik.json` (per artefak: berkas mentah, berkas tujuan, `maxSeconds`, judul, pembuat, sumber, URL, lisensi, `aiGenerated`).
2. Pixabay memblokir unduhan otomatis, jadi unduh tiap trek manual ke `Tools/musik/asli/` (tidak di-commit). Lalu `python Tools/musik/siapkan_musik.py` (butuh `ffmpeg`/`ffprobe`) menyamakan kenyaringan ke -20 LUFS, memotong ke `maxSeconds` dengan fade-out 3 dtk, dan menulis `Content/<ID>/Music/<ID>_music.mp3`.
3. Unity: **Pasang Musik Latar** (atau Setup Everything) mengisi `backgroundMusic` + `musicCredit`. Klip diimpor sebagai Vorbis ter-stream (dicek `MusicTests`).

Trek saat ini: "Gamelan Bali Yang Tenang" (LunarBoomMusic) untuk Keris Bali dan "Self-Sacrifice" (Strainsofpoise, gambus Melayu) untuk Keris Sumatra. Keduanya dari Pixabay (Pixabay Content License) dan ditandai hasil AI oleh pengunggahnya. Trek Candi Borobudur tercatat di `Tools/musik/musik.json`.

---

## 8. Pengujian

Jalankan semuanya dengan satu perintah (Unity Editor harus **ditutup** lebih dulu):

```bash
powershell -ExecutionPolicy Bypass -File Tools\compile_and_test.ps1
```

Skrip ini menjalankan `ProjectSetup.RunBatch` (termasuk Bangun Kisah dan Pasang Musik Latar dari MP3 yang sudah ada), lalu EditMode test, lalu `Tools/kartu_qr.py` dan `Tools/kartu_penanda.py` (kartu QR + kartu penanda lama, PDF/PNG Keris Bali, Keris Sumatra, Candi Borobudur). Log disimpan di `Logs/`.

| File | Yang diuji |
|---|---|
| `ArtifactTests.cs` | Dijalankan untuk **setiap** artefak (kelas dasar `ArtifactFixture`): katalog berisi tepat Keris Bali + Sumatra + Borobudur dengan kategori KerisSenjata + Candi; ditemukan lewat kode kartunya dan lewat isi QR-nya (termasuk QR hasil `QrCode.Encode`), kode unik, kartu lama (EEC1, DA26) tidak dikenali; semua mesh berasal dari GLB Blender; hotspot menunjuk bagian yang ada dan berada di muka depan; hotspot `Utuh` tampil saat utuh dan semua tampil di tahap 1; setiap tahap menggerakkan bagian; pivot di dasar; clamp skala relatif; fallback `LocalizedString`. Kelas `KerisDrawTests` (keris saja): bilah tersembunyi di sarung, tampil saat dihunus, dan jalur hunus tidak menembus sarung |
| `BorobudurTests.cs` | Root skala 1 dan child `Model` 1:200; tapak ±61,5 cm, tinggi 17,5 cm, pusat di titik letak; kategori Candi, kode E3B1, 10 tingkat, 10 hotspot `Utuh`, 1 + 4 tahap tanpa tombol Hunus; di tahap terakhir setiap tingkat melayang di atas tingkat bawahnya |
| `KerisBlenderTests.cs` | Bali: kode B532, 11 hotspot, 1 + 5 tahap, jagrak 45 cm di y = 0, keris bersandar di atasnya dengan hulu di +X, bilah 40 + 8 cm, bilah terhunus di atas sarung, tahap terakhir melepas pendok. Sumatra: kode F0E4, 9 hotspot, 1 + 4 tahap, tinggi ±52 cm di dudukan, sampir di +X, bilah 36 + 7,2 cm, bilah terhunus di samping sarung |
| `UiThemeTests.cs` | Kontras WCAG AA token warna, termasuk teks di atas kaca pada latar kamera terburuk (hitam & putih, dicampur di ruang linear); setiap `Icon` menghasilkan bentuk yang terlihat dan tidak menyentuh tepi; slider skala logaritmik & bolak-balik; aset kaca/AR sudah dibangun (`UIGlass`, `GroundGlow`, `GlassBlurFeature` di semua renderer) |
| `MarkerTests.cs` | Deteksi marker perspektif di 4 orientasi (gambar sintetis); tidak ada false positive pada noise; pose cocok dengan transform yang diketahui; pemetaan rotasi buffer kamera; keunikan rotasi kode & jarak antar kode; memilih artefak yang benar saat semua kode dicari; mendeteksi **PNG kartu cetak** asli di `Docs/` dengan cukup cepat |
| `StoryTests.cs` | Dijalankan untuk setiap artefak: setiap bab punya suara + subtitle di kedua bahasa; klip narasi terkompresi, mono, dan dimuat saat dibutuhkan; `stage` bab valid dan `focusHotspot` menunjuk hotspot yang tampil di tahap itu; Kisah memperlihatkan bongkar (keris: juga hunus) dan berakhir dirakit kembali; `SplitSentences` membagi waktu sepanjang teks |
| `MusicTests.cs` | Dijalankan untuk setiap artefak: ada musik latar berkredit; klip di-stream sebagai Vorbis; `LoopEnvelope` fade di kedua ujung |
| `QrTests.cs` | Encoder identik bit-per-bit dengan pustaka Python `qrcode`; decoder membaca QR Python mode byte & campuran; round-trip semua tingkat ECC dan versi 1-10; koreksi Reed-Solomon (kerusakan kecil terkoreksi, kerusakan besar ditolak); deteksi perspektif di 4 orientasi dengan galat sudut < 2,5 px; jauh/dekat; miring kuat dan gradasi cahaya; QR tak terbaca tetap memberi pose; tanpa false positive (noise, kartu lama) dan QR tidak terbaca sebagai kartu; pose cocok dengan transform yang diketahui; mendeteksi **PNG kartu QR cetak** di `Docs/` dengan cukup cepat (analisis + QR + kartu per frame) |

---

## 9. Cara memperluas

### Menambah artefak baru
1. Modelkan di Blender dengan skrip (pola: `Tools/blender/keris_sumatra.py`): satu objek per bagian, origin di titik sambung, 1 unit = 1 m, muka depan -Y; ekspor GLB ke `Art/<Nama>/`.
   Lalu tulis builder editor (pola: `KerisSumatraBuilder`, atau `CandiBorobudurBuilder` untuk benda besar yang perlu diperkecil) yang memanggil `GlbArtifact.Load` → `SetStages` → `Front` → `Save`.
2. `GlbArtifact.Save` membuat `ArtifactData` dan mendaftarkannya ke katalog; tambahkan ID-nya ke `ProjectSetup.PruneCatalog`.
3. Tambahkan ID ke `[TestFixture]` di `ArtifactTests`, `StoryTests`, dan `MusicTests`; beri kode kartu 6x6 (`MarkerPattern`, `MarkerTests.AllCodes`, `Tools/kartu_penanda.py`) karena `ArtifactTests` mewajibkannya.
4. **Supaya perubahan tidak hilang**, panggil builder-nya dari `ProjectSetup.RunAll` beserta `RenderThumbnail`-nya.
5. Tambahkan test khusus (pola: `KerisBlenderTests`, `BorobudurTests`).

### Kode QR artefak baru
Tidak perlu kode tambahan: isi QR diturunkan dari `artifactId`. Tambahkan baris di tabel `KARTU` pada `Tools/kartu_qr.py` untuk kartu cetaknya, dan `TestCase` baru di `Detects_PrintableQrCard_AndIsFastEnough`.

### Menambah kartu penanda (lama, opsional)
1. Pilih kode 16-bit baru dan tambahkan konstantanya di `MarkerPattern`.
2. Pastikan lulus `MarkerCode_RotationsAreDistinct` dan `MarkerCodes_AreFarFromEachOther_InEveryRotation`. Tambahkan kode baru ke kedua test tersebut.
3. Set `ArtifactData.markerCode`. `MarkerController` otomatis mengambil semua kode dari katalog.
4. Tambahkan baris baru di tabel `KARTU` pada `Tools/kartu_penanda.py`. Tambahkan juga `TestCase` baru di `Detects_PrintableCard_AndIsFastEnough`.

### Kisah dan musik untuk artefak baru
- Tambahkan entri artefak (9 bab atau berapa pun) di `Tools/narasi/kisah.json`, jalankan `kisah_tts.py`, lalu **Bangun Kisah**. `focus` harus hotspotId yang tampil pada `stage` bab itu (dicek `StoryTests`).
- Tambahkan trek di `Tools/musik/musik.json`, unduh ke `Tools/musik/asli/`, jalankan `siapkan_musik.py`, lalu **Pasang Musik Latar**.
- Tambahkan ID ke `[TestFixture]` di `StoryTests` dan `MusicTests`.

### Menambah teks UI / bahasa
- Kunci baru ditambahkan di `UIStrings`, lalu dipakai lewat `Locale.T("kunci")` dan `LocalizedLabel.Attach(label, "kunci")` agar teksnya ikut berganti bahasa.
- Konten artefak memakai `LocalizedString` di aset, bukan `UIStrings`.

---

## 10. Batasan dan hal yang perlu diketahui

- **Konten kuratorial masih draf**: semua `curatorValidated = false`. Keris Bali dan Keris Sumatra dimodelkan di Blender dari cetak biru/lembar acuan, bukan dari spesimen museum; figur hulu Bali sangat disederhanakan.
- **Narasi**: mode Kisah sudah bersuara (TTS neural, ID/EN; naskah juga masih draf, `ArtifactStory.curatorValidated = false`). Narasi per hotspot (`narrationID/EN`) dan rekaman pelafalan masih kosong, sehingga pemutar di kartu info tampil tanpa klip.
- **Musik latar** berasal dari Pixabay dan ditandai hasil AI oleh pengunggah. Periksa lisensi dan kesesuaian budayanya sebelum rilis publik.
- **Analitik** hanya menulis ke log dan belum terhubung ke penyedia mana pun.
- **Scan QR / kartu**:
  - Jaraknya berupa perkiraan, karena FOV kamera diasumsikan, bukan hasil kalibrasi.
  - Deteksi butuh pencahayaan cukup dan QR/kartu yang rata.
  - Hanya satu target per frame yang dipakai (QR lebih dulu), sehingga hanya satu artefak tampil sekaligus.
  - Decoder QR mendukung versi 1-10 (hingga 57 modul); QR lebih besar tidak dibaca. Isi QR `NUSANTARA:<id>` bukan URL, jadi aplikasi kamera biasa hanya menampilkannya sebagai teks.
  - Belum diuji di perangkat nyata; batas jarak di §6.5 berasal dari gambar sintetis.
- **AR ARCore** tidak bisa diuji di Galaxy A05. Jalur tersebut hanya teruji di Editor, sebatas cabang "tidak didukung".
- **Aset hasil generator** (scene, prefab, katalog, GLB di Art/KerisBali dan Art/KerisSumatra) jangan diedit manual; ubah skrip Blender / kode editor lalu jalankan ulang.
- Tekstur GLB diimpor glTFast sebagai sub-aset; ukuran dan kompresinya mengikuti glTFast, bukan TextureImporter Unity (perhatikan ukuran APK dan memori di perangkat kelas bawah).
- APK `Builds/Android/NusantaraAR.apk` ±49,7 MB (dibangun 29-09-2026). Sebelum pengecilan ukurannya 56,2 MB; turun berkat stripping managed Medium, kompresi LZ4HC, normal map ASTC 5×5, dan pembuangan paket yang tidak terpakai.
- **Jangan build Android dengan platform aktif selain Android**; lihat §7.5. `BuildGuard` akan menghentikan build semacam itu, jadi jangan dihapus.
