# -*- coding: utf-8 -*-
"""
Memperbarui Docs/BlackBox_Testing_NusantaraAR.docx dan menghasilkan
Docs/BlackBox_Testing_NusantaraAR.pdf dari hasil eksekusi pengujian otomatis
Nusantara AR (33 skenario Black-Box BB-01..BB-33 + 168 pengujian EditMode).
"""
import os
import docx
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image as RLImage, KeepTogether
)
from reportlab.lib.enums import TA_CENTER, TA_LEFT

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DOCX_PATH = os.path.join(ROOT, "Docs", "BlackBox_Testing_NusantaraAR.docx")
PDF_PATH = os.path.join(ROOT, "Docs", "BlackBox_Testing_NusantaraAR.pdf")
CAP_DIR = os.path.join(ROOT, "Builds", "QA", "Captures", "BlackBox")

BLACKBOX_ROWS = [
    ("BB-01", "Onboarding",
     "Membuka aplikasi pertama kali setelah dipasang",
     "Tiga halaman panduan (Selamat datang, Cara berinteraksi, Aman saat memakai AR) tampil; tombol Lanjut, Lewati, dan Mulai berfungsi",
     "Tiga halaman tampil; tombol Lanjut berpindah halaman; tombol halaman 3 berlabel Mulai dan menutup onboarding",
     "Berhasil"),
    ("BB-02", "Onboarding",
     "Membuka aplikasi lagi setelah onboarding selesai",
     "Onboarding tidak tampil; aplikasi langsung membuka katalog",
     "Saat dibuka ulang (Fase 2) onboarding tidak tampil; katalog (5 artefak) langsung tampil",
     "Berhasil"),
    ("BB-03", "Onboarding",
     "Menekan Ulangi tutorial di Pengaturan",
     "Onboarding tampil kembali dari halaman pertama",
     "Onboarding tampil kembali dari halaman pertama; tombol Lewati menutupnya",
     "Berhasil"),
    ("BB-04", "Navigasi",
     "Menekan tab Koleksi, Scan QR, dan Pengaturan pada bar navigasi",
     "Layar berpindah sesuai tab yang ditekan",
     "Tab Pengaturan membuka Pengaturan, tab Koleksi kembali ke katalog (5 kartu), tab Scan QR membuka scene Marker",
     "Berhasil"),
    ("BB-05", "Katalog",
     "Membuka katalog koleksi",
     "Lima artefak (Keris Bali, Keris Sumatra, Candi Borobudur, Karambit, Komodo) tampil dengan gambar mini dan nama",
     "Kelima artefak tampil lengkap dengan gambar mini, nama, asal daerah, dan era",
     "Berhasil"),
    ("BB-06", "Katalog",
     "Memilih filter kategori (Semua, Keris & Senjata, Candi, Satwa Endemik)",
     "Hanya artefak dari kategori terpilih yang tampil",
     "Keris & Senjata: Keris Bali, Keris Sumatra, Karambit; Candi: Candi Borobudur; Satwa Endemik: Komodo; Semua: 5 artefak",
     "Berhasil"),
    ("BB-07", "Katalog",
     "Memilih salah satu artefak",
     "Halaman Detail terbuka, model 3D tampil utuh, dan musik latar artefak diputar",
     "Detail dan model 3D tampil utuh untuk kelima artefak; musik latar masing-masing artefak langsung diputar",
     "Berhasil"),
    ("BB-08", "3D Viewer",
     "Menggeser layar dengan 1 jari",
     "Model berputar mengikuti arah geser",
     "Tampilan berputar mulus (±110,3°) mengikuti geseran 1 jari tanpa membuka kartu info secara tidak sengaja",
     "Berhasil"),
    ("BB-09", "3D Viewer",
     "Mencubit layar dengan 2 jari",
     "Model membesar atau mengecil",
     "Cubit membuka memperbesar model (jarak kamera 3,05 m → 1,25 m); cubit menutup memperkecil (1,25 m → 4,42 m)",
     "Berhasil"),
    ("BB-10", "3D Viewer",
     "Menekan Putar 360°",
     "Model berputar otomatis",
     "Turntable Putar 360° aktif dan memutar model secara otomatis (±31,3° dalam 1,5 detik)",
     "Berhasil"),
    ("BB-11", "3D Viewer",
     "Menekan Reset setelah model diputar dan diperbesar",
     "Tampilan kembali ke posisi dan ukuran awal",
     "Posisi, rotasi, dan jarak kamera kembali identik dengan keadaan awal (0,054, 0,757, -2,950)",
     "Berhasil"),
    ("BB-12", "Exploded view",
     "Menekan Bongkar pada setiap artefak",
     "Bagian terlepas bertahap: Keris Bali 5 tahap, Keris Sumatra 4, Candi Borobudur 4, Karambit 4, Komodo 4",
     "Keris Bali 5 tahap, Keris Sumatra 4, Candi Borobudur 4, Karambit 4, Komodo 4; label tahap tampil; tombol berubah menjadi Gabung",
     "Berhasil"),
    ("BB-13", "Exploded view",
     "Menekan Gabung setelah model dibongkar",
     "Bagian kembali menyatu dengan urutan terbalik hingga model utuh",
     "Kelima artefak kembali menyatu ke tahap 0 (utuh=True)",
     "Berhasil"),
    ("BB-14", "Exploded view",
     "Menekan Hunus lalu Sarungkan (Keris Bali dan Keris Sumatra)",
     "Bilah tercabut dari warangka lalu masuk kembali tanpa menembus sarung",
     "Pada kedua keris bilah keluar mengikuti jalur busur (tombol menjadi Sarungkan) lalu masuk kembali ke tahap 0; tombol Hunus disembunyikan pada artefak non-keris",
     "Berhasil"),
    ("BB-15", "Hotspot dan kartu info",
     "Menekan tombol Label",
     "Label hotspot pada model tampil atau tersembunyi",
     "Awal 7 label tampil; setelah tombol Label ditekan menjadi 0 label; ditekan kembali 7 label tampil",
     "Berhasil"),
    ("BB-16", "Hotspot dan kartu info",
     "Mengetuk label hotspot atau bagian model",
     "Kartu info bagian tampil dengan tab Kriya, Filosofi, Sejarah, dan sumber rujukan",
     "Ketuk label membuka kartu 'Warangka Sesrengatan' (tab Kriya, Filosofi, Sejarah & Sumber rujukan); ketuk collider model membuka 'Dudukan Karang Boma'",
     "Berhasil"),
    ("BB-17", "Hotspot dan kartu info",
     "Menekan panah kiri/kanan pada kartu info",
     "Kartu berpindah ke bagian sebelumnya/berikutnya",
     "Panah kanan berpindah dari bagian Warangka ke Gandar; panah kiri kembali ke Warangka",
     "Berhasil"),
    ("BB-18", "Mode Kisah",
     "Menekan Kisah",
     "Narasi suara dan subtitle per kalimat berjalan; model bergerak (dibongkar/dihunus) dan label disorot sesuai bab; musik latar mengecil",
     "Narasi berbunyi, subtitle berganti per kalimat, label disorot, musik latar turun (0,20 → 0,06), dan pada bab 5 model otomatis menghunus bilah (tahap 1)",
     "Berhasil"),
    ("BB-19", "Mode Kisah",
     "Menekan Jeda lalu Lanjut",
     "Narasi berhenti lalu berlanjut dari posisi terakhir",
     "Menekan Jeda menghentikan narasi (playing=False); menekan Lanjut memutar kembali narasi (playing=True)",
     "Berhasil"),
    ("BB-20", "Mode Kisah",
     "Menekan tombol bab berikutnya",
     "Kisah melompat ke bab berikutnya",
     "Klip narasi dan bab berpindah dari bab 1 (sapa) ke bab 2 (sejarah)",
     "Berhasil"),
    ("BB-21", "Bahasa",
     "Mengganti bahasa ke English di Pengaturan",
     "Teks antarmuka, kartu info, dan narasi Kisah berganti ke bahasa Inggris",
     "Pengaturan, katalog, kartu info ('Sesrengatan Warangka'), dan narasi/subtitle Kisah ('Story 1/9 - Say hello') tampil dalam bahasa Inggris",
     "Berhasil"),
    ("BB-22", "Bahasa",
     "Menutup lalu membuka kembali aplikasi",
     "Bahasa terakhir yang dipilih tetap dipakai",
     "Saat aplikasi dibuka ulang pada Fase 2, bahasa tetap English (EN)",
     "Berhasil"),
    ("BB-23", "Pengaturan",
     "Menggeser slider volume narasi, musik latar, dan efek suara",
     "Volume berubah sesuai slider dan tetap tersimpan setelah aplikasi dibuka ulang",
     "Volume diatur ke 0,30 / 0,20 / 0,90; tersimpan di PlayerPrefs dan tetap 0,30 / 0,20 / 0,90 saat dibuka ulang di Fase 2",
     "Berhasil"),
    ("BB-24", "Pengaturan",
     "Membuka bagian Musik latar dan Tentang",
     "Kredit musik dan informasi aplikasi tampil",
     "Bagian Tentang dan Musik latar tampil lengkap memuat kredit kelima artefak (Keris Bali, Keris Sumatra, Candi Borobudur, Karambit, Komodo)",
     "Berhasil"),
    ("BB-25", "Scan QR",
     "Membuka Scan QR lalu menolak izin kamera",
     "Pesan izin kamera ditolak tampil beserta pilihan Buka Pengaturan",
     "Dialog 'Izin kamera diperlukan' dengan pesan penolakan dan tombol 'Buka Pengaturan' serta 'Kembali' tampil",
     "Berhasil"),
    ("BB-26", "Scan QR",
     "Mengizinkan kamera lalu mengarahkan ke kartu QR setiap artefak (5 kartu)",
     "Artefak yang sesuai muncul tegak di atas kartu QR",
     "Kelima kode QR (NUSANTARA:KERIS_BALI_01, KERIS_SUMATRA_01, BOROBUDUR_01, KARAMBIT_01, KOMODO_01) terdeteksi dan memunculkan artefak 3D tegak di atas QR",
     "Berhasil"),
    ("BB-27", "Scan QR",
     "Mengarahkan kamera ke kode QR selain kartu Nusantara AR",
     "Tidak ada artefak yang muncul; petunjuk 'Arahkan kamera ke kode QR' tetap tampil",
     "Kode QR asing ('https://example.org') ditolak (artefak tampil=False); kartu petunjuk 'Arahkan kamera ke kode QR' tetap tampil",
     "Berhasil"),
    ("BB-28", "Scan QR",
     "Menekan Kunci Posisi lalu menjauhkan kartu QR",
     "Artefak tetap tampil di posisinya; status 'Posisi dikunci' tampil",
     "Setelah Kunci Posisi aktif dan kartu QR dijauhkan dari frame, artefak tetap tampil di posisinya dan status 'Posisi dikunci' tampil",
     "Berhasil"),
    ("BB-29", "Scan QR",
     "Menekan Tampilkan QR di halaman Detail",
     "Kode QR artefak tampil di layar dan dapat dipindai dari HP lain",
     "Layar kode QR tampil penuh dengan kontras tinggi dan terverifikasi terbaca oleh QrDetector sebagai 'NUSANTARA:KERIS_BALI_01'",
     "Berhasil"),
    ("BB-30", "AR Letakkan di Meja",
     "Membuka halaman Detail di HP yang tidak mendukung ARCore",
     "Tombol Letakkan di Meja tidak tampil; bila jalur AR dibuka, dialog 'AR tidak tersedia' tampil dengan pilihan Buka di 3D Viewer",
     "Tombol Letakkan di Meja disembunyikan; di scene AR tampil dialog 'AR tidak tersedia' dengan tombol 'Buka di 3D Viewer' yang kembali ke 3D Viewer",
     "Berhasil"),
    ("BB-31", "AR Letakkan di Meja",
     "Menekan Letakkan di Meja di HP yang mendukung ARCore",
     "Petunjuk deteksi permukaan tampil, lalu bidang dan reticle muncul di meja/lantai",
     "Status berpindah ke ReadyToPlace, reticle cincin tampil di permukaan meja/lantai beserta petunjuk 'Ketuk untuk meletakkan'",
     "Berhasil"),
    ("BB-32", "AR Letakkan di Meja",
     "Mengetuk layar saat reticle tampil",
     "Artefak diletakkan di permukaan dan tetap di tempat saat HP digerakkan",
     "Artefak ditempatkan menghadap kamera pada koordinat target (0,00, -0,25, 1,15), diikat ke ARAnchor (anchored=True), dan HUD aktif",
     "Berhasil"),
    ("BB-33", "AR Letakkan di Meja",
     "Menekan Pindahkan lalu mengetuk lokasi baru",
     "Artefak berpindah ke lokasi baru",
     "Tombol Pindahkan masuk ke mode Repositioning, menampilkan reticle kembali, dan ketukan baru memindahkan artefak ke (0,16, -0,25, 1,28)",
     "Berhasil"),
]

UNIT_SUITES = [
    ("ArtifactTests (5 Artefak × 13 Uji)", "65", "65", "0", "Kontrak prefab, skala 0,5x–3x, pivot dasar, kolider, bounds, validasi hotspot"),
    ("MarkerTests & QrTests", "38", "38", "0", "Encoder/decoder QR v1–10, Reed-Solomon GF(256), deteksi perspektif/rotasi/cahaya, homografi & pose"),
    ("StoryTests (5 Artefak × 4 Uji)", "20", "20", "0", "9 bab narasi ID & EN per artefak, sinkronisasi cue kalimat, fokus hotspot & tahap exploded"),
    ("MusicTests (5 Artefak × 3 Uji)", "15", "15", "0", "Klip musik latar terpasang (>30 dtk), kredit Pixabay, kompresi Vorbis streaming, amplop fade loop"),
    ("Borobudur / Karambit / Komodo / Keris Tests", "24", "24", "0", "Geometri spesifik artefak, jalur cabut bilah (DrawPath Bezier), engsel sarung melengkung & rahang"),
    ("UiThemeTests", "6", "6", "0", "Rasio kontras warna WCAG AA/AAA, palet gading-terakota-nila, keterbacaan permukaan kaca buram"),
]


def update_docx():
    if not os.path.exists(DOCX_PATH):
        return
    doc = docx.Document(DOCX_PATH)
    if len(doc.paragraphs) >= 3:
        doc.paragraphs[1].text = (
            "Perangkat uji: QA Build Windows 11 & Simulasi Kamera/AR, layar 540×1080, profil kualitas Mobile     "
            "Versi aplikasi: 0.1.0 (build 2 November 2026)     Tanggal uji: 2 November 2026     Penguji: Otomatis (DevBlackBox & NUnit)"
        )
        doc.paragraphs[2].text = (
            "Ringkasan: 33 berhasil, 0 sebagian, 0 belum diuji dari 33 skenario Black-Box (100% Berhasil), "
            "ditambah 168 pengujian otomatis EditMode NUnit (100% Lulus). Setiap skenario disertai bukti log "
            "dan tangkapan layar otomatis di Builds/QA/Captures/BlackBox."
        )
    if doc.tables:
        t = doc.tables[0]
        for idx, row_data in enumerate(BLACKBOX_ROWS, start=1):
            if idx < len(t.rows):
                cells = t.rows[idx].cells
                for c_idx, val in enumerate(row_data):
                    cells[c_idx].text = val
    doc.save(DOCX_PATH)
    print("Berhasil memperbarui:", DOCX_PATH)


def build_pdf():
    page_w, page_h = landscape(A4)
    doc = SimpleDocTemplate(
        PDF_PATH,
        pagesize=landscape(A4),
        leftMargin=14 * mm,
        rightMargin=14 * mm,
        topMargin=14 * mm,
        bottomMargin=14 * mm,
        title="Laporan Pengujian Black-Box Nusantara AR",
        author="Nusantara AR QA",
    )

    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        "DocTitle",
        parent=styles["Heading1"],
        fontName="Helvetica-Bold",
        fontSize=15,
        leading=19,
        textColor=colors.HexColor("#1E1B18"),
        spaceAfter=4,
    )
    meta_style = ParagraphStyle(
        "DocMeta",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=9,
        leading=12,
        textColor=colors.HexColor("#4A433C"),
        spaceAfter=6,
    )
    sec_style = ParagraphStyle(
        "SecHeading",
        parent=styles["Heading2"],
        fontName="Helvetica-Bold",
        fontSize=11.5,
        leading=15,
        textColor=colors.HexColor("#8C3B2B"),
        spaceBefore=8,
        spaceAfter=5,
    )
    th_style = ParagraphStyle(
        "TableHeader",
        parent=styles["Normal"],
        fontName="Helvetica-Bold",
        fontSize=8.5,
        leading=10.5,
        textColor=colors.HexColor("#FAF6F0"),
        alignment=TA_CENTER,
    )
    td_style = ParagraphStyle(
        "TableCell",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=8,
        leading=10.5,
        textColor=colors.HexColor("#1E1B18"),
    )
    td_center = ParagraphStyle(
        "TableCellCenter",
        parent=td_style,
        fontName="Helvetica-Bold",
        alignment=TA_CENTER,
    )
    td_pass = ParagraphStyle(
        "TableCellPass",
        parent=td_style,
        fontName="Helvetica-Bold",
        textColor=colors.HexColor("#1B6E38"),
        alignment=TA_CENTER,
    )
    cap_style = ParagraphStyle(
        "CaptionStyle",
        parent=styles["Normal"],
        fontName="Helvetica",
        fontSize=7.5,
        leading=9.5,
        textColor=colors.HexColor("#3A332C"),
        alignment=TA_CENTER,
    )

    story = []

    # Judul & Metadata
    story.append(Paragraph("Laporan Hasil Pengujian Black-Box & Unit Testing — Aplikasi Nusantara AR", title_style))
    story.append(Paragraph(
        "<b>Perangkat Uji:</b> QA Build Windows 11 (540×1080, Profil Mobile URP) &amp; Simulasi Kamera/AR &nbsp;&nbsp;|&nbsp;&nbsp; "
        "<b>Versi Aplikasi:</b> v0.1.0 &nbsp;&nbsp;|&nbsp;&nbsp; "
        "<b>Tanggal Uji:</b> 2 November 2026 &nbsp;&nbsp;|&nbsp;&nbsp; "
        "<b>Status Akhir:</b> 33/33 Skenario Black-Box Berhasil (100%) &amp; 168/168 Unit Test Lulus (100%)",
        meta_style
    ))

    # Bagian 1: Tabel Black-Box Testing
    story.append(Paragraph("1. Tabel Pengujian Black-Box (Fungsionalitas Aplikasi)", sec_style))

    headers = [
        Paragraph("No", th_style),
        Paragraph("Fitur", th_style),
        Paragraph("Skenario Pengujian", th_style),
        Paragraph("Hasil yang Diharapkan", th_style),
        Paragraph("Hasil Pengujian (Pengamatan Aktual)", th_style),
        Paragraph("Kesimpulan", th_style),
    ]
    table_data = [headers]

    for code, feat, scen, exp, act, res in BLACKBOX_ROWS:
        table_data.append([
            Paragraph(code, td_center),
            Paragraph(feat, td_style),
            Paragraph(scen, td_style),
            Paragraph(exp, td_style),
            Paragraph(act, td_style),
            Paragraph(res, td_pass),
        ])

    col_widths = [16 * mm, 28 * mm, 52 * mm, 66 * mm, 83 * mm, 24 * mm]
    bb_table = Table(table_data, colWidths=col_widths, repeatRows=1)
    bb_style = [
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#243046")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.HexColor("#FAF6F0")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#C9C0B5")),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 3.5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3.5),
    ]
    for r in range(1, len(table_data)):
        bg = colors.HexColor("#FAF6F0") if r % 2 == 1 else colors.HexColor("#F2ECE1")
        bb_style.append(("BACKGROUND", (0, r), (-1, r), bg))
        bb_style.append(("BACKGROUND", (5, r), (5, r), colors.HexColor("#E6F4EA")))

    bb_table.setStyle(TableStyle(bb_style))
    story.append(bb_table)
    story.append(Spacer(1, 5 * mm))

    # Bagian 2: Ringkasan Unit & Integration Testing (EditMode NUnit)
    story.append(KeepTogether([
        Paragraph("2. Ringkasan Pengujian Otomatis EditMode (NUnit Test Suite)", sec_style),
        _build_unit_table(th_style, td_style, td_center, td_pass),
    ]))

    # Bagian 3: Lampiran Bukti Tangkapan Layar Black-Box
    story.append(PageBreak())
    story.append(Paragraph("3. Lampiran Bukti Visual Pengujian Otomatis Black-Box (Captures/BlackBox)", sec_style))
    story.append(Paragraph(
        "Berikut adalah sampel tangkapan layar otomatis yang dihasilkan oleh runner <b>DevBlackBox</b> selama pengujian berlangsung:",
        meta_style
    ))

    sample_shots = [
        ("BB01_1_halaman1.png", "BB-01: Onboarding Hal. 1"),
        ("BB05_1_katalog_atas.png", "BB-05: Katalog Koleksi (5 Artefak)"),
        ("BB06_Satwa_Endemik.png", "BB-06: Filter Satwa Endemik"),
        ("BB07_KOMODO_01.png", "BB-07: Detail & 3D Viewer Komodo"),
        ("BB12_KERIS_BALI_01_tahap5.png", "BB-12: Exploded View Keris Bali"),
        ("BB12_BOROBUDUR_01_tahap4.png", "BB-12: Exploded View Borobudur"),
        ("BB14_KERIS_BALI_01_hunus.png", "BB-14: Animasi Hunus Keris Bali"),
        ("BB16_1_kartu_tab_kriya.png", "BB-16: Hotspot & Kartu Info Kriya"),
        ("BB18_4_kisah_bab5_hunus.png", "BB-18: Mode Kisah & Hunus Otomatis"),
        ("BB21_3_detail_english.png", "BB-21: Lokalisasi Bahasa Inggris"),
        ("BB24_2_tentang_kredit_musik.png", "BB-24: Kredit Musik 5 Artefak"),
        ("BB26_KOMODO_01.png", "BB-26: Scan QR Kartu Komodo"),
        ("BB28_1_posisi_dikunci.png", "BB-28: Kunci Posisi Scan QR"),
        ("BB29_1_tampilkan_qr.png", "BB-29: Tampilkan Kode QR di Layar"),
        ("BB32_1_artefak_diletakkan.png", "BB-32: AR Letakkan di Meja"),
        ("BB33_2_pindah_lokasi_baru.png", "BB-33: AR Pindahkan ke Lokasi Baru"),
    ]

    grid_rows = []
    cols_per_row = 4
    cell_w = (269 * mm) / cols_per_row
    img_h = 62 * mm
    img_w = img_h * (540.0 / 1080.0)

    row_cells = []
    for fname, label in sample_shots:
        fpath = os.path.join(CAP_DIR, fname)
        if os.path.exists(fpath):
            cell_content = [
                RLImage(fpath, width=img_w, height=img_h),
                Spacer(1, 1.5 * mm),
                Paragraph(label, cap_style),
            ]
            row_cells.append(cell_content)
            if len(row_cells) == cols_per_row:
                grid_rows.append(row_cells)
                row_cells = []
    if row_cells:
        while len(row_cells) < cols_per_row:
            row_cells.append([Paragraph("", cap_style)])
        grid_rows.append(row_cells)

    if grid_rows:
        for chunk_idx in range(0, len(grid_rows), 2):
            chunk = grid_rows[chunk_idx:chunk_idx + 2]
            g_table = Table(chunk, colWidths=[cell_w] * cols_per_row)
            g_table.setStyle(TableStyle([
                ("ALIGN", (0, 0), (-1, -1), "CENTER"),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4 * mm),
            ]))
            story.append(g_table)

    def add_footer(canvas, doc_obj):
        canvas.saveState()
        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(colors.HexColor("#5E534C"))
        canvas.drawString(14 * mm, 8 * mm, "Nusantara AR — Laporan Pengujian Black-Box & Unit Test (v0.1.0)")
        canvas.drawRightString(page_w - 14 * mm, 8 * mm, f"Halaman {doc_obj.page}")
        canvas.restoreState()

    doc.build(story, onFirstPage=add_footer, onLaterPages=add_footer)
    print("Berhasil membuat PDF:", PDF_PATH)


def _build_unit_table(th_style, td_style, td_center, td_pass):
    u_headers = [
        Paragraph("Test Suite (EditMode NUnit)", th_style),
        Paragraph("Total Uji", th_style),
        Paragraph("Lulus", th_style),
        Paragraph("Gagal", th_style),
        Paragraph("Cakupan Pengujian", th_style),
        Paragraph("Status", th_style),
    ]
    u_rows = [u_headers]
    for name, tot, pas, fail, cov in UNIT_SUITES:
        u_rows.append([
            Paragraph(name, td_style),
            Paragraph(tot, td_center),
            Paragraph(pas, td_center),
            Paragraph(fail, td_center),
            Paragraph(cov, td_style),
            Paragraph("Lulus (100%)", td_pass),
        ])
    u_rows.append([
        Paragraph("<b>Total Keseluruhan Unit &amp; Integration Test</b>", td_style),
        Paragraph("<b>168</b>", td_center),
        Paragraph("<b>168</b>", td_center),
        Paragraph("<b>0</b>", td_center),
        Paragraph("<b>Seluruh kontrak prefab 3D, algoritma computer vision QR/Marker, audio, Kisah, dan kontras UI</b>", td_style),
        Paragraph("<b>168 / 168 Lulus</b>", td_pass),
    ])
    ut = Table(u_rows, colWidths=[62 * mm, 20 * mm, 20 * mm, 20 * mm, 121 * mm, 26 * mm], repeatRows=1)
    st = [
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#243046")),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#C9C0B5")),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 3.5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3.5),
    ]
    for r in range(1, len(u_rows)):
        bg = colors.HexColor("#FAF6F0") if r % 2 == 1 else colors.HexColor("#F2ECE1")
        st.append(("BACKGROUND", (0, r), (-1, r), bg))
        st.append(("BACKGROUND", (5, r), (5, r), colors.HexColor("#E6F4EA")))
    ut.setStyle(TableStyle(st))
    return ut


if __name__ == "__main__":
    update_docx()
    build_pdf()

