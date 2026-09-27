# Keris Jawa (hulu "Jawa Demam", warangka "Sampir Perahu Kandang") dari cetak biru -> Blender -> FBX untuk Unity.
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_jawa.py
# Tambahkan "-- --no-render" di akhir untuk melewati render pratinjau.
#
# Hasil:
#   Assets/NusantaraAR/Art/KerisJawa/keris_jawa.fbx        (dibaca KerisJawaBuilder di Unity)
#   Assets/NusantaraAR/Art/KerisJawa/Textures/*.png         (albedo, metallic/smoothness, normal map)
#   Tools/blender/keris_jawa.blend                          (file kerja; di luar Assets agar tidak diimpor Unity)
#   Docs/KerisJawa_Blender_*.png                            (render pratinjau)
#
# Ruang Blender: Z ke atas, muka depan -Y, 1 unit = 1 m. Gandik di -X, tanduk warangka yang tinggi di +X.
# Nama objek = nama bagian di Unity (awalan sebelum "_" menentukan ArtifactPart; mis. Hulu_Selut -> Hulu).
# Nama material (pamor, kemuning, sonokeling, kuningan, kuningan_ukir, dudukan) dipetakan ulang ke material URP.

import bpy, bmesh, math, os, sys, struct, zlib
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'KerisJawa')
TEX = os.path.join(ART, 'Textures')
FBX = os.path.join(ART, 'keris_jawa.fbx')
BLEND = os.path.join(HERE, 'keris_jawa.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
RENDER = '--no-render' not in ARGS

BH = 0.440          # pangkal bilah (sisi bawah ganja) saat tersarung, ruang Model
BLADE_L = 0.345     # panjang bilah tanpa pesi
PEKSI_L = 0.075
DRAWN_DX = 0.22     # geser bilah saat dihunus (hanya untuk render pratinjau; Unity punya tahapnya sendiri)
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
        self.v, self.f, self.uv = [], [], []

    def point(self, p):
        self.v.append([float(c) for c in p])
        return len(self.v) - 1

    def face(self, idx, uvs):
        self.f.append(list(idx)); self.uv.append([(float(a), float(b)) for a, b in uvs])

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


def build(name, mb, mat, origin=(0, 0, 0), sharp_deg=55.0, subsurf=0):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in mb.v]
    uvl = bm.loops.layers.uv.new('UVMap')
    for idx, uvs in zip(mb.f, mb.uv):
        if len(set(idx)) < len(idx):
            continue
        try:
            f = bm.faces.new([vs[i] for i in idx])
        except ValueError:
            continue
        for lp, t in zip(f.loops, uvs):
            lp[uvl].uv = t
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
    o = Vector(origin)
    me.transform(Matrix.Translation(-o))
    ob = bpy.data.objects.new(name, me)
    ob.location = o
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(mat)
    if subsurf:
        m = ob.modifiers.new('Subdivision', 'SUBSURF')
        m.levels = m.render_levels = subsurf
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
    mb.grid(P, start=(c[0], c[1], c[2] - r), end=(c[0], c[1], c[2] + r))


# ============================================================ bagian keris

def blade_half_width(u, side):
    """side -1 = sisi gandik (-X, depan), +1 = sisi belakang (+X)."""
    body = 0.0155 * (1 - 0.35 * u)
    base = 0.0020 if side < 0 else 0.0050            # sor-soran melebar, tidak simetris
    w = body + base * (1 - sstep(0.0, 0.12, u))
    tip = 1 - np.clip((u - 0.86) / 0.14, 0, 1) ** 1.5  # meruncing ke pucuk
    return w * tip, tip


def part_wilah():
    mb = MB()
    us = np.concatenate([np.linspace(0, 0.12, 14, endpoint=False),
                         np.linspace(0.12, 0.86, 32, endpoint=False),
                         np.linspace(0.86, 0.988, 10)])
    C = 48
    tau = TAU * np.arange(C) / C
    t = tau + 0.175 * np.sin(2 * tau)                 # kolom lebih rapat di tengah (ada-ada)
    c, s = np.cos(t), np.sin(t)
    P = np.zeros((len(us), C, 3))
    for i, u in enumerate(us):
        wF, tip = blade_half_width(u, -1)
        wB, _ = blade_half_width(u, 1)
        b = 0.0042 * (1 - 0.55 * u) * np.sqrt(tip)       # setengah tebal
        ridge = 0.25 * sstep(0.02, 0.10, u) * (1 - sstep(0.80, 0.95, u))
        e = np.where(c < 0, 0.55 * (1 - sstep(0.06, 0.095, u)), 0.0)  # gandik: tepi depan tumpul di pangkal
        f = e + (1 - e) * (1 - np.abs(c) ** 1.8) ** 0.75                 # penampang lensa, tepi tajam
        P[i, :, 0] = c * np.where(c < 0, wF, wB)
        P[i, :, 1] = s * b * (f + ridge * np.exp(-(c / 0.12) ** 2))
        P[i, :, 2] = BH - BLADE_L * u
    uv = lambda i, j: (0.5 + P[i, j % C, 0] / 0.05, us[i])
    mb.grid(P, uv=uv, start=(0, 0, BH), start_uv=(0.5, 0.0), end=(0, 0, BH - BLADE_L), end_uv=(0.5, 1.0))

    # Pesi (peksi): tangkai persegi membulat yang masuk ke hulu.
    zs = np.linspace(BH - 0.002, BH + PEKSI_L - 0.003, 8)
    C2 = 16; a = TAU * np.arange(C2) / C2
    Q = np.zeros((len(zs), C2, 3))
    for i, z in enumerate(zs):
        r = 0.0046 - 0.0024 * (z - BH) / PEKSI_L
        Q[i, :, 0] = r * spow(np.cos(a), 0.7)
        Q[i, :, 1] = 0.85 * r * spow(np.sin(a), 0.7)
        Q[i, :, 2] = z
    mb.grid(Q, uv=lambda i, j: (0.5 + Q[i, j % C2, 0] / 0.05, (zs[i] - BH) / BLADE_L),
            start=(0, 0, BH - 0.002), end=(0, 0, BH + PEKSI_L))
    return mb


def part_ganja():
    mb = MB()
    # Profil samping (x, z di atas pangkal bilah): sirah cecak di depan (-X), buntut urang melengkung di belakang (+X).
    prof = [(-0.0190, 0.0000), (0.0060, 0.0000), (0.0170, 0.0000), (0.0205, 0.0010), (0.0228, 0.0030),
            (0.0238, 0.0056), (0.0230, 0.0072), (0.0212, 0.0071), (0.0180, 0.0080), (0.0100, 0.0092),
            (0.0000, 0.0095), (-0.0100, 0.0095), (-0.0152, 0.0093), (-0.0182, 0.0087), (-0.0200, 0.0074),
            (-0.0205, 0.0057), (-0.0198, 0.0042), (-0.0186, 0.0030), (-0.0192, 0.0012)]
    Q = closed_spline(prof, 64)
    area = 0.5 * np.sum(Q[:, 0] * np.roll(Q[:, 1], -1) - np.roll(Q[:, 0], -1) * Q[:, 1])
    tg = np.roll(Q, -1, 0) - np.roll(Q, 1, 0)
    nrm = np.stack([tg[:, 1], -tg[:, 0]], 1); nrm /= np.linalg.norm(nrm, axis=1)[:, None]
    if area < 0:
        nrm = -nrm
    T, r = 0.0060, 0.0012
    phis = np.linspace(-np.pi / 2, np.pi / 2, 7)
    P = np.zeros((len(phis), len(Q), 3))
    for i, ph in enumerate(phis):
        inset = r * (1 - np.cos(ph))
        q = Q - nrm * inset
        P[i, :, 0] = q[:, 0]; P[i, :, 1] = T * np.sin(ph); P[i, :, 2] = BH + q[:, 1]
    uv = lambda i, j: (0.5 + P[i, j % len(Q), 0] / 0.05, (P[i, j % len(Q), 2] - BH) / BLADE_L + 0.5 * i / 6)
    mb.grid(P, uv=uv, cap_start=True, cap_end=True)
    return mb


def part_mendak():
    mb = MB()
    prof = [(0.0048, 0.0085), (0.0080, 0.0085), (0.0092, 0.0094), (0.0099, 0.0110), (0.0105, 0.0132),
            (0.0109, 0.0158), (0.0117, 0.0180), (0.0121, 0.0195), (0.0114, 0.0205), (0.0048, 0.0205)]
    prof = [(r, BH + z) for r, z in prof]
    P = lathe(prof, 48)
    mb.grid(P, uv=lambda i, j: (j / 48, i / len(prof)), wrap_rows=True)
    for k in range(18):                              # butiran kuningan melingkar
        a = TAU * (k + 0.5) / 18
        sphere(mb, (0.0110 * math.cos(a), 0.0110 * math.sin(a), BH + 0.0145), 0.0013)
    return mb


def part_selut():
    mb = MB()
    prof = [(0.0112, 0.0205), (0.0122, 0.0214), (0.0130, 0.0234), (0.0136, 0.0258), (0.0134, 0.0282),
            (0.0128, 0.0302), (0.0121, 0.0310)]
    prof = [(r, BH + z) for r, z in prof]
    P = lathe(prof, 48)
    mb.grid(P, uv=lambda i, j: (6.0 * j / 48, i / (len(prof) - 1)),
            start=(0, 0, BH + 0.0205), start_uv=(0.5, 0.0), end=(0, 0, BH + 0.031), end_uv=(0.5, 1.0))
    return mb


HULU_CTRL = np.array([(0.000, 0.0295), (0.000, 0.048), (-0.0015, 0.068), (-0.004, 0.087),
                      (-0.008, 0.102), (-0.013, 0.1115)])
HULU_TOP = 0.1305   # puncak hulu di atas pangkal bilah (selut + hulu = 11 cm)


def hulu_rings(k):
    """Cincin penampang hulu Jawa Demam; k meregangkan tulang agar puncaknya tepat HULU_TOP."""
    ctrl = HULU_CTRL.copy(); ctrl[:, 1] = 0.0295 + (ctrl[:, 1] - 0.0295) * k
    n = 26
    pts, T, N, _ = sweep2d(ctrl, n)
    s = np.linspace(0, 1, n)
    hw = np.interp(s, [0, .12, .35, .55, .72, .86, 1], [.0122, .0135, .0150, .0142, .0150, .0140, .0122])
    ht = np.interp(s, [0, .3, .6, .85, 1], [.0122, .0128, .0126, .0124, .0114])
    kF = 1 + 0.16 * np.exp(-((s - 0.38) / 0.12) ** 2)   # perut membusung di depan (-X)
    kB = 1 + 0.30 * np.exp(-((s - 0.74) / 0.08) ** 2)   # bungkul (tonjolan punggung) di belakang
    C = 24; a = TAU * np.arange(C) / C; ca, sa = np.cos(a), np.sin(a)
    pc = spow(ca, 0.9)

    def ring(p, nv, wf, wb, h, sc=1.0):
        off = np.where(ca > 0, wb, wf) * pc * sc
        return np.stack([p[0] + nv[0] * off, h * sa * sc, BH + p[1] + nv[1] * off], 1)

    rows, cents = [], []
    for i in range(n):
        rows.append(ring(pts[i], N[i], hw[i] * kF[i], hw[i] * kB[i], ht[i]))
        cents.append((pts[i][0], 0.0, BH + pts[i][1]))
    d = 0.011
    for kk in range(1, 5):                           # kepala membulat
        ph = kk / 5 * np.pi / 2
        c = pts[-1] + T[-1] * d * np.sin(ph)
        rows.append(ring(c, N[-1], hw[-1] * kF[-1], hw[-1] * kB[-1], ht[-1], np.cos(ph)))
        cents.append((c[0], 0.0, BH + c[1]))
    tip = pts[-1] + T[-1] * d
    return np.array(rows), np.array(cents), (tip[0], 0.0, BH + tip[1]), T[-1], n


def hulu_geometry():
    k = 1.0
    for _ in range(3):
        P, cents, tip, Tend, n = hulu_rings(k)
        top = P[..., 2].max() - BH
        k *= (HULU_TOP - 0.0295) / (top - 0.0295)
    return hulu_rings(k)


def part_hulu():
    P, cents, tip, Tend, n = hulu_geometry()
    R, C = P.shape[:2]
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, i / R), end=tip, end_uv=(0.5, 1.0),
            start=(cents[0][0], 0.0, cents[0][2]), start_uv=(0.5, 0.0))

    # Tutup kuningan: cangkang tipis menutup lengkung puncak (s >= 0.86), mengembang 0,8 mm.
    i0 = int(round(0.86 * (n - 1)))
    rows = []
    for i in range(i0, R):
        off = P[i] - cents[i]; ln = np.linalg.norm(off, axis=1)[:, None]
        if i == i0:
            rows.append(cents[i] + off * (1 - 0.0006 / ln))     # bibir masuk ke kayu
        rows.append(cents[i] + off * (1 + 0.0008 / ln))
    Q = np.array(rows)
    tt = np.array(tip) + np.array([Tend[0], 0.0, Tend[1]]) * 0.0008
    mt = MB()
    mt.grid(Q, uv=lambda i, j: (j / C, i / len(Q)), end=tt, end_uv=(0.5, 1.0))
    return mb, mt


WARANGKA_X = 0.93   # lebar total 18 cm
WARANGKA_CTRL = [(-0.060, 0.458), (-0.066, 0.440), (-0.050, 0.4255), (-0.020, 0.4225), (0.020, 0.4225),
                 (0.055, 0.4275), (0.085, 0.440), (0.105, 0.462), (0.115, 0.495)]


def part_warangka():
    n = 46
    ctrl = [(0.02 + (x - 0.02) * WARANGKA_X, z) for x, z in WARANGKA_CTRL]
    pts, T, N, L = sweep2d(ctrl, n)
    s = np.linspace(0, 1, n)
    h = taper(s, 0.28, 0.45, 0.0030, 0.0175, 0.0025)    # setengah tinggi (tegak lurus tulang)
    t = taper(s, 0.28, 0.45, 0.0030, 0.0110, 0.0028)    # setengah tebal (Y): 2,2 cm di tengah
    C = 28; a = TAU * np.arange(C) / C; ca, sa = np.cos(a), np.sin(a)
    pc = np.where(ca < 0, spow(ca, 0.40), spow(ca, 0.65))  # sisi atas (mulut) lebih datar
    qs = spow(sa, 0.6)
    P = np.zeros((n, C, 3))
    for i in range(n):
        P[i, :, 0] = pts[i, 0] + N[i, 0] * h[i] * pc
        P[i, :, 1] = t[i] * qs
        P[i, :, 2] = pts[i, 1] + N[i, 1] * h[i] * pc
    mb = MB()
    st = pts[0] - T[0] * 0.0022; en = pts[-1] + T[-1] * 0.002
    mb.grid(P, uv=lambda i, j: (i / (n - 1), j / C),
            start=(st[0], 0, st[1]), start_uv=(0.0, 0.5), end=(en[0], 0, en[1]), end_uv=(1.0, 0.5))
    return mb


def shield(aF, aB, b, C):
    """Penampang gandar: sisi depan (-X) membulat, sisi belakang (+X) meruncing seperti perisai."""
    a = TAU * np.arange(C) / C; c, s = np.cos(a), np.sin(a)
    x = np.where(c < 0, aF * c, aB * c)
    y = np.where(c < 0, b * s, b * s * np.sqrt(np.clip(1 - c, 0, None)))
    return x, y


def part_gandar():
    top, bot = 0.415, 0.046
    zs = list(np.linspace(top, bot, 30))
    scales = [1.0] * len(zs)
    for kk in range(1, 5):                            # ujung bawah membulat
        ph = kk / 5 * np.pi / 2
        zs.append(bot - 0.010 * np.sin(ph)); scales.append(np.cos(ph))
    C = 32
    P = np.zeros((len(zs), C, 3))
    for i, (z, sc) in enumerate(zip(zs, scales)):
        q = np.clip((top - z) / (top - bot), 0, 1) ** 0.9
        x, y = shield(0.017 - 0.008 * q, 0.019 - 0.009 * q, 0.0085 - 0.003 * q, C)
        P[i, :, 0] = x * sc; P[i, :, 1] = y * sc; P[i, :, 2] = z
    mb = MB()
    mb.grid(P, uv=lambda i, j: ((top - zs[i]) / 0.38, j / C), start=(0, 0, top), end=(0, 0, bot - 0.010))
    return mb


def part_dudukan():
    mb = MB()
    C = 48; a = TAU * np.arange(C) / C
    rows = [(0.97, 0.0), (1.0, 0.003), (1.0, 0.024), (0.975, 0.028)]
    P = np.zeros((len(rows), C, 3))
    for i, (sc, z) in enumerate(rows):
        P[i, :, 0] = 0.08 * sc * spow(np.cos(a), 0.5)
        P[i, :, 1] = 0.05 * sc * spow(np.sin(a), 0.5)
        P[i, :, 2] = z
    planar = lambda p: (0.5 + p[0] / 0.16, 0.5 + p[1] / 0.16)
    mb.grid(P, uv=lambda i, j: planar(P[i, j % C]), start=(0, 0, 0), start_uv=(0.5, 0.5),
            end=(0, 0, 0.028), end_uv=(0.5, 0.5))
    # Soket penjepit ujung gandar
    rows = [(1.0, 0.026), (1.0, 0.046), (0.9, 0.052)]
    C2 = 32
    Q = np.zeros((len(rows), C2, 3))
    for i, (sc, z) in enumerate(rows):
        x, y = shield(0.016, 0.018, 0.012, C2)
        Q[i, :, 0] = x * sc; Q[i, :, 1] = y * sc; Q[i, :, 2] = z
    mb.grid(Q, end=(0, 0, 0.053))
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


def vnoise(h, w, cy, cx, seed):
    """Value noise periodik (bisa diulang tanpa sambungan di kedua arah)."""
    g = np.random.default_rng(seed).random((cy, cx))
    yy = np.arange(h) * cy / h; xx = np.arange(w) * cx / w
    y0 = np.floor(yy).astype(int); x0 = np.floor(xx).astype(int)
    fy = smooth01(yy - y0)[:, None]; fx = smooth01(xx - x0)[None, :]
    y1 = (y0 + 1) % cy; x1 = (x0 + 1) % cx
    a = g[np.ix_(y0, x0)]; b = g[np.ix_(y0, x1)]; c = g[np.ix_(y1, x0)]; d = g[np.ix_(y1, x1)]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(h, w, cy, cx, seed, octaves=4):
    s, amp, tot = 0.0, 1.0, 0.0
    for o in range(octaves):
        s = s + amp * vnoise(h, w, cy * 2 ** o, cx * 2 ** o, seed + 17 * o)
        tot += amp; amp *= 0.5
    return s / tot


def uvgrid(h, w):
    U = (np.arange(w) + 0.5) / w; V = (np.arange(h) + 0.5) / h
    return np.meshgrid(U, V)


def mix(c0, c1, t):
    return np.asarray(c0)[None, None] + (np.asarray(c1) - np.asarray(c0))[None, None] * t[..., None]


def normal_from_height(H, k):
    gx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) * 0.5
    gy = (np.roll(H, -1, 0) - np.roll(H, 1, 0)) * 0.5
    n = np.stack([-k * gx, -k * gy, np.ones_like(H)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def tex_pamor():
    h, w = 1024, 256
    U, V = uvgrid(h, w)
    field = fbm(h, w, 16, 4, 11) * 9.0 + V * 6.0 + 0.4 * np.sin(TAU * U)
    fr = field % 1.0; d = np.minimum(fr, 1 - fr)
    line = 1 - sstep(0.04, 0.13, d)                               # garis pamor bergelombang
    blob = sstep(0.62, 0.70, fbm(h, w, 32, 8, 23, 3))             # butir "wos wutah"
    speck = (np.random.default_rng(5).random((h, w)) > 0.985) * 0.3
    m = np.clip(line * 0.9 + blob * 0.6 + speck, 0, 1)
    grain = (fbm(h, w, 128, 32, 31, 2) - 0.5) * 0.08
    col = mix((0.20, 0.20, 0.21), (0.82, 0.82, 0.84), m) + grain[..., None]
    ms = np.zeros((h, w, 4)); ms[..., 0] = 0.85 + 0.10 * m; ms[..., 3] = 0.45 + 0.27 * m
    return col, ms


def tex_kemuning():
    h = w = 512
    U, V = uvgrid(h, w)
    n = fbm(h, w, 4, 8, 41)
    ring = 0.5 + 0.5 * np.sin(TAU * (V * 14 + n * 3.0))            # serat searah u (panjang benda)
    streak = fbm(h, w, 64, 8, 43, 3)
    t = np.clip(ring ** 2.5 * 0.7 + (streak - 0.5) * 0.5, 0, 1)
    return mix((0.80, 0.62, 0.34), (0.55, 0.36, 0.16), t)


def tex_sonokeling():
    h = w = 512
    U, V = uvgrid(h, w)
    n = fbm(h, w, 8, 4, 51)
    ring = 0.5 + 0.5 * np.sin(TAU * (U * 9 + n * 2.5))             # serat searah v (panjang hulu)
    streak = fbm(h, w, 8, 64, 53, 3)
    t = np.clip(ring ** 2.0 * 0.75 + (streak - 0.5) * 0.6, 0, 1)
    return mix((0.36, 0.19, 0.11), (0.13, 0.06, 0.04), t)


def tex_hulu_normal():
    """Ukiran utu (sulur spiral berpasangan) di pita depan hulu + alur cincin di pangkal."""
    h = w = 512
    U, V = uvgrid(h, w)
    SU, SV = 0.085, 0.11          # perkiraan ukuran fisik (m) satu satuan UV
    H = np.zeros((h, w))
    band = sstep(0.27, 0.30, U) * (1 - sstep(0.70, 0.73, U)) * sstep(0.18, 0.21, V) * (1 - sstep(0.69, 0.72, V))
    for vc in (0.30, 0.45, 0.60):
        for uc, mir in ((0.41, -1.0), (0.59, 1.0)):
            du = (U - uc) * SU * mir; dv = (V - vc) * SV
            r = np.hypot(du, dv); th = np.arctan2(dv, du)
            spiral = 0.5 + 0.5 * np.cos(TAU * r / 0.0022 - th)
            H += 0.6 * spiral * (1 - sstep(0.0050, 0.0062, r))
    H += 0.7 * np.exp(-((U - 0.5) * SU / 0.0012) ** 2) * band                     # batang tengah
    for u0 in (0.30, 0.70):
        H += 0.5 * np.exp(-((U - u0) * SU / 0.0007) ** 2) * sstep(0.18, 0.2, V) * (1 - sstep(0.7, 0.72, V))
    for v0 in (0.20, 0.70):
        H += 0.5 * np.exp(-((V - v0) * SV / 0.0007) ** 2) * sstep(0.28, 0.3, U) * (1 - sstep(0.7, 0.72, U))
    for v0 in (0.05, 0.09):                                                        # alur cincin
        H -= 0.8 * np.exp(-((V - v0) * SV / 0.0007) ** 2)
    return normal_from_height(H, 4.0)


def tex_kuningan_normal():
    """Ornamen selut: dua baris butiran + bunga empat kelopak (bisa diulang di arah u)."""
    h = w = 256
    U, V = uvgrid(h, w)
    H = np.zeros((h, w))
    for v0 in (0.14, 0.86):
        du = ((U * 4) % 1 - 0.5) / 4 * 1.1; dv = V - v0
        r = np.hypot(du, dv)
        H += np.sqrt(np.clip(1 - (r / 0.09) ** 2, 0, 1))
    for u0 in (0.0, 0.5, 1.0):
        du = (U - u0) * 1.1; dv = V - 0.5
        r = np.hypot(du, dv); th = np.arctan2(dv, du)
        petal = 0.22 * (0.55 + 0.45 * np.abs(np.cos(2 * th)))
        H += 0.8 * (1 - sstep(petal - 0.03, petal, r)) + 0.5 * (1 - sstep(0.04, 0.06, r))
    H -= 0.6 * np.exp(-((V - 0.27) / 0.012) ** 2) + 0.6 * np.exp(-((V - 0.73) / 0.012) ** 2)
    return normal_from_height(H, 5.0)


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    col, ms = tex_pamor()
    out = {
        'pamor': col, 'pamor_ms': ms, 'kemuning': tex_kemuning(), 'sonokeling': tex_sonokeling(),
        'hulu_normal': tex_hulu_normal(), 'kuningan_normal': tex_kuningan_normal(),
    }
    for name, arr in out.items():
        write_png(os.path.join(TEX, name + '.png'), arr)
    return {k: os.path.join(TEX, k + '.png') for k in out}


# ============================================================ material Blender (pratinjau; Unity membuat material URP sendiri)

def material(name, color=None, image=None, normal=None, metallic=0.0, rough=0.5, nstrength=1.0):
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
    if image:
        tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(image, check_existing=True)
        nt.links.new(tx.outputs['Color'], bsdf.inputs['Base Color'])
    if normal:
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = bpy.data.images.load(normal, check_existing=True)
        tn.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = nstrength
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    return m


# ============================================================ render pratinjau

def render_previews(objs):
    sc = bpy.context.scene
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        sc.render.engine = 'CYCLES'
    if sc.render.engine == 'CYCLES':
        sc.cycles.samples = 32
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 900, 1000
    sc.render.film_transparent = False
    world = bpy.data.worlds.new('Studio'); sc.world = world
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs[0].default_value = (0.62, 0.60, 0.57, 1.0); bg.inputs[1].default_value = 0.9

    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.0; sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new('Fill', bpy.data.lights.new('Fill', 'SUN'))
    fill.data.energy = 1.2; fill.rotation_euler = (math.radians(70), 0, math.radians(150))
    sc.collection.objects.link(fill)

    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam)
    sc.camera = cam
    blade_group = [o for n, o in objs.items() if n in ('Wilah', 'Ganja', 'Mendak', 'Hulu', 'Hulu_Selut', 'Hulu_Tutup')]

    def ortho(path, cx, cz, size):
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = size
        cam.location = (cx, -2.0, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
        sc.render.filepath = path; bpy.ops.render.render(write_still=True)

    def persp(path, target, eye, lens=50):
        cam.data.type = 'PERSP'; cam.data.lens = lens
        cam.location = eye
        cam.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = path; bpy.ops.render.render(write_still=True)

    objs['Wilah'].hide_render = True
    ortho(os.path.join(DOCS, 'KerisJawa_Blender_tersarung.png'), 0.025, 0.29, 0.62)
    objs['Wilah'].hide_render = False
    for o in blade_group:
        o.location.x += DRAWN_DX
    ortho(os.path.join(DOCS, 'KerisJawa_Blender_dihunus.png'), 0.11, 0.30, 0.64)
    persp(os.path.join(DOCS, 'KerisJawa_Blender_34.png'), (0.11, 0, 0.30), (0.75, -0.95, 0.62), 50)
    persp(os.path.join(DOCS, 'KerisJawa_Blender_hulu.png'), (DRAWN_DX - 0.004, 0, BH + 0.06), (DRAWN_DX - 0.16, -0.22, BH + 0.12), 60)
    for o in blade_group:
        o.location.x -= DRAWN_DX


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = {
        'pamor': material('pamor', image=tex['pamor'], metallic=0.9, rough=0.35),
        'kemuning': material('kemuning', image=tex['kemuning'], rough=0.5),
        'sonokeling': material('sonokeling', image=tex['sonokeling'], normal=tex['hulu_normal'], rough=0.45),
        'kuningan': material('kuningan', color=(0.78, 0.56, 0.22), metallic=1.0, rough=0.3),
        'kuningan_ukir': material('kuningan_ukir', color=(0.78, 0.56, 0.22), normal=tex['kuningan_normal'], metallic=1.0, rough=0.32),
        'dudukan': material('dudukan', color=(0.10, 0.06, 0.04), rough=0.55),
    }

    hulu, tutup = part_hulu()
    blade_origin = (0.0, 0.0, BH)
    objs = {}
    for name, mb, mat, origin, sub in [
        ('Dudukan', part_dudukan(), M['dudukan'], (0, 0, 0), 0),
        ('Gandar', part_gandar(), M['kemuning'], (0, 0, 0), 0),
        ('Warangka', part_warangka(), M['kemuning'], (0, 0, 0.4225), 0),
        ('Wilah', part_wilah(), M['pamor'], blade_origin, 0),
        ('Ganja', part_ganja(), M['pamor'], blade_origin, 0),
        ('Mendak', part_mendak(), M['kuningan'], blade_origin, 0),
        ('Hulu', hulu, M['sonokeling'], blade_origin, 1),
        ('Hulu_Selut', part_selut(), M['kuningan_ukir'], blade_origin, 0),
        ('Hulu_Tutup', tutup, M['kuningan'], blade_origin, 1),
    ]:
        objs[name] = build(name, mb, mat, origin, subsurf=sub)

    # Laporan ukuran (untuk dicocokkan dengan cetak biru)
    dg = bpy.context.evaluated_depsgraph_get()
    total = 0
    print('\n== Ukuran bagian (m, ruang Model) ==')
    for name, ob in objs.items():
        ev = ob.evaluated_get(dg); me = ev.to_mesh()
        co = np.array([ob.matrix_world @ v.co for v in me.vertices])
        tris = sum(len(p.vertices) - 2 for p in me.polygons); total += tris
        lo, hi = co.min(0), co.max(0)
        print(f'{name:11s} x[{lo[0]:+.4f},{hi[0]:+.4f}] y[{lo[1]:+.4f},{hi[1]:+.4f}] z[{lo[2]:.4f},{hi[2]:.4f}]'
              f'  ukuran {(hi - lo)[0] * 100:.1f} x {(hi - lo)[1] * 100:.1f} x {(hi - lo)[2] * 100:.1f} cm  tris {tris}')
        ev.to_mesh_clear()
    print(f'Total segitiga: {total}')

    for ob in bpy.data.objects:
        ob.select_set(ob.type == 'MESH')
    os.makedirs(ART, exist_ok=True)
    # axis_forward='Z': Blender +Y -> FBX +Z, sehingga di Unity (yang membalik X) muka depan -Y Blender = -Z Unity
    # dan +X Blender tetap +X Unity.
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
    print('FBX:', FBX)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)
    if RENDER:
        render_previews(objs)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)  # simpan lagi beserta kamera & lampu
        print('Render pratinjau: Docs/KerisJawa_Blender_*.png')


main()
