# Karambit (kurambiak) Minangkabau jantan: bilah cakar harimau 7 gerigi, cincin kuningan, hulu kayu kemuning berlubang,
# sarung kayu berukir tinta emas, di atas dudukan kayu -> Blender -> GLB.
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/karambit.py
# Tambahkan "-- --no-render" di akhir untuk melewati render pratinjau.
#
# Hasil:
#   Assets/NusantaraAR/Art/Karambit/karambit.glb      (model PBR, tekstur tertanam)
#   Tools/blender/karambit_textures/*.png             (sumber tekstur: warna, ORM, normal map)
#   Tools/blender/karambit.blend                      (file kerja; di luar Assets agar tidak diimpor Unity)
#   Docs/Karambit_Blender_*.png                       (render pratinjau)
#
# Ukuran direka dari sumber (lihat Docs/Karambit_Data.md), bukan diukur dari spesimen: gagang ±4 buku jari (pedoman
# perajin Sungai Pua), lengkung bilah ±90-120 derajat dan tebal ½ cm menipis ke mata (Wiraseptya & Afdhal 2019),
# panjang total sebanding karambit Met Museum 36.25.823ab (14,6 cm) - versi jantan dibuat sedikit lebih besar.
#
# Ruang Blender: Z ke atas, muka depan -Y, 1 unit = 1 m. Profil cakar di bidang XZ (menghadap penonton).
# Pangkal bilah (tengah lebar bilah, tepat di bawah cincin) di titik asal; hulu tegak ke atas dengan lubang di puncak,
# bilah turun lalu melengkung ke +X. Punggung bilah = busur lingkaran berpusat PUSAT (jari-jari R_OUT), mata (tajam)
# di sisi dalam/cekung. Sarung mengikuti busur yang sama dan ber-origin di PUSAT: memutarnya di sumbu Y = sarung
# meluncur menyusuri lengkung bilah (exploded view di Unity memakai pivot yang sama).
# Hierarki: Karambit > Pisau (Mata, Cincin, Hulu) + Sarung + Dudukan.

import bpy, math, os, sys
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from keris_sumatra import (MB, build, sweep2d, spow, sstep, smooth01, write_png, uvgrid, fbm2, blur, mix,
                           normal_from_height, orm, pbr, export_glb, look, studio, tex_jackwood, tex_kain,
                           brass_finish, TAU)

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'Karambit')
GLB = os.path.join(ART, 'karambit.glb')
TEX = os.path.join(HERE, 'karambit_textures')
BLEND = os.path.join(HERE, 'karambit.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
RENDER = '--no-render' not in ARGS

# Bilah
W0 = 0.022                     # lebar bilah di pangkal (punggung -> mata)
R_OUT = 0.052                  # jari-jari punggung
CX = R_OUT - W0 / 2            # PUSAT busur = (CX, 0, 0): tengah lebar pangkal bilah tepat di x = 0
SWEEP = math.radians(120)      # sapuan punggung dari pangkal ke ujung
TH0 = math.pi                  # sudut pangkal (sisi kiri pusat); sudut naik = bilah turun lalu melengkung ke +X
TH_START = TH0 - 0.06          # bilah sedikit masuk ke dalam cincin
GERIGI = 7                     # jantan: 7 gerigi di punggung bawah (betina 5)
GERIGI_T = (0.05, 0.31)        # rentang gerigi (fraksi sapuan) di punggung dekat pangkal
GERIGI_D = 0.0017              # dalam takik gerigi
PUTING_L = 0.045               # puting (half tang) masuk ke hulu
# Cincin & hulu
CINCIN_Z = (-0.0045, 0.011)
CINCIN_A, CINCIN_B = 0.0125, 0.0085
HULU_CTRL = [(0.000, 0.011), (-0.001, 0.030), (-0.004, 0.050), (-0.009, 0.066), (-0.014, 0.077)]
LUBANG_RM = 0.0145             # jari-jari tengah cincin lubang (Ø dalam 2,2 cm, Ø luar 3,6 cm)
LUBANG_W, LUBANG_T = 0.0036, 0.0064   # setengah lebar radial dan setengah tebal (Y) cincin lubang
# Sarung
SARUNG_MOUTH = TH0 + math.radians(9)
SARUNG_END = TH0 + SWEEP + math.radians(11)
# Dudukan
BASE_TOP, BASE_BOT = -0.066, -0.080
BASE_CX, BASE_HX, BASE_HY = 0.030, 0.085, 0.034


# ============================================================ bilah (mata + puting)

def blade_w(t):
    """Lebar bilah (radial) sepanjang sapuan: tetap lebar di pangkal lalu menyempit ke ujung runcing."""
    t = np.clip(np.asarray(t, float), 0.0, 1.0)
    return W0 * (1 - t ** 1.7) ** 0.75


def blade_r_in(t):
    return R_OUT - blade_w(t)


def blade_half_t(t):
    """Setengah tebal di punggung: 2,5 mm (tebal ½ cm) menipis ke ujung."""
    t = np.clip(np.asarray(t, float), 0.0, 1.0)
    return 0.0025 * (1 - 0.55 * t) * (1 - 0.6 * sstep(0.85, 1.0, t))


def gerigi(t):
    """Takik gerigi di punggung (0..1): tujuh gigi gergaji yang miring ke arah ujung."""
    t = np.asarray(t, float)
    a, b = GERIGI_T
    f = np.clip((t - a) / (b - a), 0.0, 1.0) * GERIGI
    fr = f % 1.0
    inside = (t > a) & (t < b)
    return np.where(inside, (1 - fr) ** 1.6 * sstep(0.0, 0.08, fr), 0.0)


def arc_point(theta, r):
    return CX + r * np.cos(theta), r * np.sin(theta)


def part_mata():
    n = 150
    ts = np.concatenate([np.linspace((TH_START - TH0) / SWEEP, 0.0, 4, endpoint=False), np.linspace(0.0, 0.995, n)])
    th = TH0 + ts * SWEEP
    C = 36
    tau = TAU * np.arange(C) / C
    s = 0.5 - 0.5 * np.cos(tau)            # 0 = mata (tajam, sisi dalam), 1 = punggung
    side = -np.sign(np.sin(tau))           # -1 = muka depan (-Y)
    side[side == 0] = 1
    P = np.zeros((len(ts), C, 3)); S = np.zeros((len(ts), C))
    for i, (t, a) in enumerate(zip(ts, th)):
        tc = max(t, 0.0)
        r_out = R_OUT - GERIGI_D * gerigi(tc)
        r_in = blade_r_in(tc)
        r = r_in + (r_out - r_in) * s
        # Penampang baji: tajam di mata, punggung tumpul membulat; sisi muka sedikit cekung (asahan).
        half = blade_half_t(tc) * s ** 0.62 * np.abs(np.sin(tau)) ** 0.30
        x, z = arc_point(a, r)
        P[i, :, 0] = x; P[i, :, 1] = side * half; P[i, :, 2] = z
        S[i] = s
    tip_x, tip_z = arc_point(TH0 + 0.999 * SWEEP, R_OUT - 0.2 * blade_w(0.995))
    uv = lambda i, j: (np.clip(ts[i], 0, 1), S[i, j % C])
    mb = MB()
    st_x, st_z = arc_point(th[0], R_OUT - W0 / 2)
    mb.grid(P, uv=uv, start=(st_x, 0.0, st_z), start_uv=(0.0, 0.5), end=(tip_x, 0.0, tip_z), end_uv=(1.0, 0.8))

    # Puting: tangkai pipih yang masuk ke hulu (half tang), meruncing ke atas.
    zs = np.linspace(-0.002, PUTING_L, 10)
    C2 = 16; a2 = TAU * np.arange(C2) / C2
    Q = np.zeros((len(zs), C2, 3))
    for i, z in enumerate(zs):
        k = z / PUTING_L
        Q[i, :, 0] = (0.0050 - 0.0022 * k) * spow(np.cos(a2), 0.6)
        Q[i, :, 1] = (0.0020 - 0.0006 * k) * spow(np.sin(a2), 0.6)
        Q[i, :, 2] = z
    mb.grid(Q, uv=lambda i, j: (0.01, 0.5), start=(0, 0, -0.002), end=(0, 0, PUTING_L))
    return mb


# ============================================================ cincin & hulu

def part_cincin():
    """Selongsong kuningan berpenampang elips dengan dua lis timbul."""
    z0, z1 = CINCIN_Z
    prof = [(0.82, z0), (0.97, z0), (1.02, z0 + 0.0012), (1.02, z0 + 0.0030), (0.95, z0 + 0.0040),
            (0.95, z1 - 0.0050), (1.03, z1 - 0.0036), (1.03, z1 - 0.0016), (0.99, z1), (0.82, z1)]
    C = 48; a = TAU * np.arange(C) / C
    P = np.zeros((len(prof), C, 3))
    for i, (sc, z) in enumerate(prof):
        P[i, :, 0] = CINCIN_A * sc * spow(np.cos(a), 0.85)
        P[i, :, 1] = CINCIN_B * sc * spow(np.sin(a), 0.85)
        P[i, :, 2] = z
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, i / (len(prof) - 1)), wrap_rows=True)
    return mb


def lubang_center():
    pts, T, N, _ = sweep2d(HULU_CTRL, 40)
    return pts[-1] + T[-1] * (LUBANG_RM - 0.0010), T[-1]


def part_hulu():
    """Gagang kayu kemuning: pegangan sedikit membungkuk ke belakang (-X), bercincin lubang untuk telunjuk di puncak."""
    n = 46
    pts, T, N, _ = sweep2d(HULU_CTRL, n)
    s = np.linspace(0, 1, n)
    hw = np.interp(s, [0, .06, .35, .60, .85, 1], [.0117, .0121, .0128, .0122, .0104, .0082])
    ht = np.interp(s, [0, .06, .35, .60, .85, 1], [.0079, .0082, .0088, .0084, .0072, .0060])
    C = 40; a = TAU * np.arange(C) / C
    pc, ps = spow(np.cos(a), 0.8), spow(np.sin(a), 0.85)
    P = np.zeros((n, C, 3))
    for i in range(n):
        P[i, :, 0] = pts[i, 0] + N[i, 0] * hw[i] * pc
        P[i, :, 1] = ht[i] * ps
        P[i, :, 2] = pts[i, 1] + N[i, 1] * hw[i] * pc
    mb = MB()
    V_GRIP = 0.80          # V 0..0.80 = pegangan (ukiran + pita hitam), 0.82..1 = kayu polos untuk cincin lubang
    mb.grid(P, uv=lambda i, j: (j / C, V_GRIP * i / (n - 1)), start=(pts[0][0], 0.0, pts[0][1]), start_uv=(0.5, 0.0))

    # Cincin lubang (torus pipih). Sambungan dengan pegangan tertanam di dalam ujung pegangan.
    c, t = lubang_center()
    m = 64; k = 20
    ph = TAU * np.arange(m) / m
    be = TAU * np.arange(k) / k
    Q = np.zeros((m, k, 3))
    for i, p in enumerate(ph):
        d = np.array([math.cos(p), math.sin(p)])
        rr = LUBANG_RM + LUBANG_W * spow(np.cos(be), 0.7)
        Q[i, :, 0] = c[0] + d[0] * rr
        Q[i, :, 1] = LUBANG_T * spow(np.sin(be), 0.8)
        Q[i, :, 2] = c[1] + d[1] * rr
    mb.grid(Q, uv=lambda i, j: (i / m, 0.82 + 0.17 * j / k), wrap_cols=True, wrap_rows=True)
    return mb


# ============================================================ sarung

def sarung_section(theta):
    """(jari-jari dalam, jari-jari luar, setengah tebal) sarung pada sudut theta."""
    t = (theta - TH0) / SWEEP
    mouth = 1 - sstep(SARUNG_MOUTH, SARUNG_MOUTH + math.radians(12), theta)      # bibir mulut melebar
    end = sstep(TH0 + SWEEP - math.radians(4), SARUNG_END, theta)                  # ekor membulat
    r_in = np.minimum(blade_r_in(t), R_OUT - 0.0060) - 0.0030 - 0.0028 * mouth
    r_out = R_OUT + 0.0032 + 0.0040 * mouth
    half = blade_half_t(np.clip(t, 0, 0.9)) + 0.0028 + 0.0014 * mouth
    mid = 0.5 * (r_in + r_out)
    r_in = r_in + (mid - r_in) * 0.15 * end
    return r_in, r_out, half * (1 - 0.25 * end)


def part_sarung():
    """Sarung kayu mengikuti lengkung bilah; koordinat relatif terhadap PUSAT (origin objek Sarung)."""
    mb = MB()
    n = 90
    th = np.linspace(SARUNG_MOUTH, SARUNG_END, n)
    C = 40; a = TAU * np.arange(C) / C
    pc, ps = spow(np.cos(a), 0.55), spow(np.sin(a), 0.75)

    def rows(ths, off=0.0):
        P = np.zeros((len(ths), C, 3))
        for i, t in enumerate(ths):
            ri, ro, h = sarung_section(t)
            rc, hr = 0.5 * (ri + ro), 0.5 * (ro - ri) + off
            r = rc + hr * pc
            P[i, :, 0] = r * math.cos(t); P[i, :, 1] = (h + off) * ps; P[i, :, 2] = r * math.sin(t)
        return P

    P = rows(th)
    ri, ro, _ = sarung_section(th[-1])
    tip_r = 0.5 * (ri + ro)
    tip_t = SARUNG_END + 0.012
    u = lambda i: (th[i] - SARUNG_MOUTH) / (SARUNG_END - SARUNG_MOUTH)
    mb.grid(P, uv=lambda i, j: (u(i), j / C), cap_start=True,
            end=(tip_r * math.cos(tip_t), 0.0, tip_r * math.sin(tip_t)), end_uv=(1.0, 0.5))

    # Pengikat kuningan di bawah mulut (seperti karambit Met 36.25.869ab).
    mb.mat = 1
    for t0 in (TH0 + math.radians(30), TH0 + math.radians(92)):
        tb = np.linspace(t0, t0 + math.radians(3.2), 6)
        B = rows(tb, 0.0009)
        bump = np.sin(np.linspace(0, np.pi, len(tb)))
        rc = np.hypot(B[..., 0], B[..., 2])
        B[..., 0] *= (1 + 0.012 * bump[:, None]); B[..., 2] *= (1 + 0.012 * bump[:, None])
        B[..., 1] *= (1 + 0.10 * bump[:, None])
        mb.grid(B, uv=lambda i, j: (j / C, i / (len(tb) - 1)))
    return mb


# ============================================================ dudukan

def part_dudukan():
    """Alas kayu oval + tiang di belakang bilah dengan dua lengan penjepit cincin (karambit berdiri tegak)."""
    mb = MB()
    C = 48; a = TAU * np.arange(C) / C
    rows = [(0.97, BASE_BOT), (1.0, BASE_BOT + 0.002), (1.0, BASE_TOP - 0.003), (0.985, BASE_TOP - 0.001),
            (0.95, BASE_TOP)]
    P = np.zeros((len(rows), C, 3))
    for i, (sc, z) in enumerate(rows):
        P[i, :, 0] = BASE_CX + BASE_HX * sc * spow(np.cos(a), 0.45)
        P[i, :, 1] = 0.006 + BASE_HY * sc * spow(np.sin(a), 0.45)
        P[i, :, 2] = z
    planar = lambda p: (0.5 + (p[0] - BASE_CX) / 0.2, 0.5 + p[1] / 0.2)
    mb.grid(P, uv=lambda i, j: planar(P[i, j % C]), start=(BASE_CX, 0.006, BASE_BOT), start_uv=(0.5, 0.5),
            end=(BASE_CX, 0.006, BASE_TOP), end_uv=(0.5, 0.5))

    def block(x0, x1, y0, y1, z0, z1, e=0.6, k=24):
        """Balok membulat (penampang superelips di bidang XY, diekstrusi sepanjang Z)."""
        ang = TAU * np.arange(k) / k
        cx, cy, hx, hy = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
        zs = [z0, z0 + 0.0008, z1 - 0.0008, z1]
        sc = [0.92, 1.0, 1.0, 0.92]
        Q = np.zeros((4, k, 3))
        for i, (z, s) in enumerate(zip(zs, sc)):
            Q[i, :, 0] = cx + hx * s * spow(np.cos(ang), e); Q[i, :, 1] = cy + hy * s * spow(np.sin(ang), e)
            Q[i, :, 2] = z
        mb.grid(Q, uv=lambda i, j: (0.5 + Q[i, j % k, 0] / 0.2, 0.5 + Q[i, j % k, 2] / 0.2),
                start=(cx, cy, z0), end=(cx, cy, z1))

    block(-0.0085, 0.0085, 0.0110, 0.0230, BASE_TOP - 0.002, 0.0105)      # tiang di belakang cincin
    for sx in (-1, 1):                                                     # lengan penjepit kiri/kanan cincin
        x0, x1 = sorted((sx * 0.0133, sx * 0.0185))
        block(x0, x1, -0.0105, 0.0150, 0.0000, 0.0090, e=0.5, k=20)
    return mb


# ============================================================ tekstur (numpy -> PNG)

def spiral(X, Y, cx, cy, pitch, rmax, flip=1.0):
    """Pilin kaluak paku (pucuk pakis bergelung): garis spiral Archimedes, 1 = garis."""
    dx, dy = X - cx, (Y - cy) * flip
    r = np.hypot(dx, dy); th = np.arctan2(dy, dx)
    line = sstep(0.62, 0.80, 0.5 + 0.5 * np.cos(TAU * r / pitch - th))
    return line * (1 - sstep(rmax - 0.0006, rmax, r)) * sstep(0.0004, 0.0008, r)


def tex_mata():
    """Baja tempa: serat lipatan halus memanjang, mata terasah lebih terang, ukiran kaluak paku dekat pangkal."""
    h, w = 512, 2048                     # V = punggung<->mata (0 = mata), U = pangkal -> ujung
    U, V = uvgrid(h, w)
    L = R_OUT * SWEEP                    # panjang busur punggung (m) per satu satuan U
    X = U * L; Y = V * blade_w(U)        # ukuran fisik (m)
    wu = fbm2(U, V, 8, 3, 501, 4) - 0.5
    field = V * 7 + U * 3 + 2.4 * fbm2(U + 0.08 * wu, V, 10, 3, 511, 5)
    fr = field % 1.0
    folds = (1 - sstep(0.04, 0.12, np.minimum(fr, 1 - fr))) * (0.3 + 0.7 * fbm2(U, V, 40, 6, 521, 3))
    edge = 1 - sstep(0.10, 0.30, V)      # jalur asahan di mata
    # Kaluak paku: dua pilin berhadapan di dekat pangkal, dihubungkan tangkai melengkung.
    k = np.zeros((h, w))
    yc = 0.55 * blade_w(0.20)
    for cx, fl in ((0.16 * L, 1.0), (0.27 * L, -1.0)):
        k = np.maximum(k, spiral(X, Y, cx, yc, 0.0016, 0.0050, fl))
    stem = np.exp(-((Y - yc - 0.0030 * np.sin(TAU * (X - 0.16 * L) / (0.22 * L))) / 0.00045) ** 2)
    k = np.maximum(k, stem * sstep(0.10 * L, 0.13 * L, X) * (1 - sstep(0.31 * L, 0.33 * L, X)))
    k = blur(k, 1)
    grain = (fbm2(U, V, 160, 20, 531, 2) - 0.5) * 0.05
    base = mix((0.30, 0.31, 0.32), (0.58, 0.59, 0.60), np.clip(0.45 * folds + 0.55 * edge, 0, 1)) + grain[..., None]
    col = base * (1 - 0.55 * k)[..., None]
    rough = 0.42 - 0.14 * edge + 0.25 * k + 0.06 * folds
    H = 0.6 + 0.15 * folds - 0.5 * k
    return col, orm(rough, np.ones_like(rough) * (1 - 0.3 * k)), normal_from_height(blur(H, 1), 2.0)


def tex_hulu():
    """Kayu kemuning kuning-jingga berserat halus, pita ukiran kaluak paku, dan dua pita hitam (warna kayu & hitam)."""
    h = w = 1024
    U, V = uvgrid(h, w)
    ring = 0.5 + 0.5 * np.sin(TAU * (U * 9 + 1.2 * fbm2(U, V, 4, 3, 601, 4)))      # serat memanjang pegangan
    streak = fbm2(U, V, 80, 4, 611, 3)
    t = np.clip(ring ** 2 * 0.45 + (streak - 0.5) * 0.6 + 0.25, 0, 1)
    wood = mix((0.80, 0.58, 0.27), (0.56, 0.35, 0.13), t)
    # Pita ukiran (V 0,22..0,58 pegangan): pilin berpasangan berselang-seling, latar dicungkil.
    SX, SY = 0.075, 0.078                 # perkiraan ukuran fisik satu satuan UV (keliling, panjang pegangan / 0,8)
    v0, v1 = 0.22, 0.58
    band = sstep(v0, v0 + 0.004, V) * (1 - sstep(v1 - 0.004, v1, V))
    nu, nv = 4, 2
    cu, cv = U * nu, (V - v0) / (v1 - v0) * nv
    iu, iv = np.floor(cu), np.floor(cv)
    lx = (cu - iu - 0.5) * SX / nu; ly = (cv - iv - 0.5) * (v1 - v0) * SY / nv
    flip = np.where((iu + iv) % 2 == 0, 1.0, -1.0)
    motif = spiral(lx, ly, 0.0, 0.0, 0.0021, 0.0072, flip)
    H = 1 - band * (1 - (0.15 + 0.85 * motif))
    for vr in (v0 - 0.012, v1 + 0.012):
        H += 0.4 * np.exp(-((V - vr) * SY / 0.0006) ** 2)
    H = blur(H, 1)
    black = (1 - sstep(0.075, 0.085, V)) + sstep(0.725, 0.735, V) * (1 - sstep(0.80, 0.81, V))
    black = np.clip(black, 0, 1)
    shade = 0.45 + 0.55 * np.clip(blur(H, 3), 0, 1)
    col = wood * shade[..., None]
    col = col + (np.asarray((0.035, 0.028, 0.024)) - col) * black[..., None]
    rough = 0.38 + 0.12 * (1 - np.clip(H, 0, 1)) - 0.12 * black
    return col, orm(rough, np.zeros_like(rough)), normal_from_height(H, 3.0)


def tex_sarung():
    """Kayu sawo kemerahan; muka depan & belakang berukir sulur dan pilin kaluak paku bergaris tinta emas."""
    h = w = 1024
    U, V = uvgrid(h, w)                   # U sepanjang sarung (pangkal -> ekor), V keliling penampang
    ring = 0.5 + 0.5 * np.sin(TAU * (V * 14 + 1.5 * fbm2(U, V, 3, 6, 701, 4)))      # serat memanjang sarung
    streak = fbm2(U, V, 6, 80, 711, 3)
    t = np.clip(ring ** 2 * 0.5 + (streak - 0.5) * 0.6 + 0.2, 0, 1)
    wood = mix((0.46, 0.20, 0.09), (0.25, 0.09, 0.035), t)
    SX = 0.12                             # panjang sarung kira-kira (m) per satuan U
    SY = 0.040                            # keliling penampang (m) per satuan V
    gold = np.zeros((h, w)); groove = np.zeros((h, w))
    for vc in (0.25, 0.75):               # tengah muka belakang (+Y) dan muka depan (-Y)
        Y = (V - vc) * SY; X = U * SX
        stem = 0.0018 * np.sin(TAU * X / 0.034)
        line = np.exp(-((Y - stem) / 0.00035) ** 2) * sstep(0.10, 0.14, U) * (1 - sstep(0.86, 0.90, U))
        sp = np.zeros((h, w))
        for n in range(6):
            xc = (0.15 + 0.13 * n) * SX
            side = 1.0 if n % 2 == 0 else -1.0
            sp = np.maximum(sp, spiral(X, Y, xc, side * 0.0030, 0.0011, 0.0029, side))
        m = np.maximum(line, sp)
        gold = np.maximum(gold, m)
        groove = np.maximum(groove, blur(m, 2))
    # Lis emas di bibir mulut.
    gold = np.maximum(gold, np.exp(-((U - 0.045) / 0.004) ** 2))
    H = 0.7 - 0.45 * groove + 0.1 * t
    col = wood * (0.85 + 0.15 * (1 - groove))[..., None]
    col = col + (np.asarray((0.86, 0.66, 0.28)) - col) * gold[..., None]
    rough = 0.34 + 0.10 * t - 0.10 * gold
    return col, orm(rough, 0.95 * gold), normal_from_height(blur(H, 1), 2.5)


def tex_kuningan():
    """Kuningan cincin & pengikat: alur halus melingkar, patina di cekungan."""
    h, w = 256, 512
    U, V = uvgrid(h, w)
    H = 0.7 + 0.2 * np.sin(TAU * V * 10) ** 8 - 0.15 * (fbm2(U, V, 24, 6, 801, 3) - 0.5)
    col, ormt = brass_finish(blur(H, 1), 811)
    return col, ormt, normal_from_height(blur(H, 1), 1.5)


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    out = {}
    out['mata'], out['mata_orm'], out['mata_n'] = tex_mata()
    out['hulu_kemuning'], out['hulu_kemuning_orm'], out['hulu_kemuning_n'] = tex_hulu()
    out['sarung_ukir'], out['sarung_ukir_orm'], out['sarung_ukir_n'] = tex_sarung()
    out['kuningan'], out['kuningan_orm'], out['kuningan_n'] = tex_kuningan()
    col, rough = tex_jackwood((0.24, 0.13, 0.06), (0.12, 0.06, 0.025), 91)
    out['dudukan'], out['dudukan_orm'] = col, orm(rough, np.zeros_like(rough))
    out['kain'] = tex_kain()                                      # hanya untuk render
    out['meja'], _ = tex_jackwood((0.22, 0.12, 0.06), (0.11, 0.055, 0.025), 93)
    for name, arr in out.items():
        write_png(os.path.join(TEX, name + '.png'), arr)
    return {k: os.path.join(TEX, k + '.png') for k in out}


# ============================================================ render pratinjau

def render_previews(tex, pisau, sarung, dudukan, objs):
    sc = bpy.context.scene
    lights = studio(center=(0.03, 0, 0.01), scale=0.55)
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.03, 0, BASE_BOT))
    meja = bpy.context.active_object; meja.name = 'Meja'
    meja.data.materials.append(pbr('meja', base=tex['meja'], rough=0.55, uv_scale=(3.0, 1.5)))
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.03, 0.0, BASE_BOT + 0.0005))
    kain = bpy.context.active_object; kain.name = 'Kain'
    kain.scale = (0.40, 0.30, 1.0); kain.rotation_euler = (0, 0, math.radians(-4))
    kain.data.materials.append(pbr('kain', base=tex['kain'], rough=0.9))
    props = [meja, kain]

    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(path, res, persp=None, ortho=None):
        sc.render.resolution_x, sc.render.resolution_y = res
        if ortho:
            (cx, cz, size) = ortho
            cam.data.type = 'ORTHO'; cam.data.ortho_scale = size
            cam.location = (cx, -1.0, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
        else:
            target, eye, lens = persp
            cam.data.type = 'PERSP'; cam.data.lens = lens
            cam.location = eye; look(cam, target)
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print('Render:', path)

    def show_props(v):
        for p in props:
            p.hide_render = not v

    show_props(False)
    shot(os.path.join(DOCS, 'Karambit_Blender_tersarung.png'), (1100, 1100), ortho=(0.030, 0.012, 0.215))
    sarung.rotation_euler = (0, -math.radians(130), 0)
    shot(os.path.join(DOCS, 'Karambit_Blender_dihunus.png'), (1100, 1100), ortho=(0.030, 0.012, 0.215))
    sarung.rotation_euler = (0, 0, 0)
    shot(os.path.join(DOCS, 'Karambit_Blender_hulu.png'), (1200, 1000),
         persp=((-0.010, 0.0, 0.055), (-0.10, -0.17, 0.10), 60))

    # Hero: karambit terhunus di dudukan, sarung tergeletak di atas kain.
    show_props(True)
    sarung.rotation_euler = (math.radians(90), 0, math.radians(-12))
    sarung.location = (0.075, -0.075, BASE_BOT + 0.0105)
    shot(os.path.join(DOCS, 'Karambit_Blender_hero.png'), (1600, 900),
         persp=((0.040, -0.03, -0.012), (-0.13, -0.53, 0.15), 42))
    sarung.rotation_euler = (0, 0, 0); sarung.location = (CX, 0, 0)
    show_props(False)
    return lights + props + [cam]


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.name = 'Karambit'
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = {
        'mata': pbr('baja_mata', tex['mata'], tex['mata_orm'], tex['mata_n'], nstrength=0.6),
        'hulu': pbr('kayu_kemuning', tex['hulu_kemuning'], tex['hulu_kemuning_orm'], tex['hulu_kemuning_n'],
                    coat=0.35, coat_rough=0.15),
        'sarung': pbr('kayu_sarung_ukir', tex['sarung_ukir'], tex['sarung_ukir_orm'], tex['sarung_ukir_n'],
                      coat=0.3, coat_rough=0.18),
        'kuningan': pbr('kuningan', tex['kuningan'], tex['kuningan_orm'], tex['kuningan_n']),
        'dudukan': pbr('kayu_dudukan', tex['dudukan'], tex['dudukan_orm'], coat=0.3, coat_rough=0.2),
    }

    def empty(name, parent=None):
        e = bpy.data.objects.new(name, None); e.empty_display_type = 'PLAIN_AXES'; e.empty_display_size = 0.03
        sc.collection.objects.link(e); e.parent = parent
        return e

    root = empty('Karambit')
    pisau = empty('Pisau', root)
    objs = {
        'Mata': build('Mata', part_mata(), M['mata'], pisau, sharp_deg=70.0),
        'Cincin': build('Cincin', part_cincin(), M['kuningan'], pisau),
        'Hulu': build('Hulu', part_hulu(), M['hulu'], pisau),
        'Sarung': build('Sarung', part_sarung(), [M['sarung'], M['kuningan']], root),
        'Dudukan': build('Dudukan', part_dudukan(), M['dudukan'], root),
    }
    objs['Sarung'].location = (CX, 0, 0)          # origin di PUSAT busur
    bpy.context.view_layer.update()

    total = 0
    print('\n== Ukuran bagian (m, ruang dunia) ==')
    for name, ob in objs.items():
        co = np.array([ob.matrix_world @ v.co for v in ob.data.vertices])
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons); total += tris
        lo, hi = co.min(0), co.max(0)
        print(f'{name:10s} x[{lo[0]:+.4f},{hi[0]:+.4f}] y[{lo[1]:+.4f},{hi[1]:+.4f}] z[{lo[2]:+.4f},{hi[2]:+.4f}]'
              f'  ukuran {(hi - lo)[0] * 100:.1f} x {(hi - lo)[1] * 100:.1f} x {(hi - lo)[2] * 100:.1f} cm  tris {tris}')
    knife = np.vstack([np.array([objs[n].matrix_world @ v.co for v in objs[n].data.vertices]) for n in ('Mata', 'Hulu')])
    lo, hi = knife.min(0), knife.max(0)
    print(f'Karambit tanpa sarung: {(hi - lo)[0] * 100:.1f} x {(hi - lo)[2] * 100:.1f} cm (lebar x tinggi)')
    print(f'Busur punggung bilah: {R_OUT * SWEEP * 100:.1f} cm, PUSAT = ({CX:.4f}, 0, 0)')
    print(f'Total segitiga: {total}')

    os.makedirs(ART, exist_ok=True)
    export_glb(root, GLB)
    print('GLB:', GLB, f'({os.path.getsize(GLB) / 1e6:.2f} MB)')
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)
    if RENDER:
        render_previews(tex, pisau, objs['Sarung'], objs['Dudukan'], objs)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        print('Render pratinjau: Docs/Karambit_Blender_*.png')


if __name__ == '__main__':
    main()
