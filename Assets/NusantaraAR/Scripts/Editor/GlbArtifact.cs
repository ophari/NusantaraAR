using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Utilitas bersama untuk artefak yang dimodelkan di Blender (<c>Tools/blender/*.py</c>) dan diekspor sebagai GLB.
    /// GLB diimpor oleh paket glTFast (com.unity.cloud.gltfast) menjadi GameObject + mesh + material + tekstur.
    /// Kelas ini menyusunnya menjadi prefab artefak: objek Blender dikelompokkan per bagian (<see cref="ArtifactPart"/>),
    /// pivot bagian di titik sambungnya, model digeser agar dasarnya di y = 0, lalu tahap exploded view dan hotspot dipasang.
    ///
    /// Sumbu: Blender (Z ke atas, muka depan -Y) -> ruang Model Unity (Y ke atas, muka depan -Z), yaitu (x, y, z) -> (x, z, y).
    /// Konversi glTF->Unity oleh glTFast bisa berupa pencerminan X atau Z; keduanya disamakan dengan memeriksa sisi +X
    /// sebuah bagian acuan dan memutar model 180° di Y bila perlu.
    /// </summary>
    static class GlbArtifact
    {
        public const string Root = "Assets/NusantaraAR";
        const string CatalogPath = Root + "/Resources/ContentCatalog.asset";

        /// <summary>Titik ruang Blender (m) ke ruang Model Unity.</summary>
        public static Vector3 B(float x, float y, float z) => new Vector3(x, z, y);

        public class Built
        {
            public GameObject root;
            public Transform model;
            public readonly Dictionary<string, ArtifactPart> parts = new Dictionary<string, ArtifactPart>();
            public ExplodedViewController exploded;
            public ArtifactPart this[string name] => parts[name];
        }

        /// <param name="partOf">Nama objek Blender (node GLB) -> nama bagian.</param>
        /// <param name="pivots">Nama bagian -> pivot di ruang Model (lihat <see cref="B"/>).</param>
        /// <param name="rightPart">Bagian yang pusatnya harus berada di sisi +X (pemeriksa arah sumbu).</param>
        /// <param name="basePart">Bagian alas (dudukan/jagrak): pusat XZ-nya menjadi pivot root.</param>
        public static Built Load(string id, string glbPath, Dictionary<string, string> partOf,
            Dictionary<string, Vector3> pivots, string rightPart, string basePart)
        {
            if (!File.Exists(glbPath))
                throw new FileNotFoundException("GLB Blender belum diekspor (jalankan skrip di Tools/blender).", glbPath);
            AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceSynchronousImport);
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (glb == null)
                throw new InvalidOperationException("GLB tidak terimpor sebagai model: " + glbPath + " (paket glTFast belum terpasang?)");

            var b = new Built { root = new GameObject(id) };
            b.model = new GameObject("Model").transform;
            b.model.SetParent(b.root.transform, false);

            var src = (GameObject)PrefabUtility.InstantiatePrefab(glb);
            PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            src.transform.SetParent(b.model, false);
            // Animasi GLB ("Cabut_Keris") tidak dipakai: aplikasi punya exploded view sendiri.
            foreach (var c in src.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in src.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(c);

            string PartFor(Transform t)
            {
                for (; t != null && t != b.model; t = t.parent)
                    if (partOf.TryGetValue(t.name, out var p)) return p;
                return null;
            }

            var renderers = src.GetComponentsInChildren<MeshRenderer>(true);
            var unknown = renderers.Where(r => PartFor(r.transform) == null).Select(r => r.name).ToList();
            if (unknown.Count > 0) throw new InvalidOperationException("Objek GLB tanpa bagian: " + string.Join(", ", unknown));

            var right = Encapsulate(renderers.Where(r => PartFor(r.transform) == rightPart));
            if (b.model.InverseTransformPoint(right.center).x < 0f)
                src.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            foreach (var name in pivots.Keys)
            {
                var go = new GameObject(name);
                go.transform.SetParent(b.model, false);
                go.transform.localPosition = pivots[name];
                var ap = go.AddComponent<ArtifactPart>();
                ap.partName = name;
                ap.renderers = new Renderer[0];
                b.parts[name] = ap;
            }
            foreach (var r in renderers)
            {
                var part = b.parts[PartFor(r.transform)];
                r.transform.SetParent(part.transform, true);
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && r.GetComponent<Collider>() == null)
                    r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; // oklusi hotspot
                part.renderers = part.renderers.Concat(new Renderer[] { r }).ToArray();
            }
            UnityEngine.Object.DestroyImmediate(src);
            foreach (var kv in b.parts)
                if (kv.Value.renderers.Length == 0) throw new InvalidOperationException("Bagian kosong di GLB: " + kv.Key);

            // Dasar di y = 0, pusat alas di XZ = 0 (root prefab = titik letak di bidang AR / kartu).
            var all = Encapsulate(b.parts.Values.SelectMany(p => p.renderers));
            var bas = Encapsulate(b.parts[basePart].renderers);
            b.model.localPosition = new Vector3(-bas.center.x, -all.min.y, -bas.center.z);
            foreach (var kv in b.parts)
            {
                var pb = Encapsulate(kv.Value.renderers);
                Debug.Log($"[NusantaraAR] {id} {kv.Key}: min {b.model.InverseTransformPoint(pb.min):F3} max {b.model.InverseTransformPoint(pb.max):F3} (ruang Model)");
            }

            b.exploded = b.root.AddComponent<ExplodedViewController>();
            var instance = b.root.AddComponent<ArtifactInstance>();
            instance.modelRoot = b.model;
            instance.exploded = b.exploded;
            instance.autoRotate = b.root.AddComponent<AutoRotate>();
            return b;
        }

        /// <summary>
        /// glTFast menyimpan tekstur GLB sebagai sub-aset ARGB32 tanpa kompresi (~5 MB per 1024², plus mip).
        /// Agar hemat memori & APK, material GLB disalin ke <c>{artDir}/Materials</c> dan teksturnya diganti dengan PNG
        /// sumber yang sama dari skrip Blender (<paramref name="blenderTexDir"/>), diimpor Unity dengan kompresi ASTC.
        /// Tekstur sub-aset GLB tidak lagi direferensikan prefab, sehingga tidak ikut ke build.
        /// Konvensi nama PNG: tanpa akhiran = warna (sRGB); <c>_n</c> = normal map, <c>_orm</c> = oklusi/roughness/metallic (linear).
        /// </summary>
        public static void UseCompressedTextures(Built b, string blenderTexDir, string artDir)
        {
            string texDir = artDir + "/Textures", matDir = artDir + "/Materials";
            Directory.CreateDirectory(texDir);
            Directory.CreateDirectory(matDir);
            var copies = new Dictionary<Material, Material>();
            foreach (var r in b.parts.Values.SelectMany(p => p.renderers))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!copies.TryGetValue(src, out var copy))
                        copies[src] = copy = CompressedCopy(src, blenderTexDir, texDir, matDir);
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
        }

        static Material CompressedCopy(Material src, string blenderTexDir, string texDir, string matDir)
        {
            string path = matDir + "/" + src.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(src);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = src.shader;
                m.CopyPropertiesFromMaterial(src);
            }
            foreach (var prop in src.GetTexturePropertyNames())
            {
                var tex = src.GetTexture(prop);
                if (tex == null) continue;
                string png = Path.Combine(blenderTexDir, tex.name + ".png");
                if (!File.Exists(png))
                    throw new FileNotFoundException("Tekstur sumber Blender tidak ditemukan (jalankan skrip Blender).", png);
                m.SetTexture(prop, ImportCompressed(png, texDir));
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D ImportCompressed(string png, string texDir)
        {
            string name = Path.GetFileNameWithoutExtension(png);
            string dst = texDir + "/" + name + ".png";
            bool changed = !File.Exists(dst) || !File.ReadAllBytes(dst).SequenceEqual(File.ReadAllBytes(png));
            if (changed)
            {
                File.Copy(png, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(dst);
            bool normal = name.EndsWith("_n"), linear = normal || name.EndsWith("_orm");
            // Tipe Default (bukan NormalMap): shader glTF membaca normal map sebagai RGB apa adanya, sama seperti sub-aset glTFast.
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = !linear;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            // Normal map 5x5 (5,1 bpp) alih-alih 4x4 (8 bpp): ~36% lebih kecil, detail pamor/ukiran tetap terbaca.
            android.format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            var ios = imp.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = 2048;
            ios.format = android.format;
            imp.SetPlatformTextureSettings(ios);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        public static Bounds Encapsulate(IEnumerable<Renderer> renderers)
        {
            var list = renderers.ToList();
            if (list.Count == 0) throw new InvalidOperationException("Tidak ada renderer.");
            var bb = list[0].bounds;
            foreach (var r in list.Skip(1)) bb.Encapsulate(r.bounds);
            return bb;
        }

        /// <summary>
        /// Tahap exploded view. <paramref name="offsets"/> per tahap = pergeseran kumulatif (ruang Model) dari pose utuh.
        /// Tahap 0 = utuh; renderer <paramref name="hiddenWhenAssembled"/> disembunyikan (mis. bilah di dalam sarung).
        /// </summary>
        public static void SetStages(Built b, string[] hiddenWhenAssembled,
            params (string id, string en, Dictionary<string, Vector3> offsets)[] stages)
        {
            var moving = stages.SelectMany(s => s.offsets.Keys).Distinct().ToList();
            var home = moving.ToDictionary(n => n, n => b.parts[n].transform.localPosition);
            b.exploded.stages = new List<ExplodedViewController.Stage>();
            for (int i = 0; i < stages.Length; i++)
            {
                var st = new ExplodedViewController.Stage { label = new LocalizedString(stages[i].id, stages[i].en) };
                foreach (var n in moving)
                {
                    stages[i].offsets.TryGetValue(n, out var off);
                    st.poses.Add(new ExplodedViewController.PartPose
                    {
                        part = b.parts[n].transform, localPosition = home[n] + off, localRotation = Quaternion.identity
                    });
                }
                if (i == 0)
                    foreach (var n in hiddenWhenAssembled) st.hiddenRenderers.AddRange(b.parts[n].renderers);
                b.exploded.stages.Add(st);
            }
        }

        public static Dictionary<string, Vector3> Move(IEnumerable<string> parts, Vector3 offset, Dictionary<string, Vector3> basis = null)
        {
            var d = basis != null ? new Dictionary<string, Vector3>(basis) : new Dictionary<string, Vector3>();
            foreach (var p in parts) d[p] = (d.TryGetValue(p, out var o) ? o : Vector3.zero) + offset;
            return d;
        }

        /// <summary>
        /// Titik 3 mm di depan permukaan terdepan (-Z) bagian pada koordinat xy ruang Model (raycast dari depan ke
        /// collider bagian), dikembalikan dalam ruang lokal bagian.
        /// </summary>
        public static (string part, Vector3 local) Front(Built b, string partName, float x, float y)
        {
            var part = b.parts[partName];
            Physics.SyncTransforms();
            var dir = b.model.TransformDirection(Vector3.forward);
            var ray = new Ray(b.model.TransformPoint(new Vector3(x, y, -2f)), dir);
            float best = float.MaxValue;
            foreach (var r in part.renderers)
                if (r.TryGetComponent<Collider>(out var c) && c.Raycast(ray, out var hit, 4f) && hit.distance < best)
                    best = hit.distance;
            if (best == float.MaxValue)
            {
                var bb = Encapsulate(part.renderers);
                var lo = b.model.InverseTransformPoint(bb.min); var hi = b.model.InverseTransformPoint(bb.max);
                throw new InvalidOperationException($"Hotspot di luar bagian {partName}: ({x:F3}, {y:F3}); bagian x {lo.x:F3}..{hi.x:F3}, y {lo.y:F3}..{hi.y:F3}");
            }
            var p = ray.GetPoint(best - 0.003f);
            return (partName, part.transform.InverseTransformPoint(p));
        }

        /// <summary>Simpan prefab, isi ArtifactData lewat <paramref name="fill"/>, dan daftarkan ke katalog.</summary>
        public static ArtifactData Save(Built b, string contentDir, Action<ArtifactData, GameObject> fill)
        {
            Directory.CreateDirectory(contentDir);
            Directory.CreateDirectory(Root + "/Resources");
            AssetDatabase.Refresh();
            b.exploded.SnapTo(0);
            string id = b.root.name;
            var prefab = PrefabUtility.SaveAsPrefabAsset(b.root, contentDir + "/" + id + ".prefab");
            UnityEngine.Object.DestroyImmediate(b.root);

            string dataPath = contentDir + "/" + id + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(dataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ArtifactData>();
                AssetDatabase.CreateAsset(data, dataPath);
            }
            fill(data, prefab);
            EditorUtility.SetDirty(data);

            var catalog = AssetDatabase.LoadAssetAtPath<ContentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ContentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.artifacts.RemoveAll(a => a == null);
            if (!catalog.artifacts.Contains(data)) catalog.artifacts.Add(data);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>
        /// Hotspot draf. Filosofi/sejarah yang kosong ditandai "diisi kurator"; <paramref name="refs"/> = sumber rujukan
        /// daring untuk isi teks (ditampilkan setelah sumber model).
        /// </summary>
        public static HotspotData Hotspot(Dictionary<string, (string part, Vector3 local)> positions, string id, HotspotStage stage,
            string titleID, string titleEN, string regional, LocalizedString material, LocalizedString craft, string source, string note,
            LocalizedString philosophy = default, LocalizedString history = default, params string[] refs)
        {
            var pending = new LocalizedString("[Draf] Diisi kurator setelah spesimen asli ditetapkan.",
                "[Draft] To be written by the curator once the real specimen is chosen.");
            var (part, local) = positions[id];
            var sources = new List<string> { source };
            sources.AddRange(refs);
            return new HotspotData
            {
                hotspotId = id,
                partName = part,
                localPosition = local,
                visibleFrom = stage,
                title = new LocalizedString(titleID, titleEN),
                regionalTerm = regional,
                material = material,
                philosophy = philosophy.IsEmpty ? pending : philosophy,
                craft = craft,
                history = history.IsEmpty ? pending : history,
                transcript = craft,
                sources = sources,
                curatorValidated = false,
                curatorNote = note
            };
        }
    }
}
