# Komodo (Varanus komodoensis) betina menjaga sarangnya: diorama savana Pulau Rinca dengan gundukan sarang burung gosong
# (megapoda) yang dipotong untuk memperlihatkan telur -> Blender -> GLB.
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/komodo.py
# Tambahkan "-- --no-render" di akhir untuk melewati render pratinjau.
#
# Hasil:
#   Assets/NusantaraAR/Art/Komodo/komodo.glb      (model PBR, tekstur tertanam)
#   Tools/blender/komodo_textures/*.png           (sumber tekstur: warna, ORM, normal map)
#   Tools/blender/komodo.blend                    (file kerja; di luar Assets agar tidak diimpor Unity)
#   Docs/Komodo_Blender_*.png                     (render pratinjau)
#
# Model berskala 1:1 (1 unit = 1 m); aplikasi memperkecilnya 1:10. Proporsi direka dari foto acuan dan data bersumber
# (lihat Docs/Komodo_Data.md), bukan dipindai atau diukur dari spesimen: betina dewasa ±2,3 m, panjang moncong-kloaka
# ±separuh panjang total, ±60 gigi bergerigi berujung jingga (lapisan besi), lidah kuning bercabang.
#
# Ruang Blender: Z ke atas, muka depan -Y. Komodo melangkah ke -X (sisi kirinya menghadap penonton), ekor melengkung
# ke belakang (+Y); sarang di +X. Tulang tubuh = satu kurva (CTRL) dari ujung moncong ke ujung ekor; setiap penampang
# adalah superelips (setengah lebar w, tinggi atas ht, tinggi bawah hb) yang diatur terhadap panjang busur a dari moncong.
# Kepala dipotong di garis mulut menjadi Kepala (rahang atas) dan Rahang (rahang bawah, origin di engsel ENGSEL);
# Lidah ber-origin di pangkalnya dan tersimpan di dalam mulut (dijulurkan sejauh JULUR di Unity).
# Hierarki: Komodo > Alas, Sarang, Telur, Tubuh, Kepala, Rahang, Lidah.

import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from keris_sumatra import (MB, build, spline, spow, sstep, write_png, uvgrid, fbm2, blur, mix, normal_from_height, orm,
                           pbr, export_glb, look, studio, TAU)

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'Komodo')
GLB = os.path.join(ART, 'komodo.glb')
TEX = os.path.join(HERE, 'komodo_textures')
BLEND = os.path.join(HERE, 'komodo.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
RENDER = '--no-render' not in ARGS
UP = np.array([0.0, 0.0, 1.0])

# ============================================================ ukuran & tulang tubuh

# Kurva tengah tubuh: moncong (-X) -> kepala lurus -> leher -> bahu -> panggul -> ekor melengkung ke +Y.
CTRL = [(-1.200, -0.040, 0.165), (-1.075, -0.040, 0.200), (-0.950, -0.040, 0.235),     # kepala (lurus)
        (-0.800, -0.025, 0.262), (-0.640, -0.005, 0.278),                              # leher -> bahu
        (-0.360, 0.000, 0.285), (-0.080, 0.000, 0.275),                                # badan -> panggul
        (0.120, 0.015, 0.235), (0.320, 0.000, 0.170), (0.470, -0.100, 0.115),          # pangkal ekor
        (0.525, -0.280, 0.075), (0.430, -0.450, 0.052), (0.270, -0.545, 0.040)]        # ujung ekor menyapu ke depan
_DENSE, LEN = spline(CTRL, 4001)
_A = np.linspace(0.0, LEN, 4001)
HEAD_L = 0.275                 # moncong -> belakang kepala (sambungan leher)
A_SHOULDER, A_HIP = 0.60, 1.13 # posisi bahu dan panggul (panjang busur dari moncong)

# Penampang tubuh terhadap a (m dari moncong): setengah lebar, tinggi atas, tinggi bawah. Perut betina membulat.
PROF_A = [0.00, 0.02, 0.05, 0.10, 0.15, 0.20, 0.24, 0.275, 0.33, 0.42, 0.52, 0.60, 0.75, 0.90, 1.05, 1.15, 1.25,
          1.40, 1.60, 1.85, 2.10, 2.40]
PROF_W = [.024, .040, .053, .063, .069, .077, .086, .089, .096, .108, .135, .168, .212, .228, .205, .165, .122,
          .092, .066, .044, .025, .005]
PROF_HT = [.017, .026, .035, .045, .053, .059, .063, .066, .074, .088, .104, .124, .140, .142, .136, .120, .100,
           .082, .066, .047, .028, .006]
PROF_HB = [.015, .024, .033, .042, .051, .062, .072, .077, .086, .098, .112, .122, .158, .170, .155, .120, .093,
           .074, .055, .036, .021, .005]

# Kepala
P0, P2 = np.array(CTRL[0]), np.array(CTRL[2])
HEAD_DIR = (P0 - P2) / np.linalg.norm(P0 - P2)  # arah moncong (maju, sedikit menunduk)
A_ENGSEL = 0.245               # engsel rahang (sendi kuadrat) di garis mulut
JAW_OPEN = 24.0                # derajat; rahang dibuka di Unity tahap 1 (sama dengan render "mulut")
JULUR = 0.28                   # lidah dijulurkan sejauh ini di sepanjang HEAD_DIR
A_EYE = 0.150
N_TEETH = 12                   # per sisi per rahang -> 48 gigi tampak (komodo ±60)

# Sarang & telur (ukuran gundukan diperkecil agar muat di diorama; sarang megapoda asli bisa jauh lebih besar)
NEST_C = np.array([1.05, 0.15])
NEST_R, NEST_H = 0.50, 0.42
ALCOVE_D, ALCOVE_W, ALCOVE_TOP = 0.32, 0.75, 0.29
EGG_L, EGG_D = 0.090, 0.058    # telur ±9 x 6 cm
EGG_OUT = 0.32                 # telur dikeluarkan ke depan (-Y) sejauh ini di Unity tahap 2

# Alas diorama
BASE_C = np.array([0.0, 0.10])
BASE_RX, BASE_RY, BASE_E = 1.62, 0.92, 0.55
BASE_BOT = -0.10

# Sisik: sel per petak tekstur (U sepanjang tubuh, V keliling). U dihitung dari ds / keliling sehingga sel tetap persegi.
N_U, N_V = 40, 80


def prof(a):
    a = np.asarray(a, float)
    return np.interp(a, PROF_A, PROF_W), np.interp(a, PROF_A, PROF_HT), np.interp(a, PROF_A, PROF_HB)


def cl(a):
    a = np.asarray(a, float)
    return np.stack([np.interp(a, _A, _DENSE[:, k]) for k in range(3)], -1)


def frame(a):
    """(pusat, T maju ke ekor, Lat ke kanan T = kiri hewan, Up)."""
    a = float(a)
    e = 0.004
    t = cl(min(a + e, LEN)) - cl(max(a - e, 0.0))
    t /= np.linalg.norm(t)
    lat = np.cross(t, UP); lat /= np.linalg.norm(lat)
    return cl(a), t, lat, np.cross(lat, t)


def superellipse(phi, w, ht, hb, e=0.85):
    """Penampang: phi 0 = punggung, pi = perut; mengembalikan (lateral, vertikal)."""
    s, c = np.sin(phi), np.cos(phi)
    return w * spow(s, e), np.where(c >= 0, ht, hb) * spow(c, e)


def perim(w, ht, hb):
    return math.pi * (1.5 * (w + 0.5 * (ht + hb)) - math.sqrt(w * 0.5 * (ht + hb)))


def u_skin(as_, ws, hts, hbs, u0=0.0):
    """Koordinat U kulit: kumulatif ds / (keliling / N_V) / N_U sehingga sel sisik tetap persegi."""
    p = np.array([perim(w, t, b) for w, t, b in zip(ws, hts, hbs)])
    ds = np.diff(as_, prepend=as_[0])
    return u0 + np.cumsum(ds * N_V / (np.maximum(p, 1e-3) * N_U))


# ============================================================ tubuh (badan, leher, ekor, kaki)

def neck_fold(a, phi):
    """Lipatan kulit leher & gelambir (keriput melintang di sisi bawah leher)."""
    band = sstep(0.27, 0.32, a) * (1 - sstep(0.50, 0.58, a))
    under = np.clip(-np.cos(phi) + 0.25, 0, 1.25)
    return 1 + 0.045 * band * under * np.sin(TAU * (a - 0.27) / 0.034) ** 2


def part_tubuh():
    mb = MB()
    C = 56
    a0 = 0.205
    # Baris lebih rapat di leher dan pangkal ekor; ujung ekor ditutup titik.
    as_ = np.concatenate([np.linspace(a0, 0.60, 70, endpoint=False), np.linspace(0.60, LEN - 0.004, 130)])
    phi = TAU * np.arange(C) / C
    ws, hts, hbs = prof(as_)
    k = 0.975 + 0.025 * sstep(a0, 0.30, as_)              # leher sedikit lebih kecil dari kepala di daerah tumpang
    P = np.zeros((len(as_), C, 3))
    for i, a in enumerate(as_):
        c, t, lat, up = frame(a)
        l, v = superellipse(phi, ws[i] * k[i], hts[i] * k[i], hbs[i] * k[i])
        f = neck_fold(a, phi)
        P[i] = c + np.outer(l * f, lat) + np.outer(v * f, up)
    U = u_skin(as_, ws, hts, hbs)
    tip = cl(LEN)
    c0 = frame(a0)[0]
    mb.grid(P, uv=lambda i, j: (U[i], j / C), start=c0, start_uv=(U[0], 0.5), end=tip, end_uv=(U[-1] + 0.02, 0.5))
    tubuh_legs(mb)
    tubuh_ears(mb)
    return mb


def rmf_tube(mb, pts, radii, C=20, flat=None, u0=0.0, cap_end=None):
    """Tabung menyusuri titik 3D dengan bingkai minimal-putar; flat = skala arah 'atas' (telapak pipih)."""
    pts = np.asarray(pts, float); n = len(pts)
    T = np.gradient(pts, axis=0); T /= np.linalg.norm(T, axis=1)[:, None]
    ref = UP if abs(T[0] @ UP) < 0.9 else np.array([1.0, 0.0, 0.0])
    N = ref - (ref @ T[0]) * T[0]; N /= np.linalg.norm(N)
    phi = TAU * np.arange(C) / C
    P = np.zeros((n, C, 3))
    fl = np.ones(n) if flat is None else np.asarray(flat, float)
    for i in range(n):
        if i:
            N = N - (N @ T[i]) * T[i]; N /= np.linalg.norm(N)
        B = np.cross(T[i], N)
        P[i] = pts[i] + np.outer(radii[i] * fl[i] * np.cos(phi), N) + np.outer(radii[i] * np.sin(phi), B)
    seg = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))])
    U = u0 + seg * N_V / (np.maximum(TAU * np.asarray(radii), 1e-3) * N_U)
    mb.grid(P, uv=lambda i, j: (U[i], j / C), end=cap_end, end_uv=(U[-1], 0.5))
    return P


def leg_points(joints, n=40):
    pts, _ = spline(joints, n)
    return pts


def foot(mb, center, heading, side, lens, spread, toe_r=0.013):
    """Telapak pipih + lima jari mengipas ke depan-luar, masing-masing bercakar melengkung (bahan cakar = slot 1)."""
    h = np.array([math.cos(heading), math.sin(heading), 0.0])
    for k, (L, ang) in enumerate(zip(lens, spread)):
        a = heading + side * math.radians(ang)
        d = np.array([math.cos(a), math.sin(a), 0.0])
        base = center + d * 0.020 + np.array([0, 0, 0.016])
        tipp = center + d * (0.020 + L) + np.array([0, 0, 0.008])
        mid = 0.5 * (base + tipp) + np.array([0, 0, 0.008])        # jari sedikit melengkung (buku jari)
        pts = leg_points([base, mid, tipp], 12)
        r = np.linspace(toe_r, toe_r * 0.62, 12)
        mb.mat = 0
        rmf_tube(mb, pts, r, C=10, flat=np.full(12, 0.75), u0=k * 0.37)
        # Cakar: kerucut melengkung ke bawah dari ujung jari sampai menyentuh tanah.
        mb.mat = 1
        cb = tipp - d * 0.004
        ctip = np.array([cb[0] + d[0] * 0.034, cb[1] + d[1] * 0.034, 0.0015])
        cmid = 0.5 * (cb + ctip) + np.array([0, 0, 0.010])
        cp = leg_points([cb, cmid, ctip], 8)
        rr = np.linspace(toe_r * 0.62, 0.0008, 8)
        rmf_tube(mb, cp, rr, C=8, cap_end=ctip)
    mb.mat = 0
    # Telapak: elipsoid pipih.
    C = 16; rows = 6
    th = np.linspace(0.15, math.pi - 0.15, rows)
    phi = TAU * np.arange(C) / C
    side_v = np.array([-h[1], h[0], 0.0])
    P = np.zeros((rows, C, 3))
    for i, t in enumerate(th):
        rad = math.sin(t)
        P[i] = (center + h * 0.032 * math.cos(t) + np.outer(rad * 0.030 * np.cos(phi), side_v)
                + np.outer(rad * 0.016 * np.sin(phi), UP) + np.array([0, 0, 0.016]))
    mb.grid(P, start=center + h * 0.032 + np.array([0, 0, 0.016]), end=center - h * 0.032 + np.array([0, 0, 0.016]))


FRONT_TOES = ([0.040, 0.058, 0.066, 0.060, 0.042], [-40, -18, 0, 20, 45])
HIND_TOES = ([0.050, 0.078, 0.096, 0.088, 0.060], [-45, -20, 0, 18, 42])


def legs():
    """Sendi tiap kaki (bahu/panggul -> siku/lutut -> pergelangan -> telapak). Langkah diagonal: kiri depan & kanan
    belakang maju, kanan depan & kiri belakang mundur. side -1 = sisi kiri hewan (menghadap penonton, -Y)."""
    out = []
    csh, _, _, _ = frame(A_SHOULDER)
    chip, _, _, _ = frame(A_HIP)
    for side, dx in ((-1, -0.07), (1, 0.06)):
        j0 = csh + np.array([0.0, side * 0.06, -0.03])
        j1 = csh + np.array([0.0, side * 0.15, -0.05])
        el = np.array([csh[0] + 0.5 * dx + 0.04, side * 0.30, 0.165])
        wr = np.array([csh[0] + dx, side * 0.32, 0.060])
        pa = np.array([wr[0] - 0.025, side * 0.33, 0.020])
        out.append(('depan', side, [j0, j1, el, wr, pa], [0.090, 0.084, 0.064, 0.050, 0.036, 0.027], FRONT_TOES))
    for side, dx in ((-1, 0.08), (1, -0.09)):
        j0 = chip + np.array([0.0, side * 0.05, -0.03])
        j1 = chip + np.array([0.0, side * 0.14, -0.06])
        kn = np.array([chip[0] + 0.3 * dx - 0.07, side * 0.32, 0.170])
        an = np.array([chip[0] + dx + 0.03, side * 0.34, 0.062])
        pa = np.array([an[0] - 0.035, side * 0.36, 0.020])
        out.append(('belakang', side, [j0, j1, kn, an, pa], [0.105, 0.098, 0.072, 0.054, 0.038, 0.029], HIND_TOES))
    return out


def tubuh_legs(mb):
    for name, side, joints, r, toes in legs():
        pts = leg_points(joints, 44)
        s = np.linspace(0, 1, 44)
        knots = np.linspace(0, 1, len(r))
        radii = np.interp(s, knots, r)
        # Siku/lutut menonjol sedikit.
        radii *= 1 + 0.10 * np.exp(-((s - 0.5) / 0.06) ** 2)
        mb.mat = 0
        rmf_tube(mb, pts, radii, C=20, flat=np.interp(s, [0, 0.8, 1], [1, 1, 0.65]), u0=0.5 if side > 0 else 0.0)
        pa = joints[-1]
        heading = math.pi + (math.radians(12) if name == 'depan' else math.radians(6)) * -side
        foot(mb, np.array([pa[0], pa[1], 0.0]), heading, side, *toes,
             toe_r=0.013 if name == 'depan' else 0.015)


def tubuh_ears(mb):
    """Lubang telinga: oval gelap (bahan slot 2) di sisi leher di belakang sudut rahang."""
    mb.mat = 2
    a = 0.290
    c, t, lat, up = frame(a)
    w, ht, hb = prof(a)
    for sgn in (-1, 1):
        phi = sgn * (math.pi / 2 - 0.05)
        l, v = superellipse(np.array([phi]), w, ht, hb)
        p = c + lat * l[0] + up * v[0]
        n = (p - c); n /= np.linalg.norm(n)
        q = p - n * 0.004
        C = 12; rows = 4
        th = np.linspace(0.2, math.pi - 0.2, rows)
        ph = TAU * np.arange(C) / C
        P = np.zeros((rows, C, 3))
        for i, tt in enumerate(th):
            rad = math.sin(tt)
            P[i] = q + n * 0.006 * math.cos(tt) + np.outer(rad * 0.011 * np.cos(ph), t) + np.outer(rad * 0.008 * np.sin(ph), up)
        mb.grid(P, start=q + n * 0.006, end=q - n * 0.006)
    mb.mat = 0


# ============================================================ kepala, rahang, gigi, lidah

def head_scale(a):
    return 1.0 - 0.14 * sstep(0.21, HEAD_L, a)          # bagian belakang kepala menyusup ke dalam leher


def mouth_v(a, hb):
    """Tinggi garis mulut relatif sumbu kepala (negatif = di bawah sumbu)."""
    return -(0.08 + 0.10 * sstep(0.02, 0.22, a)) * hb


def head_ring(a, dense=480):
    """Penampang kepala penuh (rapat) dan nilai bantu: (pts lateral/vertikal, w, ht, hb, vm)."""
    w, ht, hb = [float(x) for x in prof(a)]
    k = head_scale(a)
    brow = 1 + 0.07 * math.exp(-((a - A_EYE) / 0.035) ** 2)         # tonjolan alis di atas mata
    w, ht, hb = w * k * (1 + 0.03 * math.exp(-((a - A_EYE) / 0.035) ** 2)), ht * k * brow, hb * k
    # Rahang bawah membulat ke samping di belakang (otot rahang & pipi).
    jowl = 1 + 0.10 * sstep(0.12, 0.24, a)
    phi = TAU * np.arange(dense) / dense
    # Kepala lebih "kotak" dari badan: atas pipih, sisi tegak (eksponen superelips lebih kecil), terutama di belakang.
    l, v = superellipse(phi, w, ht, hb * jowl, e=0.80 - 0.18 * sstep(0.03, 0.15, a))
    return l, v, w, ht, hb * jowl, mouth_v(a, hb * jowl)


def clip_arc(l, v, vm, upper, K):
    """Busur penampang di atas (upper) atau di bawah garis mulut vm, diambil ulang K titik berjarak busur sama,
    dari sisi kiri (l < 0) ke kanan. Ujungnya tepat di garis mulut."""
    n = len(l)
    start = n // 2 if upper else 0                       # mulai dari perut (upper) / punggung (lower) agar busur utuh
    idx = (np.arange(n) + start) % n
    L, V = l[idx], v[idx]
    keep = V >= vm if upper else V <= vm
    ks = np.nonzero(keep)[0]
    i0, i1 = ks[0], ks[-1]
    def cross(i, j):
        t = (vm - V[i]) / (V[j] - V[i])
        return L[i] + t * (L[j] - L[i])
    pa = np.array([cross(i0 - 1, i0), vm])
    pb = np.array([cross(i1, i1 + 1), vm])
    arc = np.vstack([pa, np.stack([L[i0:i1 + 1], V[i0:i1 + 1]], 1), pb])
    seg = np.linalg.norm(np.diff(arc, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], K)
    out = np.stack([np.interp(t, s, arc[:, 0]), np.interp(t, s, arc[:, 1])], 1)
    if out[0, 0] > out[-1, 0]:
        out = out[::-1]
    return out


def part_head(upper):
    """Kepala (upper=True: tengkorak & rahang atas, langit-langit) atau Rahang (rahang bawah, dasar mulut).
    Koordinat dunia; origin objek diatur pemanggil."""
    mb = MB()
    K, J = 36, 11
    as_ = np.concatenate([np.linspace(0.0, 0.03, 6, endpoint=False), np.linspace(0.03, HEAD_L, 52)])
    if not upper:
        as_ = as_[1:]
    rows_arc = np.zeros((len(as_), K, 3)); rows_pal = np.zeros((len(as_), J, 3))
    ws, hts, hbs = prof(as_)
    for i, a in enumerate(as_):
        c, t, lat, up = frame(a)
        l, v, w, ht, hb, vm = head_ring(a)
        arc = clip_arc(l, v, vm, upper, K)
        rows_arc[i] = c + np.outer(arc[:, 0], lat) + np.outer(arc[:, 1], up)
        # Langit-langit (cekung ke atas) / dasar mulut (cekung ke bawah) di antara kedua ujung busur.
        xl, xr = arc[0, 0], arc[-1, 0]
        tt = np.linspace(-1, 1, J)
        depth = (0.22 * ht if upper else -0.18 * hb) * (1 - tt ** 2) * sstep(0.0, 0.05, a)
        lx = xl + (tt + 1) / 2 * (xr - xl)
        rows_pal[i] = c + np.outer(lx, lat) + np.outer(vm + depth, up)
        rows_pal[i, 0] = rows_arc[i, 0]; rows_pal[i, -1] = rows_arc[i, -1]
    U = u_skin(as_, ws, hts, hbs, 7.0 if upper else 3.0)
    v0, v1 = (-0.25, 0.25) if upper else (0.75, 0.25)
    tip = 0.5 * (rows_arc[0].mean(0) + rows_pal[0].mean(0)) - frame(as_[0])[1] * 0.004
    back = 0.5 * (rows_arc[-1].mean(0) + rows_pal[-1].mean(0))
    mb.mat = 0
    mb.grid(rows_arc, uv=lambda i, j: (U[i], v0 + (v1 - v0) * j / (K - 1)), wrap_cols=False,
            start=tip, start_uv=(U[0], 0.0 if upper else 0.5), end=back, end_uv=(U[-1], 0.0))
    mb.mat = 1
    mb.grid(rows_pal[:, ::-1], uv=lambda i, j: (as_[i] * 3, j / (J - 1)), wrap_cols=False,
            start=tip, start_uv=(0.0, 0.5), end=back, end_uv=(1.0, 0.5))
    teeth(mb, upper, rows_pal, as_)
    if upper:
        eyes_nostrils(mb)
    return mb


def tooth(mb, base, down, back, lat, h, half_l, half_t):
    """Gigi pipih bergerigi melengkung ke belakang (seperti gigi hiu); V tekstur 0 = gusi, 1 = ujung."""
    rows = 6; C = 10
    phi = TAU * np.arange(C) / C
    P = np.zeros((rows, C, 3))
    for i in range(rows):
        s = i / (rows - 1)
        ctr = base + down * (h * s - 0.004) + back * (0.35 * h * s ** 1.8)
        sc = (1 - s) ** 0.85 + 0.04
        P[i] = ctr + np.outer(half_l * sc * spow(np.cos(phi), 0.6), back) + np.outer(half_t * sc * np.sin(phi), lat)
    tipp = base + down * h + back * (0.35 * h) + down * 0.0015
    mb.grid(P, uv=lambda i, j: (j / C, i / (rows - 1) * 0.97), end=tipp, end_uv=(0.5, 1.0))


def teeth(mb, upper, rows_pal, as_):
    mb.mat = 2
    for sgn in (0, 1):                        # sisi kiri (kolom awal) & kanan (kolom akhir) dari langit-langit
        for k in range(N_TEETH):
            a = 0.022 + (0.205 - 0.022) * (k + (0.0 if upper else 0.5)) / N_TEETH
            i = min(max(int(np.searchsorted(as_, a)), 1), len(as_) - 1)
            f = (a - as_[i - 1]) / (as_[i] - as_[i - 1])
            c, t, lat, up = frame(a)
            row = rows_pal[i - 1] + (rows_pal[i] - rows_pal[i - 1]) * f
            edge = row[0] if sgn == 0 else row[-1]
            inner = row[2] if sgn == 0 else row[-3]
            base = edge + (inner - edge) * 0.55
            size = 0.55 + 0.45 * math.sin(math.pi * min(1.0, (k + 0.5) / N_TEETH * 1.1))
            h = 0.013 * size
            down = -up if upper else up
            tooth(mb, base, down, t, lat, h, 0.0055 * size, 0.0019 * size)
    mb.mat = 0


def blob(mb, c, ax, ay, az, rows=6, C=12, frame3=None):
    """Elipsoid kecil (mata, lubang hidung)."""
    ex, ey, ez = frame3 if frame3 is not None else (np.array([1.0, 0, 0]), np.array([0, 1.0, 0]), UP)
    th = np.linspace(0.0, math.pi, rows + 2)[1:-1]
    ph = TAU * np.arange(C) / C
    P = np.zeros((rows, C, 3))
    for i, t in enumerate(th):
        P[i] = c + ez * az * math.cos(t) + np.outer(math.sin(t) * ax * np.cos(ph), ex) + np.outer(math.sin(t) * ay * np.sin(ph), ey)
    mb.grid(P, uv=lambda i, j: (0.5, 0.5), start=c + ez * az, end=c - ez * az, start_uv=(0.5, 0.5), end_uv=(0.5, 0.5))


def surface_point(a, phi):
    c, t, lat, up = frame(a)
    l, v, *_ = head_ring(a)
    n = len(l)
    j = int(round((phi % TAU) / TAU * n)) % n
    p = c + lat * l[j] + up * v[j]
    nrm = p - c; nrm /= np.linalg.norm(nrm)
    return p, nrm, t, lat, up


def eye_points():
    out = []
    for sgn in (-1, 1):
        p, n, t, lat, up = surface_point(A_EYE, sgn * (math.pi / 2 - 0.62))
        out.append((p - n * 0.006, n, t))
    return out


def eyes_nostrils(mb):
    mb.mat = 3
    for c, n, t in eye_points():
        b = np.cross(n, t)
        blob(mb, c, 0.0125, 0.0095, 0.0095, frame3=(t, b, n))          # mata ±2,5 cm
    for sgn in (-1, 1):
        p, n, t, lat, up = surface_point(0.030, sgn * (math.pi / 2 - 0.55))
        b = np.cross(n, t)
        blob(mb, p - n * 0.002, 0.0055, 0.0030, 0.0035, frame3=(t, b, n))
    mb.mat = 0


def hinge():
    c, t, lat, up = frame(A_ENGSEL)
    *_, vm = head_ring(A_ENGSEL)
    return c + up * vm


def tongue_root():
    c, t, lat, up = frame(0.0)
    return P0 - HEAD_DIR * 0.36 + up * mouth_v(0.2, prof(0.2)[2]) - up * 0.006


def part_lidah():
    """Lidah kuning bercabang tersimpan di dasar mulut (pangkal di dalam leher), lurus searah HEAD_DIR."""
    mb = MB()
    root = tongue_root()
    d = HEAD_DIR
    lat = np.cross(-d, UP); lat /= np.linalg.norm(lat)
    up = np.cross(lat, -d)
    L = 0.33
    C = 12; phi = TAU * np.arange(C) / C
    # Batang lidah: dari pangkal sampai pangkal cabang; cabang sepanjang ±12 cm (foto acuan: lidah terjulur bercabang dalam).
    FORK = 0.21
    s = np.linspace(0, FORK + 0.01, 26)
    wv = np.interp(s, [0, 0.03, 0.15, FORK + 0.01], [0.008, 0.012, 0.010, 0.0080])
    tv = np.interp(s, [0, 0.03, FORK + 0.01], [0.0050, 0.0052, 0.0036])
    P = np.zeros((len(s), C, 3))
    for i, si in enumerate(s):
        P[i] = root + d * si + np.outer(wv[i] * np.cos(phi), lat) + np.outer(tv[i] * np.sin(phi), up)
    mb.grid(P, uv=lambda i, j: (s[i] / L, j / C), start=root - d * 0.002, start_uv=(0.0, 0.5))
    # Dua cabang yang menyebar ke samping dan meruncing.
    for sgn in (-1, 1):
        s2 = np.linspace(FORK, L, 20)
        f = (s2 - FORK) / (L - FORK)
        off = sgn * (0.0040 + 0.014 * f ** 1.3)
        w2 = 0.0048 * (1 - f) ** 0.7 + 0.0005
        t2 = 0.0032 * (1 - f) ** 0.6 + 0.0004
        Q = np.zeros((len(s2), C, 3))
        for i in range(len(s2)):
            Q[i] = root + d * s2[i] + lat * off[i] + np.outer(w2[i] * np.cos(phi), lat) + np.outer(t2[i] * np.sin(phi), up)
        tipp = root + d * (L + 0.003) + lat * sgn * (0.0040 + 0.014)
        mb.grid(Q, uv=lambda i, j: (s2[i] / L, j / C), end=tipp, end_uv=(1.0, 0.5))
    return mb


# ============================================================ sarang, telur, alas

def nest_radius(z, phi):
    """Jari-jari gundukan pada tinggi z dan arah phi (0 = +X); ceruk di muka depan (-Y) memperlihatkan telur."""
    base = NEST_R * np.clip(1 - (z / NEST_H) ** 2, 0, 1) ** 0.62
    dphi = (phi + math.pi / 2 + math.pi) % TAU - math.pi              # 0 di arah depan (-Y)
    alc = np.exp(-(dphi / ALCOVE_W) ** 2) * (1 - sstep(ALCOVE_TOP - 0.07, ALCOVE_TOP, z))
    return np.maximum(base - ALCOVE_D * alc * np.clip(base / NEST_R, 0, 1) ** 0.3, 0.015)


def part_sarang():
    mb = MB()
    C = 96
    zs = np.concatenate([np.linspace(0.0, 0.30, 26), np.linspace(0.30, NEST_H * 0.985, 14)[1:]])
    phi = TAU * np.arange(C) / C
    P = np.zeros((len(zs), C, 3))
    for i, z in enumerate(zs):
        r = nest_radius(z, phi)
        bump = 1 + 0.05 * (fbm2(phi[None] / TAU, np.full((1, C), z / NEST_H), 9, 4, 1201, 3)[0] - 0.5)
        r = r * bump
        P[i, :, 0] = NEST_C[0] + r * np.cos(phi)
        P[i, :, 1] = NEST_C[1] + r * np.sin(phi)
        P[i, :, 2] = z
    P[0, :, 2] = -0.012                                                  # sedikit terbenam di alas
    top = np.array([NEST_C[0], NEST_C[1], NEST_H])
    # UV proyeksi miring (x, y + z) agar serasah tidak meruncing ke puncak seperti UV silinder.
    proj = lambda p: (float(p[0]) * 1.4, (float(p[1]) + 0.8 * float(p[2])) * 1.4)
    mb.grid(P, uv=lambda i, j: proj(P[i, j % C]), end=top, end_uv=proj(top))
    return mb


def egg_layout():
    """Telur di lantai ceruk: 5 di bawah, 2 di atas (posisi, sudut arah sumbu panjang)."""
    cx, cy = NEST_C[0], NEST_C[1] - NEST_R + 0.14
    r = EGG_D / 2
    lay = [(-0.078, 0.030, r, 0.4), (-0.022, 0.060, r, -0.3), (0.035, 0.040, r, 0.9), (0.085, 0.065, r, -0.6),
           (0.000, -0.020, r, 0.1), (-0.045, 0.040, r + EGG_D * 0.82, 1.2), (0.050, 0.050, r + EGG_D * 0.82, -1.0)]
    return [(np.array([cx + dx, cy + dy, z]), a) for dx, dy, z, a in lay]


def part_telur():
    mb = MB()
    for k, (c, ang) in enumerate(egg_layout()):
        ex = np.array([math.cos(ang), math.sin(ang), 0.0])
        ey = np.array([-math.sin(ang), math.cos(ang), 0.0])
        rows, C = 12, 16
        th = np.linspace(0, math.pi, rows + 2)[1:-1]
        ph = TAU * np.arange(C) / C
        P = np.zeros((rows, C, 3))
        for i, t in enumerate(th):
            rad = math.sin(t) * (1 + 0.07 * math.cos(t))               # telur sedikit lonjong satu sisi
            P[i] = c + ex * (EGG_L / 2) * math.cos(t) + np.outer(rad * (EGG_D / 2) * np.cos(ph), ey) + np.outer(rad * (EGG_D / 2) * np.sin(ph), UP)
        u0 = (k % 4) * 0.25
        mb.grid(P, uv=lambda i, j: (u0 + 0.25 * j / C, (i + 1) / (rows + 1)),
                start=c + ex * EGG_L / 2, end=c - ex * EGG_L / 2, start_uv=(u0, 0.0), end_uv=(u0, 1.0))
    return mb


def base_shape(t):
    """Garis keliling alas (superelips) pada sudut t."""
    return BASE_C[0] + BASE_RX * spow(np.cos(t), BASE_E), BASE_C[1] + BASE_RY * spow(np.sin(t), BASE_E)


def ground_relief(x, y):
    """Relief tanah halus; datar (0) di bawah tubuh, kaki, sarang dan jalur telur."""
    h = 0.010 * (fbm2(np.asarray(x) / 4 + 0.5, np.asarray(y) / 4 + 0.5, 6, 6, 1301, 3) - 0.5)
    return h


def rocks():
    return [((-1.28, -0.56), 0.11, 0.07, 0.08, 1401), ((-1.05, 0.74), 0.16, 0.12, 0.13, 1402),
            ((1.38, 0.70), 0.13, 0.10, 0.10, 1403), ((1.47, -0.38), 0.09, 0.07, 0.06, 1404),
            ((-0.25, 0.88), 0.10, 0.07, 0.07, 1405), ((0.62, -0.62), 0.06, 0.05, 0.04, 1406)]


def grass_tufts():
    rng = np.random.default_rng(1501)
    pts = []
    tries = 0
    while len(pts) < 34 and tries < 4000:
        tries += 1
        t = rng.uniform(0, TAU); rr = math.sqrt(rng.uniform(0.25, 0.92))
        x, y = base_shape(t)
        x = BASE_C[0] + (x - BASE_C[0]) * rr; y = BASE_C[1] + (y - BASE_C[1]) * rr
        if abs(x + 0.40) < 0.95 and -0.45 < y < 0.45:            # tubuh & kaki
            continue
        if -0.10 < x < 0.68 and -0.68 < y < 0.12:                  # ekor
            continue
        if np.hypot(x - NEST_C[0], y - NEST_C[1]) < NEST_R + 0.06:   # sarang
            continue
        if 0.80 < x < 1.32 and -0.72 < y < -0.20:                  # jalur telur keluar
            continue
        if -1.55 < x < -1.05 and -0.30 < y < 0.15:                 # ruang lidah di depan moncong
            continue
        if any(math.hypot(x - rx, y - ry) < rs + 0.07 for (rx, ry), rs, _, _, _ in rocks()):
            continue
        if any(math.hypot(x - px, y - py) < 0.12 for px, py, _ in pts):
            continue
        pts.append((x, y, rng.uniform(0.6, 1.0)))
    return pts


def part_alas():
    mb = MB()
    C = 120
    t = TAU * np.arange(C) / C
    ex, ey = base_shape(t)
    rings = np.linspace(0.03, 1.0, 26)
    P = np.zeros((len(rings), C, 3))
    for i, rr in enumerate(rings):
        x = BASE_C[0] + (ex - BASE_C[0]) * rr; y = BASE_C[1] + (ey - BASE_C[1]) * rr
        P[i, :, 0] = x; P[i, :, 1] = y; P[i, :, 2] = ground_relief(x, y) * sstep(0.2, 0.6, rr) * (rr < 0.999)
    # Bibir alas: membulat ke bawah lalu sisi tegak sampai dasar.
    rim = []
    for f, dz in ((1.008, -0.006), (1.012, -0.018), (1.012, BASE_BOT + 0.006), (1.006, BASE_BOT)):
        x = BASE_C[0] + (ex - BASE_C[0]) * f; y = BASE_C[1] + (ey - BASE_C[1]) * f
        rim.append(np.stack([x, y, np.full(C, dz)], 1))
    P = np.vstack([P, np.array(rim)])
    nt = len(rings)
    planar = lambda i, j: ((P[i, j % C, 0] + 2) / 1.0, (P[i, j % C, 1] + 2) / 1.0) if i < nt else (j / C * 9.0, 0.5 + (P[i, j % C, 2]) * 2)
    mb.mat = 0
    mb.grid(P, uv=planar, start=(BASE_C[0], BASE_C[1], 0.0), start_uv=((BASE_C[0] + 2), (BASE_C[1] + 2)),
            end=(BASE_C[0], BASE_C[1], BASE_BOT), end_uv=(0.5, 0.5))
    # Batu (slot 1): bola tergencet dengan derau.
    mb.mat = 1
    for (rx, ry), sx, sy, sz, seed in rocks():
        rows, Cc = 10, 20
        th = np.linspace(0, math.pi * 0.62, rows + 1)[1:]
        ph = TAU * np.arange(Cc) / Cc
        Q = np.zeros((rows, Cc, 3))
        for i, tt in enumerate(th):
            n = fbm2(ph[None] / TAU, np.full((1, Cc), tt / math.pi), 5, 3, seed, 3)[0]
            k = 0.82 + 0.36 * n
            Q[i, :, 0] = rx + sx * math.sin(tt) * np.cos(ph) * k
            Q[i, :, 1] = ry + sy * math.sin(tt) * np.sin(ph) * k
            Q[i, :, 2] = sz * math.cos(tt) * (0.9 + 0.2 * n) - 0.035
        Q = Q[::-1]                                          # baris dari bawah ke puncak
        mb.grid(Q, uv=lambda i, j: (j / Cc * 2, i / rows), end=(rx, ry, sz - 0.03), end_uv=(0.5, 1.0))
    # Rumput savana kering (slot 2): rumpun bilah prisma segitiga yang melengkung keluar.
    mb.mat = 2
    rng = np.random.default_rng(1601)
    for x, y, s in grass_tufts():
        for b in range(6):
            ang = rng.uniform(0, TAU)
            lean = rng.uniform(0.25, 0.65)
            hgt = s * rng.uniform(0.10, 0.22)
            d = np.array([math.cos(ang), math.sin(ang), 0.0])
            side = np.array([-d[1], d[0], 0.0])
            rows = 5
            Q = np.zeros((rows, 3, 3))
            for i in range(rows):
                f = i / (rows - 1)
                ctr = np.array([x, y, -0.01]) + d * (0.012 + lean * hgt * f ** 1.6) + UP * (hgt * f)
                wv = 0.0045 * (1 - f) + 0.0004
                Q[i, 0] = ctr - side * wv; Q[i, 1] = ctr + side * wv; Q[i, 2] = ctr + d * wv * 0.6 + UP * 0.001
            mb.grid(Q, uv=lambda i, j: (0.2 + 0.3 * j, i / (rows - 1) * 0.95),
                    end=np.array([x, y, -0.01]) + d * (0.012 + lean * hgt) + UP * (hgt + 0.01), end_uv=(0.5, 1.0))
    mb.mat = 0
    return mb


# ============================================================ tekstur (numpy -> PNG)

def voronoi(U, V, nu, nv, seed, jitter=0.8):
    """Voronoi berulang (nu x nv sel): F1, F2 (dalam satuan sel) dan nilai acak sel terdekat."""
    rng = np.random.default_rng(seed)
    jx = 0.5 + jitter * (rng.random((nv, nu)) - 0.5)
    jy = 0.5 + jitter * (rng.random((nv, nu)) - 0.5)
    val = rng.random((nv, nu))
    gu, gv = U * nu, V * nv
    iu, iv = np.floor(gu).astype(int), np.floor(gv).astype(int)
    fu, fv = gu - iu, gv - iv
    F1 = np.full(U.shape, 9.0); F2 = np.full(U.shape, 9.0); C1 = np.zeros(U.shape)
    for dv in (-1, 0, 1):
        for du in (-1, 0, 1):
            cu, cv = (iu + du) % nu, (iv + dv) % nv
            d = np.hypot(fu - (du + jx[cv, cu]), fv - (dv + jy[cv, cu]))
            m1 = d < F1
            F2 = np.where(m1, F1, np.minimum(F2, d))
            C1 = np.where(m1, val[cv, cu], C1)
            F1 = np.where(m1, d, F1)
    return F1, F2, C1


def tex_kulit():
    """Kulit komodo: sisik bulat menonjol (di atas osteoderm) dengan alur gelap; punggung abu-cokelat gelap berbintik
    jingga-kekuningan, perut lebih terang. U sepanjang tubuh (berulang), V keliling (0 = punggung, 0,5 = perut)."""
    h, w = 2048, 1024
    U, V = uvgrid(h, w)
    # Sedikit dibengkokkan (periodik) agar barisan sisik tidak tampak seperti kisi.
    Uw = U + (fbm2(U, V, 8, 16, 2141, 3) - 0.5) * 0.6 / N_U
    Vw = V + (fbm2(U, V, 8, 16, 2151, 3) - 0.5) * 0.6 / N_V
    F1, F2, cell = voronoi(Uw, Vw, N_U, N_V, 2101, jitter=0.9)
    groove = 1 - sstep(0.02, 0.10, F2 - F1)
    dome = np.clip(1 - (F1 / 0.62) ** 2, 0, 1) ** 0.6
    Hgt = dome * (1 - groove) + 0.08 * (fbm2(U, V, 12, 24, 2111, 3) - 0.5)
    dorsal = 0.5 + 0.5 * np.cos(TAU * V)                     # 1 di punggung, 0 di perut
    mott = fbm2(U, V, 6, 12, 2121, 4)
    speck = (fbm2(U, V, 24, 48, 2131, 2) > 0.62) * (cell > 0.55)
    back = mix((0.19, 0.165, 0.135), (0.29, 0.235, 0.175), np.clip(mott * 1.3 - 0.2, 0, 1))
    belly = mix((0.36, 0.33, 0.26), (0.43, 0.39, 0.30), mott)
    col = back * dorsal[..., None] + belly * (1 - dorsal)[..., None]
    col = col * (0.82 + 0.30 * cell)[..., None]
    col = col + (np.asarray((0.55, 0.38, 0.17)) - col) * (0.55 * speck * dorsal)[..., None]
    col = col * (0.55 + 0.45 * dome)[..., None]
    col = col + (np.asarray((0.06, 0.05, 0.045)) - col) * (0.85 * groove)[..., None]
    rough = 0.70 + 0.18 * groove - 0.10 * dome
    occ = np.clip(0.55 + 0.45 * (1 - groove), 0, 1)
    o = orm(rough, np.zeros_like(rough)); o[..., 0] = occ
    return col, o, normal_from_height(blur(Hgt, 1), 4.0)


def tex_mulut():
    """Rongga mulut: merah muda kemerahan, gusi tebal; lembap (agak mengilap)."""
    h = w = 512
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 16, 8, 2201, 4)
    vein = np.exp(-((fbm2(U, V, 6, 3, 2211, 3) - 0.5) / 0.02) ** 2)
    edge = np.exp(-((V - 0.0) / 0.09) ** 2) + np.exp(-((V - 1.0) / 0.09) ** 2)
    col = mix((0.62, 0.32, 0.31), (0.50, 0.22, 0.22), n) * (1 - 0.25 * vein)[..., None]
    col = col + (np.asarray((0.52, 0.20, 0.20)) - col) * np.clip(edge, 0, 1)[..., None]
    return col, orm(0.32 + 0.15 * n, np.zeros((h, w)))


def tex_gigi():
    """Gigi: krem di pangkal, ujung dan tepi gerigi jingga-cokelat karena lapisan besi (LeBlanc dkk. 2024)."""
    h, w = 256, 64
    U, V = uvgrid(h, w)
    edge = np.exp(-(np.minimum(np.abs(U - 0.0), np.abs(U - 1.0)) / 0.07) ** 2) + np.exp(-((U - 0.5) / 0.07) ** 2)
    iron = np.clip(sstep(0.55, 0.92, V) + 0.8 * edge * sstep(0.15, 0.5, V), 0, 1)
    col = mix((0.86, 0.80, 0.66), (0.66, 0.34, 0.10), iron)
    gum = 1 - sstep(0.0, 0.12, V)
    col = col + (np.asarray((0.55, 0.24, 0.22)) - col) * gum[..., None]
    return col, orm(0.30 - 0.08 * iron, np.zeros((h, w)))


def tex_lidah():
    """Lidah: pangkal merah muda -> kuning terang di batang dan cabang."""
    h, w = 64, 512
    U, V = uvgrid(h, w)
    t = sstep(0.08, 0.40, U)
    col = mix((0.70, 0.36, 0.28), (0.96, 0.80, 0.18), t) * (0.92 + 0.12 * fbm2(U, V, 32, 4, 2301, 2))[..., None]
    return col, orm(0.35 + 0.0 * U, np.zeros((h, w)))


def tex_tanah():
    """Tanah savana Rinca: tanah kering cokelat keabuan berdebu, bercak gelap, kerikil kecil, serpih rumput kering."""
    h = w = 1024
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 6, 6, 2401, 5)
    m = fbm2(U, V, 24, 24, 2405, 4)
    P1, P2, pc = voronoi(U, V, 90, 90, 2421, 1.0)
    pebble = np.clip(1 - P1 / 0.20, 0, 1) * (pc > 0.80)
    fib = sstep(0.70, 0.78, fbm2(U, V, 160, 18, 2431, 2)) * sstep(0.45, 0.65, fbm2(U, V, 8, 8, 2433, 3))
    col = mix((0.47, 0.38, 0.28), (0.33, 0.26, 0.19), np.clip(0.6 * n + 0.6 * (m - 0.5) + 0.2, 0, 1))
    col = col + (np.asarray((0.50, 0.48, 0.44)) - col) * (0.7 * (pebble > 0.05))[..., None] * (0.7 + 0.3 * pebble)[..., None]
    col = col + (np.asarray((0.66, 0.57, 0.38)) - col) * (0.5 * fib)[..., None]
    Hgt = 0.5 * n + 0.3 * m + 0.6 * pebble ** 0.5 * (pc > 0.80) + 0.15 * fib
    return col, orm(0.90 - 0.08 * (pebble > 0.05), np.zeros((h, w))), normal_from_height(blur(Hgt, 1), 4.0)


def tex_batu():
    """Batu vulkanik abu-abu dengan sedikit lumut kerak."""
    h = w = 512
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 6, 6, 2501, 5)
    fine = fbm2(U, V, 40, 40, 2505, 3)
    lich = sstep(0.70, 0.76, fbm2(U, V, 14, 14, 2511, 3))
    col = mix((0.46, 0.44, 0.41), (0.30, 0.29, 0.27), np.clip(0.7 * n + 0.5 * (fine - 0.5) + 0.15, 0, 1))
    col = col + (np.asarray((0.56, 0.54, 0.40)) - col) * (0.45 * lich)[..., None]
    return col, orm(0.82 + 0.1 * n, np.zeros((h, w))), normal_from_height(blur(n + 0.4 * fine, 1), 6.0)


def tex_rumput():
    """Bilah rumput savana kering: hijau zaitun di pangkal -> jerami pucat di ujung."""
    h, w = 256, 64
    U, V = uvgrid(h, w)
    col = mix((0.40, 0.38, 0.20), (0.78, 0.68, 0.42), sstep(0.05, 0.6, V))
    col = col * (0.9 + 0.15 * np.sin(TAU * U * 6) ** 2)[..., None]
    return col, orm(0.75 + 0 * U, np.zeros((h, w)))


def tex_sarang():
    """Gundukan sarang megapoda: tanah gembur gelap bercampur serasah daun kering dan ranting."""
    h = w = 1024
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 8, 8, 2601, 5)
    m = fbm2(U, V, 30, 30, 2605, 3)
    # Serasah: daun kering kecil memanjang (sel Voronoi diregangkan) dengan warna beragam, plus ranting tipis.
    F1, F2, cell = voronoi(U, V, 28, 20, 2611, 1.0)
    edge = fbm2(U, V, 90, 90, 2613, 2)
    leaf = (1 - sstep(0.26, 0.40, F1 + 0.25 * (edge - 0.5))) * (cell > 0.40) * (0.45 + 0.55 * cell)
    shade = fbm2(U, V, 40, 40, 2615, 2)
    twig = np.exp(-((fbm2(U, V, 6, 28, 2621, 2) - 0.5) / 0.010) ** 2) * (fbm2(U, V, 10, 10, 2623, 2) > 0.5)
    col = mix((0.34, 0.26, 0.19), (0.22, 0.16, 0.11), np.clip(0.7 * n + 0.5 * (m - 0.5) + 0.15, 0, 1))
    leafc = mix((0.52, 0.40, 0.25), (0.36, 0.25, 0.14), shade)
    col = col + (leafc - col) * (0.75 * leaf)[..., None]
    col = col + (np.asarray((0.28, 0.20, 0.13)) - col) * (0.8 * twig)[..., None]
    Hgt = 0.5 * n + 0.3 * leaf + 0.3 * twig + 0.2 * m
    return col, orm(0.86 + 0 * n, np.zeros((h, w))), normal_from_height(blur(Hgt, 1), 3.0)


def tex_telur():
    """Kulit telur liat putih gading, sedikit bernoda tanah."""
    h = w = 256
    U, V = uvgrid(h, w)
    n = fbm2(U, V, 8, 8, 2701, 4)
    dirt = sstep(0.62, 0.80, fbm2(U, V, 12, 12, 2711, 3))
    col = mix((0.86, 0.83, 0.74), (0.78, 0.74, 0.64), n)
    col = col + (np.asarray((0.52, 0.42, 0.30)) - col) * (0.4 * dirt)[..., None]
    return col, orm(0.62 + 0.1 * n, np.zeros((h, w)))


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    out = {}
    out['kulit'], out['kulit_orm'], out['kulit_n'] = tex_kulit()
    out['mulut'], out['mulut_orm'] = tex_mulut()
    out['gigi'], out['gigi_orm'] = tex_gigi()
    out['lidah'], out['lidah_orm'] = tex_lidah()
    out['tanah'], out['tanah_orm'], out['tanah_n'] = tex_tanah()
    out['batu'], out['batu_orm'], out['batu_n'] = tex_batu()
    out['rumput'], out['rumput_orm'] = tex_rumput()
    out['sarang'], out['sarang_orm'], out['sarang_n'] = tex_sarang()
    out['telur'], out['telur_orm'] = tex_telur()
    for name, arr in out.items():
        write_png(os.path.join(TEX, name + '.png'), arr)
    return {k: os.path.join(TEX, k + '.png') for k in out}


# ============================================================ pose & titik hotspot (dicetak untuk KomodoBuilder.cs)

def rot_y(p, pivot, deg):
    """Putar titik p pada sumbu Y Blender melalui pivot; deg positif = ujung depan (-X) turun (= +Z di ruang Model Unity)."""
    t = math.radians(-deg)
    d = np.asarray(p) - pivot
    x = d[0] * math.cos(t) + d[2] * math.sin(t)
    z = -d[0] * math.sin(t) + d[2] * math.cos(t)
    return pivot + np.array([x, d[1], z])


def hotspot_targets():
    """Titik (x, z) Blender = (x, y) ruang Model Unity pada pose tahap 1 (rahang terbuka, lidah terjulur)."""
    H = hinge()
    out = {}
    # Kulit: sisi kiri badan di antara kaki depan dan belakang.
    c, t, lat, up = frame(0.88)
    out['kulit'] = c + up * 0.02
    # Cakar: kaki depan kiri (sisi penonton), jari tengah.
    name, side, joints, r, toes = legs()[0]
    pa = joints[-1]
    heading = math.pi + math.radians(12) * -side
    d = np.array([math.cos(heading), math.sin(heading), 0.0])
    out['cakar'] = np.array([pa[0], pa[1], 0.0]) + d * 0.066 + UP * 0.012           # jari tengah (cakar tipis, sinar bisa lolos)
    c, *_ = frame(1.80)
    out['ekor'] = c
    out['mata'] = min((e[0] for e in eye_points()), key=lambda p: p[1])      # mata sisi penonton (-Y)
    out['sarang'] = np.array([NEST_C[0] + 0.28, NEST_C[1] - 0.30, 0.25])
    eggs = egg_layout()
    out['telur'] = eggs[4][0]
    rx, ry = rocks()[0][0]
    out['alas'] = np.array([rx, ry, 0.03])
    # Rahang terbuka: titik dirotasi di engsel.
    c, t, lat, up = frame(0.12)
    l, v, w, ht, hb, vm = head_ring(0.12)
    out['bisa'] = rot_y(c + up * (vm - 0.45 * (vm + hb)) , H, JAW_OPEN)
    # Gigi bawah sisi depan: titik tengah sebuah gigi pada rahang terbuka (diperiksa dengan raycast di Blender;
    # gigi miring mengikuti rahang, jadi titik di sumbu kepala bisa jatuh di celah antargigi).
    out['gigi'] = np.array([-1.060, -0.092, 0.150])
    root = tongue_root()
    out['lidah'] = root + HEAD_DIR * (0.30 + JULUR)
    return out


# ============================================================ render pratinjau

def render_previews(tex, objs):
    sc = bpy.context.scene
    lights = studio(center=(0.0, 0.1, 0.15), scale=4.2)
    bpy.ops.mesh.primitive_plane_add(size=14.0, location=(0, 0, BASE_BOT - 0.001))
    lantai = bpy.context.active_object; lantai.name = 'Lantai'
    lantai.data.materials.append(pbr('lantai', color=(0.10, 0.09, 0.085), rough=0.8))
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam)
    sc.camera = cam
    sc.render.resolution_percentage = 100

    def shot(path, res, target, eye, lens):
        sc.render.resolution_x, sc.render.resolution_y = res
        cam.data.type = 'PERSP'; cam.data.lens = lens
        cam.location = eye; look(cam, target)
        cam.data.clip_start = 0.01
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print('Render:', path)

    rahang, lidah, telur = objs['Rahang'], objs['Lidah'], objs['Telur']
    shot(os.path.join(DOCS, 'Komodo_Blender_hero.png'), (1600, 900), (0.05, 0.10, 0.20), (-1.6, -4.4, 1.9), 46)
    shot(os.path.join(DOCS, 'Komodo_Blender_kepala.png'), (1200, 900), (-1.05, -0.05, 0.21), (-1.75, -0.95, 0.42), 55)
    # Pose tahap 1: rahang terbuka, lidah terjulur.
    rahang.rotation_euler = (0, -math.radians(JAW_OPEN), 0)
    lidah.location = Vector(lidah.location) + Vector(HEAD_DIR * JULUR)
    shot(os.path.join(DOCS, 'Komodo_Blender_mulut.png'), (1200, 900), (-1.25, -0.05, 0.19), (-1.85, -1.05, 0.40), 50)
    rahang.rotation_euler = (0, 0, 0)
    lidah.location = Vector(lidah.location) - Vector(HEAD_DIR * JULUR)
    shot(os.path.join(DOCS, 'Komodo_Blender_sarang.png'), (1200, 900), (1.05, -0.10, 0.12), (0.75, -1.55, 0.55), 50)
    return lights + [lantai, cam]


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.name = 'Komodo'
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = {
        'kulit': pbr('kulit_komodo', tex['kulit'], tex['kulit_orm'], tex['kulit_n'], nstrength=1.0),
        'mulut': pbr('mulut', tex['mulut'], tex['mulut_orm'], coat=0.4, coat_rough=0.2),
        'gigi': pbr('gigi', tex['gigi'], tex['gigi_orm']),
        'mata': pbr('mata', color=(0.025, 0.018, 0.012), rough=0.08, coat=0.8, coat_rough=0.02),
        'cakar': pbr('cakar', color=(0.07, 0.06, 0.05), rough=0.35),
        'lidah': pbr('lidah', tex['lidah'], tex['lidah_orm'], coat=0.5, coat_rough=0.15),
        'tanah': pbr('tanah', tex['tanah'], tex['tanah_orm'], tex['tanah_n']),
        'batu': pbr('batu', tex['batu'], tex['batu_orm'], tex['batu_n']),
        'rumput': pbr('rumput', tex['rumput'], tex['rumput_orm']),
        'sarang': pbr('sarang', tex['sarang'], tex['sarang_orm'], tex['sarang_n']),
        'telur': pbr('telur', tex['telur'], tex['telur_orm']),
    }

    root = bpy.data.objects.new('Komodo', None); root.empty_display_type = 'PLAIN_AXES'
    sc.collection.objects.link(root)

    def obj(name, mb, mats, pivot=None, sharp=60.0):
        if pivot is not None:
            mb.v = [[x - pivot[0], y - pivot[1], z - pivot[2]] for x, y, z in mb.v]
        ob = build(name, mb, mats, root, sharp_deg=sharp)
        if pivot is not None:
            ob.location = tuple(float(c) for c in pivot)
        return ob

    H = hinge()
    R = tongue_root()
    objs = {
        'Alas': obj('Alas', part_alas(), [M['tanah'], M['batu'], M['rumput']]),
        'Sarang': obj('Sarang', part_sarang(), M['sarang']),
        'Telur': obj('Telur', part_telur(), M['telur']),
        'Tubuh': obj('Tubuh', part_tubuh(), [M['kulit'], M['cakar'], M['mata']], sharp=80.0),
        'Kepala': obj('Kepala', part_head(True), [M['kulit'], M['mulut'], M['gigi'], M['mata']], sharp=80.0),
        'Rahang': obj('Rahang', part_head(False), [M['kulit'], M['mulut'], M['gigi']], pivot=H, sharp=80.0),
        'Lidah': obj('Lidah', part_lidah(), M['lidah'], pivot=R),
    }
    bpy.context.view_layer.update()

    total = 0
    print('\n== Ukuran bagian (m, ruang dunia) ==')
    for name, ob in objs.items():
        co = np.array([ob.matrix_world @ v.co for v in ob.data.vertices])
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons); total += tris
        lo, hi = co.min(0), co.max(0)
        print(f'{name:8s} x[{lo[0]:+.3f},{hi[0]:+.3f}] y[{lo[1]:+.3f},{hi[1]:+.3f}] z[{lo[2]:+.3f},{hi[2]:+.3f}]'
              f'  tris {tris}')
    print(f'Panjang tulang tubuh (moncong-ujung ekor): {LEN:.3f} m; moncong-panggul {A_HIP:.2f} m')
    print(f'Total segitiga: {total}')
    print(f'ENGSEL rahang (Blender): ({H[0]:.4f}, {H[1]:.4f}, {H[2]:.4f})')
    print(f'PANGKAL lidah (Blender): ({R[0]:.4f}, {R[1]:.4f}, {R[2]:.4f})')
    print(f'HEAD_DIR (Blender): ({HEAD_DIR[0]:.4f}, {HEAD_DIR[1]:.4f}, {HEAD_DIR[2]:.4f}); julur {JULUR} m')
    print('== Hotspot tahap 1: Front(part, x, y) dengan (x, y) = (x, z) Blender ==')
    for k, p in hotspot_targets().items():
        print(f'  {k:7s} x {p[0]:+.4f}  y {p[2]:+.4f}   (y Blender {p[1]:+.3f})')

    os.makedirs(ART, exist_ok=True)
    export_glb(root, GLB)
    print('GLB:', GLB, f'({os.path.getsize(GLB) / 1e6:.2f} MB)')
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)
    if RENDER:
        render_previews(tex, objs)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        print('Render pratinjau: Docs/Komodo_Blender_*.png')


if __name__ == '__main__':
    main()
