# Nusantara AR

Aplikasi mobile AR untuk edukasi benda budaya Indonesia, sesuai **PRD v1.1** (`prd_aplikasi_ar_benda_budaya_indonesia_v1.1.md`).

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
- **Nusantara AR → Build Keris Bali / Build Keris Sumatra** — impor ulang GLB Blender satu keris (prefab, konten, thumbnail).
- **Nusantara AR → Render Thumbnails** — render ulang thumbnail katalog saja (batch: `ProjectSetup.RenderThumbnailsBatch`, tanpa `-nographics`).
- Test: Window → General → Test Runner → EditMode (`NusantaraAR.Tests.EditMode`).
- Build QA Windows dengan tangkapan layar otomatis alur utama (define `NUSANTARA_CAPTURE`, profil kualitas Mobile):
  `-executeMethod NusantaraAR.EditorTools.BuildScript.BuildWindowsCapture`, lalu jalankan
  `Builds/QA/NusantaraAR.exe -screen-width 540 -screen-height 1170 -screen-fullscreen 0` → hasil di `Builds/QA/Captures/`.
  Build ini pindah ke platform Windows lalu mengembalikan platform semula, jadi build APK sesudahnya tetap aman.

Build Android terakhir sudah diverifikasi: targetSdk 36, minSdk 26, hanya `arm64-v8a`, ARCore `optional`,
dan semua library native berselaras 16 KB (segmen LOAD `0x4000`).

## Catatan teknis / penyimpangan dari PRD

- **Canvas Scaler match = 0 (lebar)**, bukan 0,5: dengan 0,5 lebar kanvas turun ke ~980 unit di HP 9:19,5 sehingga tata letak 1080 unit terpotong.
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

Kartu QR dibuat ulang dengan `python Tools/kartu_qr.py` (butuh `pip install pillow qrcode`). Isi QR diturunkan dari
`artifactId` (`ArtifactData.QrText`), jadi artefak baru otomatis punya QR. Atau tekan tombol **Tampilkan QR** di halaman
detail untuk menampilkannya di layar HP/laptop lain.

Kartu penanda 6x6 lama (`Docs/KartuPenanda_*_A5.pdf`, kode `B532` / `F0E4`, `python Tools/kartu_penanda.py`) kodenya
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
    Core/        Locale & UIStrings (ID/EN), AppSession, AppSettings, Analytics (opt-in), MainController
    Content/     ArtifactData, HotspotData, ContentCatalog (model data PRD §6.2)
    Artifact/    ArtifactInstance, ArtifactPart, ExplodedViewController, AutoRotate
    Interaction/ TouchGestures (aturan gestur PRD §4.3), OrbitCameraController (3D Viewer)
    AR/          ARController (state machine §4.2), PlacementController (reticle + ARAnchor), CameraPermission, ReticleView
    UI/          UIKit (token desain §5.1), ArtifactHud, HotspotOverlay (label di AR), HotspotCard, Catalog/Settings/Onboarding
    Audio/       AudioManager (2 AudioSource: narasi + SFX, ducking)
  Scripts/Editor/ ProjectSetup, PreviewRenderer, BuildScript, BuildGuard (pengaman platform build),
                  GlbArtifact (GLB -> prefab artefak), KerisBaliBuilder, KerisSumatraBuilder (bagian, tahap, hotspot, konten)
  Content/KERIS_BALI_01/          prefab, ArtifactData, thumbnail (dibangkitkan KerisBaliBuilder)
  Content/KERIS_SUMATRA_01/       prefab, ArtifactData, thumbnail (dibangkitkan KerisSumatraBuilder)
  Art/KerisBali/                  keris_bali.glb (ekspor Blender) + Materials/ + Textures/ (ASTC, dari builder)
  Art/KerisSumatra/               keris_sumatra.glb (ekspor Blender) + Materials/ + Textures/
Tools/blender/keris_bali.py, keris_sumatra.py   skrip Blender pemodel keris (+ .blend, *_textures/ hasilnya)
Tools/kartu_qr.py                 kartu kode QR cetak A5
Tools/kartu_penanda.py            kartu penanda 6x6 lama (cadangan)
  Resources/ContentCatalog.asset
  Scenes/Main.unity, Scenes/AR.unity
```

UI dibangun dari kode (tanpa YAML scene), jadi perubahan tampilan cukup di `Scripts/Runtime/UI`.

## Model keris (Blender → GLB → Unity)

Semua model 3D dibuat di **Blender 5.2** oleh skrip (tanpa klik manual), diekspor sebagai **GLB**, lalu diimpor Unity
lewat **glTFast**. Model lama (keris sementara dari `keris3d`, Keris Bali prosedural buatan Unity, Keris Jawa) sudah
dihapus dari proyek (masih ada di riwayat git).

| Keris | Skrip Blender | Isi model | Exploded view |
| --- | --- | --- | --- |
| **Keris Bali** `KERIS_BALI_01` | `Tools/blender/keris_bali.py` | Bilah 40 cm luk 9 pamor banyu tetes, ganja maswatu emas, hulu figur dewa emas + selut berpermata, warangka Branggah kayu pelet, pendok & cincin emas berpermata, **jagrak Karang Boma** 45 cm. 11 hotspot | 5 tahap: hunus → hulu + selut → ganja → warangka → pendok |
| **Keris Sumatra** `KERIS_SUMATRA_01` | `Tools/blender/keris_sumatra.py` | Bilah 36 cm luk 7 pamor wos wutah, hulu burl berukir, mendak, sampir bulan sabit, pendok kuningan, dudukan kayu. 9 hotspot | 4 tahap: hunus → hulu + mendak → ganja → warangka |

Mengubah model:

1. Ubah skripnya, lalu ekspor ulang GLB (+ tekstur, `.blend`, render `Docs/Keris*_Blender_*.png`):

   ```bash
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_bali.py
   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_sumatra.py
   ```
2. Di Unity: **Nusantara AR → Build Keris Bali / Build Keris Sumatra** (atau Setup Everything).

Catatan builder (`GlbArtifact`):
- Setiap objek Blender dipetakan ke satu bagian (`ArtifactPart`); objek tanpa bagian menghentikan build.
- Sumbu Blender (Z atas, depan -Y) → Unity (Y atas, depan -Z); arah hasil glTFast diperiksa otomatis.
- Tekstur mentah GLB (ARGB32, ±70 MB/keris) **tidak** dipakai: material disalin dan memakai PNG sumber yang sama dari
  `Tools/blender/*_textures/`, dikompres ASTC (±10 MB/keris di HP).
- Hotspot diletakkan dengan raycast ke muka depan bagiannya; animasi `Cabut_Keris` di GLB diabaikan (aplikasi memakai exploded view).

## Menambah artefak baru

1. Modelkan di Blender dengan **objek terpisah per bagian** (origin di titik sambung, 1 unit = 1 m, muka depan -Y), ekspor GLB ke `Art/<Nama>/`.
2. Tulis builder seperti `KerisSumatraBuilder`: `GlbArtifact.Load` → `UseCompressedTextures` → `SetStages` → `Front` (hotspot) → `Save`, lalu panggil dari `ProjectSetup.RunAll` dan tambahkan ID-nya ke `PruneCatalog`.
3. Tambahkan baris di `Tools/kartu_qr.py` (kartu QR cetak) dan ID di `[TestFixture]` `ArtifactTests`. Kode kartu 6x6 lama opsional (`MarkerPattern` + `Tools/kartu_penanda.py`).
4. Setel `curatorValidated = true` hanya setelah sign-off kurator (gerbang rilis PRD §6.4).

## Status terhadap PRD v1.1

| Sudah | Catatan |
| --- | --- |
| FR-01 cek ARCore + instal, fallback 3D Viewer | `ARController` |
| FR-02/03 scanning + coaching (tips setelah 15 dtk), reticle, ARAnchor | |
| FR-04 skala 1:1 | ukuran dari cetak biru (Bali 40 cm, Sumatra 36 cm), bukan spesimen |
| FR-05 gestur (rotasi, pinch 0,5-3x, geser 2 jari) + disambiguasi, blokir sentuhan UI | |
| FR-06 Reset Tampilan, Pindahkan | |
| FR-07 exploded view berurutan, bisa dibalik | Bali 5 tahap (termasuk lepas pendok), Sumatra 4 tahap |
| Hunus / Sarungkan: animasi bilah dicabut dari warangka tanpa membongkar | jalur `drawOut` di builder; test `Draw_BladeLeavesSheathWithoutPassingThroughIt` memastikan bilah tidak menembus warangka/gandar |
| FR-08 hotspot per state: titik + garis + label nama bagian langsung di AR (tidak saling tumpuk), redup saat tertutup, ketuk bagian model | |
| FR-09/10 kartu info menempel di samping bagian (menggantikan bottom sheet), 3 tab, audio + pelafalan, sumber, navigasi antar bagian | belum ada rekaman audio; transkrip terpisah ditiadakan (teks tab = isi narasi) |
| FR-11/12/13 katalog → detail (3D Viewer) → AR, onboarding | |
| FR-14 ID/EN | implementasi ringan (`Locale`), bukan Unity Localization package |

**Belum / perlu keputusan:**
- Model keris dibuat di Blender dari cetak biru/lembar acuan (bukan spesimen museum; figur hulu Bali sangat disederhanakan), dan semua teks kuratorial masih **draf**. Filosofi & sejarah sengaja dikosongkan untuk diisi kurator.
- Belum ada rekaman narasi/pelafalan dan font Playfair/Inter (saat ini LiberationSans bawaan TMP).
- Analitik baru menulis ke log (penyedia belum dipilih, PRD §14 #5).
- iOS belum diuji (PRD merekomendasikan Android dulu).
- Uji lapangan (tracking, drift, termal, FPS) wajib di perangkat nyata — belum dilakukan.
