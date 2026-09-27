using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Jawa dari cetak biru, dimodelkan di Blender (<c>Tools/blender/keris_jawa.py</c>) dan diekspor ke
    /// <c>Art/KerisJawa/keris_jawa.fbx</c> beserta teksturnya. Builder ini hanya mengimpor FBX: atur importer,
    /// buat material URP, kelompokkan objek Blender menjadi bagian modular, susun tahap exploded view dan hotspot.
    /// Hulu Jawa Demam (sonokeling, selut + tutup kuningan), mendak kuningan, bilah lurus berpamor dengan ada-ada,
    /// gandik, dan peksi, warangka sampir perahu kandang (kemuning), gandar bersilang perisai, dudukan.
    /// Skala 1:1 (1 unit = 1 m), pivot di dasar dudukan, muka menghadap -Z.
    /// Menu: Nusantara AR / Build Keris Jawa (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KerisJawaBuilder
    {
        public const string Id = "KERIS_JAWA_01";
        const string Root = "Assets/NusantaraAR";
        const string ArtDir = Root + "/Art/KerisJawa";
        const string FbxPath = ArtDir + "/keris_jawa.fbx";
        const string TexDir = ArtDir + "/Textures";
        const string MatDir = ArtDir + "/Materials";
        const string ContentDir = Root + "/Content/" + Id;
        const string PrefabPath = ContentDir + "/" + Id + ".prefab";
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";
        const string CatalogPath = Root + "/Resources/ContentCatalog.asset";

        /// <summary>Panjang bilah tanpa peksi, sesuai cetak biru (35-38 cm; model 34,5 cm + peksi 7,5 cm).</summary>
        public const float BladeLength = 0.345f;

        /// <summary>Pangkal bilah (sisi bawah ganja) saat tersarung / dihunus, ruang Model. Sama dengan origin objek Blender.</summary>
        static readonly Vector3 BladeHome = new Vector3(0f, 0.44f, 0f);
        static readonly Vector3 BladeDrawn = new Vector3(0.22f, 0.44f, 0f);
        static readonly Vector3 WarangkaHome = new Vector3(0f, 0.4225f, 0f);

        [MenuItem("Nusantara AR/Build Keris Jawa")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Keris Jawa selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            if (!File.Exists(FbxPath))
                throw new FileNotFoundException("Model Blender belum diekspor. Jalankan dulu: blender --background --factory-startup --python Tools/blender/keris_jawa.py", FbxPath);
            foreach (var d in new[] { MatDir, ContentDir, Root + "/Resources" })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            var mats = BuildMaterials();
            ImportModel(mats);
            GameObject prefab;
            Dictionary<string, (string part, Vector3 local)> hotspots;
            try
            {
                prefab = BuildPrefab(out hotspots);
            }
            finally
            {
                // Mesh hanya perlu bisa dibaca saat builder menghitung posisi hotspot.
                var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
                if (imp.isReadable)
                {
                    imp.isReadable = false;
                    imp.SaveAndReimport();
                }
            }

            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ArtifactData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            KerisJawaContent.Fill(data, prefab, hotspots);
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

        // ------------------------------------------------------------------ Tekstur + material

        static Texture2D Tex(string name)
        {
            string path = TexDir + "/" + name + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) throw new FileNotFoundException("Tekstur Blender tidak ditemukan", path);
            bool normal = name.EndsWith("_normal");
            bool linear = name.EndsWith("_ms");
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !(normal || linear);
            imp.alphaSource = linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.maxTextureSize = 1024;
            var android = imp.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(android);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Lit(string name) =>
            ProjectSetup.GetOrCreateMaterial(MatDir + "/M_" + Id + "_" + name + ".mat", "Universal Render Pipeline/Lit");

        static Material Setup(Material m, Texture2D albedo, Texture2D ms, Texture2D normal, Color tint, float metallic, float smoothness)
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
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            if (normal != null) m.EnableKeyword("_NORMALMAP");
            else m.DisableKeyword("_NORMALMAP");
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Material URP per nama material Blender (nama yang tersimpan di FBX).</summary>
        static Dictionary<string, Material> BuildMaterials()
        {
            var brass = new Color(0.78f, 0.60f, 0.25f);
            return new Dictionary<string, Material>
            {
                ["pamor"] = Setup(Lit("Pamor"), Tex("pamor"), Tex("pamor_ms"), null, Color.white, 0f, 0f),
                ["kemuning"] = Setup(Lit("Kemuning"), Tex("kemuning"), null, null, Color.white, 0f, 0.45f),
                ["sonokeling"] = Setup(Lit("Sonokeling"), Tex("sonokeling"), null, Tex("hulu_normal"), Color.white, 0f, 0.5f),
                ["kuningan"] = Setup(Lit("Kuningan"), null, null, null, brass, 1f, 0.7f),
                ["kuningan_ukir"] = Setup(Lit("KuninganUkir"), null, null, Tex("kuningan_normal"), brass, 1f, 0.7f),
                ["dudukan"] = Setup(Lit("Dudukan"), null, null, null, new Color(0.10f, 0.06f, 0.04f), 0f, 0.4f),
            };
        }

        static void ImportModel(Dictionary<string, Material> mats)
        {
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport);
            var model = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            model.globalScale = 1f;
            model.useFileScale = true;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.importCameras = false;
            model.importLights = false;
            model.importAnimation = false;
            model.isReadable = true; // dibaca saat menghitung posisi hotspot; dimatikan lagi di akhir Build()
            foreach (var kv in mats)
                model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            model.SaveAndReimport();
        }

        // ------------------------------------------------------------------ Prefab

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

        static Bounds ModelBounds(IEnumerable<Renderer> renderers)
        {
            var list = renderers.ToList();
            var b = list[0].bounds;
            foreach (var r in list.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject BuildPrefab(out Dictionary<string, (string part, Vector3 local)> hotspots)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var root = new GameObject(Id);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            var parts = new Dictionary<string, ArtifactPart>
            {
                ["Dudukan"] = MakePart(model, "Dudukan", Vector3.zero),
                ["Gandar"] = MakePart(model, "Gandar", Vector3.zero),
                ["Warangka"] = MakePart(model, "Warangka", WarangkaHome),
                ["Wilah"] = MakePart(model, "Wilah", BladeHome),
                ["Ganja"] = MakePart(model, "Ganja", BladeHome),
                ["Mendak"] = MakePart(model, "Mendak", BladeHome),
                ["Hulu"] = MakePart(model, "Hulu", BladeHome),
            };

            // Objek Blender -> bagian: awalan sebelum "_" (Hulu_Selut dan Hulu_Tutup ikut Hulu).
            var src = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            src.transform.SetParent(model, false);
            // Importer FBX Unity membalik X (kanan-tangan -> kiri-tangan); putar 180° di Y agar tanduk tinggi warangka kembali di +X.
            src.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                string key = n.Split('_')[0];
                if (!parts.TryGetValue(key, out var part))
                    throw new InvalidOperationException("Objek FBX tanpa bagian: " + n);
                r.transform.SetParent(part.transform, true);
                r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                part.renderers = part.renderers.Concat(new Renderer[] { r }).ToArray();
            }
            UnityEngine.Object.DestroyImmediate(src);
            foreach (var kv in parts)
                if (kv.Value.renderers.Length == 0) throw new InvalidOperationException("Bagian kosong di FBX: " + kv.Key);

            // Cek sumbu & skala ekspor Blender: berdiri di y = 0, tinggi ~57 cm, tanduk warangka di +X.
            var all = ModelBounds(parts.Values.SelectMany(p => p.renderers));
            var war = ModelBounds(parts["Warangka"].renderers);
            var blade = ModelBounds(parts["Wilah"].renderers);
            Debug.Log($"[NusantaraAR] Keris Jawa bounds: semua min {all.min:F4} max {all.max:F4}; warangka pusat {war.center:F4} lebar {war.size.x:F4}; wilah tinggi {blade.size.y:F4}");
            if (Mathf.Abs(all.min.y) > 0.005f || all.size.y < 0.55f || all.size.y > 0.60f)
                throw new InvalidOperationException($"Skala/sumbu FBX Keris Jawa tidak sesuai (min y {all.min.y:F4}, tinggi {all.size.y:F4} m; harapan 0 dan ~0,57 m).");
            if (war.center.x <= 0.005f)
                throw new InvalidOperationException($"Sumbu X FBX Keris Jawa terbalik: pusat warangka x = {war.center.x:F4} (harapan > 0, tanduk tinggi di kanan).");

            // Tahap exploded view (PRD FR-07): hunus -> lepas hulu + mendak -> lepas ganja -> lepas warangka.
            Dictionary<string, Vector3> Pose(Vector3 b) => new Dictionary<string, Vector3>
            {
                ["Wilah"] = b, ["Ganja"] = b, ["Mendak"] = b, ["Hulu"] = b, ["Warangka"] = WarangkaHome
            };
            var s0 = Pose(BladeHome);
            var s1 = Pose(BladeDrawn);
            var s2 = new Dictionary<string, Vector3>(s1) { ["Hulu"] = BladeDrawn + Vector3.up * 0.17f, ["Mendak"] = BladeDrawn + Vector3.up * 0.105f };
            var s3 = new Dictionary<string, Vector3>(s2) { ["Ganja"] = BladeDrawn + Vector3.up * 0.08f }; // lepas dari ujung peksi (7,5 cm)
            var s4 = new Dictionary<string, Vector3>(s3) { ["Warangka"] = WarangkaHome + Vector3.up * 0.07f };

            ExplodedViewController.Stage Stage(Dictionary<string, Vector3> poses, string id, string en, bool hideBlade)
            {
                var st = new ExplodedViewController.Stage { label = new LocalizedString(id, en) };
                foreach (var kv in poses)
                    st.poses.Add(new ExplodedViewController.PartPose { part = parts[kv.Key].transform, localPosition = kv.Value, localRotation = Quaternion.identity });
                if (hideBlade) st.hiddenRenderers.AddRange(parts["Wilah"].renderers);
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
            };

            var instance = root.AddComponent<ArtifactInstance>();
            instance.modelRoot = model;
            instance.exploded = exploded;
            instance.autoRotate = root.AddComponent<AutoRotate>();

            // Posisi hotspot (lokal terhadap bagian) dihitung pada pose "dihunus".
            exploded.SnapTo(1);
            var o1 = new Vector2(BladeDrawn.x, BladeDrawn.y);
            Vector2 V2(float x, float y) => new Vector2(x, y);
            hotspots = new Dictionary<string, (string, Vector3)>
            {
                ["hulu"] = ("Hulu", FrontLocal(parts["Hulu"], model, o1 + V2(-0.004f, 0.07f))),
                ["selut"] = ("Hulu", FrontLocal(parts["Hulu"], model, o1 + V2(0f, 0.026f))),
                ["mendak"] = ("Mendak", FrontLocal(parts["Mendak"], model, o1 + V2(0f, 0.0145f))),
                ["warangka"] = ("Warangka", FrontLocal(parts["Warangka"], model, V2(0.07f, 0.44f))),
                ["gandar"] = ("Gandar", FrontLocal(parts["Gandar"], model, V2(0f, 0.2f))),
                ["ganja"] = ("Ganja", FrontLocal(parts["Ganja"], model, o1 + V2(0.018f, 0.005f))),
                ["gandik"] = ("Wilah", FrontLocal(parts["Wilah"], model, o1 + V2(-0.014f, -0.03f))),
                ["pamor"] = ("Wilah", FrontLocal(parts["Wilah"], model, o1 + V2(0f, -0.12f))),
                ["ada_ada"] = ("Wilah", FrontLocal(parts["Wilah"], model, o1 + V2(0f, -0.22f))),
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
