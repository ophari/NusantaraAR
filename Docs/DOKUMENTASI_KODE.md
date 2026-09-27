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
| UI | uGUI + TextMeshPro, **dibuat seluruhnya dari kode** (`UIKit`), tanpa prefab UI dan tanpa layout di YAML scene |
| Bahasa kode | C#; komentar dan teks dalam Bahasa Indonesia |
| Jumlah kode | 43 file C#, ±7.450 baris (Runtime 33 file / 4.672 baris, Editor 7 / 2.361, Test 3 / 421) |

### Assembly dan namespace

| Assembly (`.asmdef`) | Folder | Namespace | Isi |
|---|---|---|---|
| `NusantaraAR.Runtime` | `Assets/NusantaraAR/Scripts/Runtime` | `NusantaraAR`, `NusantaraAR.UI`, `NusantaraAR.Marker` | Semua kode yang ikut ke APK |
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
│  │  └─ KerisSumatra/            keris_sumatra.glb (ekspor Blender)
│  ├─ Content/
│  │  ├─ KERIS_BALI_01/           .prefab + .asset (ArtifactData) + _thumb.png
│  │  └─ KERIS_SUMATRA_01/        .prefab + .asset + _thumb.png
│  ├─ Prefabs/                    ARPlane.prefab
│  ├─ Resources/                  ContentCatalog.asset (dimuat saat runtime)
│  ├─ Scenes/                     Main.unity, AR.unity, Marker.unity (DIHASILKAN oleh ProjectSetup)
│  ├─ Scripts/
│  │  ├─ Runtime/  AR/ Artifact/ Audio/ Content/ Core/ Interaction/ Marker/ UI/
│  │  └─ Editor/
│  └─ Tests/EditMode/
├─ Docs/        kartu QR + kartu penanda lama (PNG/PDF), draf laporan, dokumen ini
├─ Tools/       compile_and_test.ps1, kartu_qr.py, kartu_penanda.py, blender/keris_bali.py, blender/keris_sumatra.py (+ .blend, *_textures/)
├─ Builds/      output APK/AAB
├─ Logs/        setup.log, tests.log, tests.xml (dari Tools/compile_and_test.ps1)
└─ _Arsip_ModelLama/  model & builder lama (keris sementara, Keris Bali prosedural Unity, Keris Jawa) — tidak dipakai, boleh dihapus
```

**Penting:** scene, prefab, katalog, dan GLB adalah **output generator** (GLB dari skrip Blender, sisanya dari `Scripts/Editor/`). Jika diubah manual lewat Inspector, perubahan akan **tertimpa** saat `Nusantara AR/Setup Everything` dijalankan lagi. Sumber kebenarannya ada di kode `Scripts/Editor/`.

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
             └─ markerCode ──► MarkerController (kartu lama, cadangan) ──► FindByMarker(code)
```

Satu komponen UI, yaitu `ArtifactHud`, dipakai di ketiga scene. Tiap controller cukup memanggil `hud.Bind(instance, camera, onReset, onMove)`, dan `ArtifactHud` yang mengatur label bagian + kartu info di AR, tombol Bongkar/Gabung, Putar Otomatis, dan Tampilkan/Sembunyikan Label. Controller meneruskan ketukan di luar UI ke `hud.HandleTap` agar bagian model bisa diketuk langsung.

### 3.3 Pola yang dipakai konsisten

| Pola | Contoh | Alasan |
|---|---|---|
| Factory statis `X.Create(parent, …)` untuk layar UI | `CatalogScreen.Create`, `HotspotCard.Create`, `ArtifactHud.Create` | UI dibangun dari kode, jadi tidak ada referensi Inspector yang bisa putus |
| State machine `enum State` + `SetState()` + `RefreshTexts()` | `ARController`, `MarkerController` | Semua teks dan tombol diturunkan dari satu state |
| Event C# (`event Action<…>`) | `TouchGestures.Tapped/Dragged/Pinched`, `ExplodedViewController.StageChanged`, `Locale.Changed` | Komponen tidak saling kenal secara langsung |
| Unsubscribe di `OnDestroy` | semua controller | Mencegah handler menempel ke objek yang sudah dihancurkan saat pindah scene |
| `LocalizedString {id, en}` + `Locale.T(key)` | konten dan teks UI | Dua bahasa; EN kosong jatuh ke ID |
| Generator idempoten di editor | `ProjectSetup.RunAll`, `KerisBaliBuilder.Build`, `KerisSumatraBuilder.Build` | Proyek bisa dibangun ulang dari nol secara deterministik |

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
| `KERIS_BALI_01` | **Blender GLB** (`Tools/blender/keris_bali.py` → `KerisBaliBuilder`) | `0xB532` (46386) | 1 utuh + 5 | Bilah 40 cm + pesi 8 cm, luk 9, pamor banyu tetes, hulu figur dewa emas, warangka Branggah, di atas jagrak Karang Boma 45 cm. 11 hotspot |
| `KERIS_SUMATRA_01` | **Blender GLB** (`Tools/blender/keris_sumatra.py` → `KerisSumatraBuilder`) | `0xF0E4` (61668) | 1 utuh + 4 | Bilah 36 cm + pesi 7,2 cm, luk 7, pamor wos wutah, hulu burl berukir, sampir bulan sabit, berdiri di dudukan kayu. 9 hotspot |

Tahap Keris Bali (keris terbaring mendatar di jagrak, hulu ke +X): utuh (tersarung) → bilah dihunus (terangkat di atas sarung) → hulu + selut dilepas → ganja dilepas → warangka dilepas dari gandar → pendok dilepas dari gandar.

Tahap Keris Sumatra (keris berdiri di dudukan): utuh (tersarung) → bilah dihunus (ke samping sarung) → hulu + mendak dilepas → ganja dilepas → warangka dilepas dari gandar.

Kode kartu lama `0xEEC1` (keris sementara) dan `0xDA26` (Keris Jawa) sudah ditarik; `0xF0E4` berjarak ≥ 6 bit dari keduanya sehingga kartu lama yang terlanjur dicetak tidak akan memunculkan Keris Sumatra.

---

## 5. Referensi file — Runtime

### Core (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Core/AppSession.cs` | 122 | `AppSession` (navigasi scene + artefak terpilih + cache katalog), `AppSettings` (PlayerPrefs: volume narasi/SFX dengan cache, persetujuan analitik, onboarding selesai), `Analytics.Log` (hanya ke `Debug.Log`, dan hanya bila pengguna setuju; belum ada penyedia analitik) |
| `Core/MainController.cs` | 192 | Controller scene Main: membangun UI, membuka katalog/detail, 3D Viewer (instantiate prefab di `stageRoot`, `orbit.Frame`), cek dukungan ARCore, tombol back |
| `Core/Locale.cs` | 68 | `Language {ID, EN}`, `LocalizedString`, `Locale.Current`, event `Locale.Changed`, `Locale.T(key)` |
| `Core/UIStrings.cs` | 134 | Kamus teks antarmuka per kunci (mis. `"marker.scan"`, `"ctrl.reset"`). Konten kuratorial **tidak** disimpan di sini |
| `Core/DevCapture.cs` | 100 | Mode QA: menyusuri alur utama dan menyimpan tangkapan layar otomatis (dipakai oleh `BuildScript.BuildWindowsCapture`) |

### Content (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Content/ArtifactData.cs` | 80 | `ScriptableObject` satu artefak: identitas, kategori, region/era/ringkasan, thumbnail, prefab, data spesimen, `markerCode` (kartu lama), `QrText` (isi QR, diturunkan dari `artifactId`), flag placeholder, `hotspots`. Juga `HotspotData`, `HotspotStage`, `ArtifactCategory` |
| `Content/ContentCatalog.cs` | 52 | Daftar artefak di `Resources/ContentCatalog`; `Find(id)`, `FindByQr(text)` (awalan `QrPrefix = "NUSANTARA:"`), `FindByMarker(code)`, `NonEmptyCategories()` (kategori tanpa isi disembunyikan) |

### Artifact (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Artifact/ArtifactInstance.cs` | 120 | Lihat §4 |
| `Artifact/ArtifactPart.cs` | 26 | Lihat §4 |
| `Artifact/ExplodedViewController.cs` | 248 | Lihat §4; animasi dijalankan lewat coroutine, event `StageChanged`. Tahap dengan `drawOut` (tahap 1 keris) dianimasikan sebagai cabut dari sarung (`DrawPath`: tarik lurus sepanjang sumbu sarung sampai pucuk lolos, lalu lengkung Bezier kubik ke pose tercabut; ±1,6 dtk). `ToggleDraw` = tombol Hunus/Sarungkan (tahap 0 ↔ 1 saja), `TryGetDrawSweepBounds` untuk pembingkaian kamera |
| `Artifact/AutoRotate.cs` | 30 | Turntable, event `ActiveChanged` |

### Interaction (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Interaction/TouchGestures.cs` | 203 | Satu sumber gestur untuk semua scene. Event: `Tapped`, `Dragged` (1 jari), `Pinched` (rasio jarak), `TwoFingerPanned/Ended`, `InteractionStarted`. Dua jari diputuskan sekali menjadi Pinch **atau** Pan. Sentuhan di atas UI diabaikan (`IsOverUI`). `DpToPixels` untuk ambang yang tidak bergantung DPI |
| `Interaction/OrbitCameraController.cs` | 123 | Kamera orbit 3D Viewer: geser untuk memutar, pinch/scroll untuk zoom. `Frame(bounds)` membingkai artefak dan menjadikannya posisi reset; `EaseTo` menggeser kamera halus (dipakai `MainController` untuk mundur selama animasi hunus lalu kembali) |

### AR — mode ARCore (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `AR/ARController.cs` | 345 | State machine: `CheckingAvailability → (Unsupported / Installing → InstallFailed) → NeedsPermission / PermissionDenied → Scanning ⇄ ReadyToPlace → Placed ⇄ Repositioning`, dengan `TrackingLost` bila sesi kehilangan tracking. Tips ditampilkan setelah 15 detik tanpa bidang. Gestur: tap untuk meletakkan, geser 1 jari untuk memutar, pinch untuk skala, geser 2 jari untuk memindahkan. Semua jalur gagal menawarkan kembali ke 3D Viewer |
| `AR/PlacementController.cs` | 131 | Raycast dari tengah layar ke bidang (`UpdateTarget`), `Place` (membuat anchor + menghadap kamera), `BeginReposition`, `DragTo`, `SetPlanesVisible` |
| `AR/ReticleView.cs` | 84 | Cincin penanda posisi letak (mesh prosedural) |
| `AR/CameraPermission.cs` | 69 | `IsGranted`, `Request(callback)`, `OpenAppSettings()` (intent Android ke halaman info aplikasi). Dipakai oleh AR dan Marker |

### Marker — mode Scan QR (+ kartu penanda lama), tanpa ARCore (`NusantaraAR.Marker`)
| File | Baris | Isi |
|---|---|---|
| `Marker/MarkerController.cs` | 396 | Controller scene Marker, lihat §6.4 |
| `Marker/DarkRegions.cs` | 336 | Analisis frame bersama: threshold adaptif, komponen gelap, segi empat (+ sudut subpiksel opsional), sampling. Dipakai detektor QR dan kartu, lihat §6.2 |
| `Marker/QrCode.cs` | 709 | QR versi 1-10 tanpa library luar: encoder mode byte (untuk "Tampilkan QR"), decoder matriks (numerik/alfanumerik/byte), Reed-Solomon GF(256), `CreateTexture`, lihat §6.5 |
| `Marker/QrDetector.cs` | 284 | Detektor QR di gambar kamera: pola finder → homografi → grid modul → `QrCode.TryDecode`, lihat §6.5 |
| `Marker/CameraFeed.cs` | 151 | `WebCamTexture` → buffer grayscale **tegak** (sudah memperhitungkan `videoRotationAngle` dan mirror) di `Gray/Width/Height`. `ConfigureDisplay` memasang tekstur kamera ke `RawImage` latar dengan rotasi/aspek yang benar. `FocalPixels` adalah perkiraan fokus dari FOV asumsi |
| `Marker/MarkerDetector.cs` | 239 | Detektor kartu penanda lama + `Homography` (DLT 4 titik dan kuadrat terkecil), lihat §6.2 |
| `Marker/MarkerPattern.cs` | 56 | Definisi pola 6×6, kode artefak, `IsBlack`, `HammingDistance`, `CreateTexture` |
| `Marker/MarkerPose.cs` | 106 | Pose dari homografi + `PoseSmoother` (filter One Euro), lihat §6.3 |

### UI (`NusantaraAR.UI`)
| File | Baris | Isi |
|---|---|---|
| `UI/UIKit.cs` | 321 | `Theme` (token warna/ukuran PRD §5.1; panel teks memakai opasitas 92% agar lolos WCAG AA di atas kamera), `SpriteFactory` (rounded-rect 9-slice & lingkaran prosedural), `UIKit` (Canvas, Rect, Stretch, Place, Panel, Text, Button, HRow/VColumn, VerticalScroll, Slider, …) |
| `UI/ArtifactHud.cs` | 201 | HUD bersama: tombol Reset (+ Pindahkan di AR), dock Bongkar/Gabung, Putar Otomatis, Tampilkan/Sembunyikan Label, label tahap. `HandleTap` (ketuk bagian model → buka info, ketuk kosong → tutup), `CloseInfo` (tombol Kembali), navigasi sebelum/berikutnya |
| `UI/HotspotOverlay.cs` | 401 | Anotasi di AR setiap `LateUpdate`: titik emas di model + garis penunjuk + label "Nama (i)" di kiri/kanan objek (histeresis sisi, label bertabrakan diturunkan, posisi dihaluskan). Titik tertutup geometri diredupkan. `PickAt` meraycast model untuk memilih hotspot bagian yang diketuk. Kartu info diletakkan di samping bagian terpilih dan ikut bergerak bersama objek |
| `UI/HotspotCard.cs` | 227 | Kartu info yang mengembang di samping bagian: judul, istilah daerah, bahan, status draf, tab Kriya/Filosofi/Sejarah, sumber, tombol Dengar/Pelafalan (hanya bila ada audio), dan `<` `n / N` `>` antar bagian yang tampil di layar |
| `UI/CatalogScreen.cs` | 151 | Layar katalog per kategori, dengan tombol pengaturan dan Scan |
| `UI/MarkerCardScreen.cs` | 65 | Menampilkan kode QR artefak layar penuh (bisa di-scan dari HP lain) |
| `UI/SettingsScreen.cs` | 114 | Bahasa, volume, persetujuan analitik, ulangi tutorial, tentang |
| `UI/OnboardingScreen.cs` | 109 | Tutorial gestur + keselamatan AR + persetujuan analitik |
| `UI/LocalizedLabel.cs` | 35 | Label TMP yang otomatis berganti saat `Locale.Changed` |
| `UI/SafeArea.cs` | 31 | Menyesuaikan rect ke `Screen.safeArea` (notch) |

### Audio (`NusantaraAR`)
| File | Baris | Isi |
|---|---|---|
| `Audio/AudioManager.cs` | 118 | Singleton (`AudioManager.Instance`): narasi (play/pause/stop/seek/progress) dan SFX (`Click`). Volume diambil dari `AppSettings` |

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
- `ArtifactPose`:
  - Sumbu atas artefak = normal kartu yang menghadap kamera.
  - +Z = arah tepi atas kartu, sehingga muka artefak (-Z) menghadap pengguna.
- `PoseSmoother`: filter **One Euro** untuk posisi dan rotasi. Pose tidak bergetar saat diam dan tidak tertinggal saat bergerak cepat.

### 6.4 Loop `MarkerController`
```
Start: kumpulkan markerCode dari katalog → izin kamera → tunggu kamera (≤1,5 s) → CameraFeed.StartFeed()
Update:
  feed.Grab() ada frame baru → ConfigureDisplay → ProcessFrame:
      regions.Analyze (sekali) → FindTarget:
          1) QrDetector.Detect → FindByQr(text)
             (QR terlihat tapi tak terbaca & QR terakhir terbaca < 2 s lalu → tetap artefak itu)
          2) bila tidak ada QR dikenali dan detectLegacyCards: MarkerDetector.Detect → FindByMarker(code)
      → EnsureArtifact(data, size)  (ganti prefab bila QR/kartu lain)
      → TryEstimate(sudut, qrSizeMeters | markerSizeMeters) → ArtifactPose → smoother.Filter → lastSeen = now
  State: Searching ⇄ Tracking (hilang bila > 0,6 s tak terlihat) | Locked (pose dibekukan)
  ApplyPose: posisi/rotasi = kamera ∘ poseMarker ∘ yaw pengguna; skala = baseScale·userScale
```
- `baseScale` diatur agar lebar artefak kira-kira 2,2 kali sisi QR/kartu (`fitToMarker`), jadi keris tampil "di atas QR", bukan 1:1.
- Gestur: geser untuk memutar (yaw), pinch untuk skala (0,5×–3×).
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
| Nusantara AR / Render Stage Previews | `PreviewRenderer.Render` | Merender PNG tiap tahap exploded setiap artefak ke `Previews/{id}_stage_N.png` (untuk QA visual) |
| Nusantara AR / Build / Android APK (uji perangkat) | `BuildScript.BuildAndroidApk` | `Builds/Android/NusantaraAR.apk` |
| Nusantara AR / Build / Android App Bundle (.aab) | `BuildScript.BuildAndroidAab` | `Builds/Android/NusantaraAR.aab` |

Versi batch (tanpa GUI): `ProjectSetup.RunBatch`, `PreviewRenderer.RenderBatch`, dan `BuildScript.*`. Semuanya dipanggil lewat `-executeMethod`.

### 7.2 Urutan `ProjectSetup.RunAll` (idempoten)
1. `EnsureFolders`: membuat folder Art/Content/Resources/Prefabs/Scenes.
2. `ImportTmpEssentials`: mengimpor TMP Essential Resources bila belum ada.
3. `ConfigurePlayer`: package id, IL2CPP ARM64, SDK, orientasi, izin kamera, dan setelan Android lainnya.
4. `ConfigureXR`: loader ARCore/ARKit diset **Optional**.
5. `ConfigureURP`: menambahkan `ARBackgroundRendererFeature` ke renderer URP.
6. `KerisBaliBuilder.Build()` lalu `KerisSumatraBuilder.Build()`: impor GLB Blender, prefab modular, tahap exploded, hotspot, dan konten draf, lalu mendaftarkannya ke katalog.
7. `PruneCatalog`: katalog hanya berisi artefak model Blender (Bali, Sumatra), berurutan.
8. `BuildCommonAssets`: material reticle dan `ARPlane.prefab`.
9. `BuildMainScene`, `BuildARScene`, `BuildMarkerScene`: kamera, cahaya, EventSystem (Input System UI module), controller, dan referensinya.
10. `RenderThumbnail(dataPath, thumbPath)` untuk tiap artefak.
    - Parameternya **path**, bukan instance, karena `ArtifactData` bisa sudah di-unload setelah pergantian scene.
11. Mengisi Build Settings dengan urutan `Main`, `AR`, `Marker`, lalu `SaveAssets`.

### 7.3 Model Blender → GLB → prefab (`GlbArtifact`, `KerisBaliBuilder`, `KerisSumatraBuilder`)
- **Sumber model**: skrip Blender 5.2 tanpa GUI di `Tools/blender/` membangun geometri, tekstur PBR (warna, ORM, normal map), dan material, lalu mengekspor GLB (tekstur tertanam) ke `Art/KerisBali/keris_bali.glb` dan `Art/KerisSumatra/keris_sumatra.glb`:
  ```bash
  "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_bali.py
  "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_sumatra.py
  ```
  Tambahkan `-- --no-render` untuk melewati render pratinjau (`Docs/KerisBali_Blender_*.png`, `Docs/KerisSumatra_Blender_*.png`). `keris_bali.py` memakai utilitas dari `keris_sumatra.py`.
- **Impor**: paket **glTFast** (`com.unity.cloud.gltfast`) mengimpor GLB sebagai model beserta mesh, material (shader graph glTF PBR untuk URP), dan tekstur. Animasi GLB `Cabut_Keris` diabaikan; aplikasi memakai `ExplodedViewController`.
- **`GlbArtifact.Load`**: instansiasi GLB, kelompokkan node Blender ke `ArtifactPart` lewat tabel nama (`Warangka_Sampir` + `Warangka_Celah` → `Warangka`, `Permata_Selut` → `Selut`, dst.; node tanpa bagian memicu exception), pasang pivot bagian di titik sambung, tambah `MeshCollider` (oklusi hotspot), dan geser model agar dasar alas di y = 0 dan pusat alas di XZ = 0.
- **Sumbu**: Blender (x, y, z) → ruang Model Unity (x, z, y); muka depan Blender (-Y) menjadi -Z. Arah pencerminan glTF→Unity diperiksa dari sisi +X satu bagian acuan (hulu Bali / warangka Sumatra); bila terbalik, model diputar 180° di Y.
- **`GlbArtifact.SetStages` / `Move` / `Front`**: tahap exploded dari offset kumulatif per bagian; hotspot diletakkan 3 mm di depan permukaan terdepan (-Z) bagian pada koordinat (x, y) tertentu, dihitung pada pose "dihunus". Hotspot di luar bagian memicu exception.
- **`GlbArtifact.Save`**: prefab di `Content/<ID>/`, `ArtifactData` (+ katalog). Konten draf (`Fill`) ada di file builder masing-masing.
- Bila model Blender diubah: jalankan skrip Blender, lalu menu **Build Keris Bali / Build Keris Sumatra** (atau `Tools/compile_and_test.ps1`).

### 7.4 Konten draf
`KerisBaliBuilder.Fill` dan `KerisSumatraBuilder.Fill` menulis judul, material, teknik, dan istilah daerah ke setiap hotspot; filosofi & sejarah berisi penanda untuk kurator. Semua hotspot ditandai `curatorValidated = false` sampai divalidasi kurator.

---

## 8. Pengujian

Jalankan semuanya dengan satu perintah (Unity Editor harus **ditutup** lebih dulu):

```bash
powershell -ExecutionPolicy Bypass -File Tools\compile_and_test.ps1
```

Skrip ini menjalankan `ProjectSetup.RunBatch`, lalu EditMode test, lalu `Tools/kartu_qr.py` dan `Tools/kartu_penanda.py` (kartu QR + kartu penanda lama, PDF/PNG Keris Bali + Keris Sumatra). Log disimpan di `Logs/`.

| File | Yang diuji |
|---|---|
| `ArtifactTests.cs` | Dijalankan untuk **setiap** artefak: katalog hanya berisi Keris Bali + Sumatra; ditemukan lewat kode kartunya dan lewat isi QR-nya (termasuk QR hasil `QrCode.Encode`), kode unik, kartu lama (EEC1, DA26) tidak dikenali; semua mesh berasal dari GLB Blender; hotspot menunjuk bagian yang ada dan berada di muka depan; aturan tampil hotspot saat utuh/dihunus; setiap tahap menggerakkan bagian; pivot di dasar; clamp skala relatif; fallback `LocalizedString` |
| `KerisBlenderTests.cs` | Bali: kode B532, 11 hotspot, 1 + 5 tahap, jagrak 45 cm di y = 0, keris bersandar di atasnya dengan hulu di +X, bilah 40 + 8 cm, bilah terhunus di atas sarung, tahap terakhir melepas pendok. Sumatra: kode F0E4, 9 hotspot, 1 + 4 tahap, tinggi ±52 cm di dudukan, sampir di +X, bilah 36 + 7,2 cm, bilah terhunus di samping sarung |
| `MarkerTests.cs` | Deteksi marker perspektif di 4 orientasi (gambar sintetis); tidak ada false positive pada noise; pose cocok dengan transform yang diketahui; pemetaan rotasi buffer kamera; keunikan rotasi kode & jarak antar kode; memilih artefak yang benar saat semua kode dicari; mendeteksi **PNG kartu cetak** asli di `Docs/` dengan cukup cepat |
| `QrTests.cs` | Encoder identik bit-per-bit dengan pustaka Python `qrcode`; decoder membaca QR Python mode byte & campuran; round-trip semua tingkat ECC dan versi 1-10; koreksi Reed-Solomon (kerusakan kecil terkoreksi, kerusakan besar ditolak); deteksi perspektif di 4 orientasi dengan galat sudut < 2,5 px; jauh/dekat; miring kuat dan gradasi cahaya; QR tak terbaca tetap memberi pose; tanpa false positive (noise, kartu lama) dan QR tidak terbaca sebagai kartu; pose cocok dengan transform yang diketahui; mendeteksi **PNG kartu QR cetak** di `Docs/` dengan cukup cepat (analisis + QR + kartu per frame) |

---

## 9. Cara memperluas

### Menambah artefak baru
1. Modelkan di Blender dengan skrip (pola: `Tools/blender/keris_sumatra.py`): satu objek per bagian, origin di titik sambung, 1 unit = 1 m, muka depan -Y; ekspor GLB ke `Art/<Nama>/`.
   Lalu tulis builder editor (pola: `KerisSumatraBuilder`) yang memanggil `GlbArtifact.Load` → `SetStages` → `Front` → `Save`.
2. `GlbArtifact.Save` membuat `ArtifactData` dan mendaftarkannya ke katalog; tambahkan ID-nya ke `ProjectSetup.PruneCatalog`.
3. Tambahkan ID ke `[TestFixture]` di `ArtifactTests`.
4. **Supaya perubahan tidak hilang**, panggil builder-nya dari `ProjectSetup.RunAll` beserta `RenderThumbnail`-nya.
5. Tambahkan test khusus di `KerisBlenderTests`.

### Kode QR artefak baru
Tidak perlu kode tambahan: isi QR diturunkan dari `artifactId`. Tambahkan baris di tabel `KARTU` pada `Tools/kartu_qr.py` untuk kartu cetaknya, dan `TestCase` baru di `Detects_PrintableQrCard_AndIsFastEnough`.

### Menambah kartu penanda (lama, opsional)
1. Pilih kode 16-bit baru dan tambahkan konstantanya di `MarkerPattern`.
2. Pastikan lulus `MarkerCode_RotationsAreDistinct` dan `MarkerCodes_AreFarFromEachOther_InEveryRotation`. Tambahkan kode baru ke kedua test tersebut.
3. Set `ArtifactData.markerCode`. `MarkerController` otomatis mengambil semua kode dari katalog.
4. Tambahkan baris baru di tabel `KARTU` pada `Tools/kartu_penanda.py`. Tambahkan juga `TestCase` baru di `Detects_PrintableCard_AndIsFastEnough`.

### Menambah teks UI / bahasa
- Kunci baru ditambahkan di `UIStrings`, lalu dipakai lewat `Locale.T("kunci")` dan `LocalizedLabel.Attach(label, "kunci")` agar teksnya ikut berganti bahasa.
- Konten artefak memakai `LocalizedString` di aset, bukan `UIStrings`.

---

## 10. Batasan dan hal yang perlu diketahui

- **Konten kuratorial masih draf**: semua `curatorValidated = false`. Keris Bali dan Keris Sumatra dimodelkan di Blender dari cetak biru/lembar acuan, bukan dari spesimen museum; figur hulu Bali sangat disederhanakan.
- **Narasi audio belum ada**: field `narrationID/EN` masih kosong, sehingga pemutar tampil tanpa klip.
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
- APK `Builds/Android/NusantaraAR.apk` (49,9 MB) sudah dibangun ulang dengan Keris Bali + Keris Sumatra (Blender/glTFast), tetapi belum diuji di perangkat.
