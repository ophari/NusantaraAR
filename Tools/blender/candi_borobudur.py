# Candi Borobudur (Magelang, Jawa Tengah; abad ke-9, Wangsa Syailendra) -> Blender (-> GLB).
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/candi_borobudur.py
# Bawaan HANYA membangun scene + menyimpan .blend (ringan: tanpa render, tanpa ekspor). Tambahkan setelah "--":
#   --export   tulis GLB ke Assets/NusantaraAR/Art/CandiBorobudur/candi_borobudur.glb
#   --render   render Cycles dari 4 kamera ke Docs/CandiBorobudur_Blender_{hero,depan,stupa,lorong}.png
# Contoh di laptop lain:  blender.exe --background --factory-startup --python Tools/blender/candi_borobudur.py -- --export --render
#
# Hasil:
#   Tools/blender/candi_borobudur.blend             (file kerja, tekstur di-pack ke dalamnya; di luar Assets)
#   Tools/blender/candi_borobudur_textures/*.png    (sumber tekstur: warna, ORM, normal map)
#
# Data & sumber: Docs/Borobudur_Data.md. Angka bersumber dipakai apa adanya; angka bertanda PERKIRAAN tidak
# ditemukan sumber pastinya dan diturunkan dari proporsi agar cocok dengan angka bersumber (123 m, 35 m, dst.).
#
# Ruang Blender: Z ke atas, 1 unit = 1 m, skala asli. Muka depan (sisi timur, pintu utama) menghadap -Y,
# barat +Y, utara +X, selatan -X. Titik asal = pusat denah di permukaan tanah.
# Hierarki: Candi_Borobudur > Kamadhatu (Kaki_Candi)
#                           + Rupadhatu (Teras_1..5, Langkan_1..5, Arca_Langkan_1..5)
#                           + Tangga_Gapura (Tangga_Kaki, Tangga_1..5, Tangga_Melingkar_1..3, Gapura_1..5)
#                           + Arupadhatu (Teras_Melingkar_1..3, Stupa_Terawang_1..3, Arca_Stupa_1..3, Stupa_Induk)
# Tangga, gapura, dan arca stupa dipisah per tingkat: di Unity (CandiBorobudurBuilder) tiap tingkat = satu bagian
# yang diangkat sendiri saat bongkar.
# Tidak dimodelkan: 100 jaladwara, 32 arca singa, relief naratif asli (relief = tekstur bergaya), chattra.

import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from keris_sumatra import MB, build, lathe, spline, sstep, write_png, uvgrid, fbm2, blur, mix, \
    normal_from_height, orm, pbr, export_glb, look, TAU

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'CandiBorobudur')
GLB = os.path.join(ART, 'candi_borobudur.glb')
TEX = os.path.join(HERE, 'candi_borobudur_textures')
BLEND = os.path.join(HERE, 'candi_borobudur.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
EXPORT = '--export' in ARGS
RENDER = '--render' in ARGS

# ============================================================ data

SISI_DASAR = 123.0                  # m, denah kaki 123 x 123 m (Wikipedia EN/ID)
TINGGI = 35.0                       # m, tanah -> puncak stupa induk, tanpa chattra (Wikipedia EN/ID)
MUNDUR_TERAS_1 = 7.0                # m, teras pertama mundur 7 m dari tepi kaki (New World Encyclopedia)
RELUNG = (104, 104, 88, 72, 64)     # relung arca di langkan 1..5, jumlah 432 (Kompas 2024; idsejarah)
STUPA_TERAWANG = (32, 24, 16)       # stupa berlubang per teras melingkar, jumlah 72 (id.wikipedia)
LUBANG = ('ketupat', 'ketupat', 'persegi')   # lubang belah ketupat di 2 teras bawah, persegi di atas (id.wikipedia)

# Teras persegi: (nama, setengah sisi terluar, z bawah, z atas, relief di dinding, dalam ceruk tangga, tonjolan tangga).
# Setengah sisi Kaki = 123/2 dan Teras_1 = 61.5 - 7 bersumber; sisanya (lorong +-2 m) dan semua tinggi = PERKIRAAN.
TERAS = [
    ('Kaki_Candi', SISI_DASAR / 2, 0.0, 4.0, False, 2.5, 1.0),
    ('Teras_1', SISI_DASAR / 2 - MUNDUR_TERAS_1, 4.0, 8.4, True, 2.5, 1.0),
    ('Teras_2', 51.0, 8.4, 12.4, True, 2.5, 1.0),
    ('Teras_3', 47.5, 12.4, 16.2, True, 2.5, 1.0),
    ('Teras_4', 44.0, 16.2, 19.8, True, 2.5, 1.0),
    ('Teras_5', 41.0, 19.8, 20.8, False, 0.8, 0.4),     # pelataran rendah tempat teras melingkar berdiri
]
# Teras melingkar: (jari-jari, z bawah, z atas) dan jari-jari lingkar stupa - PERKIRAAN (jarak antarstupa +-4,7 m).
MELINGKAR = [(26.0, 20.8, 22.2), (19.5, 22.2, 23.6), (13.5, 23.6, 25.0)]
RING_R = (24.2, 17.7, 11.7)

PD, PW = 1.0, 7.0                   # tonjolan tengah tiap sisi: dalam, setengah lebar (PERKIRAAN)
N_LEKUK, S_LEKUK = 3, 1.2           # sudut berlekuk: jumlah lekuk, ukuran lekuk (PERKIRAAN)
SW = 1.2                            # setengah lebar tangga (PERKIRAAN)
GW = SW + 0.9                       # setengah celah langkan di gapura (tiang gapura 0.9 m)
LANGKAN_INSET, LANGKAN_H = 0.5, 1.0 # garis tengah langkan dari garis dinding teras; tinggi pagar (PERKIRAAN)


# ============================================================ util denah

def rot(p, k):
    x, y = p
    for _ in range(k % 4):
        x, y = -y, x
    return (x, y)


def mirror(p):                      # cermin terhadap diagonal x = -y (kuadran sisi -Y ke sisi +X)
    return (-p[1], -p[0])


def dedupe(P, closed):
    out = []
    for p in P:
        if not out or math.hypot(p[0] - out[-1][0], p[1] - out[-1][1]) > 1e-6:
            out.append((float(p[0]), float(p[1])))
    if closed and len(out) > 1 and math.hypot(out[0][0] - out[-1][0], out[0][1] - out[-1][1]) < 1e-6:
        out.pop()
    return out


def octant(H, start):
    """Seperdelapan denah: dari tengah sisi -Y ke arah +X sampai diagonal. H = setengah sisi di tonjolan tengah."""
    h = H - PD
    a = h - 2 * N_LEKUK * S_LEKUK
    pts = list(start) + [(PW, -H), (PW, -h), (a, -h)]
    for k in range(1, N_LEKUK + 1):  # sudut berlekuk: anak tangga masuk menuju diagonal
        pts += [(a + (k - 1) * S_LEKUK, -h + k * S_LEKUK), (a + k * S_LEKUK, -h + k * S_LEKUK)]
    return pts


def ring_outline(H, nd):
    """Denah tertutup (berlawanan jarum jam) dengan ceruk tangga sedalam nd di tengah keempat sisi."""
    start = [(0.0, -H + nd), (SW, -H + nd), (SW, -H)] if nd > 0 else [(0.0, -H)]
    o = octant(H, start)
    quad = o + [mirror(p) for p in o[::-1]][1:-1]
    return dedupe([rot(p, k) for k in range(4) for p in quad], True)


def langkan_paths(H):
    """Empat ruas pagar terbuka, masing-masing dari celah gapura satu sisi ke celah sisi berikutnya."""
    o = octant(H, [(GW, -H)])
    seg = o + [mirror(p) for p in o[::-1]][1:]
    return [dedupe([rot(p, k) for p in seg], False) for k in range(4)]


def offset_path(P, d, closed):
    """Geser garis sejauh d ke luar (normal kanan arah jalan), sambungan miter."""
    P = np.asarray(P, float); n = len(P)
    E = (np.roll(P, -1, 0) - P) if closed else np.diff(P, axis=0)
    E = E / np.linalg.norm(E, axis=1)[:, None]
    N = np.stack([E[:, 1], -E[:, 0]], 1)
    out = np.empty_like(P)
    for i in range(n):
        if closed:
            n1, n2 = N[i - 1], N[i]
        else:
            n1, n2 = N[max(i - 1, 0)], N[min(i, n - 2)]
        out[i] = P[i] + d * (n1 + n2) / (1.0 + n1 @ n2)
    return out


def cumlen(P, closed):
    P = np.asarray(P, float)
    Q = np.vstack([P, P[:1]]) if closed else P
    return np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(Q, axis=0), axis=1))])


def place_along(path, n):
    """n titik berjarak sama sepanjang garis terbuka: (posisi xy, sudut rotasi Z agar muka -Y lokal menghadap luar)."""
    P = np.asarray(path, float); L = cumlen(P, False); out = []
    for k in range(n):
        s = L[-1] * (k + 0.5) / n
        i = int(np.clip(np.searchsorted(L, s) - 1, 0, len(P) - 2))
        t = (P[i + 1] - P[i]) / np.linalg.norm(P[i + 1] - P[i])
        out.append((P[i] + t * (s - L[i]), math.atan2(-t[0], t[1]) + math.pi / 2))
    return out


def rz(th):
    c, s = math.cos(th), math.sin(th)
    return np.array([[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]])


# ============================================================ pembangun bentuk

def box(mb, x0, x1, y0, y1, z0, z1, k=0, bottom=False):
    """Balok; k = putaran 90 derajat di sekitar Z. UV planar dari koordinat lokal (tekstur batu 4 m)."""
    c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
         (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    ids = [mb.point((*rot(p[:2], k), p[2])) for p in c]
    faces = [((0, 1, 5, 4), 'xz'), ((1, 2, 6, 5), 'yz'), ((2, 3, 7, 6), 'xz'), ((3, 0, 4, 7), 'yz'),
             ((4, 5, 6, 7), 'xy')] + ([((3, 2, 1, 0), 'xy')] if bottom else [])
    for f, pl in faces:
        ax = {'xz': (0, 2), 'yz': (1, 2), 'xy': (0, 1)}[pl]
        mb.face([ids[i] for i in f], [(c[i][ax[0]] / 4, c[i][ax[1]] / 4) for i in f])


def lathe_rows(rows, n, off=0.0):
    """rows: (r, z[, ky, dy]) -> grid (baris, kolom, 3). ky memipihkan arah Y, dy menggeser baris ke depan/belakang."""
    a = TAU * np.arange(n) / n + off
    P = np.zeros((len(rows), n, 3))
    for i, row in enumerate(rows):
        r, z = row[0], row[1]
        ky = row[2] if len(row) > 2 else 1.0
        dy = row[3] if len(row) > 3 else 0.0
        P[i, :, 0] = r * np.cos(a); P[i, :, 1] = dy + r * ky * np.sin(a); P[i, :, 2] = z
    return P


def stamp(mb, tpl, th, pos, scale=1.0):
    """Salin templat (MB) dengan skala, putar Z, geser -- elemen berulang digabung dalam satu objek."""
    if not hasattr(tpl, 'V'):
        tpl.V = np.asarray(tpl.v, float)
    b = len(mb.v)
    mb.v.extend((tpl.V * scale @ rz(th).T + np.asarray(pos, float)).tolist())
    mb.f.extend([[i + b for i in f] for f in tpl.f])
    mb.uv.extend(tpl.uv)
    mb.m.extend(tpl.m)


def stupa_kecil(mb, z, s=1.0):
    """Stupa mahkota kecil di atas relung/gapura."""
    rows = [(0.34 * s, z), (0.36 * s, z + 0.08 * s), (0.30 * s, z + 0.30 * s), (0.16 * s, z + 0.45 * s),
            (0.07 * s, z + 0.52 * s)]
    mb.grid(lathe_rows(rows, 8), end=(0, 0, z + 0.62 * s))


# ============================================================ templat elemen berulang (muka depan -Y lokal)

def tpl_relung():
    """Relung arca di atas pagar langkan: dinding belakang, dua tiang, ambang, atap bertingkat, stupa kecil."""
    mb = MB()
    box(mb, -0.65, 0.65, 0.05, 0.45, 0.0, 1.2)
    box(mb, -0.65, -0.45, -0.45, 0.05, 0.0, 0.95)
    box(mb, 0.45, 0.65, -0.45, 0.05, 0.0, 0.95)
    box(mb, -0.65, 0.65, -0.45, 0.05, 0.95, 1.2)
    box(mb, -0.74, 0.74, -0.54, 0.54, 1.2, 1.36, bottom=True)
    box(mb, -0.56, 0.56, -0.42, 0.42, 1.36, 1.50)
    stupa_kecil(mb, 1.50)
    return mb


def tpl_gapura():
    """Gapura di celah langkan: dua tiang, ambang, blok kepala kala, atap bertingkat, stupa kecil."""
    mb = MB()
    for sg in (-1, 1):
        box(mb, *sorted((sg * SW, sg * (SW + 0.9))), -0.45, 0.45, 0.0, 2.8)
    box(mb, -(SW + 0.9), SW + 0.9, -0.45, 0.45, 2.8, 3.4, bottom=True)
    box(mb, -0.6, 0.6, -0.62, -0.45, 2.7, 3.5, bottom=True)
    box(mb, -(SW + 1.05), SW + 1.05, -0.6, 0.6, 3.4, 3.62, bottom=True)
    box(mb, -(SW + 0.5), SW + 0.5, -0.45, 0.45, 3.62, 3.84)
    stupa_kecil(mb, 3.84, 1.4)
    return mb


def tpl_arca():
    """Arca Buddha duduk bersila low-poly (tinggi 1.05 m pada skala 1). Mudra tidak dimodelkan."""
    mb = MB()
    alas = [(0.44, 0.0, 0.75), (0.46, 0.05, 0.75), (0.40, 0.10, 0.75), (0.45, 0.16, 0.75)]
    mb.grid(lathe_rows(alas, 8), start=(0, 0, 0), end=(0, 0, 0.16))
    tubuh = [(0.43, 0.16, 0.68, -0.04), (0.44, 0.23, 0.68, -0.04), (0.34, 0.32, 0.70, -0.02),
             (0.22, 0.38, 0.75, 0.02), (0.23, 0.55, 0.70, 0.03), (0.20, 0.66, 0.72, 0.03),
             (0.11, 0.73, 0.90, 0.03), (0.065, 0.76, 1.0, 0.03), (0.11, 0.80, 1.0, 0.03),
             (0.125, 0.88, 1.0, 0.03), (0.10, 0.95, 1.0, 0.03), (0.06, 1.00, 1.0, 0.03)]
    mb.grid(lathe_rows(tubuh, 8), end=(0, 0.03, 1.05))
    return mb


def tpl_stupa_terawang(kind):
    """Stupa berlubang: bantalan teratai, genta berkisi (lubang belah ketupat berselang-seling atau persegi),
    harmika persegi, yasti segi-8. Tinggi 3.72 m, diameter alas 3.16 m (PERKIRAAN)."""
    C, R, stagger = (16, 3, True) if kind == 'ketupat' else (12, 3, False)
    NC = 2 * C
    bell, _ = spline([(1.40, 0.52), (1.47, 0.95), (1.40, 1.55), (1.12, 2.15), (0.66, 2.58), (0.36, 2.72)], 80)
    tb = np.linspace(0.0, 1.0, len(bell))
    va, vb = 0.14, 0.78

    def S(u, v):
        r = np.interp(v, tb, bell[:, 0]); z = np.interp(v, tb, bell[:, 1])
        return (r * math.cos(TAU * u), r * math.sin(TAU * u), z)

    def uv(u, z):
        return (u * 2.3, z / 4)

    def ring(vs):
        return np.array([[S(k / NC, v) for k in range(NC)] for v in vs])

    mb = MB()
    uvf = lambda P: (lambda i, j: uv(j / NC, P[i, 0, 2]))
    alas = lathe_rows([(1.58, 0.0), (1.58, 0.22), (1.48, 0.28), (1.48, 0.45)], NC)
    P = np.vstack([alas, ring([0.0, va / 2, va])])
    mb.grid(P, uv=uvf(P), start=(0, 0, 0))

    # Pita berkisi: tiap sel = 8 titik tepi sel + 8 titik tepi lubang -> 8 quad.
    outer = [(0, 0), (0.5, 0), (1, 0), (1, 0.5), (1, 1), (0.5, 1), (0, 1), (0, 0.5)]
    if kind == 'ketupat':
        r = 0.36
        inner = [(0.5 - r / 2, 0.5 - r / 2), (0.5, 0.5 - r), (0.5 + r / 2, 0.5 - r / 2), (0.5 + r, 0.5),
                 (0.5 + r / 2, 0.5 + r / 2), (0.5, 0.5 + r), (0.5 - r / 2, 0.5 + r / 2), (0.5 - r, 0.5)]
    else:
        r = 0.30
        inner = [(0.5 - r, 0.5 - r), (0.5, 0.5 - r), (0.5 + r, 0.5 - r), (0.5 + r, 0.5),
                 (0.5 + r, 0.5 + r), (0.5, 0.5 + r), (0.5 - r, 0.5 + r), (0.5 - r, 0.5)]
    for row in range(R):
        shift = 0.5 * (row % 2) if stagger else 0.0
        v0 = va + (vb - va) * row / R; v1 = va + (vb - va) * (row + 1) / R
        for c in range(C):
            def pt(a, b):
                u = (c + shift + a) / C; p = S(u, v0 + b * (v1 - v0))
                return mb.point(p), uv(u, p[2])
            O = [pt(*q) for q in outer]; I = [pt(*q) for q in inner]
            for k in range(8):
                q = (k + 1) % 8
                mb.face([O[k][0], O[q][0], I[q][0], I[k][0]], [O[k][1], O[q][1], I[q][1], I[k][1]])
    # Tepi sel (bergeser setengah sel atau tidak) selalu jatuh di kelipatan 1/NC -> cincin padat menyambung rapat.
    P = ring([vb, (vb + 1) / 2, 1.0])
    ztop = bell[-1, 1]
    mb.grid(P, uv=uvf(P), end=(0, 0, ztop))
    harmika = lathe_rows([(0.50, ztop), (0.50, ztop + 0.33), (0.56, ztop + 0.38), (0.56, ztop + 0.44)], 4, math.pi / 4)
    mb.grid(harmika, start=(0, 0, ztop), end=(0, 0, ztop + 0.44))
    mb.grid(lathe_rows([(0.15, ztop + 0.44), (0.12, ztop + 0.9)], 8), end=(0, 0, ztop + 1.0))
    return mb


# ============================================================ bagian candi

def part_teras(T):
    name, half, z0, z1, relief, nd, out = T
    if name == 'Kaki_Candi':
        groups = [([(0.5, z0), (0.5, z0 + 0.35), (0.25, z0 + 0.55), (0.0, z0 + 0.7), (0.0, z1 - 0.5),
                    (0.15, z1 - 0.4), (0.3, z1 - 0.2), (0.3, z1)], 0)]
    elif relief:
        groups = [([(0.35, z0), (0.35, z0 + 0.25), (0.15, z0 + 0.4), (0.0, z0 + 0.55)], 0),
                  ([(0.0, z0 + 0.55), (0.0, z1 - 0.55)], 1),
                  ([(0.0, z1 - 0.55), (0.15, z1 - 0.45), (0.3, z1 - 0.3), (0.3, z1)], 0)]
    else:
        groups = [([(0.2, z0), (0.2, z0 + 0.15), (0.0, z0 + 0.3), (0.0, z1 - 0.25), (0.15, z1 - 0.12),
                    (0.3, z1)], 0)]
    H = wall_line(T)
    poly = np.array(ring_outline(H, nd))
    L = cumlen(poly, True)
    mb = MB()
    for rows, mat in groups:
        P = np.stack([np.column_stack([offset_path(poly, d, True), np.full(len(poly), z)]) for d, z in rows])
        U = L / (5.0 if mat == 1 else 4.0)                      # relief: 2 panel per 5 m; batu: 4 m per ubin
        Vv = [0.0, 1.0] if mat == 1 else [z / 4 for _, z in rows]
        mb.mat = mat
        mb.grid(P, uv=lambda i, j, U=U, Vv=Vv: (U[j], Vv[i]))
    top = offset_path(poly, groups[-1][0][-1][0], True)
    mb.mat = 2                                                  # lantai lorong/pelataran: batu hampar
    mb.face([mb.point((x, y, z1)) for x, y in top], [(x / 4, y / 4) for x, y in top])
    return mb


def wall_line(T):
    """Setengah sisi garis dinding (di tonjolan tengah) sehingga tepi lis terluar = setengah sisi data."""
    name, half = T[0], T[1]
    return half - (0.5 if name == 'Kaki_Candi' else 0.35 if T[4] else 0.3)


PROFIL_LANGKAN = [(-0.45, 0.0), (0.5, 0.0), (0.5, 0.18), (0.45, 0.25), (0.45, 0.82), (0.52, 0.9),
                  (0.52, LANGKAN_H), (-0.52, LANGKAN_H), (-0.52, 0.9), (-0.45, 0.82)]


def part_langkan(T, n_relung, relung):
    """Pagar langkan di tepi atas teras T + relung arca. Mengembalikan (mb, daftar posisi relung)."""
    H = wall_line(T); z = T[3]
    mb = MB(); spots = []
    per = n_relung // 4
    for seg in langkan_paths(H):
        center = offset_path(seg, -LANGKAN_INSET, False)
        Ls = cumlen(center, False) / 4
        prof = np.array(PROFIL_LANGKAN + PROFIL_LANGKAN[:1])
        Lp = cumlen(prof, False) / 4
        P = np.stack([np.column_stack([offset_path(center, d, False), np.full(len(center), z + h)])
                      for d, h in PROFIL_LANGKAN], 1)
        mb.grid(P, uv=lambda i, j, Ls=Ls, Lp=Lp: (Ls[i], Lp[j]), cap_start=True, cap_end=True)
        for p, th in place_along(center, per):
            pos = (p[0], p[1], z + LANGKAN_H)
            stamp(mb, relung, th, pos)
            spots.append((pos, th))
    return mb, spots


def part_arca(spots, arca, scale, dy):
    mb = MB()
    for pos, th in spots:
        off = rz(th) @ np.array([0.0, dy, 0.02])
        stamp(mb, arca, th, np.asarray(pos) + off, scale)
    return mb


def part_tangga(y_face, nd, out, z0, z1):
    """Empat tangga (tengah tiap sisi) yang naik dari z0 ke z1: masuk ke ceruk teras persegi (nd > 0) atau
    menonjol di tepi teras melingkar. Satu objek per tingkat agar ikut terangkat saat bongkar."""
    mb = MB()
    n = max(2, round((z1 - z0) / 0.22))
    yo, yi = y_face - out, y_face + nd - 0.02
    t, r = (yi - yo) / n, (z1 - z0) / n
    for k in range(4):
        for s in range(n):
            box(mb, -SW + 0.02, SW - 0.02, yo + s * t, yi, z0 + s * r, z0 + (s + 1) * r, k=k)
    return mb


def part_gapura(gapura, T):
    """Empat gapura di celah langkan yang berdiri di tepi atas teras T."""
    mb = MB()
    H = wall_line(T)
    for k in range(4):
        th = k * math.pi / 2
        stamp(mb, gapura, th, rz(th) @ np.array([0.0, -H + LANGKAN_INSET, T[3]]))
    return mb


def part_melingkar(R, z0, z1):
    rows = [(R + 0.3, z0), (R + 0.3, z0 + 0.2), (R, z0 + 0.35), (R, z1 - 0.25), (R + 0.2, z1 - 0.12), (R + 0.2, z1)]
    mb = MB(); n = 128
    mb.grid(lathe_rows(rows, n), uv=lambda i, j: (j / n * TAU * R / 4, rows[i][1] / 4))
    top = lathe_rows(rows[-1:], n)                               # lantai: batu hampar, UV planar
    mb.mat = 1
    mb.grid(top, uv=lambda i, j: (top[0, j % n, 0] / 4, top[0, j % n, 1] / 4), end=(0, 0, z1), end_uv=(0.0, 0.0))
    return mb


def part_stupa_ring(i, tpl_s, arca):
    """Satu lingkar stupa terawang + arca di dalamnya (menghadap ke luar). Celah tangga tepat di arah mata angin."""
    n = STUPA_TERAWANG[i]; R = RING_R[i]; z = MELINGKAR[i][2]
    ms, ma = MB(), MB()
    for j in range(n):
        a = TAU * (j + 0.5) / n
        pos = (R * math.cos(a), R * math.sin(a), z)
        stamp(ms, tpl_s, a, pos)
        stamp(ma, arca, a + math.pi / 2, (pos[0], pos[1], z + 0.02), 1.2)
    return ms, ma


def part_stupa_induk():
    z0 = MELINGKAR[-1][2]
    mb = MB(); n = 48
    bantalan = [(5.6, z0), (5.6, z0 + 0.3), (5.45, z0 + 0.4), (5.45, z0 + 0.6), (5.3, z0 + 0.7), (5.3, z0 + 0.9)]
    genta, _ = spline([(5.1, z0 + 1.0), (5.25, z0 + 1.8), (5.05, z0 + 3.1), (4.45, z0 + 4.4), (3.4, z0 + 5.4),
                       (2.0, z0 + 6.0), (1.3, z0 + 6.15)], 22)
    rows = bantalan + [tuple(p) for p in genta]
    mb.grid(lathe_rows(rows, n), uv=lambda i, j: (j / n * 8.0, rows[i][1] / 4), start=(0, 0, z0),
            end=(0, 0, rows[-1][1]))
    zh = rows[-1][1]
    mb.grid(lathe_rows([(1.70, zh), (1.70, zh + 0.85), (1.85, zh + 0.97), (1.85, zh + 1.15)], 4, math.pi / 4),
            start=(0, 0, zh), end=(0, 0, zh + 1.15))
    zy = zh + 1.15
    mb.grid(lathe_rows([(0.6, zy), (0.6, zy + 0.25), (0.72, zy + 0.35), (0.5, zy + 0.5), (0.36, zy + 1.6),
                        (0.22, TINGGI - 0.25)], 8), end=(0, 0, TINGGI))
    return mb


# ============================================================ tekstur (numpy -> PNG)
# Semua tekstur prosedural (tanpa foto) dan periodik (bisa diulang tanpa sambungan). ORM: R = AO, G = kekasaran,
# B = logam. Rupa andesit Borobudur: abu kehitaman, noda jamur kerak hitam, lumut hijau di nat, bercak lumut kerak
# putih-kekuningan, alur bekas air hujan.

def weathering(U, V, seed):
    """Masker pelapukan: noda gelap, alur hujan (memanjang vertikal), lumut, bercak lumut kerak terang."""
    dark = sstep(0.52, 0.74, fbm2(U, V, 5, 5, seed, 5))
    streak = sstep(0.52, 0.78, fbm2(U, V, 64, 2, seed + 1, 3)) * sstep(0.35, 0.7, fbm2(U, V, 4, 3, seed + 2, 3))
    moss = sstep(0.60, 0.80, fbm2(U, V, 7, 7, seed + 3, 5))
    spots = sstep(0.74, 0.84, fbm2(U, V, 40, 40, seed + 4, 3))
    return dark, streak, moss, spots


def tex_batu(seed, size=2048, rows=16, cols=8, tile=4.0, dark_k=0.5, moss_k=1.0, paving=False):
    """Susunan balok andesit tanpa semen (ubin 'tile' meter): tepi balok aus/gompal, permukaan berpori, pelapukan."""
    h = w = size
    U, V = uvgrid(h, w)
    bw, bh = tile / cols, tile / rows
    rng = np.random.default_rng(seed)
    shift = rng.random(rows) if not paving else rng.random(rows) * 0.5
    tone_t = rng.random((rows, cols)); tx_t = rng.normal(0, 1, (rows, cols)); ty_t = rng.normal(0, 1, (rows, cols))
    ry = np.minimum((V * rows).astype(int), rows - 1)
    x = U * cols + shift[ry]
    bx = np.floor(x).astype(int) % cols
    fu = x - np.floor(x); fv = V * rows - ry
    tone = tone_t[ry, bx]
    dm = np.minimum(np.minimum(fu, 1 - fu) * bw, np.minimum(fv, 1 - fv) * bh)      # jarak ke nat (m)
    chip = fbm2(U, V, 48, 48, seed + 5, 3)
    edge = sstep(0.0, 0.025 + 0.03 * chip, dm - 0.004)                                 # 0 di nat, 1 di badan balok
    joint = 1 - sstep(0.002, 0.012, dm)
    tilt = (tx_t[ry, bx] * (fu - 0.5) * bw + ty_t[ry, bx] * (fv - 0.5) * bh) * (0.03 if paving else 0.08)
    n_mid = fbm2(U, V, 24, 24, seed + 6, 4); n_hi = fbm2(U, V, 128, 128, seed + 7, 2)
    H = 0.55 * edge + tilt + 0.10 * n_mid + 0.05 * n_hi

    dark, streak, moss, spots = weathering(U, V, seed + 20)
    if paving:
        streak = streak * 0.2
    col = mix((0.19, 0.19, 0.18), (0.46, 0.45, 0.42), np.clip(0.32 + 0.30 * tone + 0.35 * (n_mid - 0.5) + 0.15 * (n_hi - 0.5), 0, 1))
    if paving:                                                  # lantai: lebih terang dan halus karena terinjak
        col = col * 1.12 + 0.02
    col = col * (1 - (0.35 + 0.35 * dark_k) * dark)[..., None]
    col = col * (1 - 0.28 * streak)[..., None]
    moss_m = np.clip(joint * 0.9 + (1 - edge) * 0.3 + moss * 0.45, 0, 1) * moss_k
    col = col * (1 - 0.65 * moss_m)[..., None] + np.array([0.09, 0.13, 0.045])[None, None] * (0.65 * moss_m)[..., None]
    col = col * (1 - 0.45 * spots)[..., None] + np.array([0.66, 0.63, 0.52])[None, None] * (0.45 * spots)[..., None]
    col = col * (1 - 0.55 * joint)[..., None]
    rough = np.clip(0.80 + 0.10 * n_mid + 0.08 * moss_m - 0.06 * spots - (0.1 if paving else 0.0), 0, 1)
    o = orm(rough, np.zeros_like(H))
    o[..., 0] = np.clip(1 - 0.6 * joint - 0.2 * (1 - edge), 0, 1)
    return np.clip(col, 0, 1), o, normal_from_height(H, 14.0)


def tex_relief():
    """Pita relief bergaya (BUKAN reproduksi relief asli): 2 register x 2 panel berbingkai berisi figur berdiri/duduk
    berprabha, pohon, dan pendapa; pelipit bawah bermanik, pelipit atas kelopak teratai, pilaster bersulur,
    nat balok tetap terlihat menembus pahatan. Lebar tekstur 5 m (panel 2.5 m), tinggi = pita dinding lorong."""
    h, w = 1024, 2048
    WX, WY = 5.0, 3.2
    U, V = uvgrid(h, w)
    X, Y = U * WX, V * WY
    xs = (np.arange(w) + 0.5) / w * WX; ys = (np.arange(h) + 0.5) / h * WY
    rng = np.random.default_rng(29)
    fig = np.zeros((h, w))

    def win(cx, cy, rx, ry):
        j0, j1 = np.searchsorted(xs, cx - rx), np.searchsorted(xs, cx + rx)
        i0, i1 = np.searchsorted(ys, cy - ry), np.searchsorted(ys, cy + ry)
        return slice(i0, i1), slice(j0, j1)

    def dome(cx, cy, rx, ry, amp, rot=0.0):
        si, sj = win(cx, cy, max(rx, ry), max(rx, ry))
        dx = X[si, sj] - cx; dy = Y[si, sj] - cy
        if rot:
            c, s = math.cos(rot), math.sin(rot); dx, dy = c * dx + s * dy, -s * dx + c * dy
        d2 = (dx / rx) ** 2 + (dy / ry) ** 2
        fig[si, sj] = np.maximum(fig[si, sj], amp * np.sqrt(np.clip(1 - d2, 0, 1)))

    def ring(cx, cy, r, t, amp):
        si, sj = win(cx, cy, r + t, r + t)
        d = np.hypot(X[si, sj] - cx, Y[si, sj] - cy)
        fig[si, sj] = np.maximum(fig[si, sj], amp * np.clip(1 - np.abs(d - r) / t, 0, 1) ** 0.5)

    def berdiri(x, y0, s, prabha):
        for dx in (-0.05, 0.05):
            dome(x + dx * s, y0 + 0.22 * s, 0.045 * s, 0.23 * s, 0.42)
        dome(x, y0 + 0.37 * s, 0.13 * s, 0.12 * s, 0.50)                                    # kain
        dome(x, y0 + 0.58 * s, 0.11 * s, 0.19 * s, 0.56)                                    # badan
        for sg in (-1, 1):
            dome(x + sg * 0.13 * s, y0 + 0.56 * s, 0.035 * s, 0.16 * s, 0.46, sg * 0.25)   # lengan
        dome(x, y0 + 0.86 * s, 0.068 * s, 0.085 * s, 0.62)                                 # kepala
        dome(x, y0 + 0.97 * s, 0.05 * s, 0.05 * s, 0.64)                                   # mahkota/usnisa
        if prabha:
            ring(x, y0 + 0.88 * s, 0.13 * s, 0.012, 0.30)

    def duduk(x, y0, s, prabha):
        dome(x, y0 + 0.10 * s, 0.20 * s, 0.10 * s, 0.50)                                    # kaki bersila
        dome(x, y0 + 0.36 * s, 0.11 * s, 0.18 * s, 0.56)
        for sg in (-1, 1):
            dome(x + sg * 0.12 * s, y0 + 0.30 * s, 0.035 * s, 0.14 * s, 0.46, sg * 0.5)
        dome(x, y0 + 0.62 * s, 0.068 * s, 0.085 * s, 0.62)
        dome(x, y0 + 0.73 * s, 0.05 * s, 0.05 * s, 0.64)
        if prabha:
            ring(x, y0 + 0.62 * s, 0.13 * s, 0.012, 0.30)

    def pohon(x, y0):
        dome(x, y0 + 0.45, 0.035, 0.45, 0.35)
        for _ in range(7):
            dome(x + rng.normal(0, 0.12), y0 + 1.0 + rng.normal(0, 0.1), 0.10 + 0.05 * rng.random(),
                 0.08 + 0.04 * rng.random(), 0.42 + 0.1 * rng.random())

    def pendapa(x, y0):
        for sg in (-1, 1):
            dome(x + sg * 0.17, y0 + 0.35, 0.025, 0.35, 0.40)                               # tiang
        dome(x, y0 + 0.08, 0.24, 0.06, 0.40)                                                 # batur
        for k, (wd, yy) in enumerate(((0.30, 0.78), (0.22, 0.92), (0.13, 1.04))):          # atap tumpang
            dome(x, y0 + yy, wd, 0.07, 0.48 + 0.03 * k)

    for reg in range(2):
        y0 = reg * 1.6 + 0.13
        for p in range(2):
            x = p * 2.5 + 0.25
            while x < p * 2.5 + 2.25:
                r = rng.random()
                if r < 0.15:
                    pohon(x + 0.12, y0); x += 0.38
                elif r < 0.25 and x < p * 2.5 + 1.9:
                    pendapa(x + 0.22, y0); x += 0.58
                elif r < 0.55:
                    duduk(x + 0.12, y0, 1.1 + 0.2 * rng.random(), rng.random() < 0.4); x += 0.34
                else:
                    berdiri(x + 0.08, y0, 1.05 + 0.2 * rng.random(), rng.random() < 0.25); x += 0.22

    bg = 0.10 + 0.05 * fbm2(U, V, 20, 10, 301, 4)
    detail = 0.04 * fbm2(U, V, 80, 40, 311, 3) + 0.025 * np.sin((X * 140.0) + 6 * fbm2(U, V, 10, 5, 321, 3))
    H = bg + np.where(fig > 0, fig + detail * sstep(0.0, 0.1, fig), 0.0)
    fx = X % 2.5; fy = Y % 1.6
    bawah, atas = fy < 0.10, fy > 1.50
    pilaster = (fx < 0.08) | (fx > 2.42)
    manik = 0.86 + 0.07 * np.sqrt(np.clip(1 - (((X % 0.08) - 0.04) / 0.03) ** 2 - ((fy - 0.05) / 0.03) ** 2, 0, 1))
    kelopak = 0.84 + 0.08 * np.abs(np.sin(X * math.pi / 0.12)) ** 0.6 * sstep(1.50, 1.56, fy)
    sulur = 0.84 + 0.05 * np.sin(Y * 55.0 + 3.0 * np.sin(fx * 90.0))
    H = np.where(bawah, manik, H); H = np.where(atas, kelopak, H); H = np.where(pilaster, sulur, H)
    rowv = (Y / 0.25) % 1.0
    bx = (X / 0.5 + 0.5 * (np.floor(Y / 0.25) % 2)) % 1.0
    joint = 1 - sstep(0.002, 0.01, np.minimum(np.minimum(rowv, 1 - rowv) * 0.25, np.minimum(bx, 1 - bx) * 0.5))
    H = H - 0.06 * joint
    H = blur(H, 1)
    cav = np.clip(1 - 2.2 * np.maximum(blur(H, 8) - H, 0), 0.35, 1)                        # rongga pahatan lebih gelap

    dark, streak, moss, spots = weathering(U, V, 340)
    n2 = fbm2(U, V, 30, 15, 331, 3)
    col = mix((0.20, 0.20, 0.19), (0.47, 0.46, 0.43), np.clip(0.25 + 0.6 * H + 0.15 * (n2 - 0.5), 0, 1))
    col = col * cav[..., None]
    col = col * (1 - 0.45 * dark * (1.2 - H))[..., None] * (1 - 0.25 * streak)[..., None]
    moss_m = np.clip((1 - cav) * 0.6 + moss * 0.3, 0, 1) * 0.7
    col = col * (1 - 0.6 * moss_m)[..., None] + np.array([0.09, 0.13, 0.045])[None, None] * (0.6 * moss_m)[..., None]
    col = col * (1 - 0.35 * spots)[..., None] + np.array([0.66, 0.63, 0.52])[None, None] * (0.35 * spots)[..., None]
    o = orm(np.clip(0.86 - 0.08 * H + 0.06 * moss_m, 0, 1), np.zeros_like(H))
    o[..., 0] = cav
    return np.clip(col, 0, 1), o, normal_from_height(H, 18.0)


def tex_arca():
    """Batu arca: pori halus tanpa nat, noda gelap di bagian cekung."""
    h = w = 1024
    U, V = uvgrid(h, w)
    n1 = fbm2(U, V, 8, 8, 401, 5); n_hi = fbm2(U, V, 96, 96, 405, 2)
    dark, streak, moss, spots = weathering(U, V, 420)
    col = mix((0.26, 0.26, 0.25), (0.50, 0.49, 0.46), np.clip(0.25 + 0.6 * n1 + 0.15 * (n_hi - 0.5), 0, 1))
    col = col * (1 - 0.5 * dark)[..., None] * (1 - 0.25 * streak)[..., None]
    col = col * (1 - 0.35 * spots)[..., None] + np.array([0.66, 0.63, 0.52])[None, None] * (0.35 * spots)[..., None]
    H = 0.2 * n1 + 0.08 * n_hi
    o = orm(np.clip(0.78 + 0.1 * n1, 0, 1), np.zeros_like(n1))
    return np.clip(col, 0, 1), o, normal_from_height(H, 8.0)


def tex_tanah():
    """Rumput halaman (hanya untuk render, tidak ikut GLB). Ubin 10 m."""
    h = w = 2048
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 12, 12, 501, 5); blade = fbm2(U, V, 256, 256, 503, 2); dirt = sstep(0.62, 0.8, fbm2(U, V, 4, 4, 507, 4))
    col = mix((0.09, 0.16, 0.04), (0.26, 0.36, 0.10), np.clip(0.2 + 0.5 * n + 0.3 * blade, 0, 1))
    col = col * (1 - dirt)[..., None] + np.array([0.30, 0.24, 0.16])[None, None] * dirt[..., None]
    return np.clip(col, 0, 1)


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    jobs = [
        ('andesit', lambda: tex_batu(7)),                                                   # dinding & pagar
        ('lantai', lambda: tex_batu(61, rows=8, cols=6, dark_k=0.2, moss_k=0.8, paving=True)),
        ('stupa', lambda: tex_batu(51, dark_k=1.0, moss_k=0.6)),                          # stupa: lebih lapuk
        ('relief', tex_relief),
        ('arca', tex_arca),
    ]
    paths = {}
    for name, fn in jobs:
        col, o, n = fn()
        for suffix, arr in (('', col), ('_orm', o), ('_n', n)):
            write_png(os.path.join(TEX, name + suffix + '.png'), arr)
            paths[name + suffix] = os.path.join(TEX, name + suffix + '.png')
        print('Tekstur:', name)
    write_png(os.path.join(TEX, 'tanah.png'), tex_tanah()); paths['tanah'] = os.path.join(TEX, 'tanah.png')
    return paths


def materials(tex):
    M = {}
    for key, name, ns in (('andesit', 'batu_andesit', 1.0), ('lantai', 'lantai_batu', 0.8), ('relief', 'relief_lorong', 1.3),
                          ('stupa', 'stupa_andesit', 1.0), ('terawang', 'stupa_terawang', 1.0), ('arca', 'arca_andesit', 0.8)):
        t = {'terawang': 'stupa'}.get(key, key)
        M[key] = pbr(name, tex[t], tex[t + '_orm'], tex[t + '_n'], nstrength=ns)
        M[key].use_backface_culling = key != 'terawang'        # kisi stupa satu lapis -> dua sisi
    return M


# ============================================================ lingkungan (hanya render; tidak ikut ekspor)

SUN_ELEV, SUN_AZ = 38.0, 115.0      # derajat; matahari pagi dari timur-tenggara (sisi -Y agak ke -X)


def lingkungan(tex):
    """Cycles (fallback EEVEE), langit fisik bila tersedia, matahari, rumput, 4 kamera siap render."""
    sc = bpy.context.scene
    try:
        sc.render.engine = 'CYCLES'
        cy = sc.cycles
        cy.samples = 256; cy.use_adaptive_sampling = True; cy.adaptive_threshold = 0.02; cy.use_denoising = True
        try:
            cy.device = 'GPU'           # dipakai bila GPU diaktifkan di Preferences; jika tidak, tetap CPU
        except TypeError:
            pass
    except TypeError:
        sc.render.engine = 'BLENDER_EEVEE'
    try:
        sc.view_settings.view_transform = 'AgX'
        for lk in ('AgX - Medium High Contrast', 'Medium High Contrast'):
            try:
                sc.view_settings.look = lk
                break
            except TypeError:
                continue
    except TypeError:
        sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 1920, 1080

    world = bpy.data.worlds.new('Langit'); sc.world = world
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    nt = world.node_tree
    bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (0.50, 0.64, 0.85, 1.0); bg.inputs['Strength'].default_value = 0.9
    try:
        sky = nt.nodes.new('ShaderNodeTexSky')
        for t in ('MULTIPLE_SCATTERING', 'NISHITA', 'SINGLE_SCATTERING', 'HOSEK_WILKIE'):
            try:
                sky.sky_type = t
                break
            except TypeError:
                continue
        for attr, val in (('sun_disc', False), ('sun_elevation', math.radians(SUN_ELEV)),
                          ('sun_rotation', math.radians(SUN_AZ)), ('altitude', 200.0), ('air_density', 1.0),
                          ('dust_density', 2.0)):
            try:
                setattr(sky, attr, val)
            except (AttributeError, TypeError):
                pass
        nt.links.new(sky.outputs['Color'], bg.inputs['Color'])
        bg.inputs['Strength'].default_value = 0.35
    except RuntimeError:
        pass

    ld = bpy.data.lights.new('Matahari', 'SUN'); ld.energy = 5.0; ld.angle = math.radians(0.8)
    ld.color = (1.0, 0.95, 0.87)
    sun = bpy.data.objects.new('Matahari', ld); sc.collection.objects.link(sun)
    el, az = math.radians(SUN_ELEV), math.radians(SUN_AZ)
    sun.location = Vector((math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el))) * 150
    look(sun, (0, 0, 0))

    bpy.ops.mesh.primitive_plane_add(size=800, location=(0, 0, -0.02))
    tanah = bpy.context.active_object; tanah.name = 'Tanah'
    tanah.data.materials.append(pbr('rumput', base=tex['tanah'], rough=0.95, uv_scale=(80.0, 80.0)))

    def kamera(name, eye, target, lens=35, ortho=None):
        c = bpy.data.objects.new(name, bpy.data.cameras.new(name)); sc.collection.objects.link(c)
        c.data.clip_start = 0.1; c.data.clip_end = 3000; c.data.lens = lens
        c.location = eye; look(c, target)
        if ortho:
            c.data.type = 'ORTHO'; c.data.ortho_scale = ortho
        return c

    cams = [kamera('Kamera', (-115, -165, 78), (0, 0, 10)),                          # hero dari tenggara
            kamera('Kamera_Depan', (0, -300, TINGGI / 2), (0, 0, TINGGI / 2), ortho=135),   # tampak timur
            kamera('Kamera_Stupa', (-24, -44, 30), (0, -6, 25), lens=28),                  # teras melingkar
            kamera('Kamera_Lorong', (-20, -58.2, 5.8), (-2, -56.4, 6.5), lens=24)]         # lorong pertama
    sc.camera = cams[0]
    return cams


def viewport():
    """Saat .blend dibuka: mode Solid bertekstur (ringan) dan pandangan langsung ke candi."""
    from mathutils import Euler
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != 'VIEW_3D':
                continue
            for sp in area.spaces:
                if sp.type != 'VIEW_3D':
                    continue
                try:
                    sp.shading.type = 'SOLID'; sp.shading.color_type = 'TEXTURE'; sp.shading.light = 'STUDIO'
                    sp.shading.show_cavity = True
                    sp.clip_start = 0.1; sp.clip_end = 3000
                    r3 = sp.region_3d
                    r3.view_location = (0, 0, 12); r3.view_distance = 190
                    r3.view_rotation = Euler((math.radians(62), 0, math.radians(-35))).to_quaternion()
                except AttributeError:
                    pass


def render_previews(cams):
    sc = bpy.context.scene
    for c, suffix in zip(cams, ('hero', 'depan', 'stupa', 'lorong')):
        sc.camera = c
        sc.render.filepath = os.path.join(DOCS, f'CandiBorobudur_Blender_{suffix}.png')
        bpy.ops.render.render(write_still=True); print('Render:', sc.render.filepath)
    sc.camera = cams[0]


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.name = 'Candi Borobudur'
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = materials(tex)

    def empty(name, parent=None, size=5.0):
        e = bpy.data.objects.new(name, None); e.empty_display_type = 'PLAIN_AXES'; e.empty_display_size = size
        sc.collection.objects.link(e); e.parent = parent
        return e

    root = empty('Candi_Borobudur', size=20.0)
    kama, rupa, tg, arupa = (empty(n, root) for n in ('Kamadhatu', 'Rupadhatu', 'Tangga_Gapura', 'Arupadhatu'))

    relung, gapura, arca = tpl_relung(), tpl_gapura(), tpl_arca()
    stupa_tpl = {k: tpl_stupa_terawang(k) for k in set(LUBANG)}

    objs = {}
    cek = {'relung': 0, 'arca_langkan': 0, 'stupa_terawang': 0, 'arca_stupa': 0}
    for T in TERAS:
        objs[T[0]] = build(T[0], part_teras(T), [M['andesit'], M['relief'], M['lantai']], kama if T[0] == 'Kaki_Candi' else rupa,
                           sharp_deg=40)
    for i, n in enumerate(RELUNG):
        mb, spots = part_langkan(TERAS[i], n, relung)
        objs[f'Langkan_{i + 1}'] = build(f'Langkan_{i + 1}', mb, M['andesit'], rupa, sharp_deg=40)
        objs[f'Arca_Langkan_{i + 1}'] = build(f'Arca_Langkan_{i + 1}', part_arca(spots, arca, 0.82, -0.12),
                                              M['arca'], rupa)
        cek['relung'] += len(spots); cek['arca_langkan'] += len(spots)
    # Tangga & gapura dipecah per tingkat: di Unity setiap tingkat diangkat sendiri saat bongkar.
    for i, T in enumerate(TERAS):
        name = 'Tangga_Kaki' if i == 0 else f'Tangga_{i}'
        objs[name] = build(name, part_tangga(-wall_line(T), T[5], T[6], T[2], T[3]), M['andesit'], tg, sharp_deg=40)
    n_gapura = 0
    for i, T in enumerate(TERAS[:5]):                   # gapura i di celah Langkan i (tepi atas teras i-1)
        objs[f'Gapura_{i + 1}'] = build(f'Gapura_{i + 1}', part_gapura(gapura, T), M['andesit'], tg, sharp_deg=40)
        n_gapura += 4
    for i, (R, z0, z1) in enumerate(MELINGKAR):
        objs[f'Teras_Melingkar_{i + 1}'] = build(f'Teras_Melingkar_{i + 1}', part_melingkar(R, z0, z1),
                                                 [M['andesit'], M['lantai']], arupa, sharp_deg=40)
        objs[f'Tangga_Melingkar_{i + 1}'] = build(f'Tangga_Melingkar_{i + 1}', part_tangga(-R, 0.3, 1.6, z0, z1),
                                                  M['andesit'], tg, sharp_deg=40)
        ms, ma = part_stupa_ring(i, stupa_tpl[LUBANG[i]], arca)
        objs[f'Stupa_Terawang_{i + 1}'] = build(f'Stupa_Terawang_{i + 1}', ms, M['terawang'], arupa, sharp_deg=50)
        objs[f'Arca_Stupa_{i + 1}'] = build(f'Arca_Stupa_{i + 1}', ma, M['arca'], arupa)
        cek['stupa_terawang'] += STUPA_TERAWANG[i]; cek['arca_stupa'] += STUPA_TERAWANG[i]
    objs['Stupa_Induk'] = build('Stupa_Induk', part_stupa_induk(), M['stupa'], arupa)

    # Laporan ukuran & jumlah segitiga
    total = 0; lo_all = np.full(3, 1e9); hi_all = np.full(3, -1e9)
    print('\n== Bagian (m) ==')
    for name, ob in objs.items():
        co = np.array([v.co for v in ob.data.vertices])
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons); total += tris
        lo, hi = co.min(0), co.max(0); lo_all = np.minimum(lo_all, lo); hi_all = np.maximum(hi_all, hi)
        print(f'{name:20s} {(hi - lo)[0]:7.2f} x {(hi - lo)[1]:7.2f} x {(hi - lo)[2]:6.2f}  z[{lo[2]:5.2f},{hi[2]:5.2f}]'
              f'  tris {tris}')
    print(f'Total segitiga: {total}')
    body = [o for n, o in objs.items() if not n.startswith('Tangga')]
    blo = np.min([np.array([v.co for v in o.data.vertices]).min(0) for o in body], 0)
    bhi = np.max([np.array([v.co for v in o.data.vertices]).max(0) for o in body], 0)
    print(f'Kotak batas tanpa tangga: {bhi[0] - blo[0]:.2f} x {bhi[1] - blo[1]:.2f} x {bhi[2]:.2f} m'
          f'  (data: {SISI_DASAR} x {SISI_DASAR} x {TINGGI} m)')
    print(f'Kotak batas dengan tangga: {hi_all[0] - lo_all[0]:.2f} x {hi_all[1] - lo_all[1]:.2f} m')
    print(f"Relung langkan: {cek['relung']} (data 432) | stupa terawang: {cek['stupa_terawang']} (data 72) | "
          f"arca total: {cek['arca_langkan'] + cek['arca_stupa']} (data 504) | gapura: {n_gapura}")
    assert cek['relung'] == sum(RELUNG) == 432
    assert cek['stupa_terawang'] == sum(STUPA_TERAWANG) == 72
    assert cek['arca_langkan'] + cek['arca_stupa'] == 504
    assert abs(bhi[2] - TINGGI) < 1e-3

    cams = lingkungan(tex)
    viewport()
    if EXPORT:
        os.makedirs(ART, exist_ok=True)
        export_glb(root, GLB)
        print('GLB:', GLB, f'({os.path.getsize(GLB) / 1e6:.2f} MB)')
    if RENDER:
        render_previews(cams)
    bpy.ops.file.pack_all()                              # .blend mandiri: bisa langsung dibuka di laptop lain
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)


if __name__ == '__main__':
    main()
