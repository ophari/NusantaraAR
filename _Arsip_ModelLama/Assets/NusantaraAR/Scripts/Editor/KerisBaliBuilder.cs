using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Bali prosedural dari lembar acuan (cetak biru): hulu figur bergaya Ganesha (abu-abu), mendak emas
    /// berpermata merah, bilah 11 luk berpamor, ganja, warangka kayu berbentuk perahu, gandar kayu, dan pendok
    /// emas berukir dengan panel gelap + roset. Semua mesh, tekstur, dan material dibuat ulang setiap kali
    /// dijalankan (idempoten; GUID aset dipertahankan).
    /// Skala 1:1 (1 unit = 1 m), pivot di dasar dudukan, muka menghadap -Z.
    /// Menu: Nusantara AR / Build Keris Bali (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KerisBaliBuilder
    {
        public const string Id = "KERIS_BALI_01";
        const string Root = "Assets/NusantaraAR";
        const string ArtDir = Root + "/Art/KerisBali";
        const string MeshDir = ArtDir + "/Meshes";
        const string TexDir = ArtDir + "/Textures";
        const string MatDir = ArtDir + "/Materials";
        const string ContentDir = Root + "/Content/" + Id;
        const string PrefabPath = ContentDir + "/" + Id + ".prefab";
        public const string DataPath =ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";
        const string CatalogPath = Root + "/Resources/ContentCatalog.asset";

        /// <summary>Asumsi panjang bilah (lembar acuan tidak mencantumkan ukuran).</summary>
        public const float BladeLength = 0.42f;

        /// <summary>Pangkal bilah (sisi bawah ganja) saat tersarung / saat dihunus, ruang Model.</summary>
        static readonly Vector3 BladeHome = new Vector3(0f, 0.499f, 0f);
        static readonly Vector3 BladeDrawn = new Vector3(0.2f, 0.499f, 0f);
        static readonly Vector3 WarangkaHome = new Vector3(0f, 0.463f, 0f);

        [MenuItem("Nusantara AR/Build Keris Bali")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Keris Bali selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            foreach (var d in new[] { MeshDir, TexDir, MatDir, ContentDir, Root + "/Resources" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            var mats = BuildMaterials();
            var prefab = BuildPrefab(mats, out var hotspots);

            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ArtifactData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            KerisBaliContent.Fill(data, prefab, hotspots);
            EditorUtility.SetDirty(data);

            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (!catalog.artifacts.Contains(data)) catalog.artifacts.Add(data);
            catalog.artifacts.RemoveAll(a => a == null);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return data;
        }

        // ------------------------------------------------------------------ Tekstur prosedural

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static int Wrap(int a, int m) => ((a % m) + m) % m;

        /// <summary>Value noise periodik (periode px x py sel) agar tekstur bisa diulang tanpa sambungan.</summary>
        static float ValueNoise(float x, float y, int px, int py, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(ix, px), x1 = Wrap(ix + 1, px), y0 = Wrap(iy, py), y1 = Wrap(iy + 1, py);
            float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
            float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float u, float v, int px, int py, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int f = 1 << o;
                sum += amp * ValueNoise(u * px * f, v * py * f, px * f, py * f, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static float Smooth(float e0, float e1, float x) => ProcMesh.Smooth(e0, e1, x);

        /// <summary>Garis pamor terang (0..1): pita bergelombang searah bilah.</summary>
        static float PamorLight(float u, float v)
        {
            float w = Fbm(u, v, 4, 12, 11);
            float w2 = Fbm(u, v, 8, 24, 12);
            float band = Mathf.Sin((u * 26f + w * 7f + w2 * 1.5f) * Mathf.PI);
            return Smooth(0.35f, 0.95f, band * 0.5f + 0.5f);
        }

        static Color PamorColor(float u, float v)
        {
            float l = PamorLight(u, v);
            float grain = 0.9f + 0.2f * Fbm(u, v, 16, 64, 13, 3);
            var c = Color.Lerp(new Color(0.13f, 0.13f, 0.14f), new Color(0.70f, 0.70f, 0.68f), l) * grain;
            c.a = 1f;
            return c;
        }

        static Color PamorMs(float u, float v)
        {
            float l = PamorLight(u, v);
            return new Color(Mathf.Lerp(0.8f, 0.95f, l), 0f, 0f, Mathf.Lerp(0.45f, 0.75f, l));
        }

        static Color TeakColor(float u, float v)
        {
            float n = Fbm(u, v, 4, 2, 21);
            float g = Mathf.Sin((u * 48f + n * 5f) * Mathf.PI * 2f);
            float line = Smooth(0.55f, 1f, g * 0.5f + 0.5f);
            float fleck = Fbm(u, v, 32, 8, 22, 3);
            var c = Color.Lerp(new Color(0.56f, 0.34f, 0.17f), new Color(0.34f, 0.19f, 0.09f), line * 0.8f + fleck * 0.25f);
            c.a = 1f;
            return c;
        }

        /// <summary>Alur ukiran (0 = di dalam alur, 1 = permukaan).</summary>
        static float Engraving(float u, float v)
        {
            float w = Fbm(u, v, 3, 3, 31);
            float a = Mathf.Abs(Mathf.Sin((u * 4f + v * 2f + w * 3f) * Mathf.PI));
            float b = Mathf.Abs(Mathf.Sin((u * 2f - v * 4f + w * 3f) * Mathf.PI));
            return Smooth(0f, 0.18f, a * b);
        }

        static Color GoldColor(float u, float v)
        {
            var gold = new Color(1f, 0.80f, 0.42f);
            var c = Color.Lerp(gold * 0.42f, gold, Engraving(u, v));
            c.a = 1f;
            return c;
        }

        static Color GoldMs(float u, float v) => new Color(1f, 0f, 0f, Mathf.Lerp(0.35f, 0.82f, Engraving(u, v)));

        static Texture2D SaveTexture(string name, int w, int h, Func<float, float, Color> f, bool linear)
        {
            string path = TexDir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = !linear;
            imp.alphaSource = linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Material

        class Mats
        {
            public Material pamor, jati, kayuGelap, emas, emasUkir, hulu, permata;
        }

        static Material Lit(string name) =>
            ProjectSetup.GetOrCreateMaterial(MatDir + "/M_" + Id + "_" + name + ".mat", "Universal Render Pipeline/Lit");

        static void SetMaps(Material m, Texture2D albedo, Texture2D ms, Color tint, float metallic, float smoothness)
        {
            m.SetTexture("_BaseMap", albedo);
            m.SetColor("_BaseColor", tint);
            if (ms != null)
            {
                // Konvensi URP Lit: Metallic (R) + Smoothness (A).
                m.SetTexture("_MetallicGlossMap", ms);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else
            {
                m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", smoothness);
            }
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
        }

        static Mats BuildMaterials()
        {
            var pamor = SaveTexture("pamor", 256, 1024, PamorColor, false);
            var pamorMs = SaveTexture("pamor_ms", 256, 1024, PamorMs, true);
            var jati = SaveTexture("jati", 512, 512, TeakColor, false);
            var gold = SaveTexture("emas_ukir", 256, 256, GoldColor, false);
            var goldMs = SaveTexture("emas_ukir_ms", 256, 256, GoldMs, true);

            var m = new Mats
            {
                pamor = Lit("Pamor"),
                jati = Lit("Jati"),
                kayuGelap = Lit("KayuGelap"),
                emas = Lit("Emas"),
                emasUkir = Lit("EmasUkir"),
                hulu = Lit("Hulu"),
                permata = Lit("Permata"),
            };
            SetMaps(m.pamor, pamor, pamorMs, Color.white, 0f, 0f);
            SetMaps(m.jati, jati, null, Color.white, 0f, 0.35f);
            SetMaps(m.kayuGelap, jati, null, new Color(0.35f, 0.35f, 0.35f), 0f, 0.3f);
            SetMaps(m.emas, null, null, new Color(1f, 0.77f, 0.34f), 1f, 0.78f);
            SetMaps(m.emasUkir, gold, goldMs, Color.white, 0f, 0f);
            SetMaps(m.hulu, null, null, new Color(0.56f, 0.56f, 0.56f), 0.6f, 0.42f);
            var red = new Color(0.78f, 0.04f, 0.06f);
            SetMaps(m.permata, null, null, red, 0f, 0.94f);
            m.permata.SetColor("_EmissionColor", red * 0.6f);
            m.permata.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(m.permata);
            return m;
        }

        // ------------------------------------------------------------------ Geometri (meter)

        static Vector2 V2(float x, float y) => new Vector2(x, y);

        static ProcMesh E(Vector3 c, Vector3 r) => ProcMesh.Ellipsoid(c, r, Quaternion.identity, 12, 8);
        static ProcMesh E(Vector3 c, Vector3 r, Quaternion rot) => ProcMesh.Ellipsoid(c, r, rot, 12, 8);

        /// <summary>Memadatkan baris sweep di awal (bagian membulat) tanpa menambah baris di bagian lurus.</summary>
        static float Warp(float s, float edge, float share) =>
            s < share ? edge * s / share : edge + (s - share) / (1f - share) * (1f - edge);

        /// <summary>Setengah lebar (x) dan setengah tebal (z) gandar pada ketinggian y.</summary>
        static Vector2 GandarSize(float y)
        {
            float s = Mathf.Clamp01((y - 0.035f) / 0.44f);
            return new Vector2(Mathf.Lerp(0.02f, 0.031f, s), Mathf.Lerp(0.0085f, 0.0115f, s));
        }

        static ProcMesh Dudukan()
        {
            var slab = ProcMesh.Sweep(12, 32, s => new Vector3(0f, 0.03f * s, 0f), s =>
            {
                float inset = 0.003f * Mathf.Pow(1f - Mathf.Sin(Mathf.PI * s), 3f);
                return new Vector3(0.08f - inset, 0.08f - inset, 0.05f - inset);
            }, ProcMesh.Super(4f));
            var collar = ProcMesh.Lathe(new List<Vector2>
            {
                V2(0.030f, 0.029f), V2(0.034f, 0.031f), V2(0.035f, 0.036f), V2(0.031f, 0.046f), V2(0.027f, 0.052f), V2(0.0262f, 0.056f)
            }, 32, 1f, 0.6f);
            return slab.Append(collar);
        }

        static ProcMesh Gandar()
        {
            const float y0 = 0.035f, y1 = 0.475f;
            return ProcMesh.Sweep(60, 28, s => new Vector3(0f, Mathf.Lerp(y0, y1, Warp(s, 0.045f, 0.2f)), 0f), s =>
            {
                float q = Warp(s, 0.045f, 0.2f);
                var g = GandarSize(Mathf.Lerp(y0, y1, q));
                float k = Mathf.Clamp01(q / 0.045f);
                float round = Mathf.Sqrt(1f - (1f - k) * (1f - k)); // ujung bawah membulat
                return new Vector3(g.x * round, g.x * round, g.y * round);
            }, ProcMesh.Super(2.4f));
        }

        static ProcMesh Sleeve(float y0, float y1, float pad, float bulge)
        {
            int rows = Mathf.Max(4, Mathf.CeilToInt((y1 - y0) / 0.006f));
            return ProcMesh.Sweep(rows, 28, s => new Vector3(0f, Mathf.Lerp(y0, y1, s), 0f), s =>
            {
                var g = GandarSize(Mathf.Lerp(y0, y1, s));
                float p = pad + bulge * Mathf.Sin(Mathf.PI * s);
                return new Vector3(g.x + p, g.x + p, g.y + p);
            }, ProcMesh.Super(2.4f));
        }

        static ProcMesh PendokGold()
        {
            var m = Sleeve(0.085f, 0.462f, 0.0012f, 0f);
            m.Append(Sleeve(0.085f, 0.095f, 0.0017f, 0.001f));
            m.Append(Sleeve(0.448f, 0.462f, 0.0017f, 0.001f));
            return m;
        }

        const float PanelY0 = 0.125f, PanelY1 = 0.425f;

        /// <summary>Setengah lebar dan setengah tebal panel ukir gelap pada ketinggian y.</summary>
        static Vector2 PanelSize(float y)
        {
            float s = Mathf.Clamp01((y - PanelY0) / (PanelY1 - PanelY0));
            float sn = Mathf.Max(0f, Mathf.Sin(Mathf.PI * s)); // sin(π) di float sedikit negatif -> Pow(x, 0.5) = NaN
            return new Vector2(0.012f * Mathf.Pow(sn, 0.5f), 0.0018f * Mathf.Pow(sn, 0.3f));
        }

        // Sedikit masuk ke selongsong agar tepi panel (tebal 0) tidak melayang di depan permukaan emas yang melengkung.
        static float PanelCenterZ(float y) => -(GandarSize(y).y + 0.0006f);
        static float PanelFrontZ(float y) => PanelCenterZ(y) - PanelSize(y).y;

        /// <summary>Tambahkan salinan cermin ke sisi belakang (z dibalik).</summary>
        static ProcMesh FrontAndBack(ProcMesh front) => front.Copy().Append(front.Mirrored(new Vector3(1f, 1f, -1f)));

        static ProcMesh PendokPanel()
        {
            var front = ProcMesh.Sweep(60, 16, s =>
            {
                float y = Mathf.Lerp(PanelY0, PanelY1, s);
                return new Vector3(0f, y, PanelCenterZ(y));
            }, s =>
            {
                var p = PanelSize(Mathf.Lerp(PanelY0, PanelY1, s));
                return new Vector3(p.x, p.x, p.y);
            }, ProcMesh.Super(4f), (s, arc, j, off) => new Vector2(0.5f + off.x / 0.03f, arc / 0.1f));
            return FrontAndBack(front);
        }

        const float RosetteY = 0.27f;

        static ProcMesh PendokRosette()
        {
            var m = new ProcMesh();
            var c = new Vector3(0f, RosetteY, PanelFrontZ(RosetteY));
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var rot = Quaternion.Euler(0f, 0f, a);
                m.Append(ProcMesh.Ellipsoid(c + rot * Vector3.right * 0.0042f, new Vector3(0.0045f, 0.0022f, 0.0012f), rot, 10, 6));
            }
            m.Append(ProcMesh.Ellipsoid(c + new Vector3(0f, 0f, -0.0008f), new Vector3(0.0026f, 0.0026f, 0.0016f), Quaternion.identity, 12, 7));

            // Sulur ke atas dan ke bawah roset: tangkai berkelok + daun berselang-seling.
            foreach (float dir in new[] { 1f, -1f })
            {
                var pts = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float y = RosetteY + dir * Mathf.Lerp(0.02f, 0.12f, i / 6f);
                    pts.Add(new Vector3(0.0022f * Mathf.Sin(i * 1.3f), y, PanelFrontZ(y) - 0.0002f));
                }
                m.Append(ProcMesh.Tube(pts, s => Mathf.Lerp(0.0009f, 0.0005f, s), 36, 8));
                for (int i = 0; i < 6; i++)
                {
                    float y = RosetteY + dir * Mathf.Lerp(0.03f, 0.11f, i / 5f);
                    float side = i % 2 == 0 ? 1f : -1f;
                    float room = Mathf.Clamp01(PanelSize(y).x / 0.008f);
                    var p = new Vector3(side * 0.0035f * room, y, PanelFrontZ(y) - 0.0002f);
                    m.Append(ProcMesh.Ellipsoid(p, new Vector3(0.003f, 0.0014f, 0.0007f) * Mathf.Max(0.5f, room),
                        Quaternion.Euler(0f, 0f, side * dir * 35f), 8, 5));
                }
            }
            return FrontAndBack(m);
        }

        /// <summary>Tulang warangka (ruang lokal, pivot = WarangkaHome): lengkung pendek di kiri, tanduk panjang naik di kanan.</summary>
        static readonly Vector3[] WarangkaSpine =
        {
            new Vector3(-0.052f, 0.058f, 0f), new Vector3(-0.058f, 0.045f, 0f), new Vector3(-0.05f, 0.03f, 0f),
            new Vector3(-0.035f, 0.02f, 0f), new Vector3(0f, 0.018f, 0f), new Vector3(0.05f, 0.02f, 0f),
            new Vector3(0.08f, 0.03f, 0f), new Vector3(0.10f, 0.05f, 0f), new Vector3(0.11f, 0.075f, 0f)
        };

        static ProcMesh Warangka()
        {
            Func<float, Vector3> spine = s => ProcMesh.CatmullRom(WarangkaSpine, s);
            Func<float, float, float, float, float> two = (s, a, mid, b) =>
                s < 0.5f ? Mathf.Lerp(a, mid, Smooth(0f, 1f, s / 0.5f)) : Mathf.Lerp(mid, b, Smooth(0f, 1f, (s - 0.5f) / 0.5f));
            var m = ProcMesh.Sweep(120, 24, spine, s =>
            {
                float hh = two(s, 0.008f, 0.018f, 0.0035f);
                float th = two(s, 0.007f, 0.017f, 0.004f);
                return new Vector3(hh, hh, th);
            }, ProcMesh.Super(2.5f), (s, arc, j, off) => new Vector2(j, arc / 0.1f));
            m.Append(ProcMesh.Ellipsoid(spine(0f), new Vector3(0.0085f, 0.0085f, 0.0075f), Quaternion.identity, 14, 9));
            m.Append(ProcMesh.Ellipsoid(spine(1f), new Vector3(0.0042f, 0.0042f, 0.0042f), Quaternion.identity, 12, 8));
            return m;
        }

        /// <summary>Simpangan luk (11 luk) sebagai fungsi u = 0 (pangkal) .. 1 (ujung).</summary>
        static float LukOffset(float u)
        {
            float t = Mathf.Clamp01((u - 0.1f) / 0.8f);
            float a = 0.0075f * Smooth(0f, 0.08f, t) * (1f - 0.45f * t);
            return a * Mathf.Sin(11f * Mathf.PI * t);
        }

        /// <summary>Garis tengah bilah dalam ruang lokal bilah (pangkal di 0, bilah menggantung ke -Y).</summary>
        static Vector3 BladeCenter(float u) => new Vector3(0.003f * (1f - u) + LukOffset(u), -BladeLength * u, 0f);

        /// <summary>(setengah lebar sisi +X/belakang, setengah lebar sisi -X/gandik, setengah tebal).</summary>
        static Vector3 BladeSize(float u)
        {
            float body = 0.018f * Mathf.Pow(1f - u, 0.55f);
            float k = 1f - Smooth(0f, 0.13f, u); // sor-soran melebar ke arah ganja
            float aPos = Mathf.Lerp(body, 0.034f, k);
            float aNeg = Mathf.Lerp(body, 0.030f, k);
            if (u > 0.015f && u < 0.1f)
            {
                float w = (u - 0.015f) / 0.085f; // greneng: tiga takik di sisi belakang
                aPos -= 0.0045f * Mathf.Pow(Mathf.Abs(Mathf.Sin(w * 3f * Mathf.PI)), 0.6f);
            }
            float b = 0.005f * (1f - 0.6f * u) * Mathf.Pow(1f - u, 0.3f);
            return new Vector3(aPos, aNeg, b);
        }

        static ProcMesh Wilah()
        {
            var m = ProcMesh.Sweep(160, 20, BladeCenter, BladeSize, ProcMesh.Lens,
                (s, arc, j, off) => new Vector2(0.5f + off.x / 0.08f, s), true, false);
            m.Append(ProcMesh.Limb(Vector3.zero, new Vector3(0f, 0.07f, 0f), 0.0035f, 0.0025f)); // pesi
            return m;
        }

        static ProcMesh Ganja()
        {
            const float x0 = -0.033f, x1 = 0.05f;
            Func<float, float> half = s => Mathf.Lerp(0.0075f, 0.0022f, Smooth(0.35f, 1f, s));
            Func<float, float> up = s =>
            {
                float x = Mathf.Lerp(x0, x1, s);
                return x > 0.03f ? 15f * (x - 0.03f) * (x - 0.03f) : 0f; // ekor melengkung naik
            };
            return ProcMesh.Sweep(80, 20, s =>
            {
                float q = Warp(s, 0.05f, 0.15f);
                return new Vector3(Mathf.Lerp(x0, x1, q), half(q) + up(q), 0f);
            }, s =>
            {
                float q = Warp(s, 0.05f, 0.15f);
                float k = Mathf.Clamp01(q / 0.05f);
                float round = Mathf.Sqrt(1f - (1f - k) * (1f - k)); // kepala membulat
                float h = half(q) * round;
                return new Vector3(h, h, Mathf.Lerp(0.0055f, 0.003f, q) * Mathf.Sqrt(round));
            }, ProcMesh.Super(3f), (s, arc, j, off) => new Vector2(j, s * 0.3f));
        }

        static ProcMesh MendakGold()
        {
            var m = ProcMesh.Lathe(new List<Vector2>
            {
                V2(0.008f, 0.015f), V2(0.010f, 0.0158f), V2(0.0122f, 0.0185f), V2(0.0128f, 0.0225f),
                V2(0.0122f, 0.0262f), V2(0.010f, 0.028f), V2(0.0082f, 0.029f)
            }, 32);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                m.Append(ProcMesh.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.0105f, 0.0278f, Mathf.Sin(a) * 0.0105f),
                    Vector3.one * 0.0011f, Quaternion.identity, 8, 5));
            }
            return m;
        }

        static ProcMesh MendakGems()
        {
            var m = new ProcMesh();
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                m.Append(ProcMesh.Ellipsoid(new Vector3(0f, 0.0225f, 0f) + dir * 0.0129f, new Vector3(0.0019f, 0.0019f, 0.0012f),
                    Quaternion.LookRotation(dir), 10, 6));
            }
            return m;
        }

        /// <summary>Hulu figur bergaya Ganesha duduk di atas teratai, dengan prabha di belakang (satu mesh abu-abu).</summary>
        static ProcMesh Hulu()
        {
            var m = new ProcMesh();
            var mirrorX = new Vector3(-1f, 1f, 1f);

            // Alas teratai
            m.Append(ProcMesh.Lathe(new List<Vector2>
            {
                V2(0.0082f, 0.029f), V2(0.0115f, 0.0305f), V2(0.015f, 0.034f), V2(0.0172f, 0.0385f), V2(0.0178f, 0.0425f), V2(0.0165f, 0.045f)
            }, 28));
            for (int i = 0; i < 12; i++)
            {
                var dir = Quaternion.Euler(0f, (i + 0.5f) * 30f, 0f) * Vector3.forward;
                m.Append(ProcMesh.Ellipsoid(new Vector3(0f, 0.0375f, 0f) + dir * 0.0168f, new Vector3(0.0042f, 0.0062f, 0.0016f),
                    Quaternion.LookRotation(dir) * Quaternion.Euler(18f, 0f, 0f), 10, 6));
            }

            // Pangkuan dan kaki bersila
            m.Append(E(new Vector3(0f, 0.051f, -0.001f), new Vector3(0.0165f, 0.0075f, 0.0125f)));
            var leg = ProcMesh.Limb(new Vector3(-0.012f, 0.049f, 0.002f), new Vector3(0.004f, 0.0485f, -0.0105f), 0.0045f, 0.0037f);
            leg.Append(E(new Vector3(0.0065f, 0.047f, -0.0125f), new Vector3(0.0042f, 0.0024f, 0.0056f), Quaternion.Euler(0f, -30f, 0f)));
            m.Append(leg);
            m.Append(leg.Mirrored(mirrorX).Move(new Vector3(0f, 0.003f, -0.0015f)));

            // Badan
            m.Append(E(new Vector3(0f, 0.066f, -0.003f), new Vector3(0.0125f, 0.013f, 0.0115f))); // perut
            m.Append(E(new Vector3(0f, 0.083f, 0f), new Vector3(0.0125f, 0.009f, 0.009f)));       // dada
            foreach (float sx in new[] { 1f, -1f })
            {
                var shoulder = new Vector3(sx * 0.012f, 0.087f, 0f);
                m.Append(E(shoulder, Vector3.one * 0.0045f));
                // Tangan depan di pangkuan
                var elbow = new Vector3(sx * 0.0165f, 0.075f, -0.006f);
                var hand = new Vector3(sx * 0.008f, 0.069f, -0.0145f);
                m.Append(ProcMesh.Limb(shoulder + new Vector3(0f, 0f, -0.001f), elbow, 0.0033f, 0.0029f));
                m.Append(ProcMesh.Limb(elbow, hand, 0.0029f, 0.0025f));
                m.Append(E(hand, new Vector3(0.0032f, 0.0028f, 0.0026f)));
                // Tangan belakang terangkat
                var bElbow = new Vector3(sx * 0.0225f, 0.097f, 0.004f);
                var bHand = new Vector3(sx * 0.019f, 0.107f, 0.004f);
                m.Append(ProcMesh.Limb(shoulder + new Vector3(0f, 0.001f, 0.003f), bElbow, 0.003f, 0.0026f));
                m.Append(ProcMesh.Limb(bElbow, bHand, 0.0026f, 0.0023f));
                m.Append(E(bHand, Vector3.one * 0.0028f));
            }
            m.Append(E(new Vector3(0.0075f, 0.0735f, -0.0155f), Vector3.one * 0.0032f)); // modaka di tangan kiri figur

            // Kapak (+x) dan kuncup teratai (-x) di tangan belakang
            m.Append(ProcMesh.Limb(new Vector3(0.019f, 0.101f, 0.004f), new Vector3(0.019f, 0.123f, 0.004f), 0.0011f, 0.0011f));
            m.Append(E(new Vector3(0.0238f, 0.1195f, 0.004f), new Vector3(0.0048f, 0.004f, 0.0011f)));
            m.Append(ProcMesh.Limb(new Vector3(-0.019f, 0.108f, 0.004f), new Vector3(-0.019f, 0.111f, 0.003f), 0.0009f, 0.0009f));
            m.Append(E(new Vector3(-0.019f, 0.1135f, 0.003f), new Vector3(0.0028f, 0.0045f, 0.0028f)));

            // Leher, kepala, dahi, telinga, mata
            m.Append(E(new Vector3(0f, 0.093f, -0.001f), new Vector3(0.0075f, 0.006f, 0.0075f)));
            m.Append(E(new Vector3(0f, 0.105f, -0.002f), Vector3.one * 0.0105f));
            var bump = E(new Vector3(0.0045f, 0.110f, -0.0105f), Vector3.one * 0.0035f);
            m.Append(bump);
            m.Append(bump.Mirrored(mirrorX));
            var ear = E(new Vector3(0.0135f, 0.104f, 0.002f), new Vector3(0.0085f, 0.0105f, 0.0022f), Quaternion.Euler(0f, -25f, 0f));
            m.Append(ear);
            m.Append(ear.Mirrored(mirrorX));
            var eye = E(new Vector3(0.0045f, 0.1065f, -0.0118f), new Vector3(0.0014f, 0.001f, 0.0008f));
            m.Append(eye);
            m.Append(eye.Mirrored(mirrorX));

            // Belalai melengkung ke modaka
            m.Append(ProcMesh.Tube(new List<Vector3>
            {
                new Vector3(0f, 0.101f, -0.0115f), new Vector3(0f, 0.095f, -0.0138f), new Vector3(0.0008f, 0.088f, -0.0152f),
                new Vector3(0.0035f, 0.0815f, -0.0163f), new Vector3(0.0085f, 0.077f, -0.0165f)
            }, s => Mathf.Lerp(0.0046f, 0.0022f, s), 30, 10));

            // Gading: utuh di +x, patah (pendek) di sisi kanan figur (-x)
            m.Append(ProcMesh.Limb(new Vector3(0.0048f, 0.099f, -0.0105f), new Vector3(0.0085f, 0.0945f, -0.0148f), 0.0013f, 0.0006f));
            m.Append(ProcMesh.Limb(new Vector3(-0.0048f, 0.099f, -0.0105f), new Vector3(-0.006f, 0.0972f, -0.0128f), 0.0013f, 0.0011f));

            // Mahkota
            m.Append(ProcMesh.Lathe(new List<Vector2>
            {
                V2(0.0098f, 0.110f), V2(0.0102f, 0.1135f), V2(0.0088f, 0.1195f), V2(0.0094f, 0.1235f),
                V2(0.0072f, 0.1295f), V2(0.0062f, 0.1335f), V2(0.0036f, 0.139f), V2(0.0012f, 0.1415f)
            }, 24).Move(new Vector3(0f, 0f, -0.0015f)));

            // Prabha (sandaran) dan ujung api
            m.Append(ProcMesh.Ellipsoid(new Vector3(0f, 0.09f, 0.0075f), new Vector3(0.0165f, 0.036f, 0.006f), Quaternion.identity, 20, 12));
            m.Append(E(new Vector3(0f, 0.128f, 0.0075f), new Vector3(0.0045f, 0.01f, 0.0045f)));
            return m;
        }

        // ------------------------------------------------------------------ Prefab

        static Mesh SaveMesh(string name, ProcMesh pm)
        {
            string path = MeshDir + "/" + Id + "_" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.name = Id + "_" + name;
            pm.WriteTo(mesh);
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static ArtifactPart MakePart(Transform model, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(model, false);
            go.transform.localPosition = localPosition;
            var p = go.AddComponent<ArtifactPart>();
            p.partName = name;
            p.renderers = new Renderer[0];
            return p;
        }

        static void AddRenderer(ArtifactPart part, string name, ProcMesh pm, Material mat)
        {
            var mesh = SaveMesh(name, pm);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(part.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            part.renderers = part.renderers.Concat(new Renderer[] { r }).ToArray();
        }

        static GameObject BuildPrefab(Mats mats, out Dictionary<string, (string part, Vector3 local)> hotspots)
        {
            var root = new GameObject(Id);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            var dudukan = MakePart(model, "Dudukan", Vector3.zero);
            AddRenderer(dudukan, "dudukan", Dudukan(), mats.kayuGelap);
            var gandar = MakePart(model, "Gandar", Vector3.zero);
            AddRenderer(gandar, "gandar", Gandar(), mats.jati);
            var pendok = MakePart(model, "Pendok", Vector3.zero);
            AddRenderer(pendok, "pendok_emas", PendokGold(), mats.emasUkir);
            AddRenderer(pendok, "pendok_panel", PendokPanel(), mats.kayuGelap);
            AddRenderer(pendok, "pendok_roset", PendokRosette(), mats.emas);
            var warangka = MakePart(model, "Warangka", WarangkaHome);
            AddRenderer(warangka, "warangka", Warangka(), mats.jati);
            var wilah = MakePart(model, "Wilah", BladeHome);
            AddRenderer(wilah, "wilah", Wilah(), mats.pamor);
            var ganja = MakePart(model, "Ganja", BladeHome);
            AddRenderer(ganja, "ganja", Ganja(), mats.pamor);
            var mendak = MakePart(model, "Mendak", BladeHome);
            AddRenderer(mendak, "mendak", MendakGold(), mats.emas);
            AddRenderer(mendak, "mendak_permata", MendakGems(), mats.permata);
            var hulu = MakePart(model, "Hulu", BladeHome);
            AddRenderer(hulu, "hulu", Hulu(), mats.hulu);

            // Tahap exploded view (PRD FR-07): hunus -> lepas hulu + mendak -> lepas ganja -> lepas warangka -> lepas pendok.
            var parts = new Dictionary<string, ArtifactPart>
            {
                ["Wilah"] = wilah, ["Ganja"] = ganja, ["Mendak"] = mendak, ["Hulu"] = hulu, ["Warangka"] = warangka, ["Pendok"] = pendok
            };
            Dictionary<string, Vector3> Pose(Vector3 blade) => new Dictionary<string, Vector3>
            {
                ["Wilah"] = blade, ["Ganja"] = blade, ["Mendak"] = blade, ["Hulu"] = blade,
                ["Warangka"] = WarangkaHome, ["Pendok"] = Vector3.zero
            };
            var s0 = Pose(BladeHome);
            var s1 = Pose(BladeDrawn);
            var s2 = new Dictionary<string, Vector3>(s1) { ["Hulu"] = BladeDrawn + Vector3.up * 0.17f, ["Mendak"] = BladeDrawn + Vector3.up * 0.105f };
            var s3 = new Dictionary<string, Vector3>(s2) { ["Ganja"] = BladeDrawn + Vector3.up * 0.078f }; // lepas dari ujung pesi (7 cm)
            var s4 = new Dictionary<string, Vector3>(s3) { ["Warangka"] = WarangkaHome + Vector3.up * 0.07f };
            var s5 = new Dictionary<string, Vector3>(s4) { ["Pendok"] = new Vector3(-0.13f, 0f, 0f) };

            ExplodedViewController.Stage Stage(Dictionary<string, Vector3> poses, string id, string en, bool hideBlade)
            {
                var st = new ExplodedViewController.Stage { label = new LocalizedString(id, en) };
                foreach (var kv in poses)
                    st.poses.Add(new ExplodedViewController.PartPose { part = parts[kv.Key].transform, localPosition = kv.Value, localRotation = Quaternion.identity });
                if (hideBlade) st.hiddenRenderers.AddRange(wilah.renderers);
                return st;
            }

            var exploded = root.AddComponent<ExplodedViewController>();
            exploded.stages = new List<ExplodedViewController.Stage>
            {
                Stage(s0, "Utuh (tersarung)", "Assembled (sheathed)", true),
                Stage(s1, "Tahap 1: bilah dihunus dari warangka", "Step 1: blade drawn from the sheath", false),
                Stage(s2, "Tahap 2: hulu dan mendak dilepas", "Step 2: hilt and mendak removed", false),
                Stage(s3, "Tahap 3: ganja dilepas", "Step 3: ganja removed", false),
                Stage(s4, "Tahap 4: warangka dilepas dari gandar", "Step 4: warangka separated from the gandar", false),
                Stage(s5, "Tahap 5: pendok dilepas dari gandar", "Step 5: pendok removed from the gandar", false),
            };

            var instance = root.AddComponent<ArtifactInstance>();
            instance.modelRoot = model;
            instance.exploded = exploded;
            instance.autoRotate = root.AddComponent<AutoRotate>();

            // Posisi hotspot (lokal terhadap bagian) dihitung pada pose "dihunus".
            exploded.SnapTo(1);
            var o1 = new Vector2(BladeDrawn.x, BladeDrawn.y);
            Vector2 Xy(Vector3 v) => new Vector2(v.x, v.y);
            hotspots = new Dictionary<string, (string, Vector3)>
            {
                ["hulu"] = ("Hulu", FrontLocal(hulu, model, o1 + V2(0f, 0.068f))),
                ["mendak"] = ("Mendak", FrontLocal(mendak, model, o1 + V2(0f, 0.022f))),
                ["warangka"] = ("Warangka", FrontLocal(warangka, model, V2(0.05f, 0.483f))),
                ["gandar"] = ("Gandar", FrontLocal(gandar, model, V2(0f, 0.07f))),
                ["pendok"] = ("Pendok", FrontLocal(pendok, model, V2(0f, RosetteY))),
                ["ganja"] = ("Ganja", FrontLocal(ganja, model, o1 + V2(0.02f, 0.006f))),
                ["gandik"] = ("Wilah", FrontLocal(wilah, model, o1 + V2(-0.022f, -0.02f))),
                ["pamor"] = ("Wilah", FrontLocal(wilah, model, o1 + Xy(BladeCenter(0.3f)))),
                ["luk"] = ("Wilah", FrontLocal(wilah, model, o1 + Xy(BladeCenter(0.6f)))),
            };
            exploded.SnapTo(0);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Titik di muka depan (-Z) bagian pada koordinat xy ruang Model, dikembalikan dalam ruang lokal bagian.</summary>
        static Vector3 FrontLocal(ArtifactPart part, Transform model, Vector2 xy, float radius = 0.006f)
        {
            var verts = new List<Vector3>();
            foreach (var r in part.renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                foreach (var v in mesh.vertices) verts.Add(model.InverseTransformPoint(r.transform.TransformPoint(v)));
            }
            var near = verts.Where(v => new Vector2(v.x - xy.x, v.y - xy.y).magnitude < radius).ToList();
            float z = (near.Count > 0 ? near : verts).Min(v => v.z) - 0.003f;
            return part.transform.InverseTransformPoint(model.TransformPoint(new Vector3(xy.x, xy.y, z)));
        }
    }
}
