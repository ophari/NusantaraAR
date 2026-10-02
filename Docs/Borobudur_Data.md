# Candi Borobudur: data acuan model 3D

Lembar data untuk `Tools/blender/candi_borobudur.py`. Angka **bersumber** dipakai apa adanya. Angka **PERKIRAAN**
tidak ditemukan sumber pastinya; nilainya diturunkan dari proporsi supaya total ukuran cocok dengan angka bersumber
(denah 123 m, tinggi 35 m). Jangan dikutip sebagai fakta.

> Catatan makalah: sumber di bawah ini berupa ensiklopedia dan situs berita, tidak memenuhi aturan referensi
> makalah (jurnal 2024–2026, Q1/Q2 atau SINTA 1–3). Untuk makalah, cari rujukan jurnal pengganti.

## Identitas

| Butir | Data | Sumber |
|---|---|---|
| Lokasi | Magelang, Jawa Tengah | [1][2] |
| Masa | Abad ke-9, era Wangsa Syailendra | [1] |
| Arsitek | Gunadharma (menurut cerita rakyat Jawa, bukan prasasti) | [1] |
| Bahan | ±55.000 m³ batu andesit, disusun tanpa semen (tonjolan, lekukan, ekor burung) | [1][2] |
| Pemugaran | Van Erp (selesai 1911); proyek besar 1975–1982 | [1] |
| Warisan Dunia UNESCO | 1991 | [1] |

## Struktur

| Butir | Data | Sumber |
|---|---|---|
| Denah | 123 × 123 m (kaki tambahan); kaki asli ±118 m per sisi | [1][2] |
| Tinggi | 35 m sampai puncak stupa induk; 42 m bila chattra (payung) dihitung | [1][2] |
| Tingkat | 9 tingkat: 6 persegi dan 3 melingkar, dimahkotai stupa induk | [1][2] |
| Kosmologi | Kamadhatu (kaki) · Rupadhatu (teras persegi berlorong) · Arupadhatu (teras melingkar + stupa induk) | [1][2] |
| Mundur teras | Teras pertama mundur 7 m dari tepi kaki; teras berikutnya ±2 m (lorong sempit) | [3][4] |
| Tangga | Di tengah keempat sisi; pintu utama di sisi timur | [1] |
| Lain | 32 arca singa, 100 jaladwara (pancuran air berukir) | [1] |
| Proporsi | Rasio tinggi kaki : badan : kepala = 4 : 6 : 9 | [5] |

## Arca, stupa, relief

| Butir | Data | Sumber |
|---|---|---|
| Relung arca di langkan 1–5 | 104 · 104 · 88 · 72 · 64 = **432** | [6][7] |
| Stupa terawang per teras melingkar | 32 · 24 · 16 = **72**, ditambah 1 stupa induk | [2] |
| Bentuk lubang stupa | Belah ketupat di dua teras bawah, persegi di teras teratas | [2] |
| Total arca Buddha | **504** (432 di relung + 72 di dalam stupa) | [1][8] |
| Mudra arca relung | Timur bhumisparsa, selatan wara, barat dhyana, utara abhaya, langkan ke-5 witarka | [9] |
| Relief | ±2.672 panel: 1.460 naratif dan 1.212 dekoratif, ±2.500 m² | [1] |
| Kaki tertutup | 160 panel Karmawibhangga | [1] |

## Nilai PERKIRAAN di model

Nilai ini dipakai karena sumber pastinya tidak ditemukan. Semuanya ada di blok `DATA` di awal script dan mudah
diganti kalau nanti ada denah ukur resmi (misalnya dari Balai Konservasi Borobudur).

| Butir | Nilai di model |
|---|---|
| Tinggi tiap tingkat (z atas) | Kaki 4,0 · T1 8,4 · T2 12,4 · T3 16,2 · T4 19,8 · T5 20,8 · lingkaran 22,2 / 23,6 / 25,0 · puncak 35,0 m |
| Setengah sisi teras 2–5 | 51,0 · 47,5 · 44,0 · 41,0 m |
| Jari-jari teras melingkar | 26,0 · 19,5 · 13,5 m (lingkar stupa 24,2 · 17,7 · 11,7 m) |
| Stupa terawang | Tinggi 3,72 m, diameter alas 3,16 m; kisi 16×3 (ketupat, baris berselang) atau 12×3 (persegi) |
| Stupa induk | Diameter alas 11,2 m, tinggi 10 m (tanpa chattra) |
| Denah berlekuk | Tonjolan tengah 14 × 1 m; 3 lekuk sudut @1,2 m |
| Tangga, langkan, relung | Tangga lebar 2,4 m (ceruk 2,5 m); pagar tebal 0,9 m dan tinggi 1,0 m; relung 1,3 × 1,1 × 2,1 m |
| Arca | Buddha duduk ±0,9 m di relung, ±1,26 m di stupa; mudra tidak dimodelkan |

**Tidak dimodelkan:** jaladwara, arca singa, relief naratif asli (dinding lorong memakai tekstur panel bergaya),
chattra.

## Model yang dihasilkan

- Skala asli, 1 unit = 1 m. Sumbu Z ke atas. Sisi timur (pintu utama) menghadap −Y.
- Jumlah segitiga ±308 ribu sebelum decimate. Bagian terberat adalah stupa terawang (±95 ribu) dan arca (±125 ribu).
- Hierarki objek:
  - `Candi_Borobudur` > `Kamadhatu` > `Kaki_Candi`
  - `Candi_Borobudur` > `Rupadhatu` > `Teras_1..5`, `Langkan_1..5`, `Arca_Langkan_1..5`
  - `Candi_Borobudur` > `Tangga_Gapura` > `Tangga_Kaki`, `Tangga_1..5`, `Tangga_Melingkar_1..3`, `Gapura_1..5`
  - `Candi_Borobudur` > `Arupadhatu` > `Teras_Melingkar_1..3`, `Stupa_Terawang_1..3`, `Arca_Stupa_1..3`, `Stupa_Induk`
  - Tangga, gapura, dan arca stupa dipisah per tingkat supaya bisa ikut terangkat saat dibongkar di aplikasi.
- Script memeriksa sendiri jumlah relung (432), stupa (72), dan arca (504), serta ukuran 123 × 123 × 35 m.

## Tekstur & material (prosedural, 2048 px, sudah di-pack di .blend)

| Material | Dipakai di | Isi |
|---|---|---|
| `batu_andesit` | dinding teras, langkan, relung, tangga, gapura, dinding teras melingkar | balok 0,5 × 0,25 m tanpa semen, tepi aus/gompal, noda jamur kerak hitam, alur air hujan, lumut di nat, bercak lumut kerak terang |
| `lantai_batu` | lantai lorong, pelataran, lantai teras melingkar | batu hampar lebih besar dan lebih halus, lumut di nat |
| `relief_lorong` | dinding utama lorong 1–4 | panel bergaya (figur berdiri/duduk berprabha, pohon, pendapa), pelipit manik & kelopak teratai, pilaster bersulur; **bukan** reproduksi relief asli |
| `stupa_andesit` / `stupa_terawang` | stupa induk / 72 stupa berlubang (dua sisi) | andesit lebih lapuk dan gelap |
| `arca_andesit` | 504 arca | batu halus tanpa nat |

Setiap material punya peta warna, ORM (AO/kekasaran), dan normal map.

## Ekspor GLB (tanpa render)

Ekspor cepat dan tidak butuh render. Selalu ekspor lewat script, karena script hanya mengekspor `Candi_Borobudur`
beserta turunannya (objek render `Tanah`, `Matahari`, dan kamera tidak ikut):

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/candi_borobudur.py -- --export
```

- Hasil GLB: `Assets/NusantaraAR/Art/CandiBorobudur/candi_borobudur.glb`.
- Setelah itu jalankan menu Unity **Nusantara AR → Build Candi Borobudur** (atau `Tools/compile_and_test.ps1`).

## Render (opsional, berat)

File .blend sudah siap render:
- Mesin render Cycles, 256 sampel dengan denoise, 1920×1080.
- Langit fisik dan matahari pagi dari timur-tenggara.
- Rumput sebagai tanah.
- 4 kamera: `Kamera` (hero), `Kamera_Depan` (tampak timur, ortografis), `Kamera_Stupa`, dan `Kamera_Lorong`.

Cara render:
- Lewat Blender: pilih kamera di Outliner, tekan Ctrl+Numpad 0, lalu F12. Kalau ada GPU, aktifkan dulu di
  Edit > Preferences > System > Cycles Render Devices.
- Lewat script: tambahkan `--render`. Hasilnya `Docs/CandiBorobudur_Blender_{hero,depan,stupa,lorong}.png`.

## Di aplikasi (Unity)

Builder: `Assets/NusantaraAR/Scripts/Editor/CandiBorobudurBuilder.cs`. ID artefak `BOROBUDUR_01`, kategori **Candi**.

- **Skala 1:200.** Tapak 61,5 cm dan tinggi 17,5 cm, supaya muat di meja (AR) dan di atas kartu QR. Model diperkecil di
  child `Model`, sedangkan root prefab tetap skala 1.
- **Bagian = tingkat.** Ada 10 bagian: `Kaki`, `Teras1..5`, `Lingkar1..3`, `StupaInduk`. Langkan, relung-arca, tangga, dan
  gapura ikut tingkat tempatnya berdiri.
- **Bongkar 4 tahap.** Tingkat dipisah ke atas dengan jarak 6 m pada skala asli:
  1. stupa induk diangkat;
  2. tiga teras melingkar (Arupadhatu) dipisah;
  3. teras persegi 3–5 dipisah;
  4. teras persegi 1–2 dipisah dari kaki (Kamadhatu).
- **10 hotspot:** kaki, langkan & relung, tangga & gapura, relief, batu andesit, teras persegi, penemuan & pemugaran, teras
  melingkar, stupa terawang, dan stupa induk. Teksnya bersumber dari daftar di bawah; rujukannya ada di `CandiRefs.cs`.
- **Mode Kisah:** 9 bab ID+EN di `Tools/narasi/kisah.json` (suara TTS `kisah_tts.py`).
- **Kartu:** kartu QR berisi `NUSANTARA:BOROBUDUR_01`; kode kartu penanda lama `0xE3B1`.
- **Performa di HP.** MeshCollider arca dan kisi stupa dilepas. Kalau model masih berat di HP, kurangi kisi stupa di
  `tpl_stupa_terawang` atau poligon arca di `tpl_arca`, lalu ekspor ulang.

## Sumber

1. Wikipedia (EN), "Borobudur". https://en.wikipedia.org/wiki/Borobudur
2. Wikipedia (ID), "Borobudur". https://id.wikipedia.org/wiki/Borobudur
3. Facts and Details, "Borobudur: History, Architecture, Components, Bas-Reliefs". https://factsanddetails.com/indonesia/Places/sub6_10b/entry-6775.html
4. New World Encyclopedia, "Borobudur". https://www.newworldencyclopedia.org/entry/Borobudur
5. F. Musacchio (2025), "Borobudur: A Buddhist mandala in stone". https://www.fabriziomusacchio.com/weekend_stories/told/2025/2025-10-26-borobudur/
6. Kompas Regional (13 Nov 2024), "Arca Candi Borobudur: Jumlah, Jenis, Letak". https://regional.kompas.com/read/2024/11/13/222228878/arca-candi-borobudur-jumlah-jenis-letak-dan-mitos-kunto-bimo
7. idsejarah.net, "Sejarah Singkat Candi Borobudur". https://idsejarah.net/2016/08/candi-borobudur.html
8. Britannica, "Borobudur". https://www.britannica.com/topic/Borobudur
9. Our Buddhism World, "How Many Buddha Statues Are There in Borobudur Temple". https://www.ourbuddhismworld.com/archives/3317
