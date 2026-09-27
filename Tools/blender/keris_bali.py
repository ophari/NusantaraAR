# Keris Bali (luk 9, pamor "banyu tetes", ganja emas, danganan togogan figur dewa, warangka Bali sesrengatan,
# pendok emas bertatah permata) di atas jagrak ukir Karang Boma -> Blender -> GLB.
#
# Jalankan (tanpa membuka jendela Blender):
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/blender/keris_bali.py
# Tambahkan "-- --no-render" di akhir untuk melewati render pratinjau.
#
# Hasil:
#   Assets/NusantaraAR/Art/KerisBali/keris_bali.glb      (model PBR, tekstur tertanam, animasi "Cabut_Keris")
#   Tools/blender/keris_bali_textures/*.png             (sumber tekstur: warna, ORM, normal map)
#   Tools/blender/keris_bali.blend                      (file kerja; di luar Assets agar tidak diimpor Unity)
#   Docs/KerisBali_Blender_*.png                        (render pratinjau)
#
# Utilitas mesh/tekstur/material/ekspor dipakai bersama dari keris_sumatra.py.
# Ruang lokal keris sama dengan Keris Sumatra: bilah menghadap -Z, pivot di pangkal bilah, lebar di X, tebal di Y.
# Di GLB, empty "Keris" meletakkan keris mendatar di atas jagrak (hulu ke +X, muka depan -Y, sisi greneng ke bawah).
# Hierarki: Keris_Bali > Jagrak (Jagrak_Ukiran, Jagrak_Kaki) + Keris > Bilah (Wilah, Ganja, Selut, Permata_Selut, Hulu, Permata_Hulu)
#           + Sarung (Warangka_Sesrengatan, Warangka_Celah, Gandar, Cincin, Permata_Warangka, Pendok).

import bpy, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from keris_sumatra import (MB, build, lathe, sphere, sweep2d, spline, closed_spline, resample, taper, spow, sstep,
                           write_png, uvgrid, fbm2, blur, mix, normal_from_height, orm, pbr, export_glb, look,
                           studio, tex_jackwood, TAU)

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'NusantaraAR', 'Art', 'KerisBali')
GLB = os.path.join(ART, 'keris_bali.glb')
TEX = os.path.join(HERE, 'keris_bali_textures')
BLEND = os.path.join(HERE, 'keris_bali.blend')
DOCS = os.path.join(ROOT, 'Docs')
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
RENDER = '--no-render' not in ARGS

BLADE_L = 0.400
LUK = 9
PESI_L = 0.080
GANJA_TOP = 0.0135
SELUT_TOP = 0.036
DRAW_Z = 0.46
GANDAR_TOP, GANDAR_BOT = -0.030, -0.438
CINCIN_ATAS = (-0.040, -0.058)
CINCIN_TENGAH = (-0.198, -0.219)

# Jagrak (ruang dunia, alas di z = 0)
BASE_TOP = 0.027
JW, JH = 0.170, 0.1045       # setengah lebar dan tinggi panel ukir di atas kaki
POST_X = 0.120               # dua tiang teratai tempat keris bersandar
KERIS_X = 0.215              # posisi mulut warangka (pivot keris) di sumbu X dunia
RELIEF, T0 = 0.011, 0.007    # tinggi relief ukiran dan setengah tebal dasar panel


# ============================================================ util geometri kecil

def unit(v):
    v = np.asarray(v, float)
    return v / np.linalg.norm(v)


def basis(n):
    n = unit(n)
    ref = np.array([1.0, 0, 0]) if abs(n[2]) > 0.9 else np.array([0, 0, 1.0])
    t2 = unit(np.cross(n, ref)); t1 = np.cross(t2, n)
    return t1, t2


def dome(mb, c, n, r, h, cols=16, rows=4):
    """Batu cabochon: kubah elips menghadap n."""
    c = np.asarray(c, float); n = unit(n); t1, t2 = basis(n)
    ph = np.linspace(0, np.pi / 2, rows + 1)[:-1]; a = TAU * np.arange(cols) / cols
    P = (c[None, None] + (t1[None, None] * np.cos(a)[None, :, None] + t2[None, None] * np.sin(a)[None, :, None])
         * (r * np.cos(ph))[:, None, None] + n[None, None] * (h * np.sin(ph))[:, None, None])
    mb.grid(P, uv=lambda i, j: (0.5 + 0.4 * math.cos(TAU * j / cols), 0.5 + 0.4 * math.sin(TAU * j / cols)),
            start=c - n * 0.0004, start_uv=(0.5, 0.5), end=c + n * h, end_uv=(0.5, 0.5))


def torus(mb, c, n, R, r, Ry=None, cols=24, rows=6):
    c = np.asarray(c, float); n = unit(n); t1, t2 = basis(n)
    Ry = R if Ry is None else Ry
    a = TAU * np.arange(cols) / cols; b = TAU * np.arange(rows) / rows
    ring = c[None] + t1[None] * (R * np.cos(a))[:, None] + t2[None] * (Ry * np.sin(a))[:, None]
    rad = t1[None] * np.cos(a)[:, None] + t2[None] * np.sin(a)[:, None]
    P = ring[None] + (rad[None] * np.cos(b)[:, None, None] + n[None, None] * np.sin(b)[:, None, None]) * r
    mb.grid(P, uv=lambda i, j: (j / cols, i / rows), wrap_rows=True)


# ============================================================ bilah (wilah, pesi) & ganja

def luk_center(u):
    u = np.asarray(u, float)
    t = np.clip((u - 0.12) / (0.965 - 0.12), 0.0, 1.0)
    return 0.0045 * sstep(0.09, 0.22, u) * (1 - 0.3 * u) * np.sin(LUK * np.pi * t)


def blade_widths(u):
    u = np.asarray(u, float)
    body = 0.0200 * (1 - 0.32 * u)
    tip = 1 - np.clip((u - 0.85) / 0.15, 0, 1) ** 1.7
    front = (0.0085 * (1 - sstep(0.0, 0.12, u)) + 0.0030 * np.exp(-((u - 0.040) / 0.011) ** 2)
             - 0.0024 * np.exp(-((u - 0.068) / 0.006) ** 2))
    back = 0.020 * (1 - sstep(0.0, 0.15, u)) ** 1.3
    f = ((u - 0.016) / 0.0095) % 1.0
    teeth = 0.0022 * sstep(0.010, 0.020, u) * (1 - sstep(0.085, 0.098, u)) * (1 - f) ** 1.5
    return (body + front) * tip, (body + back - teeth) * tip, tip


def part_wilah():
    us = np.concatenate([np.linspace(0.0, 0.13, 90, endpoint=False),
                         np.linspace(0.13, 0.86, 120, endpoint=False),
                         np.linspace(0.86, 0.994, 18)])
    xc = luk_center(us); zc = -BLADE_L * us
    dx = np.gradient(xc); dz = np.gradient(zc); ln = np.hypot(dx, dz)
    Nx, Nz = -dz / ln, dx / ln
    C = 44
    tau = TAU * np.arange(C) / C
    tt = tau + 0.16 * np.sin(2 * tau)
    c, s = np.cos(tt), np.sin(tt)
    P = np.zeros((len(us), C, 3)); W = np.zeros((len(us), C))
    for i, u in enumerate(us):
        wF, wB, tip = blade_widths(u)
        b = 0.0037 * (1 - 0.5 * u) * np.sqrt(max(tip, 0.02))
        ridge = 0.30 * sstep(0.02, 0.10, u) * (1 - sstep(0.82, 0.95, u))              # adeg-adeg (punggungan)
        e = np.where(c < 0, 0.55 * (1 - sstep(0.06, 0.10, u)), 0.0)
        f = e + (1 - e) * (1 - np.abs(c) ** 3.0) ** 0.55                             # bilik rata, kikis tajam
        groove = 0.22 * np.exp(-((np.abs(c) - 0.32) / 0.07) ** 2) * sstep(0.02, 0.05, u) * (1 - sstep(0.15, 0.24, u))
        w = c * np.where(c < 0, wF, wB)
        pej = 1 - 0.35 * np.exp(-(((w + 0.011) / 0.006) ** 2 + ((u - 0.050) / 0.026) ** 2))
        W[i] = w
        P[i, :, 0] = xc[i] + Nx[i] * w
        P[i, :, 1] = s * b * (f + ridge * np.exp(-(c / 0.10) ** 2)) * (1 - groove) * pej
        P[i, :, 2] = zc[i] + Nz[i] * w
    mb = MB()
    mb.grid(P, uv=lambda i, j: (0.45 + W[i, j % C] / 0.09, us[i]),
            start=(0, 0, 0), start_uv=(0.45, 0.0), end=(0, 0, -BLADE_L), end_uv=(0.45, 1.0))
    zs = np.linspace(-0.001, PESI_L - 0.003, 8)
    C2 = 16; a = TAU * np.arange(C2) / C2
    Q = np.zeros((len(zs), C2, 3))
    for i, z in enumerate(zs):
        r = 0.0048 - 0.0025 * z / PESI_L
        Q[i, :, 0] = r * spow(np.cos(a), 0.7); Q[i, :, 1] = 0.85 * r * spow(np.sin(a), 0.7); Q[i, :, 2] = z
    mb.grid(Q, uv=lambda i, j: (0.45 + Q[i, j % C2, 0] / 0.09, 0.02 * i / 7), start=(0, 0, -0.001), end=(0, 0, PESI_L))
    return mb


def part_ganja():
    """Ganja: palang berlapis emas di pangkal bilah, ekor segitiga meruncing di sisi greneng (+X)."""
    prof = [(-0.031, 0.000), (0.012, 0.000), (0.024, -0.014), (0.034, -0.030), (0.042, -0.046), (0.047, -0.040),
            (0.054, -0.026), (0.061, -0.012), (0.066, -0.002), (0.066, 0.006), (0.060, 0.0115), (0.050, 0.0135),
            (0.030, 0.0138), (0.000, 0.0138), (-0.020, 0.0135), (-0.028, 0.012), (-0.033, 0.009), (-0.035, 0.005),
            (-0.033, 0.002)]
    Q = closed_spline(prof, 96)
    area = 0.5 * np.sum(Q[:, 0] * np.roll(Q[:, 1], -1) - np.roll(Q[:, 0], -1) * Q[:, 1])
    tg = np.roll(Q, -1, 0) - np.roll(Q, 1, 0)
    nrm = np.stack([tg[:, 1], -tg[:, 0]], 1); nrm /= np.linalg.norm(nrm, axis=1)[:, None]
    if area < 0:
        nrm = -nrm
    T, r = 0.0058, 0.0015
    phis = np.linspace(-np.pi / 2, np.pi / 2, 7)
    P = np.zeros((len(phis), len(Q), 3))
    for i, ph in enumerate(phis):
        q = Q - nrm * r * (1 - np.cos(ph))
        P[i, :, 0] = q[:, 0]; P[i, :, 1] = T * np.sin(ph); P[i, :, 2] = q[:, 1]
    mb = MB()
    mb.grid(P, uv=lambda i, j: (0.5 + P[i, j % len(Q), 0] / 0.06, 0.5 + P[i, j % len(Q), 2] / 0.06),
            cap_start=True, cap_end=True)
    return mb


# ============================================================ selut bertatah permata & hulu figur dewa

SELUT_PROF = [(0.0100, 0.0135), (0.0112, 0.0145), (0.0110, 0.0160), (0.0122, 0.0175), (0.0150, 0.0205),
              (0.0165, 0.0240), (0.0162, 0.0275), (0.0148, 0.0305), (0.0132, 0.0330), (0.0136, 0.0345),
              (0.0130, SELUT_TOP)]


def selut_r(z):
    p = np.array(SELUT_PROF)
    return float(np.interp(z, p[:, 1], p[:, 0]))


def part_selut():
    """Cincin bawah hulu (selut): cawan membulat, dua baris butiran emas, dudukan (bezel) permata."""
    mb = MB()
    prof = [(0.0060, 0.0135)] + SELUT_PROF + [(0.0060, SELUT_TOP)]
    P = lathe(prof, 56)
    mb.grid(P, uv=lambda i, j: (3 * j / 56, 0.5 * i / len(prof)), wrap_rows=True)
    for z, n in ((0.0176, 22), (0.0318, 24)):
        rr = selut_r(z) + 0.0002
        for k in range(n):
            a = TAU * (k + 0.5) / n
            sphere(mb, (rr * math.cos(a), rr * math.sin(a), z), 0.0010, rows=4, cols=8)
    for k, (c, nrm) in enumerate(selut_gems()):
        torus(mb, c, nrm, 0.0029, 0.00045, cols=18, rows=5)
    return mb


def selut_gems():
    out = []
    z = 0.0245; dr = (selut_r(z + 0.001) - selut_r(z - 0.001)) / 0.002
    for k in range(10):
        a = TAU * k / 10 - np.pi / 2                      # batu pertama tepat di depan (-Y)
        rr = selut_r(z)
        out.append(((rr * math.cos(a), rr * math.sin(a), z), (math.cos(a), math.sin(a), -dr)))
    return out


HY = -0.0095     # posisi Y kepala figur (sedikit membungkuk ke depan)


def part_permata_selut():
    """Permata cabochon ruby / zamrud / safir bergantian di selut."""
    mb = MB()
    for k, (c, n) in enumerate(selut_gems()):
        mb.mat = k % 3
        dome(mb, c, n, 0.0025, 0.0016)
    return mb


def part_permata_hulu():
    """Satu ruby di dada figur (ikut bergerak bersama hulu)."""
    mb = MB()
    zb = SELUT_TOP - 0.0015
    dome(mb, (0.0, -0.0128, zb + 0.0445), (0, -1, 0.25), 0.0018, 0.0011)
    return mb


def hulu_figure(parent, mat):
    """Figur dewa berlutut (danganan togogan) dengan tangan menyembah: metaball -> mesh, plus mahkota & perhiasan."""
    sc = bpy.context.scene
    zb = SELUT_TOP - 0.0015
    # Resolusi metaball dibatasi minimal 5 mm, jadi figur dibangun 10x lebih besar lalu diperkecil lagi.
    S = 10.0
    mbd = bpy.data.metaballs.new('HuluMeta')
    mbd.resolution = 0.0055; mbd.render_resolution = 0.0055; mbd.threshold = 0.6
    obm = bpy.data.objects.new('HuluMeta', mbd); sc.collection.objects.link(obm)
    STIFF = 4.0; K = 1 / 0.685   # stiffness 4: permukaan bola terisolasi di ~0.685 x radius, sambungan lebih luwes

    def el(co, sx, sy, sz):
        e = mbd.elements.new(type='ELLIPSOID'); r0 = max(sx, sy, sz)
        e.co = (S * co[0], S * co[1], S * (zb + co[2])); e.radius = S * K * r0; e.stiffness = STIFF
        e.size_x, e.size_y, e.size_z = sx / r0, sy / r0, sz / r0

    def ball(co, r):
        e = mbd.elements.new(type='BALL'); e.co = (S * co[0], S * co[1], S * (zb + co[2]))
        e.radius = S * K * r; e.stiffness = STIFF

    def cap(p, q, r):
        p = Vector((p[0], p[1], zb + p[2])) * S; q = Vector((q[0], q[1], zb + q[2])) * S; d = q - p
        e = mbd.elements.new(type='CAPSULE'); e.co = (p + q) / 2; e.size_x = d.length / 2
        e.radius = S * K * r; e.stiffness = STIFF
        e.rotation = Vector((1, 0, 0)).rotation_difference(d)

    el((0, 0.003, 0.015), 0.0125, 0.0105, 0.0095)          # panggul
    el((0, -0.004, 0.011), 0.0140, 0.0150, 0.0105)         # kain berlipat menutup kaki
    el((0, -0.001, 0.029), 0.0100, 0.0082, 0.0100)         # perut
    el((0, -0.0045, 0.043), 0.0122, 0.0082, 0.0090)        # dada (membungkuk ke depan)
    cap((0, -0.006, 0.049), (0, -0.0085, 0.056), 0.0040)   # leher
    el((0, HY, 0.0635), 0.0079, 0.0084, 0.0094)            # kepala
    ball((0, HY - 0.0077, 0.0615), 0.0014)                 # hidung
    el((0, -0.0172, 0.0505), 0.0028, 0.0026, 0.0060)       # tangan menyembah
    for sg in (-1, 1):
        cap((sg * 0.0065, 0.002, 0.013), (sg * 0.0072, -0.0125, 0.017), 0.0052)    # paha
        ball((sg * 0.0074, -0.0140, 0.0145), 0.0046)                                 # lutut
        cap((sg * 0.0075, -0.0130, 0.011), (sg * 0.0060, 0.0070, 0.0045), 0.0040)   # betis terlipat
        ball((sg * 0.0120, -0.0035, 0.0475), 0.0044)                                 # bahu
        cap((sg * 0.0128, -0.004, 0.046), (sg * 0.0118, -0.0125, 0.0365), 0.0031)   # lengan atas
        cap((sg * 0.0118, -0.0125, 0.0365), (sg * 0.0025, -0.0165, 0.0470), 0.0027)  # lengan bawah

    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obm; obm.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob = bpy.context.view_layer.objects.active
    ob.data.transform(Matrix.Scale(1 / S, 4))
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print(f'  figur metaball: {tris} segitiga sebelum decimate')
    if tris > 18000:
        dm = ob.modifiers.new('Decimate', 'DECIMATE'); dm.ratio = 18000 / tris
        bpy.ops.object.modifier_apply(modifier=dm.name)
    me = ob.data
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    uvl = me.uv_layers.new(name='UVMap')             # proyeksi silinder agar tangen glTF bisa dihitung
    for lp in me.loops:
        co = me.vertices[lp.vertex_index].co
        uvl.data[lp.index].uv = (math.atan2(co.y - HY, co.x) / TAU + 0.5, (co.z - zb) / 0.1)
    me.materials.append(mat)

    orn = MB()
    crown = [(0.0080, 0.0660), (0.0090, 0.0668), (0.0092, 0.0690), (0.0085, 0.0700), (0.0089, 0.0712),
             (0.0079, 0.0722), (0.0073, 0.0745), (0.0064, 0.0752), (0.0066, 0.0770), (0.0054, 0.0778),
             (0.0052, 0.0800), (0.0042, 0.0808), (0.0040, 0.0830), (0.0030, 0.0840), (0.0022, 0.0870),
             (0.0012, 0.0900)]
    P = lathe([(r, zb + z) for r, z in crown], 32, 0.0, HY)
    orn.grid(P, uv=lambda i, j: (j / 32, i / len(crown)), start=(0, HY, zb + 0.066), end=(0, HY, zb + 0.0935))
    # Prabha (lempeng bergerigi di belakang kepala)
    n = 90; th = TAU * np.arange(n) / n; k = 1 + 0.10 * np.abs(np.cos(4.5 * th))
    Q = np.zeros((2, n, 3))
    Q[:, :, 0] = 0.0135 * k * np.cos(th); Q[:, :, 2] = zb + 0.068 + 0.0165 * k * np.sin(th)
    Q[0, :, 1] = HY + 0.0125; Q[1, :, 1] = HY + 0.0140
    orn.grid(Q, cap_start=True, cap_end=True)
    torus(orn, (0, -0.0058, zb + 0.0495), (0, -0.35, 1), 0.0095, 0.0011)             # kalung
    torus(orn, (0, -0.0010, zb + 0.0210), (0, 0, 1), 0.0112, 0.0010, Ry=0.0094)      # sabuk
    for sg in (-1, 1):
        fa = np.array([sg * 0.0025, -0.0175, 0.0485]) - np.array([sg * 0.0118, -0.0125, 0.0365])
        ua = np.array([sg * 0.0118, -0.0125, 0.0365]) - np.array([sg * 0.0128, -0.004, 0.046])
        torus(orn, (sg * 0.0048, -0.0165, zb + 0.0455), fa, 0.0036, 0.0008)           # gelang tangan
        torus(orn, (sg * 0.0124, -0.0080, zb + 0.0415), ua, 0.0042, 0.0008)           # gelang lengan
        dome(orn, (sg * 0.0074, HY, zb + 0.0610), (sg, 0, 0), 0.0022, 0.0012)         # subeng (anting)
    ornob = build('HuluOrnamen', orn, mat, None)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ornob.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.join()
    ob.name = 'Hulu'; ob.data.name = 'Hulu'; ob.parent = parent
    return ob


# ============================================================ warangka sesrengatan, gandar, cincin, pendok

# Warangka gaya Bali "sesrengatan" (dianggap terbaik oleh komunitas keris Bali), siluet mengikuti foto sampel
# jurnal Progresif 21(2) 2025 & KOMITEK 5(1) 2025: badan datar tempat ganja bertumpu, ujung belakang pendek
# tumpul membulat (sisi greneng, +X), ujung depan menjulang menjadi tanduk runcing (sisi gandik, -X).
# Profil sebagai fungsi x (ruang lokal keris): tepi atas & bawah (z), setengah tebal (y).
SES_X = [-0.100, -0.088, -0.076, -0.064, -0.052, -0.040, -0.029, 0.029, 0.045, 0.060, 0.070, 0.080]
SES_TOP = [0.055, 0.037, 0.025, 0.014, 0.007, 0.002, 0.000, 0.000, 0.000, 0.000, -0.001, -0.003]
SES_BOT = [0.055, 0.028, 0.008, -0.008, -0.020, -0.028, -0.032, -0.034, -0.033, -0.031, -0.029, -0.027]
SES_T = [0.0035, 0.0068, 0.0095, 0.0115, 0.0124, 0.0126, 0.0126, 0.0126, 0.0125, 0.0123, 0.0121, 0.0118]
SES_END_R = 0.010    # jari-jari pembulatan ujung belakang (tumpul, hampir tegak)


def part_sampir():
    n, C = 110, 36
    x0, x1 = SES_X[0], SES_X[-1]
    xs = x0 + (x1 - x0) * (1 - np.cos(np.linspace(0, np.pi, n))) / 2       # rapat di kedua ujung
    zt = np.interp(xs, SES_X, SES_TOP); zb = np.interp(xs, SES_X, SES_BOT); ty = np.interp(xs, SES_X, SES_T)
    for arr in (zt, zb, ty):                                               # haluskan sudut interpolasi linear
        arr[1:-1] = np.convolve(np.pad(arr, 3, mode='edge'), np.ones(7) / 7, 'valid')[1:-1]
    zc = (zt + zb) / 2; hz = np.maximum((zt - zb) / 2, 0.0006)
    # Ujung belakang membulat. Ring terakhir sengaja tidak menyusut jadi satu titik: ring degeneratif membuat
    # recalc normal gagal dan permukaan warangka terbalik ke dalam (tampak gelap/tembus di Unity).
    e = np.sqrt(np.clip(1 - np.clip((xs - (x1 - SES_END_R)) / SES_END_R, 0, 1) ** 2, 0.03, 1))
    hz *= e; ty *= np.sqrt(e)
    a = TAU * np.arange(C) / C
    sz = spow(np.sin(a), 0.45); cy = spow(np.cos(a), 0.55)                 # penampang persegi membulat
    P = np.zeros((n, C, 3))
    P[:, :, 0] = xs[:, None]
    P[:, :, 1] = ty[:, None] * cy[None, :]
    P[:, :, 2] = zc[:, None] + hz[:, None] * sz[None, :]
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, 0.9 * i / (n - 1)),
            start=(x0 - 0.0006, 0, zc[0]), start_uv=(0.5, 0.0), end=(x1 + 0.0004, 0, zc[-1]), end_uv=(0.5, 0.9))
    return mb


def part_celah():
    mb = MB()
    x0, x1, hy = -0.0290, 0.0412, 0.0043
    k = np.linspace(0, 1, 25); xs = x0 + (x1 - x0) * k
    ys = hy * np.sqrt(np.clip(1 - (2 * k - 1) ** 2, 0, 1)) ** 0.8
    ring = [(x, -y) for x, y in zip(xs, ys)] + [(x, y) for x, y in zip(xs[::-1][1:-1], ys[::-1][1:-1])]
    c = mb.point(((x0 + x1) / 2, 0.0, 0.0002))
    ids = [mb.point((x, y, 0.0002)) for x, y in ring]
    for j in range(len(ids)):
        mb.face([c, ids[j], ids[(j + 1) % len(ids)]], [(0.5, 0.5)] * 3)
    return mb


def gandar_dims(z):
    q = np.clip((GANDAR_TOP - z) / (GANDAR_TOP - GANDAR_BOT), 0, 1)
    return 0.029 - 0.008 * q ** 0.9, 0.0100 - 0.0020 * q


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
    zs = list(np.linspace(GANDAR_TOP, GANDAR_BOT, 48)); offs = [0.0] * 48; scales = [1.0] * 48
    z2, o2, s2 = end_cap(GANDAR_BOT, 0.011, 0.0)
    zs += z2; offs += o2; scales += s2
    P = sheath_rows(zs, offs, scales, C)
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, (GANDAR_TOP - zs[i]) / 0.40), start=(0, 0, GANDAR_TOP),
            end=(0, 0, GANDAR_BOT - 0.011))
    return mb


def band_rows(z0, z1, bulge):
    zs = [z0, z0, z0 - 0.0025] + list(np.linspace(z0 - 0.004, z1 + 0.004, 6)) + [z1 + 0.0025, z1, z1]
    offs = [-0.0003, 0.0011, 0.0018] + [bulge] * 6 + [0.0018, 0.0011, -0.0003]
    return zs, offs


def lens_point(z, off, xt, sign=-1):
    """Titik di permukaan penampang sarung (sisi depan sign=-1 / belakang +1) dengan x terdekat xt, plus normalnya."""
    a, b = gandar_dims(z); a += off; b += off
    t = np.linspace(np.pi, TAU, 4000) if sign < 0 else np.linspace(0, np.pi, 4000)
    x = a * spow(np.cos(t), 0.85); y = b * spow(np.sin(t), 0.9)
    k = int(np.argmin(np.abs(x - xt)))
    dx, dy = np.gradient(x)[k], np.gradient(y)[k]
    n = np.array([dy, -dx, 0.0])
    if n[0] * x[k] + n[1] * y[k] < 0:
        n = -n
    return np.array([x[k], y[k], z]), unit(n)


def ring_gems():
    """(pusat, normal, jari-jari, tinggi, indeks material) permata di cincin atas & cincin tengah (depan + belakang)."""
    out = []
    for sign in (-1, 1):
        zc = sum(CINCIN_ATAS) / 2
        c, n = lens_point(zc, 0.0019, 0.0, sign); out.append((c + n * 0.0001, n, 0.0034, 0.0020, 1))
        zc = sum(CINCIN_TENGAH) / 2
        c, n = lens_point(zc, 0.0026, 0.0, sign); out.append((c + n * 0.0001, n, 0.0043, 0.0024, 1))
        for xt in (-0.0145, 0.0145):
            c, n = lens_point(zc, 0.0026, xt, sign); out.append((c + n * 0.0001, n, 0.0030, 0.0018, 0))
    return out


def part_cincin():
    C = 36
    mb = MB()
    for (z0, z1), bulge in ((CINCIN_ATAS, 0.0019), (CINCIN_TENGAH, 0.0026)):
        zs, offs = band_rows(z0, z1, bulge)
        P = sheath_rows(zs, offs, [1.0] * len(zs), C)
        mb.grid(P, uv=lambda i, j, zs=zs, z0=z0: (j / C, (z0 - zs[i]) / 0.12))
    for c, n, r, h, _ in ring_gems():
        torus(mb, c, n, r + 0.0004, 0.0006, cols=20, rows=5)
    return mb


def part_permata_warangka():
    mb = MB()
    for c, n, r, h, mi in ring_gems():
        mb.mat = mi
        dome(mb, c, n, r, h)
    return mb


def part_pendok():
    C = 36
    z0 = CINCIN_TENGAH[1] + 0.001
    zs = [z0] + list(np.linspace(z0, GANDAR_BOT, 50))
    offs = [-0.0003] + [0.0010] * 50
    z2, o2, s2 = end_cap(GANDAR_BOT, 0.013, 0.0010, 7)
    scales = [1.0] * len(zs) + s2
    zs += z2; offs += o2
    P = sheath_rows(zs, offs, scales, C)
    tip = GANDAR_BOT - 0.013
    mb = MB()
    mb.grid(P, uv=lambda i, j: (j / C, (z0 - zs[i]) / 0.12), end=(0, 0, tip), end_uv=(0.5, (z0 - tip) / 0.12))
    return mb


# ============================================================ jagrak ukir Karang Boma

def post_tops():
    """Tinggi tiang (di atas kaki) agar sarung bersandar tepat di kedua tiang."""
    aR = gandar_dims(POST_X - KERIS_X)[0]
    aL = gandar_dims(-POST_X - KERIS_X)[0] + 0.0010          # tiang kiri menopang pendok
    axis = BASE_TOP + JH + aR
    return axis, JH, axis - aL - BASE_TOP


AXIS_Z, POST_TOP_R, POST_TOP_L = post_tops()


def jagrak_design(X, Z, e):
    """Desain 2D panel (X, Z di atas kaki, meter): M = bagian kayu (1) / tembus (0), R = tinggi relief 0..1.
    Simetris kiri-kanan: Boma di tengah, sayap, sulur spiral, daun, batang, tiang teratai, dan palang bawah."""
    AX = np.abs(X)
    M = np.zeros_like(X); R = np.zeros_like(X)

    def inside(d, w):
        return 1 - sstep(w - e, w + e, d)

    def put(m, rel):
        nonlocal M, R
        M = np.maximum(M, m); R = np.maximum(R, m * rel)

    def over(m, rel):
        nonlocal M, R
        M = np.maximum(M, m); R = R * (1 - m) + m * rel

    def spiral(cx, cz, Rm, pitch, w, chir):
        dx = AX - cx; dz = Z - cz; r = np.hypot(dx, dz); th = np.arctan2(dz, dx * chir)
        d = np.abs((r / pitch - th / TAU) % 1.0 - 0.5) * pitch
        m = np.maximum(inside(d, w / 2), inside(r, 0.7 * w)) * inside(r, Rm)
        prof = np.sqrt(np.clip(1 - (d / (w / 2)) ** 2, 0, 1))
        put(m, (0.55 + 0.25 * (1 - np.clip(r / Rm, 0, 1))) * (0.8 + 0.2 * prof))

    def stem(ctrl, w, rel=0.6):
        pts, L = spline(ctrl, 2)
        pts, _ = spline(ctrl, max(8, int(L / 0.0007)))
        d2 = np.full(X.shape, 1.0)
        for px, pz in pts:
            d2 = np.minimum(d2, (AX - px) ** 2 + (Z - pz) ** 2)
        d = np.sqrt(d2)
        put(inside(d, w / 2), rel * (0.8 + 0.2 * np.sqrt(np.clip(1 - (d / (w / 2)) ** 2, 0, 1))))

    def leaf(x0, z0, ang, L, W, rel=0.58, vein=True):
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        a = (AX - x0) * ca + (Z - z0) * sa; p = -(AX - x0) * sa + (Z - z0) * ca
        ww = W * np.sin(np.pi * np.clip(a / L, 0, 1)) ** 0.7
        m = inside(np.abs(p), ww) * (ww > 0.0003)
        r = rel + 0.10 * (1 - np.clip(np.abs(p) / W, 0, 1))
        if vein:
            r = r - 0.18 * np.exp(-(p / 0.0005) ** 2)
        put(m, r)

    # Palang bawah bermanik
    put(inside(np.abs(Z - 0.006), 0.006) * inside(AX, 0.166),
        0.28 + 0.22 * np.exp(-((Z - 0.0062) / 0.0022) ** 2) * (0.55 + 0.45 * np.cos(TAU * X / 0.0055)))
    # Tiang teratai penopang keris
    for sg, top in ((1, POST_TOP_R), (-1, POST_TOP_L)):              # tiang kuncup teratai
        side = (X * sg > 0)
        d = np.abs(X * sg - POST_X)
        wz = (0.0045 + 0.0030 * np.exp(-((Z - 0.030) / 0.010) ** 2) + 0.0020 * np.exp(-((Z - 0.062) / 0.006) ** 2))
        put(side * inside(d, wz) * inside(Z, top), 0.42 + 0.12 * np.sqrt(np.clip(1 - (d / wz) ** 2, 0, 1)))
        cup = np.hypot(d / 0.0120, (Z - (top - 0.0060)) / 0.0060)
        put(side * inside(cup * 0.006, 0.006) * (Z < top),
            0.58 - 0.15 * np.exp(-((np.abs(np.arctan2(Z - (top - 0.006), d)) % 0.6 - 0.3) / 0.05) ** 2))
    # Batang sulur
    stem([(0.030, 0.050), (0.045, 0.030), (0.062, 0.019), (0.080, 0.014), (0.095, 0.011)], 0.0052)
    stem([(0.088, 0.060), (0.100, 0.068), (0.099, 0.078)], 0.0046)
    stem([(0.094, 0.028), (0.110, 0.034), (0.128, 0.040)], 0.0046)
    stem([(0.124, 0.070), (0.138, 0.075)], 0.0044)
    stem([(0.028, 0.068), (0.042, 0.073)], 0.0050)
    # Sulur spiral
    spiral(0.072, 0.042, 0.025, 0.0092, 0.0055, 1)
    spiral(0.148, 0.036, 0.018, 0.0075, 0.0046, -1)
    spiral(0.098, 0.082, 0.0125, 0.0060, 0.0040, 1)
    spiral(0.150, 0.078, 0.0110, 0.0055, 0.0038, -1)
    # Daun
    leaf(0.056, 0.064, 110, 0.017, 0.0055)
    leaf(0.106, 0.016, 20, 0.019, 0.0052)
    leaf(0.158, 0.016, 80, 0.015, 0.0050)
    leaf(0.132, 0.052, 60, 0.015, 0.0050)
    leaf(0.030, 0.016, 160, 0.017, 0.0050)
    # Sayap (kipas bulu) di kiri-kanan kepala Boma
    for k in range(6):
        ang = 20 + k * 11
        leaf(0.040, 0.072, ang, 0.027 - 0.0012 * k, 0.0042, rel=0.55)

    # Karang Boma di tengah
    BZ, FR = 0.056, 0.026
    r = np.hypot(X, Z - BZ)
    for k in range(9):                                                   # rambut api (mahkota nyala)
        ang = math.radians(15 + k * 150 / 8)
        L = 0.016 - 0.003 * abs(math.cos(ang))
        a = X * math.cos(ang) + (Z - BZ) * math.sin(ang) - (FR - 0.004)
        p = -X * math.sin(ang) + (Z - BZ) * math.cos(ang)
        p = p - 0.004 * np.clip(a / L, 0, 1) ** 2 * np.sign(math.cos(ang) + 1e-6)
        ww = 0.0048 * np.sin(np.pi * np.clip(a / L, 0, 1)) ** 0.8
        put(inside(np.abs(p), ww) * (ww > 0.0003), 0.62 + 0.18 * (1 - np.clip(a / L, 0, 1)))
    over(inside(r, FR), 0.55 + 0.25 * np.clip(1 - (r / FR) ** 2, 0, 1))
    de = np.hypot(AX - 0.0105, Z - (BZ + 0.006))                          # mata melotot
    over(inside(de, 0.0068), 0.98 - 0.28 * (de / 0.0068) ** 2 - 0.20 * np.exp(-((de - 0.0045) / 0.0006) ** 2)
         + 0.12 * (de < 0.0022))
    db = np.abs(np.hypot(AX - 0.0105, Z - (BZ + 0.0065)) - 0.0098)       # alis
    over(inside(db, 0.0016) * sstep(0.001, 0.004, Z - (BZ + 0.0065)), 0.9)
    dn = np.hypot(X, Z - (BZ - 0.0035))                                   # hidung
    over(inside(dn, 0.0056), 0.95 - 0.25 * (dn / 0.0056) ** 2)
    over(inside(np.hypot(AX - 0.0028, Z - (BZ - 0.0055)), 0.0013), 0.6)
    dm = (np.hypot(X / 0.0135, (Z - (BZ - 0.0145)) / 0.0048) - 1) * 0.0048   # mulut menganga
    mouth = 1 - sstep(-e, e, dm)
    over(mouth, 0.22)
    tooth = inside(np.abs(((X / 0.0034) % 1 - 0.5) * 0.0034), 0.0013) * inside(np.abs(Z - (BZ - 0.0110)), 0.0011)
    over(tooth * mouth * (AX < 0.0095), 0.55)
    frac = (Z - (BZ - 0.0235)) / 0.013                                    # taring
    over(inside(np.abs(AX - 0.0088), 0.0027 * np.clip(frac, 0, 1)) * (frac > 0) * (frac < 1), 0.9)
    dr = np.hypot(AX - 0.0295, Z - (BZ - 0.002))                          # anting / sumping
    th = np.arctan2(Z - (BZ - 0.002), AX - 0.0295)
    over(inside(dr, 0.0065), 0.70 + 0.15 * np.cos(TAU * dr / 0.0022 - th))
    return M, R


def part_jagrak_panel():
    dx = 0.0010
    ztop = max(POST_TOP_L, POST_TOP_R) + 0.002
    xs = np.arange(-JW, JW + 1e-9, dx); zs = np.arange(0.0, ztop + 1e-9, dx)
    XN, ZN = np.meshgrid(xs, zs)
    Mn, Rn = jagrak_design(XN, ZN, 1.2 * dx)
    XC, ZC = np.meshgrid(xs[:-1] + dx / 2, zs[:-1] + dx / 2)
    Mc, _ = jagrak_design(XC, ZC, 1.2 * dx)
    cell = Mc > 0.5
    NZc, NXc = cell.shape
    used = np.zeros(XN.shape, bool)
    for di in (0, 1):
        for dj in (0, 1):
            used[di:NZc + di, dj:NXc + dj] |= cell
    act = np.zeros((NZc + 2, NXc + 2), bool); act[1:-1, 1:-1] = cell
    allact = act[:-1, :-1] & act[1:, :-1] & act[:-1, 1:] & act[1:, 1:]
    bnd = used & ~allact
    # Titik tepi digeser ke kontur M = 0,5 agar siluet tidak bergerigi seperti tangga.
    gz, gx = np.gradient(Mn, dx)
    g2 = gx ** 2 + gz ** 2 + 1e-9
    F = Mn - 0.5
    PX = XN + np.where(bnd, np.clip(-F * gx / g2, -0.8 * dx, 0.8 * dx), 0.0)
    PZ = ZN + np.where(bnd, np.clip(-F * gz / g2, -0.8 * dx, 0.8 * dx), 0.0)
    Yd = T0 + RELIEF * np.clip(Rn, 0.0, 1.2)
    TEXH = 2 * JW * 640 / 2048
    mb = MB()
    fid = -np.ones(XN.shape, int); bid = -np.ones(XN.shape, int)
    uv = {}
    for i, j in np.argwhere(used):
        z = BASE_TOP - 0.001 + PZ[i, j]
        fid[i, j] = mb.point((PX[i, j], -Yd[i, j], z)); bid[i, j] = mb.point((PX[i, j], Yd[i, j], z))
        uv[i, j] = ((PX[i, j] + JW) / (2 * JW), PZ[i, j] / TEXH)
    for i, j in np.argwhere(cell):
        q = [(i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)]
        mb.face([fid[p] for p in q], [uv[p] for p in q])
        mb.face([bid[p] for p in q[::-1]], [uv[p] for p in q[::-1]])
        for (a, b), (ni, nj) in ((((i, j), (i, j + 1)), (i - 1, j)), (((i + 1, j), (i + 1, j + 1)), (i + 1, j)),
                                 (((i, j), (i + 1, j)), (i, j - 1)), (((i, j + 1), (i + 1, j + 1)), (i, j + 1))):
            if 0 <= ni < NZc and 0 <= nj < NXc and cell[ni, nj]:
                continue
            # Dinding tembusan memakai petak polos gelap di pojok tekstur (tanpa detail normal map).
            mb.face([fid[a], fid[b], bid[b], bid[a]], [(0.002, 0.975), (0.004, 0.975), (0.004, 0.985), (0.002, 0.985)])
    return mb


def decimate(ob, target):
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if tris <= target:
        return
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    dm = ob.modifiers.new('Decimate', 'DECIMATE'); dm.ratio = target / tris
    bpy.ops.object.modifier_apply(modifier=dm.name)
    ob.data.validate()


def rrect(A, B, rc, n):
    pts = []
    for cx, cy, a0 in ((A - rc, B - rc, 0), (-(A - rc), B - rc, 90), (-(A - rc), -(B - rc), 180), (A - rc, -(B - rc), 270)):
        for t in np.radians(np.linspace(a0, a0 + 90, 10)):
            pts.append((cx + rc * math.cos(t), cy + rc * math.sin(t)))
    pts.append(pts[0])
    return resample(np.array(pts), n, endpoint=False)


def part_kaki():
    """Kaki jagrak bertingkat dua: pita kelopak di tingkat bawah, pita meander di tingkat atas."""
    mb = MB()
    n = 200
    cap_uv = lambda p: ((p[0] + 0.25) / 0.5, 0.92 + 0.06 * (p[1] + 0.07) / 0.14)
    for A, B, rc, rows, bottom in (
            (0.225, 0.062, 0.012, [(-0.004, 0.0), (0.0, 0.002), (0.0, 0.009), (-0.002, 0.0105), (-0.002, 0.0125),
                                   (-0.004, 0.014)], True),
            (0.200, 0.046, 0.010, [(0.0, 0.014), (0.001, 0.0155), (0.001, 0.0255), (-0.001, BASE_TOP)], False)):
        L = rrect(A, B, rc, n)[1]; tiles = max(1, round(L / 0.06))
        P = np.zeros((len(rows), n, 3))
        for i, (o, z) in enumerate(rows):
            Q, _ = rrect(A + o, B + o, rc + o, n)
            P[i, :, 0] = Q[:, 0]; P[i, :, 1] = Q[:, 1]; P[i, :, 2] = z
        mb.grid(P, uv=lambda i, j, P=P, tiles=tiles: (j / n * tiles, P[i, j % n, 2] / 0.03))
        for row in ([0, len(rows) - 1] if bottom else [len(rows) - 1]):
            ring = P[row]; c = np.array([0.0, 0.0, ring[0, 2]])
            ci = mb.point(c); ids = [mb.point(p) for p in ring]
            for j in range(n):
                mb.face([ci, ids[j], ids[(j + 1) % n]], [cap_uv(c), cap_uv(ring[j]), cap_uv(ring[(j + 1) % n])])
    return mb


# ============================================================ tekstur

def tex_pamor_banyu_tetes():
    """Pamor banyu tetes: tetesan air berlapis garis kontur di atas urat nikel yang mengalir memanjang."""
    h, w = 2048, 512
    U, V = uvgrid(h, w)
    SX, SY = 0.09, BLADE_L
    X, Y = U * SX, V * SY
    D = np.full((h, w), 9.0)
    rng = np.random.default_rng(521)
    for k in range(24):
        cx = rng.uniform(0.18, 0.75) * SX; cy = (k + rng.uniform(0.1, 0.9)) / 24 * SY
        L = rng.uniform(0.0045, 0.0075); W = L * rng.uniform(0.40, 0.55)
        dx = X - cx; dy = Y - cy
        wf = W * (1 + 0.55 * np.clip(dy / L, -1, 1))            # membulat ke arah pucuk, meruncing ke pangkal
        D = np.minimum(D, np.sqrt((dx / wf) ** 2 + (dy / L) ** 2))
    wu = fbm2(U, V, 6, 26, 501, 4) - 0.5
    field = U * 26 + 6.0 * fbm2(U + 0.08 * wu, V, 5, 22, 511, 5) + 1.2 * np.exp(-D)
    fr = field % 1.0; d = np.minimum(fr, 1 - fr)
    bg = (1 - sstep(0.035, 0.10, d)) * sstep(1.5, 2.0, D) * (0.35 + 0.65 * sstep(0.36, 0.50, fbm2(U, V, 16, 64, 531, 3)))
    rd = np.abs(((D - 1.0) / 0.45 + 0.5) % 1 - 0.5)
    rings = (1 - sstep(0.08, 0.18, rd)) * (D > 0.85) * (D < 1.7)
    core = 1 - sstep(0.30, 0.45, D)
    speck = (np.random.default_rng(9).random((h, w)) > 0.995) * 0.4
    m = blur(np.clip(np.maximum(np.maximum(bg * 0.9, rings * 0.95), core * 0.85) + speck, 0, 1), 1)
    grain = (fbm2(U, V, 64, 256, 541, 2) - 0.5) * 0.05
    col = mix((0.11, 0.11, 0.12), (0.76, 0.77, 0.79), m) + grain[..., None]
    return col, orm(0.45 - 0.20 * m, np.ones_like(m)), normal_from_height(blur(m, 2), 1.5)


def tex_emas_ukir():
    """Patra Bali (emas): kisi 6 x 6 sulur spiral berselang arah, daun di sudut, latar bertitik; berulang mulus."""
    h = w = 1024
    U, V = uvgrid(h, w)
    n = 6
    cu, cv = U * n, V * n
    iu, iv = np.floor(cu), np.floor(cv)
    x = cu - iu - 0.5; y = cv - iv - 0.5
    chir = np.where((iu + iv) % 2 == 0, 1.0, -1.0)
    r = np.hypot(x, y); th = np.arctan2(y, x * chir)
    d = np.abs((r / 0.12 - th / TAU) % 1.0 - 0.5) * 0.12
    sp = (1 - sstep(0.020, 0.030, d)) * (1 - sstep(0.33, 0.36, r))
    sp = np.maximum(sp, 1 - sstep(0.04, 0.06, r))
    lx = np.abs(x) - 0.5; ly = np.abs(y) - 0.5                         # daun di sudut sel (arah diagonal)
    a = -(lx + ly) / math.sqrt(2); p = (lx - ly) / math.sqrt(2)
    ww = 0.07 * np.sin(np.pi * np.clip(a / 0.30, 0, 1)) ** 0.7
    leaf = (1 - sstep(ww * 0.75, ww + 1e-4, np.abs(p))) * (ww > 0.004)
    dots = (((U * 90) % 1 - 0.5) ** 2 + ((V * 90) % 1 - 0.5) ** 2) < 0.05
    H = np.clip(0.22 + 0.78 * np.maximum(sp, leaf) - 0.08 * dots, 0, 1)
    H = blur(H, 1)
    cav = np.clip(0.15 + 0.85 * sstep(0.2, 0.85, blur(H, 3)), 0, 1)
    tarn = fbm2(U, V, 8, 8, 611, 4)
    col = mix((0.52, 0.36, 0.10), (1.0, 0.86, 0.55), cav) * (0.92 + 0.16 * tarn)[..., None]
    return col, orm(0.42 - 0.20 * cav, 0.9 + 0.1 * cav), normal_from_height(H, 4.0)


def tex_pelet():
    """Kayu pelet: cokelat hangat berserat halus dengan bercak gelap tak beraturan."""
    h = w = 1024
    U, V = uvgrid(h, w)
    ring = 0.5 + 0.5 * np.sin(TAU * (U * 24 + 1.4 * fbm2(U, V, 4, 12, 701, 4)))
    streak = fbm2(U, V, 80, 6, 703, 3)
    t = np.clip(ring ** 2.0 * 0.55 + (streak - 0.5) * 0.6 + 0.2, 0, 1)
    col = mix((0.48, 0.27, 0.11), (0.32, 0.16, 0.055), t)
    wu = fbm2(U, V, 5, 5, 705, 3) - 0.5
    spot = fbm2(U + 0.15 * wu, V, 8, 18, 707, 5)
    pel = sstep(0.60, 0.68, spot) * (0.6 + 0.4 * fbm2(U, V, 40, 40, 709, 2))
    col = col + (np.asarray((0.10, 0.05, 0.02)) - col) * (0.9 * pel)[..., None]
    return col, orm(0.32 + 0.08 * t, np.zeros_like(t))


def wood_dark(U, V, seed, along_u=True):
    """Jati/suren tua: cokelat gelap kemerahan dengan serat."""
    h, w = U.shape
    q = U if along_u else V
    ring = 0.5 + 0.5 * np.sin(TAU * ((V if along_u else U) * 30 + 1.8 * fbm2(U, V, 6, 3, seed, 4)))
    streak = fbm2(U, V, 6, 60, seed + 1, 3) if along_u else fbm2(U, V, 60, 6, seed + 1, 3)
    t = np.clip(ring ** 2 * 0.5 + (streak - 0.5) * 0.6 + 0.25, 0, 1)
    return mix((0.25, 0.10, 0.045), (0.13, 0.05, 0.02), t)


def tex_jagrak():
    h, w = 640, 2048
    U, V = uvgrid(h, w)
    X = U * 2 * JW - JW; Z = V * (2 * JW * h / w)
    M, R = jagrak_design(X, Z, 0.00035)
    col = wood_dark(U, V, 801)
    cav = np.clip((blur(R, 10) - R) * 3.0, 0, 1)
    edge = 1 - np.clip((M - 0.2) / 0.6, 0, 1)
    shade = (1 - 0.65 * cav) * (1 - 0.55 * edge) * (0.92 + 0.22 * np.clip(R, 0, 1))
    col = col * shade[..., None]
    Hn = R - blur(R, 10)
    return col, orm(0.38 + 0.30 * cav + 0.2 * edge, np.zeros_like(R)), normal_from_height(Hn, 10.0)


def tex_kaki():
    h, w = 256, 1024
    U, V = uvgrid(h, w)
    H = np.full((h, w), 0.6)
    key = ["#######.", "#.....#.", "#.###.#.", "#.#.#.#.", "#.#...#.", "#.#####.", "#.......", "########"]
    K = np.array([[c == '#' for c in row] for row in key], float)[::-1]
    band = (V > 0.54) & (V < 0.84)
    iu = np.floor(((U * 7) % 1) * 8).astype(int).clip(0, 7)
    iv = np.floor((V - 0.54) / 0.30 * 8).astype(int).clip(0, 7)
    H = np.where(band, 0.25 + 0.75 * K[iv, iu], H)
    pu = (U * 10) % 1 - 0.5; pv = (V - 0.07) / 0.24
    petal = (np.abs(pu) < 0.42 * np.sqrt(np.clip(1 - (1 - pv) ** 2, 0, 1))) & (pv > 0) & (pv < 1)
    H = np.where((pv > 0) & (pv < 1), np.where(petal, 0.95 - 0.3 * np.abs(pu), 0.25), H)
    for vg in (0.05, 0.33, 0.52, 0.86):
        H -= 0.3 * np.exp(-((V - vg) / 0.01) ** 2)
    H = blur(H, 1)
    col = wood_dark(U, V, 811)
    cav = np.clip((blur(H, 6) - H) * 3.0, 0, 1)
    col = col * ((1 - 0.6 * cav) * (0.9 + 0.2 * H))[..., None]
    return col, orm(0.40 + 0.3 * cav, np.zeros_like(H)), normal_from_height(H, 4.0)


def tex_anyaman():
    """Tikar anyaman (hanya untuk render)."""
    h = w = 1024
    U, V = uvgrid(h, w)
    a = (U * 120) % 1; b = (V * 120) % 1
    cell = ((np.floor(U * 120) + np.floor(V * 120)) % 2 == 0)
    strand = np.where(cell, 0.5 + 0.5 * np.cos(TAU * (b - 0.5)), 0.5 + 0.5 * np.cos(TAU * (a - 0.5)))
    tone = fbm2(U, V, 30, 30, 901, 3)
    t = np.clip(strand * 0.6 + tone * 0.4, 0, 1)
    return mix((0.42, 0.34, 0.24), (0.80, 0.70, 0.54), t)


def write_textures():
    os.makedirs(TEX, exist_ok=True)
    out = {}
    out['pamor'], out['pamor_orm'], out['pamor_n'] = tex_pamor_banyu_tetes()
    out['emas_ukir'], out['emas_ukir_orm'], out['emas_ukir_n'] = tex_emas_ukir()
    out['pelet'], out['pelet_orm'] = tex_pelet()
    out['jagrak'], out['jagrak_orm'], out['jagrak_n'] = tex_jagrak()
    out['kaki'], out['kaki_orm'], out['kaki_n'] = tex_kaki()
    out['anyaman'] = tex_anyaman()
    out['meja'], _ = tex_jackwood((0.22, 0.12, 0.06), (0.11, 0.055, 0.025), 91)
    for name, arr in out.items():
        write_png(os.path.join(TEX, name + '.png'), arr)
    return {k: os.path.join(TEX, k + '.png') for k in out}


def gem_material(name, color):
    m = pbr(name, color=color, rough=0.04, coat=1.0, coat_rough=0.02)
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    try:
        bsdf.inputs['IOR'].default_value = 1.77
    except KeyError:
        pass
    return m


# ============================================================ render pratinjau

def render_previews(tex, root, keris, bilah, sarung, jagrak):
    sc = bpy.context.scene
    lights = studio(center=(0.06, 0.0, 0.10), scale=1.25)
    bpy.ops.mesh.primitive_plane_add(size=2.4, location=(0, 0, -0.0005))
    meja = bpy.context.active_object; meja.name = 'Meja'
    meja.data.materials.append(pbr('meja', base=tex['meja'], rough=0.55, uv_scale=(6.0, 3.0)))
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.05, -0.04, 0.0))
    tikar = bpy.context.active_object; tikar.name = 'Tikar'; tikar.scale = (0.80, 0.55, 1)
    tikar.data.materials.append(pbr('anyaman', base=tex['anyaman'], rough=0.85))
    props = [meja, tikar]
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, res, persp=None, ortho=None):
        sc.render.resolution_x, sc.render.resolution_y = res
        if ortho:
            cx, cz, size = ortho
            cam.data.type = 'ORTHO'; cam.data.ortho_scale = size
            cam.location = (cx, -2.0, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
        else:
            target, eye, lens = persp
            cam.data.type = 'PERSP'; cam.data.lens = lens; cam.location = eye; look(cam, target)
        sc.render.filepath = os.path.join(DOCS, f'KerisBali_Blender_{name}.png')
        bpy.ops.render.render(write_still=True)
        print('Render:', sc.render.filepath)

    keris_home = keris.matrix_world.copy()
    shot('set', (1600, 900), persp=((0.06, 0.0, 0.10), (0.08, -0.95, 0.34), 50))
    shot('hulu', (1200, 1000), persp=((KERIS_X + 0.078, 0.0, AXIS_Z), (KERIS_X + 0.05, -0.23, AXIS_Z + 0.06), 60))

    # Hero seperti foto referensi: bilah terhunus bersandar di jagrak, sarung tergeletak di depan.
    wil = [o for o in bilah.children if o.name in ('Wilah',)][0]
    keris.matrix_world = Matrix.Translation((0.185, 0, AXIS_Z)) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
    sarung_home = sarung.matrix_basis.copy()
    sarung.matrix_world = (Matrix.Translation((0.25, -0.20, 0.0152)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
                           @ Matrix.Rotation(math.pi / 2, 4, 'X'))
    bpy.context.view_layer.update()
    lift = 0.0
    for px, top in ((POST_X, POST_TOP_R), (-POST_X, POST_TOP_L)):
        zs = [(wil.matrix_world @ v.co).z for v in wil.data.vertices if abs((wil.matrix_world @ v.co).x - px) < 0.009]
        if zs:
            lift = max(lift, BASE_TOP + top - min(zs))
    keris.location.z += lift
    bpy.context.view_layer.update()
    shot('hero', (1600, 900), persp=((0.07, -0.08, 0.09), (0.12, -0.92, 0.40), 45))

    # Bilah dan sarung berdiri (tanpa jagrak) untuk memperlihatkan pamor dan pendok.
    for p in props + [jagrak] + list(jagrak.children):
        p.hide_render = True
    keris.matrix_world = Matrix.Identity(4)
    sarung.matrix_basis = sarung_home
    bilah.location = (0.24, 0, 0)
    shot('dihunus', (1200, 1300), ortho=(0.09, -0.15, 0.68))
    bilah.location = (0, 0, 0)
    for p in props + [jagrak] + list(jagrak.children):
        p.hide_render = False
    keris.matrix_world = keris_home
    for p in props:
        p.hide_render = True
    return lights + props + [cam]


# ============================================================ utama

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    sc = bpy.context.scene
    sc.name = 'Keris Bali'
    sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0

    tex = write_textures()
    M = {
        'pamor': pbr('pamor_banyu_tetes', tex['pamor'], tex['pamor_orm'], tex['pamor_n'], nstrength=0.4),
        'emas_ukir': pbr('emas_ukir', tex['emas_ukir'], tex['emas_ukir_orm'], tex['emas_ukir_n']),
        'emas_poles': pbr('emas_poles', color=(1.0, 0.71, 0.29), metallic=1.0, rough=0.24),
        'pelet': pbr('kayu_pelet', tex['pelet'], tex['pelet_orm'], coat=0.35, coat_rough=0.15),
        'jagrak': pbr('jati_ukir', tex['jagrak'], tex['jagrak_orm'], tex['jagrak_n'], nstrength=0.8),
        'kaki': pbr('jati_kaki', tex['kaki'], tex['kaki_orm'], tex['kaki_n']),
        'celah': pbr('celah_gelap', color=(0.02, 0.012, 0.008), rough=0.9),
    }
    gems = [gem_material('permata_ruby', (0.50, 0.005, 0.025)),
            gem_material('permata_zamrud', (0.005, 0.36, 0.08)),
            gem_material('permata_safir', (0.012, 0.06, 0.55))]

    def empty(name, parent=None):
        e = bpy.data.objects.new(name, None); e.empty_display_type = 'PLAIN_AXES'; e.empty_display_size = 0.05
        sc.collection.objects.link(e); e.parent = parent
        return e

    root = empty('Keris_Bali')
    jagrak = empty('Jagrak', root)
    keris = empty('Keris', root)
    bilah = empty('Bilah', keris)
    sarung = empty('Sarung', keris)

    objs = {}
    for name, mb, mat, parent, recalc in [
        ('Wilah', part_wilah(), M['pamor'], bilah, True),
        ('Ganja', part_ganja(), M['emas_ukir'], bilah, True),
        ('Selut', part_selut(), M['emas_ukir'], bilah, True),
        ('Permata_Selut', part_permata_selut(), gems, bilah, True),
        ('Permata_Hulu', part_permata_hulu(), gems[:1], bilah, True),
        ('Warangka_Sesrengatan', part_sampir(), M['pelet'], sarung, True),
        ('Warangka_Celah', part_celah(), M['celah'], sarung, False),
        ('Gandar', part_gandar(), M['pelet'], sarung, True),
        ('Cincin', part_cincin(), M['emas_ukir'], sarung, True),
        ('Permata_Warangka', part_permata_warangka(), gems, sarung, True),
        ('Pendok', part_pendok(), M['emas_ukir'], sarung, True),
        ('Jagrak_Ukiran', part_jagrak_panel(), M['jagrak'], jagrak, True),
        ('Jagrak_Kaki', part_kaki(), M['kaki'], jagrak, True),
    ]:
        objs[name] = build(name, mb, mat, parent, sharp_deg=75.0 if name == 'Jagrak_Ukiran' else 55.0, recalc=recalc)
        print('  bagian:', name)
    decimate(objs['Jagrak_Ukiran'], 45000)
    objs['Hulu'] = hulu_figure(bilah, M['emas_poles'])

    # Keris mendatar di atas jagrak: sumbu lokal +Z -> dunia +X, muka depan tetap -Y.
    keris.matrix_world = Matrix.Translation((KERIS_X, 0, AXIS_Z)) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
    bpy.context.view_layer.update()

    total = 0
    print('\n== Ukuran bagian (m, ruang lokal induk) ==')
    for name, ob in objs.items():
        co = np.array([v.co for v in ob.data.vertices])
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons); total += tris
        lo, hi = co.min(0), co.max(0)
        print(f'{name:18s} x[{lo[0]:+.4f},{hi[0]:+.4f}] y[{lo[1]:+.4f},{hi[1]:+.4f}] z[{lo[2]:+.4f},{hi[2]:+.4f}]'
              f'  ukuran {(hi - lo)[0] * 100:.1f} x {(hi - lo)[1] * 100:.1f} x {(hi - lo)[2] * 100:.1f} cm  tris {tris}')
    blade = np.array([v.co for v in objs['Wilah'].data.vertices])
    print(f'Panjang bilah (pangkal -> pucuk): {-blade[:, 2].min() * 1000:.1f} mm')
    print(f'Sumbu keris di atas meja: {AXIS_Z * 1000:.1f} mm; tiang kanan/kiri {POST_TOP_R * 1000:.1f}/{POST_TOP_L * 1000:.1f} mm')
    print(f'Total segitiga: {total}')

    sc.frame_start, sc.frame_end = 1, 48
    for f, z in ((1, 0.0), (10, 0.012), (48, DRAW_Z)):
        bilah.location = (0, 0, z); bilah.keyframe_insert('location', frame=f)
    bilah.animation_data.action.name = 'Cabut_Keris'
    sc.frame_set(1)

    os.makedirs(ART, exist_ok=True)
    export_glb(root, GLB)
    print('GLB:', GLB, f'({os.path.getsize(GLB) / 1e6:.2f} MB)')
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print('BLEND:', BLEND)
    if RENDER:
        act = bilah.animation_data.action
        bilah.animation_data.action = None
        render_previews(tex, root, keris, bilah, sarung, jagrak)
        bilah.animation_data.action = act
        sc.frame_set(1)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
        print('Render pratinjau: Docs/KerisBali_Blender_*.png')


if __name__ == '__main__':
    main()
