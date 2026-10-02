# Membuat kartu kode QR cetak (A5, 300 dpi) untuk setiap artefak di aplikasi (mode Scan QR).
# Isi QR = "NUSANTARA:" + artifactId, harus sama dengan ContentCatalog.QrPrefix / ArtifactData.QrText.
# Jalankan dari mana saja: python Tools/kartu_qr.py  (butuh: pip install pillow qrcode)
# Kartu penanda lama (pola 6x6, cadangan) tetap dibuat oleh Tools/kartu_penanda.py.
from PIL import Image, ImageDraw, ImageFont
import os
import qrcode

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
DPI = 300
mm = lambda v: int(round(v / 25.4 * DPI))
W, H = mm(148), mm(210)
gold, teak, stone = (212, 175, 55), (30, 27, 24), (110, 100, 90)
F = lambda n, s: ImageFont.truetype('C:/Windows/Fonts/' + n, s)
title, sub, body, small = F('georgiab.ttf', 110), F('georgia.ttf', 60), F('arial.ttf', 44), F('arial.ttf', 36)

QR_PREFIX = 'NUSANTARA:'
KARTU = [('Keris Bali', 'KerisBali', 'KERIS_BALI_01'), ('Keris Sumatra', 'KerisSumatra', 'KERIS_SUMATRA_01'),
         ('Candi Borobudur', 'CandiBorobudur', 'BOROBUDUR_01')]


def kartu(nama, berkas, artifact_id):
    isi = QR_PREFIX + artifact_id
    qr = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_M, border=0)
    qr.add_data(isi)
    qr.make(fit=True)
    modules = qr.modules
    n = len(modules)

    card = Image.new('RGB', (W, H), (245, 243, 239)); d = ImageDraw.Draw(card)
    for off, wd in [(mm(6), 10), (mm(8.5), 3)]:
        d.rectangle([off, off, W - off, H - off], outline=gold, width=wd)
    for cx, cy in [(mm(6), mm(6)), (W - mm(6), mm(6)), (mm(6), H - mm(6)), (W - mm(6), H - mm(6))]:
        r = mm(3); d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=gold)

    def center(txt, y, font, fill):
        w = d.textlength(txt, font=font); d.text(((W - w) / 2, y), txt, font=font, fill=fill)
    center('Nusantara AR', mm(14), title, teak)
    center('Kode QR \u2014 ' + nama, mm(27), sub, (107, 91, 46))

    # Sisi QR 8 cm (tanpa zona putih), zona putih 4 modul sesuai standar QR.
    side = mm(80); cell = side / n; quiet = int(round(cell * 4))
    mx = (W - side) // 2; my = mm(36) + quiet
    d.rectangle([mx - quiet, my - quiet, mx + side + quiet, my + side + quiet], fill=(255, 255, 255))
    for v in range(n):
        for u in range(n):
            if modules[v][u]:
                d.rectangle([mx + u * cell, my + v * cell, mx + (u + 1) * cell - 1, my + (v + 1) * cell - 1], fill=(0, 0, 0))

    y = my + side + quiet + mm(5)
    for line, font, fill in [('Cara pakai:', body, teak),
                             ('1. Buka Nusantara AR, pilih "Scan QR (AR)".', body, teak),
                             ('2. Arahkan kamera ke kode QR dari jarak 20\u201340 cm.', body, teak),
                             ('3. ' + nama + ' 3D muncul di atas kode QR. Tekan "Kunci Posisi"', body, teak),
                             ('    untuk menjelajah tanpa terus mengarahkan kamera.', body, teak),
                             ('Cetak tanpa diperkecil (sisi kode QR 8 cm), jangan terlipat,', small, stone),
                             ('dan hindari kertas mengilap. Isi QR: ' + isi, small, stone)]:
        if font is small and line.startswith('Cetak'):
            y += mm(3)
        d.text((mm(16), y), line, font=font, fill=fill); y += int(font.size * 1.4)
    card.save('Docs/KartuQR_%s_A5.png' % berkas, dpi=(DPI, DPI))
    card.save('Docs/KartuQR_%s_A5.pdf' % berkas, 'PDF', resolution=DPI)
    print('Selesai: Docs/KartuQR_%s_A5.pdf (QR versi %d, %d modul)' % (berkas, qr.version, n))


for k in KARTU:
    kartu(*k)
