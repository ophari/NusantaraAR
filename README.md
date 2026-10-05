# Nusantara AR

Aplikasi mobile AR untuk edukasi benda budaya dan satwa khas Indonesia, sesuai **PRD v1.1** (`prd_aplikasi_ar_benda_budaya_indonesia_v1.1.md`).

- **Engine:** Unity 6.3 LTS (6000.3.25f1), URP 17.3
- **AR:** AR Foundation / ARCore XR Plugin / ARKit XR Plugin 6.3.5 (ARCore *Optional*)
- **Android:** minSdk 26, targetSdk 36, IL2CPP, ARM64, OpenGLES3
- **Input:** Input System (EnhancedTouch)
- **Model 3D:** Blender 5.2 → GLB, diimpor dengan glTFast 6.20 (`com.unity.cloud.gltfast`)

> Dokumentasi kode lengkap (arsitektur, alur data, referensi tiap file, algoritma Scan QR, pipeline editor, pengujian): **[Docs/DOKUMENTASI_KODE.md](Docs/DOKUMENTASI_KODE.md)**

## Menjalankan

1. Buka folder ini di Unity Hub dengan editor **6000.3.25f1** (modul Android Build Support).
2. Menu **Nusantara AR → Setup Everything** — idempoten; membangun ulang prefab, konten draf, dan scene.
   Batch: `Unity -batchmode -projectPath . -buildTarget Android -executeMethod NusantaraAR.EditorTools.ProjectSetup.RunBatch`
3. Buka `Assets/NusantaraAR/Scenes/Main.unity` lalu Play. Mouse = sentuhan, scroll = pinch.
   Di Editor, scene AR akan menampilkan jalur "AR tidak tersedia" → "Buka di 3D Viewer" (normal, tanpa perangkat).
4. Build: **Nusantara AR → Build → Android APK** (uji perangkat) atau **Android App Bundle** (Play Store, butuh keystore rilis).
   Batch: `Unity -batchmode -projectPath . -buildTarget Android -executeMethod NusantaraAR.EditorTools.BuildScript.BuildAndroidApk`

> **Penting:** APK harus di-build dengan **platform aktif Android**. Bila platform aktif masih Windows/Standalone
> (mis. setelah build QA), URP membuang varian shader XR dan **model 3D tidak tampil di HP** walau build "sukses".
> `BuildScript` otomatis pindah platform, dan `BuildGuard` menghentikan build yang salah platform. Detail: §7.5 dokumentasi kode.

Pasang ke HP Android (USB debugging aktif):

```bash
adb install -r Builds/Android/NusantaraAR.apk
```

Alat bantu lain:
- **Nusantara AR → Render Stage Previews** → `Previews/<ID>_stage_N.png` (setiap tahap exploded view + titik hotspot, semua artefak).
- **Nusantara AR → Build Keris Bali / Build Keris Sumatra / Build Candi Borobudur / Build Karambit / Build Komodo** — impor ulang GLB Blender satu artefak (prefab, konten, thumbnail).
- **Nusantara AR → Render Thumbnails** — render ulang thumbnail katalog saja (batch: `ProjectSetup.RenderThumbnailsBatch`, tanpa `-nographics`).
- **Nusantara AR → Build AR Visuals** — aset tampilan saja, tanpa menyentuh scene: grid bidang AR, reticle terakota, glow
  bawah keris, material UI kaca, dan `GlassBlurFeature` di renderer URP (batch: `ProjectSetup.BuildVisualAssetsBatch`).
- **Nusantara AR → Bangun Kisah** — aset mode Kisah dari `Tools/narasi/` (naskah + MP3 hasil `python Tools/narasi/kisah_tts.py`, butuh `pip install edge-tts` dan internet).
- **Nusantara AR → Pasang Musik Latar** — musik latar tiap artefak dari `Tools/musik/musik.json` (MP3 diolah `python Tools/musik/siapkan_musik.py`, butuh ffmpeg).
- Test: Window → General → Test Runner → EditMode (`NusantaraAR.Tests.EditMode`).
- Build QA Windows dengan tangkapan layar otomatis alur utama (define `NUSANTARA_CAPTURE`, profil kualitas Mobile):
  `-executeMethod NusantaraAR.EditorTools.BuildScript.BuildWindowsCapture`, lalu jalankan
  `Builds/QA/NusantaraAR.exe -screen-width 540 -screen-height 1170 -screen-fullscreen 0` → hasil di `Builds/QA/Captures/`.
  Build ini pindah ke platform Windows lalu mengembalikan platform semula, jadi build APK sesudahnya tetap aman.

Build Android terakhir sudah diverifikasi: targetSdk 36, minSdk 26, hanya `arm64-v8a`, ARCore `optional`,
dan semua library native berselaras 16 KB (segmen LOAD `0x4000`).

## Catatan teknis / penyimpangan dari PRD

- **Canvas Scaler match = 0 (lebar)**, bukan 0,5: dengan 0,5 lebar kanvas turun ke ~980 unit di HP 9:19,5 sehingga tata letak 1080 unit terpotong.
- **Palet PRD §5.1 (jati/emas) diganti** gading · terakota · nila (referensi desain baru); emas tinggal untuk titik hotspot.
  Kontras WCAG AA dijaga `UiThemeTests`, termasuk teks di atas kaca pada latar kamera terburuk (hitam/putih).
- **Kaca buram sungguhan**: URP tidak punya grab-pass untuk uGUI, jadi `GlassBlurFeature` (Render Graph) mem-blur warna kamera
  ke RT ¼ resolusi yang dibaca shader `NusantaraAR/UI/Glass`. Blur hanya menangkap yang digambar kamera (model, feed kamera,
  latar `Backdrop` di scene Main), bukan UI lain di bawah panel. Hanya berjalan bila ada panel kaca aktif.
- **Profil kualitas "PC"** (Forward+, HDR) dari template tidak dipakai di Android; di player Windows profil ini tidak menampilkan objek 3D ke layar.
  Aplikasi ini menargetkan profil "Mobile" — bila kelak ada target desktop, selidiki dulu.
- Efek SSAO dihapus dari renderer URP (mahal di mobile, dan resource-nya tidak ikut di build).
- Exploded view memakai coroutine + SmoothStep (tanpa DOTween) agar tanpa dependensi Asset Store.

## Dua mode AR

| Mode | Butuh | Cara kerja |
| --- | --- | --- |
| **Scan QR** (utama) | Kamera saja — jalan juga di HP tanpa ARCore (mis. Samsung Galaxy A05) | Kamera biasa (WebCamTexture) + detektor & decoder QR buatan sendiri (`Scripts/Runtime/Marker`, tanpa library luar). Keris muncul di atas kode QR; "Kunci Posisi" menahan objek agar QR boleh dijauhkan. Kartu penanda 6x6 lama tetap dikenali sebagai cadangan. |
| **Letakkan di Meja** | HP di daftar ARCore | AR Foundation: deteksi bidang + reticle + ARAnchor. Tombol disembunyikan di HP tanpa ARCore. |

Kartu kode QR (cetak A5 tanpa diperkecil, sisi QR 8 cm) — satu kartu per artefak:

| Artefak | Kartu QR | Isi QR |
| --- | --- | --- |
| Keris Bali (model Blender) | `Docs/KartuQR_KerisBali_A5.pdf` | `NUSANTARA:KERIS_BALI_01` |
| Keris Sumatra (model Blender) | `Docs/KartuQR_KerisSumatra_A5.pdf` | `NUSANTARA:KERIS_SUMATRA_01` |
| Candi Borobudur (model Blender) | `Docs/KartuQR_CandiBorobudur_A5.pdf` | `NUSANTARA:BOROBUDUR_01` |
| Karambit (model Blender) | `Docs/KartuQR_Karambit_A5.pdf` | `NUSANTARA:KARAMBIT_01` |
| Komodo (model Blender) | `Docs/KartuQR_Komodo_A5.pdf` | `NUSANTARA:KOMODO_01` |

Kartu QR dibuat ulang dengan `python Tools/kartu_qr.py` (butuh `pip install pillow qrcode`). Isi QR diturunkan dari
`artifactId` (`ArtifactData.QrText`), jadi artefak baru otomatis punya QR. Atau tekan tombol **Tampilkan QR** di halaman
detail untuk menampilkannya di layar HP/laptop lain.

Kartu penanda 6x6 lama (`Docs/KartuPenanda_*_A5.pdf`, kode `B532` / `F0E4` / `E3B1` / `C616` / `DC4E`, `python Tools/kartu_penanda.py`) kodenya
tetap disimpan dan masih dikenali di mode Scan QR sebagai cadangan (`MarkerController.detectLegacyCards`), tetapi tidak
lagi ditampilkan di UI. Kartu `EEC1` (keris sementara) dan `DA26` (Keris Jawa) sudah ditarik dan tidak dikenali lagi.

Keterbatasan Scan QR: FOV kamera diperkirakan (64° sisi panjang), jadi objek tetap menempel di QR tapi
perspektifnya bisa sedikit berbeda; pelacakan hilang bila QR tertutup sebagian, buram, atau terlalu jauh/gelap
(modul QR minimal ±2,7 px di buffer deteksi, kira-kira QR 8 cm dari jarak ±50 cm).
Opsi peningkatan: Vuforia Engine (image target, gratis paket Basic, butuh akun + license key).

## Struktur

```
Assets/NusantaraAR/
  Scripts/Runtime/
    Core/        Locale & UIStrings (ID/EN), AppSession, AppSettings, Analytics (opt-in), MainController, DevCapture (QA)
    Content/     ArtifactData, HotspotData, ArtifactStory (mode Kisah), ContentCatalog (model data PRD §6.2)
    Artifact/    ArtifactInstance, ArtifactPart, ExplodedViewController, AutoRotate, GroundGlow
    Interaction/ TouchGestures (aturan gestur PRD §4.3), OrbitCameraController (3D Viewer)
    AR/          ARController (state machine §4.2), PlacementController (reticle + ARAnchor), CameraPermission, ReticleView
    UI/          UIKit (token desain), IconFactory (ikon prosedural), GlassSurface (kaca), ArtifactHud (rel + tombol putar),
                 HotspotOverlay/HotspotCard, StoryPanel (Kisah), TopBar, BottomNav, DetailSheet, CoachCard, ControlPanel,
                 Catalog/Settings/Onboarding
    Rendering/   GlassBlurFeature (blur kamera untuk UI kaca)
    Audio/       AudioManager (3 AudioSource: narasi + SFX + musik latar; fade & ducking saat narasi)
    Marker/      MarkerController, QrDetector/QrCode, DarkRegions, CameraFeed, MarkerPose (+ kartu 6x6 lama)
  Scripts/Editor/ ProjectSetup, PreviewRenderer, BuildScript, BuildGuard (pengaman platform build),
                  GlbArtifact (GLB -> prefab artefak), KerisBaliBuilder, KerisSumatraBuilder, CandiBorobudurBuilder,
                  KarambitBuilder, KomodoBuilder
                  (bagian, tahap, hotspot, konten), KerisRefs/CandiRefs/KarambitRefs/KomodoRefs (sumber rujukan), StoryBuilder (Kisah), MusicBuilder (musik latar)
  Content/KERIS_BALI_01/          prefab, ArtifactData, thumbnail (dibangkitkan KerisBaliBuilder), Story/ (Kisah), Music/
  Content/KERIS_SUMATRA_01/       prefab, ArtifactData, thumbnail (dibangkitkan KerisSumatraBuilder), Story/, Music/
  Content/BOROBUDUR_01/           prefab, ArtifactData, thumbnail (dibangkitkan CandiBorobudurBuilder), Story/, Music/
  Content/KARAMBIT_01/            prefab, ArtifactData, thumbnail (dibangkitkan KarambitBuilder), Story/, Music/
  Content/KOMODO_01/              prefab, ArtifactData, thumbnail (dibangkitkan KomodoBuilder), Story/, Music/
  Art/KerisBali/                  keris_bali.glb (ekspor Blender) + Materials/ + Textures/ (ASTC, dari builder)
  Art/KerisSumatra/               keris_sumatra.glb (ekspor Blender) + Materials/ + Textures/
  Art/CandiBorobudur/             candi_borobudur.glb (ekspor Blender) + Materials/ + Textures/
  Art/Karambit/                   karambit.glb (ekspor Blender) + Materials/ + Textures/
  Art/Komodo/                     komodo.glb (ekspor Blender) + Materials/ + Textures/
  Resources/ContentCatalog.asset, UIGlass.mat, GroundGlow.mat
  Scenes/Main.unity, AR.unity, Marker.unity (Scan QR)
Tools/blender/keris_bali.py, keris_sumatra.py, candi_borobudur.py, karambit.py, komodo.py   skrip Blender pemodel artefak (+ .blend, *_textures/ hasilnya)
Tools/kartu_qr.py                 kartu kode QR cetak A5
Tools/kartu_penanda.py            kartu penanda 6x6 lama (cadangan)
Tools/narasi/                     naskah Kisah (kisah.json), waktu subtitle, kisah_tts.py (suara TTS)
Tools/musik/                      daftar trek (musik.json), siapkan_musik.py (asli/ tidak di-commit)
```

UI dibangun dari kode (tanpa YAML scene), jadi perubahan tampilan cukup di `Scripts/Runtime/UI`. Shader kaca & blur ada di
`Assets/NusantaraAR/Shaders/`.

## Model artefak (Blender → GLB → Unity)

Semua model 3D dibuat di **Blender 5.2** oleh skrip (tanpa klik manual), diekspor sebagai **GLB**, lalu diimpor Unity
lewat **glTFast**. Model lama (keris sementara dari `keris3d`, Keris Bali prosedural buatan Unity, Keris Jawa) sudah
dihapus dari proyek (masih ada di riwayat git).

| Artefak | Skrip Blender | Isi model | Exploded view |
| --- | --- | --- | --- |
| **Keris Bali** `KERIS_BALI_01` | `Tools/blender/keris_bali.py` | Bilah 40 cm luk 9 pamor banyu tetes, ganja emas, hulu (danganan) figur dewa emas + selut berpermata, warangka sesrengatan khas Bali dari kayu timoho berpelet, pendok & cincin emas berpermata, **jagrak Karang Boma** 45 cm. 11 hotspot | 5 tahap: hunus → hulu + selut → ganja → warangka → pendok |
| **Keris Sumatra** `KERIS_SUMATRA_01` | `Tools/blender/keris_sumatra.py` | Bilah 36 cm luk 7 pamor wos wutah, hulu burl berukir, mendak, sampir bulan sabit, pendok kuningan, dudukan kayu. 9 hotspot | 4 tahap: hunus → hulu + mendak → ganja → warangka |
| **Candi Borobudur** `BOROBUDUR_01` (kategori Candi) | `Tools/blender/candi_borobudur.py` (data & sumber: `Docs/Borobudur_Data.md`) | 123 × 123 × 35 m, **prefab 1:200** (61,5 × 17,5 cm): kaki, 5 teras persegi berelief, 5 langkan dengan 432 relung-arca, tangga & gapura, 3 teras melingkar dengan 72 stupa terawang + arca, stupa induk. ±308 ribu segitiga. 10 hotspot | 4 tahap, tingkat dipisah ke atas: stupa induk → teras melingkar (Arupadhatu) → teras 3-5 → teras 1-2 dari kaki (Kamadhatu) |
| **Karambit** `KARAMBIT_01` | `Tools/blender/karambit.py` (data, sumber & foto acuan: `Docs/Karambit_Data.md`) | Kurambiak Minangkabau jantan, tinggi 15,9 cm: bilah cakar 7 gerigi (punggung = busur R 5,2 cm, 120°) bermotif kaluak paku, cincin kuningan, hulu kayu kemuning berukir dengan lubang telunjuk, sarung kayu berukir tinta emas, dudukan kayu. 9 hotspot | 4 tahap: sarung diputar pada pusat busur (meluncur lepas menyusuri lengkung) → pisau diangkat → hulu → cincin |
| **Komodo** `KOMODO_01` (kategori Satwa Endemik) | `Tools/blender/komodo.py` (data, sumber & foto acuan: `Docs/Komodo_Data.md`) | Diorama **1:10**: komodo betina ±2,3 m (23 cm di aplikasi) melangkah di savana Pulau Rinca, di depan gundukan sarang burung gosong yang dipotong sehingga 7 telurnya terlihat. Kulit bersisik prosedural, 48 gigi bergerigi berujung jingga (lapisan besi), lidah kuning bercabang tersimpan di mulut. ±80 ribu segitiga. 10 hotspot | 4 tahap: rahang dibuka 24° di engsel + lidah dijulurkan → telur dikeluarkan dari sarang → komodo diangkat → rahang bawah dipisah |

Mengubah model:

1. Ubah skripnya, lalu ekspor ulang GLB (+ tekstur, `.blend`, render `Docs/Keris*_Blender_*.png`):

   ```bash
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_bali.py
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_sumatra.py
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/candi_borobudur.py -- --export
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/karambit.py
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/komodo.py
   ```
   Skrip candi bawaannya hanya menyimpan `.blend`; `--export` menulis GLB, `--render` merender pratinjau Cycles (berat).
2. Di Unity: **Nusantara AR → Build Keris Bali / Build Keris Sumatra / Build Candi Borobudur / Build Karambit / Build Komodo** (atau Setup Everything).

Catatan builder (`GlbArtifact`):
- Setiap objek Blender dipetakan ke satu bagian (`ArtifactPart`); objek tanpa bagian menghentikan build.
- Sumbu Blender (Z atas, depan -Y) → Unity (Y atas, depan -Z); arah hasil glTFast diperiksa otomatis.
- Tekstur mentah GLB (ARGB32, ±70 MB/keris) **tidak** dipakai: material disalin dan memakai PNG sumber yang sama dari
  `Tools/blender/*_textures/`, dikompres ASTC (±10 MB/keris di HP).
- Hotspot diletakkan dengan raycast ke muka depan bagiannya (`Front`; artefak besar memakai `reach`/`gap` lebih besar);
  animasi `Cabut_Keris` di GLB diabaikan (aplikasi memakai exploded view).
- Artefak yang terlalu besar untuk meja (candi 1:200, komodo 1:10) diperkecil **di child `Model`**, bukan di root prefab: root harus tetap
  skala 1 karena Scan QR menghitung skala dari ukuran dunia prefab, dan slider AR memakai skala root.
- Model simetris (candi) memanggil `Load` dengan `rightPart = null` agar pemeriksaan arah +X dilewati.
- Bagian yang harus bergerak menyusuri lengkung (sarung karambit) diberi pivot di pusat lengkung lalu diputar lewat
  overload `SetStages(..., rotations)` + `Turn`; bergeser lurus akan membuat bilah cakar menembus sarungnya. Rahang komodo
  memakai cara yang sama dengan pivot di engsel rahang.
- Bagian yang tersembunyi saat utuh (bilah keris di sarang, lidah komodo di mulut) masuk `hiddenWhenAssembled`;
  hotspot-nya bertahap `Bilah` agar baru tampil mulai tahap 1.

## Menambah artefak baru

1. Modelkan di Blender dengan **objek terpisah per bagian** (origin di titik sambung, 1 unit = 1 m, muka depan -Y), ekspor GLB ke `Art/<Nama>/`.
2. Tulis builder seperti `KerisSumatraBuilder`: `GlbArtifact.Load` → `UseCompressedTextures` → `SetStages` → `Front` (hotspot) → `Save`, lalu panggil dari `ProjectSetup.RunAll` dan tambahkan ID-nya ke `PruneCatalog`.
3. Tambahkan baris di `Tools/kartu_qr.py` (kartu QR cetak) dan ID di `[TestFixture]` `ArtifactTests`, `StoryTests`, `MusicTests`.
   Tes `ArtifactTests` mewajibkan kode kartu 6x6 (`MarkerPattern` + `Tools/kartu_penanda.py` + `MarkerTests.AllCodes`); pilih kode
   yang keempat rotasinya berjarak ≥ 10 dan ≥ 6 dari kode lain. Kategori baru: tambahkan di **akhir** `ArtifactCategory` + `cat.*` di `UIStrings`.
4. Setel `curatorValidated = true` hanya setelah sign-off kurator (gerbang rilis PRD §6.4).

## Status terhadap PRD v1.1

| Sudah | Catatan |
| --- | --- |
| FR-01 cek ARCore + instal, fallback 3D Viewer | `ARController` |
| FR-02/03 scanning + coaching (tips setelah 15 dtk), reticle, ARAnchor | |
| FR-04 skala 1:1 | ukuran dari cetak biru (Bali 40 cm, Sumatra 36 cm), bukan spesimen |
| FR-05 gestur (rotasi, pinch 0,5-3x, geser 2 jari) + disambiguasi, blokir sentuhan UI | |
| FR-06 Reset Tampilan, Pindahkan | |
| FR-07 exploded view berurutan, bisa dibalik | Bali 5 tahap (termasuk lepas pendok), Sumatra 4 tahap, Karambit 4 tahap (sarung diputar lepas), Komodo 4 tahap (rahang dibuka, telur keluar) |
| Hunus / Sarungkan: animasi bilah dicabut dari warangka tanpa membongkar | jalur `drawOut` di builder; test `Draw_BladeLeavesSheathWithoutPassingThroughIt` memastikan bilah tidak menembus warangka/gandar |
| FR-08 hotspot per state: titik + garis + label nama bagian langsung di AR (tidak saling tumpuk), redup saat tertutup, ketuk bagian model | |
| FR-09/10 kartu info menempel di samping bagian (menggantikan bottom sheet), 3 tab, audio + pelafalan, sumber, navigasi antar bagian | isi diperiksa terhadap sumber daring (`KerisRefs`); audio per bagian belum ada; transkrip terpisah ditiadakan (teks tab = isi narasi) |
| FR-11/12/13 katalog → detail (3D Viewer) → AR, onboarding | |
| Mode Kisah: narator (suara TTS perempuan, ID/EN) bercerita 9 bab per keris, subtitle per kalimat, model ikut dihunus/dibongkar, label bagian disorot | `StoryPanel`, naskah `Tools/narasi/kisah.json` |
| Musik latar per keris (gamelan Bali / gambus Melayu), fade & diredam saat narasi, slider volume + kredit di Pengaturan | trek Pixabay, ditandai AI oleh pengunggah |
| FR-14 ID/EN | implementasi ringan (`Locale`), bukan Unity Localization package |

**Belum / perlu keputusan:**
- Model keris dibuat di Blender dari cetak biru/lembar acuan (bukan spesimen museum; figur hulu Bali sangat disederhanakan). Teks kuratorial dan naskah Kisah sudah diperiksa terhadap sumber daring, tetapi masih **draf** sampai divalidasi kurator; tab yang belum punya isi diberi penanda "diisi kurator".
- Narasi mode Kisah memakai TTS, bukan rekaman narator. Narasi per bagian dan rekaman pelafalan belum ada. Font Playfair/Inter juga belum ada (saat ini LiberationSans bawaan TMP).
- Musik latar (Pixabay, ditandai AI) perlu ditinjau lisensi dan kesesuaian budayanya sebelum rilis publik.
- Analitik baru menulis ke log (penyedia belum dipilih, PRD §14 #5).
- iOS belum diuji (PRD merekomendasikan Android dulu).
- Uji lapangan (tracking, drift, termal, FPS) wajib di perangkat nyata — belum dilakukan.
