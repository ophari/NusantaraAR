# Membuat kartu penanda cetak (A5, 300 dpi) untuk setiap artefak di aplikasi: Keris Bali (B532), Keris Sumatra (F0E4),
# Candi Borobudur (E3B1), Karambit (C616), dan Komodo (DC4E).
# Kode harus sama dengan MarkerPattern.cs. Jalankan dari mana saja: python Tools/kartu_penanda.py  (butuh: pip install pillow)
from PIL import Image, ImageDraw, ImageFont
import os
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
DPI = 300
mm = lambda v: int(round(v / 25.4 * DPI))
W, H = mm(148), mm(210)
gold, teak, stone = (212, 175, 55), (30, 27, 24), (110, 100, 90)
F = lambda n, s: ImageFont.truetype('C:/Windows/Fonts/' + n, s)
title, sub, body, small = F('georgiab.ttf', 110), F('georgia.ttf', 60), F('arial.ttf', 44), F('arial.ttf', 36)

KARTU = [('Keris Bali', 'KerisBali', 0xB532), ('Keris Sumatra', 'KerisSumatra', 0xF0E4),
         ('Candi Borobudur', 'CandiBorobudur', 0xE3B1), ('Karambit', 'Karambit', 0xC616),
         ('Komodo', 'Komodo', 0xDC4E)]


def kartu(nama, berkas, code):
    card = Image.new('RGB', (W, H), (245, 243, 239)); d = ImageDraw.Draw(card)
    for off, wd in [(mm(6), 10), (mm(8.5), 3)]:
        d.rectangle([off, off, W - off, H - off], outline=gold, width=wd)
    for cx, cy in [(mm(6), mm(6)), (W - mm(6), mm(6)), (mm(6), H - mm(6)), (W - mm(6), H - mm(6))]:
        r = mm(3); d.polygon([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill=gold)

    def center(txt, y, font, fill):
        w = d.textlength(txt, font=font); d.text(((W - w) / 2, y), txt, font=font, fill=fill)
    center('Nusantara AR', mm(14), title, teak)
    center('Kartu Penanda \u2014 ' + nama, mm(27), sub, (107, 91, 46))
    side = mm(80); cell = side / 6; quiet = int(cell)
    mx = (W - side) // 2; my = mm(38) + quiet
    d.rectangle([mx - quiet, my - quiet, mx + side + quiet, my + side + quiet], fill=(255, 255, 255))

    def black(u, v):
        if u in (0, 5) or v in (0, 5):
            return True
        bit = (v - 1) * 4 + (u - 1)
        return (code >> (15 - bit)) & 1 == 1
    for v in range(6):
        for u in range(6):
            if black(u, v):
                d.rectangle([mx + u * cell, my + v * cell, mx + (u + 1) * cell - 1, my + (v + 1) * cell - 1], fill=(0, 0, 0))
    y = my + side + quiet + mm(6)
    for line, font, fill in [('Cara pakai:', body, teak),
                             ('1. Buka Nusantara AR, pilih "Scan Kartu (AR)".', body, teak),
                             ('2. Arahkan kamera ke kotak hitam dari jarak 20\u201340 cm.', body, teak),
                             ('3. ' + nama + ' 3D muncul di atas kartu. Tekan "Kunci Posisi"', body, teak),
                             ('    untuk menjelajah tanpa terus mengarahkan kamera.', body, teak),
                             ('Cetak tanpa diperkecil (sisi kotak hitam 8 cm), jangan terlipat,', small, stone),
                             ('dan hindari kertas mengilap. Kode kartu: %04X' % code, small, stone)]:
        if font is small and line.startswith('Cetak'):
            y += mm(3)
        d.text((mm(16), y), line, font=font, fill=fill); y += int(font.size * 1.4)
    card.save('Docs/KartuPenanda_%s_A5.png' % berkas, dpi=(DPI, DPI))
    card.save('Docs/KartuPenanda_%s_A5.pdf' % berkas, 'PDF', resolution=DPI)
    print('Selesai: Docs/KartuPenanda_%s_A5.pdf' % berkas)


for k in KARTU:
    kartu(*k)
