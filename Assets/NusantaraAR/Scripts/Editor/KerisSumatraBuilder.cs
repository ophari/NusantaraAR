using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Sumatra (luk 7, pamor wos wutah) dari <c>Tools/blender/keris_sumatra.py</c> -> <c>Art/KerisSumatra/keris_sumatra.glb</c>.
    /// Keris berdiri tersarung di dudukan kayu; pivot root di dasar dudukan, muka menghadap -Z.
    /// Exploded view 4 tahap: hunus -> lepas hulu + mendak -> lepas ganja -> lepas warangka.
    /// Menu: Nusantara AR / Build Keris Sumatra (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KerisSumatraBuilder
    {
        public const string Id = "KERIS_SUMATRA_01";
        const string GlbPath = Root + "/Art/KerisSumatra/keris_sumatra.glb";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";

        /// <summary>Panjang bilah (pangkal -> pucuk) sesuai cetak biru.</summary>
        public const float BladeLength = 0.360f;
        /// <summary>Geser bilah saat dihunus (ke samping warangka).</summary>
        static readonly Vector3 Drawn = new Vector3(0.20f, 0f, 0f);
        /// <summary>
        /// Tarikan ekstra di luar panjang bilah sebelum bilah digeser ke samping (animasi hunus): sampir warangka
        /// menjulang ±7 cm di atas mulut sarung, ditambah jarak aman.
        /// </summary>
        const float DrawClearance = 0.12f;

        [MenuItem("Nusantara AR/Build Keris Sumatra")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Keris Sumatra selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>
            {
                ["Wilah"] = "Wilah", ["Ganja"] = "Ganja", ["Mendak"] = "Mendak", ["Hulu"] = "Hulu",
                ["Warangka_Sampir"] = "Warangka", ["Warangka_Celah"] = "Warangka", ["Gandar"] = "Gandar",
                ["Pendok_Atas"] = "Pendok", ["Pendok_Bawah"] = "Pendok", ["Dudukan"] = "Dudukan",
            };
            // Semua objek Blender ber-origin di pangkal bilah (titik sambung bilah-hulu dan mulut warangka).
            var pivots = new Dictionary<string, Vector3>();
            foreach (var p in new[] { "Dudukan", "Gandar", "Pendok", "Warangka", "Wilah", "Ganja", "Mendak", "Hulu" })
                pivots[p] = Vector3.zero;
            var b = Load(Id, GlbPath, partOf, pivots, "Warangka", "Dudukan");
            UseCompressedTextures(b, "Tools/blender/keris_sumatra_textures", Root + "/Art/KerisSumatra");

            var blade = new[] { "Wilah", "Ganja", "Mendak", "Hulu" };
            var s1 = Move(blade, Drawn);
            var s2 = Move(new[] { "Mendak" }, Vector3.up * 0.100f, Move(new[] { "Hulu" }, Vector3.up * 0.135f, s1));
            var s3 = Move(new[] { "Ganja" }, Vector3.up * 0.078f, s2);      // lolos dari ujung pesi (7,2 cm)
            var s4 = Move(new[] { "Warangka" }, Vector3.up * 0.070f, s3);
            SetStages(b, new[] { "Wilah" },
                ("Utuh (tersarung)", "Assembled (sheathed)", new Dictionary<string, Vector3>()),
                ("Tahap 1: bilah dihunus dari warangka", "Step 1: blade drawn from the sheath", s1),
                ("Tahap 2: hulu dan mendak dilepas", "Step 2: hilt and mendak removed", s2),
                ("Tahap 3: ganja dilepas", "Step 3: ganja removed", s3),
                ("Tahap 4: warangka dilepas dari gandar", "Step 4: warangka separated from the gandar", s4));
            // Animasi hunus: bilah ditarik ke atas sampai pucuknya lolos dari mulut warangka, lalu melengkung ke samping.
            b.exploded.stages[1].drawOut = Vector3.up * (BladeLength + DrawClearance);

            // Hotspot dihitung pada pose "dihunus"; koordinat ruang Model (x, y) = (x, z) Blender.
            b.exploded.SnapTo(1);
            float d = Drawn.x;
            var pos = new Dictionary<string, (string, Vector3)>
            {
                ["hulu"] = Front(b, "Hulu", d + 0.012f, 0.070f),
                ["mendak"] = Front(b, "Mendak", d, 0.017f),
                ["warangka"] = Front(b, "Warangka", 0.0f, -0.025f),
                ["pendok"] = Front(b, "Pendok", 0.0f, -0.100f),
                ["gandar"] = Front(b, "Gandar", 0.0f, -0.205f),
                ["ganja"] = Front(b, "Ganja", d + 0.020f, 0.005f),
                ["gandik"] = Front(b, "Wilah", d - 0.012f, -0.030f),
                ["pamor"] = Front(b, "Wilah", d, -0.120f),
                ["luk"] = Front(b, "Wilah", d, -0.250f),
            };
            return Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
        }

        const string Source = "Cetak biru Keris Sumatra (proyek) + glosarium draf PRD v1.1 §6.1 - menunggu validasi kurator";
        const string Note = "Belum divalidasi (model Blender dari cetak biru).";

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Keris Sumatra", "Sumatran Keris");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Sumatra", "Sumatra");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Sumatra dengan bilah 7 luk berpamor wos wutah, hulu kayu burl berukir yang melengkung seperti kepala burung, warangka sampir bulan sabit, dan pendok kuningan berukir. Model dibuat di Blender dari cetak biru, bukan dipindai dari spesimen.",
                "A Sumatran-style keris with a 7-wave wos wutah patterned blade, a carved burl-wood hilt curving like a bird's head, a crescent sampir sheath top and engraved brass pendok. The model was made in Blender from a blueprint, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari cetak biru)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 mengikuti cetak biru: bilah 36 cm + pesi 7,2 cm, hulu ±10 cm, warangka sampir lebar 19 cm. Bukan hasil pengukuran spesimen.",
                "1:1 scale follows the blueprint: 36 cm blade + 7.2 cm tang (pesi), ~10 cm hilt, 19 cm wide sampir. Not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KerisSumatraCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari cetak biru: ukiran, motif pamor, dan serat kayu adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from a blueprint: the carving, pamor pattern and wood grain are simplified. It will be replaced by an asset from a curator-validated specimen.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note);
            d.hotspots = new List<HotspotData>
            {
                H("hulu", HotspotStage.Utuh, "Hulu (Deder)", "Hilt (Deder)", "Sumatra/Semenanjung: hulu, deder",
                    new LocalizedString("Pada model: kayu burl (jati berpola) berpernis (sesuai cetak biru, perlu dikonfirmasi)", "On the model: varnished burl (figured teak) wood (per the blueprint, to be confirmed)"),
                    new LocalizedString("Pegangan keris yang dipasang pada pesi. Bentuknya melengkung seperti pistol dengan kepala bergaya paruh burung; bagian bawahnya diukir motif sulur dan bunga.",
                        "The keris handle, fitted onto the pesi. It curves like a pistol grip with a stylised bird-beak head; the lower part is carved with scroll and flower motifs.")),
                H("mendak", HotspotStage.Utuh, "Mendak", "Mendak (ring)", "",
                    new LocalizedString("Pada model: kuningan timbul (perlu dikonfirmasi)", "On the model: embossed brass (to be confirmed)"),
                    new LocalizedString("Cincin logam bertingkat di antara hulu dan ganja, dihiasi deretan butiran dan kelopak.",
                        "The stepped metal ring between the hilt and the ganja, decorated with a row of beads and petals.")),
                H("warangka", HotspotStage.Utuh, "Warangka Sampir", "Warangka (crescent sheath top)", "Sumatra: sampir",
                    new LocalizedString("Pada model: kayu burl berpernis (perlu dikonfirmasi)", "On the model: varnished burl wood (to be confirmed)"),
                    new LocalizedString("Kepala sarung berbentuk bulan sabit; kedua ujungnya melengkung ke atas dan satu sisi meruncing lebih panjang. Di tengahnya ada celah tempat bilah masuk.",
                        "The crescent-shaped sheath top; both ends curve upward and one side tapers longer. The slot for the blade is in the middle.")),
                H("pendok", HotspotStage.Utuh, "Pendok", "Pendok (sheath sleeves)", "",
                    new LocalizedString("Pada model: kuningan/perunggu 85/15 berpatina (perlu dikonfirmasi)", "On the model: patinated 85/15 brass/bronze (to be confirmed)"),
                    new LocalizedString("Selongsong logam repoussé berukir sulur yang membalut gandar; pada model terbagi dua: di bawah sampir dan di ujung sarung.",
                        "Repoussé metal sleeves with scroll decoration over the gandar; on the model they come in two parts: below the sampir and at the sheath tip.")),
                H("gandar", HotspotStage.Utuh, "Gandar", "Gandar (sheath body)", "",
                    new LocalizedString("Pada model: kayu nangka (perlu dikonfirmasi)", "On the model: jackwood (to be confirmed)"),
                    new LocalizedString("Badan sarung yang membungkus bilah, meruncing ke bawah; bagian tengahnya dibiarkan kayu.",
                        "The sheath body that covers the blade, tapering downward; its middle is left as bare wood.")),
                H("ganja", HotspotStage.Bilah, "Ganja (Gonjo)", "Ganja (base plate)", "",
                    new LocalizedString("Besi/baja tempa berpamor (perlu dikonfirmasi)", "Forged, patterned iron/steel (to be confirmed)"),
                    new LocalizedString("Bantalan melintang di pangkal bilah. Pada model dibuat terpisah dan dilepas pada tahap 3.",
                        "The cross-piece at the base of the blade. On this model it is a separate piece, removed in step 3.")),
                H("gandik", HotspotStage.Bilah, "Gandik & Kembang Kacang", "Gandik & Kembang Kacang", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Pangkal depan bilah yang tebal, dengan kembang kacang (lengkung seperti belalai) dan pejetan. Sisi belakang bergerigi disebut greneng.",
                        "The thick front base of the blade, with the kembang kacang (trunk-like curl) and pejetan. The serrated back edge is the greneng.")),
                H("pamor", HotspotStage.Bilah, "Pamor Wos Wutah", "Pamor (wos wutah)", "",
                    new LocalizedString("Baja lipat dengan bahan pamor nikel (perlu dikonfirmasi)", "Fold-forged steel with nickel pamor (to be confirmed)"),
                    new LocalizedString("Urat perak di permukaan bilah hasil tempa lipat. Wos wutah berarti 'beras tumpah' - motif butiran yang tersebar acak. Motif pada model dibuat prosedural.",
                        "The silvery veins on the blade surface from fold-forging. Wos wutah means 'spilled rice' - a randomly scattered pattern. The pattern on the model is procedural.")),
                H("luk", HotspotStage.Bilah, "Luk 7", "Luk (7 waves)", "7 luk (menurut cetak biru, perlu divalidasi)",
                    default,
                    new LocalizedString("Lekukan pada bilah. Jumlah luk selalu ganjil; model ini dibuat dengan 7 luk dan panjang bilah 36 cm sesuai cetak biru.",
                        "The waves of the blade. The number of luk is always odd; this model has 7, with a 36 cm blade, following the blueprint.")),
            };
        }
    }
}
