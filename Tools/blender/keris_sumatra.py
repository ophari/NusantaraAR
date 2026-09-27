# Keris Sumatra (luk 7, pamor "wos wutah", hulu burl berukir, sampir bulan sabit, pendok kuningan) -> Blender -> GLB.
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_sumatra.py
# Tambahkan "-- --no-render" di akhir untuk melewati render pratinjau.
#
# Hasil:
#   Assets/NusantaraAR/Art/KerisSumatra/keris_sumatra.glb   (model PBR, tekstur tertanam, animasi "Cabut_Keris")
#   Tools/blender/keris_sumatra_textures/*.png              (sumber tekstur: warna, ORM, normal map)
#   Tools/blender/keris_sumatra.blend                       (file kerja; di luar Assets agar tidak diimpor Unity)
#   Docs/KerisSumatra_Blender_*.png                         (render pratinjau)
#
# Ruang Blender: Z ke atas, muka depan -Y, 1 unit = 1 m. Bilah menghadap -Z, lebar bilah di sumbu X, tebal di Y.
# Pivot = pangkal bilah (pertemuan wilah dan ganja) di titik asal; sisi gandik/kembang kacang di -X,
# greneng, lengkung kepala hulu dan tanduk panjang sampir di +X.
# Hierarki: Keris_Sumatra > Bilah (Wilah, Ganja, Mendak, Hulu) + Sarung (Warangka_Sampir, Warangka_Celah, Gandar,
# Pendok_Atas, Pendok_Bawah) + Dudukan (soket kayu penjepit ujung sarung, agar keris bisa berdiri).
# Empty "Bilah" dianimasikan naik sepanjang +Z (mencabut keris).

import bpy, bmesh, math, os, sys, struct, zlib
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'KerisSumatra')
GLB = os.path.join(ART, 'keris_sumatra.glb')
TEX = os.path.join(HERE, 'keris_sumatra_textures')
BLEND = os.path.join(HERE, 'keris_sumatra.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
RENDER = '--no-render' not in ARGS

BLADE_L = 0.360       # panjang bilah (pangkal -> pucuk), sesuai cetak biru 360 mm
PESI_L = 0.072
GANJA_H = 0.010
MENDAK_TOP = 0.0236
LUK = 7
DRAW_Z = 0.42         # jarak cabut penuh pada animasi
GANDAR_TOP, GANDAR_BOT = -0.030, -0.386
TAU = 2.0 * np.pi


# ============================================================ util numerik

def smooth01(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def sstep(a, b, x):
    return smooth01((np.asarray(x, float) - a) / (b - a))


def catmull(p0, p1, p2, p3, t):
    return 0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)


def resample(dense, n, endpoint=True):
    seg = np.linalg.norm(np.diff(dense, axis=0), axis=1)
    L = np.concatenate([[0.0], np.cumsum(seg)])
    s = np.linspace(0.0, L[-1], n, endpoint=endpoint)
    return np.stack([np.interp(s, L, dense[:, k]) for k in range(dense.shape[1])], 1), L[-1]


def spline(ctrl, n):
    """Catmull-Rom terbuka lewat titik kendali, diambil ulang n titik berjarak busur sama."""
    P = np.asarray(ctrl, float)
    P = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    dense = [catmull(P[i - 1], P[i], P[i + 1], P[i + 2], t)
             for i in range(1, len(P) - 2) for t in np.linspace(0, 1, 24, endpoint=False)]
    dense.append(P[-2])
    return resample(np.array(dense), n)


def closed_spline(ctrl, n):
    P = np.asarray(ctrl, float); m = len(P)
    dense = [catmull(P[(i - 1) % m], P[i], P[(i + 1) % m], P[(i + 2) % m], t)
             for i in range(m) for t in np.linspace(0, 1, 16, endpoint=False)]
    dense.append(dense[0])
    return resample(np.array(dense), n, endpoint=False)[0]


def sweep2d(ctrl, n):
    """Tulang di bidang XZ: titik (x,z), tangen T, normal N (T diputar -90 derajat)."""
    pts, L = spline(ctrl, n)
    T = np.gradient(pts, axis=0)
    T /= np.linalg.norm(T, axis=1)[:, None]
    N = np.stack([T[:, 1], -T[:, 0]], 1)
    return pts, T, N, L


def taper(s, c0, c1, lo0, hi, lo1, p0=0.8, p1=1.0):
    """Naik dari lo0 ke hi di [0,c0], datar sampai c1, turun ke lo1 di ujung."""
    s = np.asarray(s, float); out = np.full_like(s, hi)
    m = s < c0; out[m] = lo0 + (hi - lo0) * np.sin(s[m] / c0 * np.pi / 2) ** p0
    m = s > c1; out[m] = lo1 + (hi - lo1) * np.cos((s[m] - c1) / (1 - c1) * np.pi / 2) ** p1
    return out


def spow(x, e):
    return np.sign(x) * np.abs(x) ** e


# ============================================================ pembangun mesh

class MB:
    """Pengumpul geometri (verteks + wajah + UV per sudut) sebelum dijadikan objek Blender."""

    def __init__(self):
        self.v, self.f, self.uv, self.m = [], [], [], []
        self.mat = 0                      # indeks slot material untuk wajah berikutnya

    def point(self, p):
        self.v.append([float(c) for c in p])
        return len(self.v) - 1

    def face(self, idx, uvs):
        self.f.append(list(idx)); self.uv.append([(float(a), float(b)) for a, b in uvs]); self.m.append(self.mat)

    def grid(self, P, uv=None, wrap_cols=True, wrap_rows=False, start=None, end=None,
             start_uv=None, end_uv=None, cap_start=False, cap_end=False):
        """P: (baris, kolom, 3). Baris = sepanjang benda, kolom = keliling penampang."""
        P = np.asarray(P, float); R, C = P.shape[:2]
        b = len(self.v); self.v.extend(P.reshape(-1, 3).tolist())
        if uv is None:
            uv = lambda i, j: (j / (C if wrap_cols else C - 1), i / (R if wrap_rows else max(R - 1, 1)))
        idx = lambda i, j: b + (i % R) * C + (j % C)
        nc = C if wrap_cols else C - 1
        nr = R if wrap_rows else R - 1
        for i in range(nr):
            for j in range(nc):
                self.face([idx(i, j), idx(i, j + 1), idx(i + 1, j + 1), idx(i + 1, j)],
                          [uv(i, j), uv(i, j + 1), uv(i + 1, j + 1), uv(i + 1, j)])
        for row, pt, puv, cap in ((0, start, start_uv, cap_start), (R - 1, end, end_uv, cap_end)):
            if pt is not None:
                c = self.point(pt)
                for j in range(nc):
                    a, e = uv(row, j), uv(row, j + 1)
                    self.face([idx(row, j), idx(row, j + 1), c], [a, e, puv or ((a[0] + e[0]) / 2, a[1])])
            elif cap:
                self.face([idx(row, j) for j in range(C)], [uv(row, j) for j in range(C)])


def build(name, mb, mat, parent, sharp_deg=55.0, recalc=True):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in mb.v]
    uvl = bm.loops.layers.uv.new('UVMap')
    for idx, uvs, mi in zip(mb.f, mb.uv, mb.m):
        if len(set(idx)) < len(idx):
            continue
        try:
            f = bm.faces.new([vs[i] for i in idx])
        except ValueError:
            continue
        f.material_index = mi
        for lp, t in zip(f.loops, uvs):
            lp[uvl].uv = t
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-7)
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method='BEAUTY', ngon_method='BEAUTY')
    lim = math.radians(sharp_deg)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > lim:
            e.smooth = False
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    for m in (mat if isinstance(mat, (list, tuple)) else [mat]):
        me.materials.append(m)
    return ob


def lathe(prof, n, cx=0.0, cy=0.0):
    prof = np.asarray(prof, float); a = TAU * np.arange(n) / n
    P = np.zeros((len(prof), n, 3))
    P[..., 0] = cx + prof[:, 0, None] * np.cos(a)[None]
    P[..., 1] = cy + prof[:, 0, None] * np.sin(a)[None]
    P[..., 2] = prof[:, 1, None]
    return P


def sphere(mb, c, r, rows=5, cols=8):
    lat = np.linspace(-np.pi / 2, np.pi / 2, rows + 2)[1:-1]
    a = TAU * np.arange(cols) / cols
    P = np.zeros((rows, cols, 3))
    P[..., 0] = c[0] + r * np.cos(lat)[:, None] * np.cos(a)[None]
    P[..., 1] = c[1] + r * np.cos(lat)[:, None] * np.sin(a)[None]
    P[..., 2] = c[2] + r * np.sin(lat)[:, None]
    mb.grid(P, uv=lambda i, j: (0.5 + 0.05 * math.cos(TAU * j / cols), 0.40 + 0.05 * i / rows),
            start=(c[0], c[1], c[2] - r), end=(c[0], c[1], c[2] + r))


# ============================================================ bilah (wilah, pesi, ganja)

def luk_center(u):
    """Garis tengah bilah ber-luk 7: tujuh setengah gelombang antara sor-soran dan pucuk."""
    u = np.asarray(u, float)
    a, b = 0.13, 0.965
    t = np.clip((u - a) / (b - a), 0.0, 1.0)
    amp = 0.0050 * sstep(0.10, 0.24, u) * (1 - 0.35 * u)
    return amp * np.sin(LUK * np.pi * t)


def blade_widths(u):
    """Setengah lebar sisi depan (gandik, -X) dan belakang (greneng, +X), diukur tegak lurus garis tengah."""
    u = np.asarray(u, float)
    body = 0.0185 * (1 - 0.30 * u)
    tip = 1 - np.clip((u - 0.84) / 0.16, 0, 1) ** 1.7
    front = (0.0095 * (1 - sstep(0.0, 0.13, u))
             + 0.0032 * np.exp(-((u - 0.045) / 0.012) ** 2)      # kembang kacang (belalai gajah)
             - 0.0026 * np.exp(-((u - 0.076) / 0.007) ** 2))     # lekuk jalen / lambe gajah
    back = 0.022 * (1 - sstep(0.0, 0.16, u)) ** 1.3
    f = ((u - 0.018) / 0.0105) % 1.0                             # greneng: gigi gergaji menghadap pucuk
    teeth = 0.0024 * sstep(0.012, 0.022, u) * (1 - sstep(0.095, 0.108, u)) * (1 - f) ** 1.5
    return (body + front) * tip, (body + back - teeth) * tip, tip


def part_wilah():
    us = np.concatenate([np.linspace(0.0, 0.14, 92, endpoint=False),
                         np.linspace(0.14, 0.86, 96, endpoint=False),
                         np.linspace(0.86, 0.994, 18)])
    xc = luk_center(us)
    zc = -BLADE_L * us
    dx = np.gradient(xc); dz = np.gradient(zc)
    ln = np.hypot(dx, dz)
    Nx, Nz = -dz / ln, dx / ln                                    # normal ke +X (sisi belakang)
    C = 40
    tau = TAU * np.arange(C) / C
    tt = tau + 0.16 * np.sin(2 * tau)                             # kolom lebih rapat di tengah (ada-ada)
    c, s = np.cos(tt), np.sin(tt)
    P = np.zeros((len(us), C, 3)); W = np.zeros((len(us), C))
    for i, u in enumerate(us):
        wF, wB, tip = blade_widths(u)
        b = 0.0034 * (1 - 0.55 * u) * np.sqrt(max(tip, 0.02))    # setengah tebal
        ridge = 0.22 * sstep(0.02, 0.10, u) * (1 - sstep(0.80, 0.95, u))
        e = np.where(c < 0, 0.55 * (1 - sstep(0.06, 0.10, u)), 0.0)   # gandik: tepi depan tumpul di pangkal
        f = e + (1 - e) * (1 - np.abs(c) ** 1.8) ** 0.75                 # penampang lensa, tepi tajam
        w = c * np.where(c < 0, wF, wB)
        pej = 1 - 0.35 * np.exp(-(((w + 0.010) / 0.0055) ** 2 + ((u - 0.055) / 0.028) ** 2))  # pejetan
        W[i] = w
        P[i, :, 0] = xc[i] + Nx[i] * w
        P[i, :, 1] = s * b * (f + ridge * np.exp(-(c / 0.12) ** 2)) * pej
        P[i, :, 2] = zc[i] + Nz[i] * w
    uv = lambda i, j: (0.45 + W[i, j % C] / 0.08, us[i])
    mb = MB()
    mb.grid(P, uv=uv, start=(0, 0, 0), start_uv=(0.45, 0.0), end=(0, 0, -BLADE_L), end_uv=(0.45, 1.0))

    # Pesi: tangkai persegi membulat yang masuk ke hulu.
    zs = np.linspace(-0.001, PESI_L - 0.003, 8)
    C2 = 16; a = TAU * np.arange(C2) / C2
    Q = np.zeros((len(zs), C2, 3))
    for i, z in enumerate(zs):
        r = 0.0046 - 0.0024 * z / PESI_L
        Q[i, :, 0] = r * spow(np.cos(a), 0.7)
        Q[i, :, 1] = 0.85 * r * spow(np.sin(a), 0.7)
        Q[i, :, 2] = z
    mb.grid(Q, uv=lambda i, j: (0.45 + Q[i, j % C2, 0] / 0.08, 0.02 * i / 7),
            start=(0, 0, -0.001), end=(0, 0, PESI_L))
    return mb


def part_ganja():
    # Profil samping (x, z di atas pangkal bilah): sirah cecak di depan (-X), buntut urang melengkung di belakang (+X).
    prof = [(-0.0280, 0.0000), (0.0100, 0.0000), (0.0330, 0.0000), (0.0375, 0.0012), (0.0400, 0.0035),
            (0.0408, 0.0062), (0.0398, 0.0080), (0.0375, 0.0078), (0.0330, 0.0086), (0.0200, 0.0097),
            (0.0000, 0.0100), (-0.0150, 0.0100), (-0.0225, 0.0098), (-0.0265, 0.0090), (-0.0290, 0.0075),
            (-0.0296, 0.0056), (-0.0288, 0.0040), (-0.0275, 0.0028), (-0.0284, 0.0010)]
    Q = closed_spline(prof, 72)
    area = 0.5 * np.sum(Q[:, 0] * np.roll(Q[:, 1], -1) - np.roll(Q[:, 0], -1) * Q[:, 1])
    tg = np.roll(Q, -1, 0) - np.roll(Q, 1, 0)
    nrm = np.stack([tg[:, 1], -tg[:, 0]], 1); nrm /= np.linalg.norm(nrm, axis=1)[:, None]
    if area < 0:
        nrm = -nrm
    T, r = 0.0048, 0.0012
    phis = np.linspace(-np.pi / 2, np.pi / 2, 7)
    P = np.zeros((len(phis), len(Q), 3))
    for i, ph in enumerate(phis):
        q = Q - nrm * r * (1 - np.cos(ph))
        P[i, :, 0] = q[:, 0]; P[i, :, 1] = T * np.sin(ph); P[i, :, 2] = q[:, 1]
    uv = lambda i, j: (0.45 + P[i, j % len(Q), 0] / 0.08, 0.002 + P[i, j % len(Q), 2] + 0.01 * i / 6)
    mb = MB()
    mb.grid(P, uv=uv, cap_start=True, cap_end=True)
    return mb


# ============================================================ mendak & hulu

def part_mendak():
    mb = MB()
    prof = [(0.0050, 0.0100), (0.0094, 0.0100), (0.0102, 0.0104), (0.0104, 0.0112), (0.0099, 0.0117),
            (0.0105, 0.0123), (0.0109, 0.0150), (0.0113, 0.0178), (0.0119, 0.0196), (0.0125, 0.0205),
            (0.0127, 0.0213), (0.0121, 0.0221), (0.0126, 0.0227), (0.0122, MENDAK_TOP), (0.0050, MENDAK_TOP)]
    P = lathe(prof, 48)
    mb.grid(P, uv=lambda i, j: (j / 48, i / len(prof)), wrap_rows=True)
    for k in range(18):                              # butiran kuningan melingkar
        a = TAU * (k + 0.5) / 18
        sphere(mb, (0.0104 * math.cos(a), 0.0104 * math.sin(a), 0.0136), 0.00115)
    return mb


HULU_CTRL = [(0.000, MENDAK_TOP), (0.000, 0.040), (0.001, 0.058), (0.004, 0.074), (0.010, 0.088),
             (0.020, 0.098), (0.033, 0.104)]


def part_hulu():
    """Hulu Sumatra/Semenanjung: pegangan pistol yang membungkuk ke +X, kepala stilasi burung dengan paruh."""
    n = 52
    pts, T, N, _ = sweep2d(HULU_CTRL, n)
    s = np.linspace(0, 1, n)
    hw = np.interp(s, [0, .04, .08, .14, .40, .62, .80, .92, 1],
                   [.0121, .0133, .0133, .0117, .0138, .0127, .0148, .0162, .0158])
    ht = np.interp(s, [0, .08, .14, .40, .80, 1], [.0119, .0126, .0112, .0126, .0136, .0138])
    kB = 1 + 0.20 * np.exp(-((s - 0.36) / 0.10) ** 2)    # perut membusung di sisi dalam (+N)
    kF = 1 + 0.10 * np.exp(-((s - 0.88) / 0.07) ** 2)    # ubun-ubun kepala di sisi luar (-N)
    C = 40; a = TAU * np.arange(C) / C; ca, sa = np.cos(a), np.sin(a)
    pc = spow(ca, 0.85); ps = spow(sa, 0.9)

    def ring(p, nv, wf, wb, h, sc=1.0):
        off = np.where(ca > 0, wb, wf) * pc * sc
        return np.stack([p[0] + nv[0] * off, h * ps * sc, p[1] + nv[1] * off], 1)

    rows = [ring(pts[i], N[i], hw[i] * kF[i], hw[i] * kB[i], ht[i]) for i in range(n)]
    d, beak = 0.017, 0.0032
    for kk in range(1, 6):                           # paruh: meruncing dan sedikit menunduk
        ph = kk / 6 * np.pi / 2
        c = pts[-1] + T[-1] * d * np.sin(ph) + N[-1] * beak * np.sin(ph) ** 2
        rows.append(ring(c, N[-1], hw[-1] * kF[-1], hw[-1] * kB[-1], ht[-1], np.cos(ph) ** 0.75))
    tip = pts[-1] + T[-1] * d + N[-1] * beak
    P = np.array(rows); R = len(P)
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, i / (R - 1)), start=(pts[0][0], 0.0, pts[0][1]), start_uv=(0.5, 0.0),
            end=(tip[0], 0.0, tip[1]), end_uv=(0.5, 1.0))
    return mb


# ============================================================ sarung (sampir, gandar, pendok)

SAMPIR_CTRL = [(-0.064, 0.022), (-0.063, 0.006), (-0.052, -0.010), (-0.030, -0.019), (0.005, -0.021),
               (0.040, -0.017), (0.070, -0.005), (0.092, 0.015), (0.106, 0.042), (0.112, 0.070)]


def part_sampir():
    n = 64
    pts, T, N, _ = sweep2d(SAMPIR_CTRL, n)
    s = np.linspace(0, 1, n)
    h = taper(s, 0.22, 0.50, 0.0065, 0.0215, 0.0012, 0.7, 1.2)    # setengah tinggi (tegak lurus tulang)
    t = taper(s, 0.22, 0.50, 0.0060, 0.0135, 0.0022)              # setengah tebal (Y): 2,7 cm di tengah
    C = 32; a = TAU * np.arange(C) / C; ca, sa = np.cos(a), np.sin(a)
    pc = np.where(ca < 0, spow(ca, 0.45), spow(ca, 0.70))          # sisi atas (mulut) lebih datar
    qs = spow(sa, 0.6)
    P = np.zeros((n, C, 3))
    for i in range(n):
        P[i, :, 0] = pts[i, 0] + N[i, 0] * h[i] * pc
        P[i, :, 1] = t[i] * qs
        P[i, :, 2] = pts[i, 1] + N[i, 1] * h[i] * pc
    # Mulut sampir rata di z = 0 tempat ganja duduk; kedua tanduk tetap melengkung ke atas.
    x = P[..., 0]
    mask = sstep(-0.052, -0.038, x) * (1 - sstep(0.050, 0.066, x))
    P[..., 2] -= mask * np.maximum(P[..., 2], 0.0)
    st = pts[0] - T[0] * 0.003; en = pts[-1] + T[-1] * 0.0015
    mb = MB()
    mb.grid(P, uv=lambda i, j: (i / (n - 1), j / C),
            start=(st[0], 0, st[1]), start_uv=(0.0, 0.5), end=(en[0], 0, en[1]), end_uv=(1.0, 0.5))
    return mb


def part_celah():
    """Celah (slot) bilah di mulut sampir: bidang gelap tipis tepat di atas permukaan rata."""
    mb = MB()
    x0, x1, hy = -0.0270, 0.0392, 0.0040
    k = np.linspace(0, 1, 25)
    xs = x0 + (x1 - x0) * k
    ys = hy * np.sqrt(np.clip(1 - (2 * k - 1) ** 2, 0, 1)) ** 0.8
    ring = [(x, -y) for x, y in zip(xs, ys)] + [(x, y) for x, y in zip(xs[::-1][1:-1], ys[::-1][1:-1])]
    c = mb.point(((x0 + x1) / 2, 0.0, 0.0002))
    ids = [mb.point((x, y, 0.0002)) for x, y in ring]
    for j in range(len(ids)):
        mb.face([c, ids[j], ids[(j + 1) % len(ids)]], [(0.5, 0.5)] * 3)
    return mb


def gandar_dims(z):
    q = np.clip((GANDAR_TOP - z) / (GANDAR_TOP - GANDAR_BOT), 0, 1)
    return 0.025 - 0.007 * q ** 0.9, 0.0095 - 0.0020 * q


def lens(a, b, C):
    ang = TAU * np.arange(C) / C
    return a * spow(np.cos(ang), 0.85), b * spow(np.sin(ang), 0.9)


def sheath_rows(zs, offs, scales, C):
    P = np.zeros((len(zs), C, 3))
    for i, (z, o, sc) in enumerate(zip(zs, offs, scales)):
        a, b = gandar_dims(min(z, GANDAR_TOP))
        x, y = lens((a + o) * sc, (b + o) * sc, C)
        P[i, :, 0] = x; P[i, :, 1] = y; P[i, :, 2] = z
    return P


def end_cap(z_bot, depth, off, k=5):
    zs, offs, scales = [], [], []
    for kk in range(1, k):
        ph = kk / k * np.pi / 2
        zs.append(z_bot - depth * np.sin(ph)); offs.append(off); scales.append(np.cos(ph) ** 0.8)
    return zs, offs, scales


def part_gandar():
    C = 36
    zs = list(np.linspace(GANDAR_TOP, GANDAR_BOT, 44)); offs = [0.0] * 44; scales = [1.0] * 44
    z2, o2, s2 = end_cap(GANDAR_BOT, 0.011, 0.0)
    zs += z2; offs += o2; scales += s2
    P = sheath_rows(zs, offs, scales, C)
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, (GANDAR_TOP - zs[i]) / 0.36), start=(0, 0, GANDAR_TOP),
            end=(0, 0, GANDAR_BOT - 0.011))
    return mb


PENDOK_ATAS = (-0.036, -0.150)
PENDOK_BAWAH_TOP = -0.262
PENDOK_V = 0.14       # panjang (m) satu satuan V tekstur pendok


def pendok_offset(z, rims):
    o = 0.0010
    for zr in rims:
        o += 0.0009 * np.exp(-((z - zr) / 0.0014) ** 2)
    return o


def part_pendok_atas():
    C = 36
    z0, z1 = PENDOK_ATAS
    zs = list(np.linspace(z0, z1, 46))
    offs = [pendok_offset(z, (z1 + 0.003,)) for z in zs]
    zs.append(z1); offs.append(-0.0003)                          # bibir masuk ke gandar
    P = sheath_rows(zs, offs, [1.0] * len(zs), C)
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, (z0 - zs[i]) / PENDOK_V))
    return mb


def part_pendok_bawah():
    C = 36
    z0 = PENDOK_BAWAH_TOP
    zs = [z0] + list(np.linspace(z0, GANDAR_BOT, 44))
    offs = [-0.0003] + [pendok_offset(z, (z0 - 0.003,)) for z in zs[1:]]
    z2, o2, s2 = end_cap(GANDAR_BOT, 0.013, 0.0010, 7)
    scales = [1.0] * len(zs) + s2
    zs += z2; offs += o2
    P = sheath_rows(zs, offs, scales, C)
    mb = MB()
    tip = GANDAR_BOT - 0.013
    mb.grid(P, uv=lambda i, j: (j / C, (z0 - zs[i]) / PENDOK_V), end=(0, 0, tip),
            end_uv=(0.5, (z0 - tip) / PENDOK_V))
    return mb


DUDUKAN_Z = GANDAR_BOT - 0.019     # dasar dudukan (ujung pendok terbenam di soketnya)


def part_dudukan():
    """Dudukan kayu gelap: lempeng oval berbibir + soket lensa yang menjepit ujung sarung agar keris berdiri."""
    mb = MB()
    C = 48; a = TAU * np.arange(C) / C
    zb = DUDUKAN_Z
    rows = [(0.97, zb), (1.0, zb + 0.003), (1.0, zb + 0.014), (0.985, zb + 0.017), (0.94, zb + 0.019), (0.93, zb + 0.022)]
    P = np.zeros((len(rows), C, 3))
    for i, (sc, z) in enumerate(rows):
        P[i, :, 0] = 0.080 * sc * spow(np.cos(a), 0.5); P[i, :, 1] = 0.050 * sc * spow(np.sin(a), 0.5); P[i, :, 2] = z
    planar = lambda p: (0.5 + p[0] / 0.2, 0.5 + p[1] / 0.2)
    mb.grid(P, uv=lambda i, j: planar(P[i, j % C]), start=(0, 0, zb), start_uv=(0.5, 0.5),
            end=(0, 0, zb + 0.022), end_uv=(0.5, 0.5))
    z_top = -0.359
    pa, pb = gandar_dims(z_top)
    ring = [(0.0245, 0.0135, zb + 0.020), (0.0245, 0.0135, z_top - 0.006), (0.0255, 0.0142, z_top - 0.003),
            (0.0240, 0.0130, z_top), (pa + 0.0012, pb + 0.0012, z_top)]
    C2 = 36
    Q = np.zeros((len(ring), C2, 3))
    for i, (ra, rb, z) in enumerate(ring):
        x, y = lens(ra, rb, C2)
        Q[i, :, 0] = x; Q[i, :, 1] = y; Q[i, :, 2] = z
    mb.grid(Q, uv=lambda i, j: (j / C2, 0.3 * i / (len(ring) - 1)))
    return mb


# ============================================================ tekstur (numpy -> PNG)

def write_png(path, arr):
    """arr: (tinggi, lebar, kanal) float 0..1, baris 0 = v 0 (bawah). PNG ditulis atas ke bawah."""
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]
    h, w, c = a.shape
    raw = b''.join(b'\x00' + a[y].tobytes() for y in range(h))

    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    ct = {1: 0, 3: 2, 4: 6}[c]
    with open(path, 'wb') as fh:
        fh.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0))
                 + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def uvgrid(h, w):
    U = (np.arange(w) + 0.5) / w; V = (np.arange(h) + 0.5) / h
    return np.meshgrid(U, V)


def noise2(x, y, px, py, seed):
    """Value noise periodik (px x py sel) pada koordinat kisi sembarang."""
    g = np.random.default_rng(seed).random((py, px))
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = smooth01(x - x0); fy = smooth01(y - y0)
    x0 %= px; y0 %= py; x1 = (x0 + 1) % px; y1 = (y0 + 1) % py
    a = g[y0, x0]; b = g[y0, x1]; c = g[y1, x0]; d = g[y1, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm2(U, V, px, py, seed, octaves=4):
    s, amp, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        k = 2 ** o
        s = s + amp * noise2(U * px * k, V * py * k, px * k, py * k, seed + 17 * o)
        tot += amp; amp *= 0.5
    return s / tot


def blur(H, r):
    out = H
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for k in range(-r, r + 1):
            acc += np.roll(out, k, axis)
        out = acc / (2 * r + 1)
    return out


def mix(c0, c1, t):
    return np.asarray(c0)[None, None] + (np.asarray(c1) - np.asarray(c0))[None, None] * t[..., None]


def normal_from_height(H, k):
    gx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) * 0.5
    gy = (np.roll(H, -1, 0) - np.roll(H, 1, 0)) * 0.5
    n = np.stack([-k * gx, -k * gy, np.ones_like(H)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def orm(rough, metal):
    out = np.ones(rough.shape + (3,))
    out[..., 1] = rough; out[..., 2] = metal
    return out


def tex_pamor():
    """Baja lipat nikel: urat perak bergelombang memanjang, terputus-putus menjadi butir 'wos wutah'."""
    h, w = 2048, 512                      # U = 80 mm melintang bilah, V = 360 mm sepanjang bilah
    U, V = uvgrid(h, w)
    wu = fbm2(U, V, 6, 28, 101, 4) - 0.5
    wv = fbm2(U, V, 6, 28, 131, 4) - 0.5
    field = U * 34 + 7.0 * fbm2(U + 0.10 * wu, V + 0.06 * wv, 5, 20, 151, 5)
    fr = field % 1.0; d = np.minimum(fr, 1 - fr)
    lines = (1 - sstep(0.045, 0.11, d)) * (0.25 + 0.75 * sstep(0.36, 0.50, fbm2(U, V, 18, 70, 171, 3)))
    field2 = U * 13 + 9.0 * fbm2(U + 0.2 * wv, V, 7, 26, 181, 4)                  # lapisan kedua yang menyilang tipis
    fr2 = field2 % 1.0
    lines2 = (1 - sstep(0.02, 0.05, np.minimum(fr2, 1 - fr2))) * sstep(0.45, 0.60, fbm2(U, V, 10, 40, 185, 3))
    grains = sstep(0.70, 0.74, fbm2(U + 0.03 * wu, V, 30, 140, 191, 3))              # butir beras tercecer
    speck = (np.random.default_rng(7).random((h, w)) > 0.995) * 0.4
    m = blur(np.clip(lines * 0.9 + lines2 * 0.6 + grains * 0.7 + speck, 0, 1), 1)
    grain = (fbm2(U, V, 64, 256, 211, 2) - 0.5) * 0.05
    col = mix((0.10, 0.10, 0.11), (0.74, 0.75, 0.77), m) + grain[..., None]
    return col, orm(0.45 - 0.20 * m, np.ones_like(m)), normal_from_height(blur(m, 2), 1.5)


def tex_burl(seed, h=1024, w=1024):
    """Kayu burl: serat berpusar acak dengan mata kayu gelap."""
    U, V = uvgrid(h, w)
    q1 = fbm2(U, V, 3, 3, seed, 4); q2 = fbm2(U, V, 3, 3, seed + 1, 4)
    f = fbm2(U + 0.45 * (q1 - 0.5), V + 0.45 * (q2 - 0.5), 5, 5, seed + 2, 5)
    knots = np.zeros((h, w))
    for eu, ev, rr in np.random.default_rng(seed + 3).random((70, 3)):
        du = (U - eu + 0.5) % 1 - 0.5; dv = (V - ev + 0.5) % 1 - 0.5
        r = np.hypot(du, dv)
        f = f + 0.10 * np.exp(-r / 0.015)
        knots = np.maximum(knots, np.exp(-(r / (0.0025 + 0.004 * rr)) ** 2))
    rings = 0.5 + 0.5 * np.sin(TAU * f * 14)
    fine = fbm2(U, V, 48, 48, seed + 4, 3)
    blotch = fbm2(U, V, 6, 6, seed + 5, 3)
    t = np.clip(0.30 * rings ** 1.5 + 0.55 * (fine - 0.5) + 0.60 * (blotch - 0.5) + 0.40, 0, 1)
    col = mix((0.60, 0.31, 0.13), (0.27, 0.11, 0.04), t)
    col = col + (np.asarray((0.12, 0.05, 0.02)) - col) * (0.9 * knots)[..., None]
    return col, 0.22 + 0.12 * t


def hulu_carving():
    """Ukiran sulur (spiral berpasangan) dan bunga empat kelopak di bagian bawah hulu, plus alur cincin di leher."""
    h = w = 1024
    U, V = uvgrid(h, w)
    SX, SY = 0.085, 0.13                  # perkiraan ukuran fisik (m) satu satuan UV
    v0, v1 = 0.10, 0.58
    band = sstep(v0, v0 + 0.004, V) * (1 - sstep(v1 - 0.004, v1, V))
    nu, nv = 4, 3
    cu = U * nu; cv = (V - v0) / (v1 - v0) * nv
    iu = np.floor(cu); iv = np.floor(cv)
    lx = (cu - iu - 0.5) * SX / nu
    ly = (cv - iv - 0.5) * (v1 - v0) * SY / nv
    flip = np.where((iu + iv) % 2 == 0, 1.0, -1.0)
    r = np.hypot(lx, ly); th = np.arctan2(ly, lx * flip)
    spiral = sstep(0.45, 0.62, 0.5 + 0.5 * np.cos(TAU * r / 0.0024 - th)) * (1 - sstep(0.0066, 0.0076, r))
    motif = np.maximum(spiral, 1 - sstep(0.0010, 0.0016, r))
    ex = cu % 1.0; ex = np.where(ex > 0.5, ex - 1, ex) * SX / nu
    ey = cv % 1.0; ey = np.where(ey > 0.5, ey - 1, ey) * (v1 - v0) * SY / nv
    rr = np.hypot(ex, ey); ang = np.arctan2(ey, ex)
    petal = 0.0040 * (0.30 + 0.70 * np.abs(np.cos(2 * ang)))
    motif = np.maximum(motif, 1 - sstep(petal - 0.0006, petal, rr))
    H = 1 - band * (1 - (0.08 + 0.92 * motif))
    for vr in (v0 - 0.012, v1 + 0.012):                          # lis timbul pembatas
        H += 0.5 * np.exp(-((V - vr) * SY / 0.0007) ** 2)
    for vg in (0.030, 0.060):                                     # alur cincin di leher
        H -= 0.7 * np.exp(-((V - vg) * SY / 0.0006) ** 2)
    return blur(H, 1)


def tex_hulu():
    col, rough = tex_burl(61)
    H = hulu_carving()
    shade = 0.42 + 0.58 * np.clip(blur(H, 3), 0, 1)
    return col * shade[..., None], orm(rough + 0.12 * (1 - np.clip(H, 0, 1)), np.zeros_like(rough)), \
        normal_from_height(H, 3.5)


def tex_jackwood(light=(0.58, 0.34, 0.15), dark=(0.40, 0.21, 0.08), seed=81):
    """Kayu nangka: serat lurus halus sepanjang V."""
    h, w = 1024, 512
    U, V = uvgrid(h, w)
    ring = 0.5 + 0.5 * np.sin(TAU * (U * 30 + 1.6 * fbm2(U, V, 4, 16, seed, 4)))
    streak = fbm2(U, V, 96, 6, seed + 1, 3)
    t = np.clip(ring ** 2.2 * 0.6 + (streak - 0.5) * 0.7 + 0.15, 0, 1)
    return mix(light, dark, t), 0.34 + 0.10 * t


def brass_finish(H, seed):
    """Kuningan 85/15: timbulan terpoles terang, cekungan berpatina gelap dan lebih kasar."""
    cav = np.clip(0.15 + 0.85 * sstep(0.2, 0.85, blur(H, 3)), 0, 1)
    h, w = H.shape
    U, V = uvgrid(h, w)
    tarn = fbm2(U, V, 8, 8, seed, 4)
    col = mix((0.24, 0.18, 0.08), (0.88, 0.70, 0.36), cav) * (0.88 + 0.24 * tarn)[..., None]
    return col, orm(0.52 - 0.22 * cav + 0.06 * (tarn - 0.5), 0.88 + 0.12 * cav)


def tex_pendok():
    """Repoussé: dua panel (depan/belakang) berisi batang sulur bergelombang, spiral dan daun, latar bertitik."""
    h = w = 1024
    U, V = uvgrid(h, w)
    SX, SY = 0.10, PENDOK_V
    X, Y = U * SX, V * SY
    H = np.full((h, w), 0.70)
    for uc in (0.25, 0.75):
        xm = (U - uc) * SX
        inside = 1 - sstep(0.0192, 0.0202, np.abs(xm))
        xs = 0.0075 * np.sin(TAU * Y / (SY / 3))
        motif = np.exp(-((xm - xs) / 0.0009) ** 2)
        for n in range(6):
            yc = (n + 0.5) * SY / 6
            side = -np.sign(np.sin(TAU * yc / (SY / 3)))
            dx = xm - side * 0.0088; dy = Y - yc
            r = np.hypot(dx, dy); th = np.arctan2(dy, dx * side)
            sp = sstep(0.55, 0.72, 0.5 + 0.5 * np.cos(TAU * r / 0.0026 - th)) * (1 - sstep(0.0070, 0.0080, r))
            motif = np.maximum(motif, np.maximum(sp, 1 - sstep(0.0009, 0.0014, r)))
            yl = n * SY / 6                                              # sepasang daun di tiap silang batang
            for sg in (-1.0, 1.0):
                ang = sg * 0.9
                ax = (xm - 0.0) * np.cos(ang) + (Y - yl) * np.sin(ang)
                ay = -(xm - 0.0) * np.sin(ang) + (Y - yl) * np.cos(ang)
                L = 0.0062
                wl = 0.0019 * np.clip(1 - (ax / L) ** 2, 0, 1)
                leaf = (1 - sstep(wl * 0.7, wl + 1e-5, np.abs(ay))) * (np.abs(ax) < L)
                motif = np.maximum(motif, leaf)
        dots = ((X / 0.0011) % 1 - 0.5) ** 2 + ((Y / 0.0011) % 1 - 0.5) ** 2 < 0.06
        panel = 0.22 + 0.78 * np.clip(motif, 0, 1) - 0.10 * dots
        H = np.where(inside > 0.5, panel, H)
        H = np.maximum(H, 0.70 + 0.30 * np.exp(-((np.abs(xm) - 0.0215) / 0.0006) ** 2))
    H = np.maximum(H, 0.70 + 0.30 * np.exp(-((V - 0.012) * SY / 0.0006) ** 2))
    H = blur(H, 1)
    col, ormt = brass_finish(H, 301)
    return col, ormt, normal_from_height(H, 5.0)


def tex_mendak():
    """Mendak: pita kelopak teratai (padma) di atas baris butiran."""
    h, w = 256, 512
    U, V = uvgrid(h, w)
    H = np.full((h, w), 0.6)
    pu = (U * 16) % 1 - 0.5; pv = (V - 0.50) / 0.30
    petal = (np.abs(pu) < 0.42 * np.sqrt(np.clip(1 - (2 * pv - 1) ** 2, 0, 1))) * (pv > 0) * (pv < 1)
    H = np.where(petal, 1.0, np.where((pv > 0) & (pv < 1), 0.25, H))
    for vg in (0.46, 0.84):
        H -= 0.4 * np.exp(-((V - vg) / 0.012) ** 2)
    H = blur(H, 1)
    col, ormt = brass_finish(H, 311)
    return col, ormt, normal_from_height(H, 3.0)


def tex_kain():
    """Kain alas (hanya untuk render): batik kawung (empat elips di tiap titik kisi) dan pinggiran tumpal."""
    h = w = 1024
    U, V = uvgrid(h, w)
    n = 14
    x = (U * n) % 1 - 0.5; y = (V * n) % 1 - 0.5
    motif = np.zeros((h, w)); line = np.zeros((h, w))
    for cx, cy, a, b in ((0.5, 0, .21, .12), (-0.5, 0, .21, .12), (0, 0.5, .12, .21), (0, -0.5, .12, .21)):
        d = ((x - cx) / a) ** 2 + ((y - cy) / b) ** 2
        motif = np.maximum(motif, d < 1.0)
        line = np.maximum(line, (d > 0.55) & (d < 0.72))
    dot = np.hypot(x, y) < 0.05
    edge = np.minimum(np.minimum(U, 1 - U), np.minimum(V, 1 - V))
    saw = np.abs(((U + V) * 60) % 1 - 0.5) * 2
    tumpal = (edge < 0.04) * (edge / 0.04 < saw)
    inner = edge >= 0.045
    wear = fbm2(U, V, 16, 16, 401, 4)
    t = np.where(inner, np.clip(motif * 1.0 - line * 0.7 + dot, 0, 1), tumpal * 1.0)
    col = mix((0.045, 0.035, 0.032), (0.46, 0.28, 0.13), t * (0.7 + 0.5 * wear))
    return col * (0.85 + 0.3 * fbm2(U, V, 200, 200, 403, 2))[..., None]


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    out = {}
    out['pamor'], out['pamor_orm'], out['pamor_n'] = tex_pamor()
    col, rough = tex_burl(41)
    out['burl'], out['burl_orm'] = col, orm(rough, np.zeros_like(rough))
    out['hulu'], out['hulu_orm'], out['hulu_n'] = tex_hulu()
    col, rough = tex_jackwood()
    out['nangka'], out['nangka_orm'] = col, orm(rough, np.zeros_like(rough))
    out['pendok'], out['pendok_orm'], out['pendok_n'] = tex_pendok()
    out['mendak'], out['mendak_orm'], out['mendak_n'] = tex_mendak()
    out['kain'] = tex_kain()
    out['meja'], _ = tex_jackwood((0.22, 0.12, 0.06), (0.11, 0.055, 0.025), 91)
    for name, arr in out.items():
        write_png(os.path.join(TEX, name + '.png'), arr)
    return {k: os.path.join(TEX, k + '.png') for k in out}


# ============================================================ material PBR (pola node yang dikenali ekspor glTF)

def pbr(name, base=None, orm_img=None, normal=None, color=None, metallic=0.0, rough=0.5,
        nstrength=1.0, coat=0.0, coat_rough=0.08, uv_scale=None):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = rough
    if color is not None:
        bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    mapping = None
    if uv_scale:
        tc = nt.nodes.new('ShaderNodeTexCoord'); mapping = nt.nodes.new('ShaderNodeMapping')
        mapping.inputs['Scale'].default_value = (*uv_scale, 1.0)
        nt.links.new(tc.outputs['UV'], mapping.inputs['Vector'])

    def img(path, noncolor=False):
        tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(path, check_existing=True)
        if noncolor:
            tx.image.colorspace_settings.name = 'Non-Color'
        if mapping:
            nt.links.new(mapping.outputs['Vector'], tx.inputs['Vector'])
        return tx

    if base:
        nt.links.new(img(base).outputs['Color'], bsdf.inputs['Base Color'])
    if orm_img:
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(img(orm_img, True).outputs['Color'], sep.inputs['Color'])
        nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
        nt.links.new(sep.outputs['Blue'], bsdf.inputs['Metallic'])
    if normal:
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = nstrength
        nt.links.new(img(normal, True).outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if coat:
        bsdf.inputs['Coat Weight'].default_value = coat
        bsdf.inputs['Coat Roughness'].default_value = coat_rough
    return m


# ============================================================ ekspor GLB

def export_glb(root, path=GLB):
    for ob in bpy.data.objects:
        ob.select_set(False)
    stack = [root]
    while stack:
        o = stack.pop(); o.select_set(True); stack.extend(o.children)
    bpy.context.view_layer.objects.active = root
    kw = dict(filepath=path, export_format='GLB', use_selection=True, export_apply=True, export_yup=True,
              export_texcoords=True, export_normals=True, export_tangents=True, export_materials='EXPORT',
              export_image_format='AUTO', export_animations=True, export_animation_mode='ACTIONS',
              export_cameras=False, export_lights=False, export_extras=False)
    while True:
        try:
            bpy.ops.export_scene.gltf(**kw)
            return
        except TypeError as e:                        # parameter berganti nama antar versi Blender: buang lalu ulangi
            bad = next((k for k in kw if f'"{k}"' in str(e) or f"'{k}'" in str(e)), None)
            if bad is None or bad in ('filepath', 'export_format'):
                raise
            print('  (lewati parameter glTF tak dikenal:', bad, ')')
            kw.pop(bad)


# ============================================================ render pratinjau

def look(ob, target):
    ob.rotation_euler = (Vector(target) - ob.location).to_track_quat('-Z', 'Y').to_euler()


def studio(center=(0, 0, 0), scale=1.0):
    """Mesin render, warna AgX, langit gradasi dan tiga lampu area. Mengembalikan objek lampu."""
    sc = bpy.context.scene
    for eng in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT', 'CYCLES'):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    if sc.render.engine == 'CYCLES':
        sc.cycles.samples = 96
    else:
        for attr, val in (('taa_render_samples', 64), ('use_raytracing', True), ('use_shadows', True)):
            try:
                setattr(sc.eevee, attr, val)
            except AttributeError:
                pass
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
    sc.render.film_transparent = False

    world = bpy.data.worlds.new('Studio'); sc.world = world
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    wn = world.node_tree
    bg = next(n for n in wn.nodes if n.type == 'BACKGROUND')
    tc = wn.nodes.new('ShaderNodeTexCoord'); sep = wn.nodes.new('ShaderNodeSeparateXYZ')
    ramp = wn.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.35; ramp.color_ramp.elements[0].color = (0.035, 0.030, 0.028, 1)
    ramp.color_ramp.elements[1].position = 0.95; ramp.color_ramp.elements[1].color = (0.80, 0.74, 0.66, 1)
    mr = wn.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = -1.0; mr.inputs['From Max'].default_value = 1.0
    wn.links.new(tc.outputs['Generated'], sep.inputs['Vector'])
    wn.links.new(sep.outputs['Z'], mr.inputs['Value'])
    wn.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    wn.links.new(ramp.outputs['Color'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 0.8

    def area(name, loc, target, power, size, color=(1.0, 0.96, 0.9)):
        ld = bpy.data.lights.new(name, 'AREA'); ld.energy = power; ld.size = size; ld.color = color
        ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob)
        ob.location = loc; look(ob, target)
        return ob

    c = Vector(center); k = scale
    return [area('Key', c + Vector((-0.45, -0.55, 0.85)) * k, c, 70 * k * k, 0.7 * k),
            area('Rim', c + Vector((0.55, 0.65, 0.45)) * k, c, 55 * k * k, 0.5 * k, (0.9, 0.95, 1.0)),
            area('Fill', c + Vector((0.75, -0.45, 0.25)) * k, c, 18 * k * k, 0.8 * k)]


def render_previews(tex, bilah, sarung, dudukan):
    sc = bpy.context.scene
    lights = studio()

    # Meja kayu + kain alas (hanya untuk render hero).
    bpy.ops.mesh.primitive_plane_add(size=1.6, location=(0, 0, 0))
    meja = bpy.context.active_object; meja.name = 'Meja'
    meja.data.materials.append(pbr('meja', base=tex['meja'], rough=0.55, uv_scale=(5.0, 2.5)))
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 0.0, 0.0006))
    kain = bpy.context.active_object; kain.name = 'Kain'
    kain.scale = (0.62, 0.46, 1.0); kain.rotation_euler = (0, 0, math.radians(-4))
    kain.data.materials.append(pbr('kain', base=tex['kain'], rough=0.9))
    props = [meja, kain]

    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(path, res, persp=None, ortho=None):
        sc.render.resolution_x, sc.render.resolution_y = res
        if ortho:
            (cx, cz, size) = ortho
            cam.data.type = 'ORTHO'; cam.data.ortho_scale = size
            cam.location = (cx, -2.0, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
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

    # Studio berdiri (tanpa meja)
    show_props(False)
    shot(os.path.join(DOCS, 'KerisSumatra_Blender_tersarung.png'), (900, 1200), ortho=(0.022, -0.136, 0.60))
    bilah.location = (0.19, 0.0, 0.0)
    shot(os.path.join(DOCS, 'KerisSumatra_Blender_dihunus.png'), (1200, 1150), ortho=(0.075, -0.136, 0.62))
    bilah.location = (0.0, 0.0, 0.0)
    shot(os.path.join(DOCS, 'KerisSumatra_Blender_hulu.png'), (1200, 1000),
         persp=((0.018, 0.0, 0.035), (-0.13, -0.24, 0.12), 60))

    # Hero: keris tergeletak di atas kain; bilah terhunus bersilang di atas sarung.
    show_props(True)
    dudukan.hide_render = True
    sarung.rotation_euler = (math.radians(90), 0, math.radians(107))
    sarung.location = (0.13, 0.05, 0.0141)
    alpha = math.radians(3.6)
    bilah.rotation_euler = (-math.pi / 2 - alpha, 0, math.radians(-115))
    bilah.location = (0.095, -0.090, 0.0152)
    shot(os.path.join(DOCS, 'KerisSumatra_Blender_hero.png'), (1600, 900),
         persp=((-0.005, -0.03, 0.0), (0.12, -0.70, 0.50), 50))

    for o in (bilah, sarung):
        o.location = (0, 0, 0); o.rotation_euler = (0, 0, 0)
    show_props(False)
    dudukan.hide_render = False
    return lights + props + [cam]


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.name = 'Keris Sumatra'
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = {
        'pamor': pbr('pamor_wos_wutah', tex['pamor'], tex['pamor_orm'], tex['pamor_n'], nstrength=0.6),
        'hulu': pbr('burl_hulu_berukir', tex['hulu'], tex['hulu_orm'], tex['hulu_n'], coat=0.4, coat_rough=0.12),
        'burl': pbr('burl_sampir', tex['burl'], tex['burl_orm'], coat=0.4, coat_rough=0.12),
        'nangka': pbr('kayu_nangka', tex['nangka'], tex['nangka_orm'], coat=0.3, coat_rough=0.2),
        'pendok': pbr('kuningan_pendok', tex['pendok'], tex['pendok_orm'], tex['pendok_n']),
        'mendak': pbr('kuningan_mendak', tex['mendak'], tex['mendak_orm'], tex['mendak_n']),
        'celah': pbr('celah_gelap', color=(0.02, 0.012, 0.008), rough=0.9),
        'dudukan': pbr('kayu_dudukan', tex['meja'], rough=0.45, coat=0.3, coat_rough=0.2),
    }

    def empty(name, parent=None):
        e = bpy.data.objects.new(name, None); e.empty_display_type = 'PLAIN_AXES'; e.empty_display_size = 0.05
        sc.collection.objects.link(e); e.parent = parent
        return e

    root = empty('Keris_Sumatra')
    bilah = empty('Bilah', root)
    sarung = empty('Sarung', root)

    objs = {}
    for name, mb, mat, parent, recalc in [
        ('Wilah', part_wilah(), M['pamor'], bilah, True),
        ('Ganja', part_ganja(), M['pamor'], bilah, True),
        ('Mendak', part_mendak(), M['mendak'], bilah, True),
        ('Hulu', part_hulu(), M['hulu'], bilah, True),
        ('Warangka_Sampir', part_sampir(), M['burl'], sarung, True),
        ('Warangka_Celah', part_celah(), M['celah'], sarung, False),
        ('Gandar', part_gandar(), M['nangka'], sarung, True),
        ('Pendok_Atas', part_pendok_atas(), M['pendok'], sarung, True),
        ('Pendok_Bawah', part_pendok_bawah(), M['pendok'], sarung, True),
        ('Dudukan', part_dudukan(), M['dudukan'], root, True),
    ]:
        objs[name] = build(name, mb, mat, parent, recalc=recalc)

    # Laporan ukuran (untuk dicocokkan dengan cetak biru)
    total = 0
    print('\n== Ukuran bagian (m) ==')
    for name, ob in objs.items():
        me = ob.data
        co = np.array([ob.matrix_world @ v.co for v in me.vertices])
        tris = sum(len(p.vertices) - 2 for p in me.polygons); total += tris
        lo, hi = co.min(0), co.max(0)
        print(f'{name:16s} x[{lo[0]:+.4f},{hi[0]:+.4f}] y[{lo[1]:+.4f},{hi[1]:+.4f}] z[{lo[2]:+.4f},{hi[2]:+.4f}]'
              f'  ukuran {(hi - lo)[0] * 100:.1f} x {(hi - lo)[1] * 100:.1f} x {(hi - lo)[2] * 100:.1f} cm  tris {tris}')
    blade = np.array([v.co for v in objs['Wilah'].data.vertices])
    print(f'Panjang bilah (pangkal -> pucuk): {-blade[:, 2].min() * 1000:.1f} mm')
    print(f'Total segitiga: {total}')

    # Animasi mencabut keris: empty "Bilah" naik sepanjang poros pesi.
    sc.frame_start, sc.frame_end = 1, 48
    for f, z in ((1, 0.0), (10, 0.012), (48, DRAW_Z)):
        bilah.location = (0, 0, z); bilah.keyframe_insert('location', frame=f)
    bilah.animation_data.action.name = 'Cabut_Keris'
    sc.frame_set(1)

    os.makedirs(ART, exist_ok=True)
    export_glb(root)
    print('GLB:', GLB, f'({os.path.getsize(GLB) / 1e6:.2f} MB)')
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)
    if RENDER:
        act = bilah.animation_data.action
        bilah.animation_data.action = None          # agar pose render tidak ditimpa kurva animasi
        render_previews(tex, bilah, sarung, objs['Dudukan'])
        bilah.animation_data.action = act
        sc.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)  # simpan lagi beserta kamera, lampu, meja
        print('Render pratinjau: Docs/KerisSumatra_Blender_*.png')


if __name__ == '__main__':      # keris_bali.py mengimpor file ini sebagai modul utilitas
    main()
