using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Bali (luk 9, pamor banyu tetes, hulu dederan figur dewa, warangka Branggah) di atas jagrak ukir Karang Boma,
    /// dari <c>Tools/blender/keris_bali.py</c> -> <c>Art/KerisBali/keris_bali.glb</c>.
    /// Keris tersarung terbaring mendatar di jagrak (hulu ke +X); pivot root di dasar jagrak, muka menghadap -Z.
    /// Exploded view 5 tahap: hunus (bilah naik di atas sarung) -> lepas hulu + selut -> lepas ganja -> lepas warangka
    /// -> lepas pendok. Menu: Nusantara AR / Build Keris Bali (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KerisBaliBuilder
    {
        public const string Id = "KERIS_BALI_01";
        const string GlbPath = Root + "/Art/KerisBali/keris_bali.glb";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";

        /// <summary>Panjang bilah (pangkal -> pucuk) pada model Blender.</summary>
        public const float BladeLength = 0.400f;
        /// <summary>Pangkal bilah (pivot keris) di jagrak, ruang Blender: KERIS_X dan AXIS_Z di keris_bali.py.</summary>
        static readonly Vector3 KerisPivot = B(0.215f, 0f, 0.159f);
        const float DrawnLift = 0.17f;
        /// <summary>
        /// Tarikan ekstra di luar panjang bilah sebelum bilah diangkat (animasi hunus): warangka Branggah menjorok
        /// ±6,5 cm melewati pangkal bilah (x 0,280 ruang Model), ditambah ±2 cm jarak aman.
        /// </summary>
        const float DrawClearance = 0.09f;

        [MenuItem("Nusantara AR/Build Keris Bali")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Keris Bali selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>
            {
                ["Wilah"] = "Wilah", ["Ganja"] = "Ganja", ["Selut"] = "Selut", ["Permata_Selut"] = "Selut",
                ["Hulu"] = "Hulu", ["Permata_Hulu"] = "Hulu",
                ["Warangka_Branggah"] = "Warangka", ["Warangka_Celah"] = "Warangka", ["Gandar"] = "Gandar",
                ["Cincin"] = "Cincin", ["Permata_Warangka"] = "Cincin", ["Pendok"] = "Pendok",
                ["Jagrak_Ukiran"] = "Jagrak", ["Jagrak_Kaki"] = "Jagrak",
            };
            var pivots = new Dictionary<string, Vector3> { ["Jagrak"] = Vector3.zero };
            foreach (var p in new[] { "Gandar", "Cincin", "Pendok", "Warangka", "Wilah", "Ganja", "Selut", "Hulu" })
                pivots[p] = KerisPivot;
            var b = Load(Id, GlbPath, partOf, pivots, "Hulu", "Jagrak");
            UseCompressedTextures(b, "Tools/blender/keris_bali_textures", Root + "/Art/KerisBali");

            // Sumbu keris = +X ruang Model (hulu ke kanan).
            var axis = Vector3.right;
            var blade = new[] { "Wilah", "Ganja", "Selut", "Hulu" };
            var s1 = Move(blade, Vector3.up * DrawnLift);                    // bilah terangkat di atas sarung
            var s2 = Move(new[] { "Selut" }, axis * 0.100f, Move(new[] { "Hulu" }, axis * 0.130f, s1));
            var s3 = Move(new[] { "Ganja" }, axis * 0.085f, s2);            // lolos dari ujung pesi (8 cm)
            var s4 = Move(new[] { "Warangka" }, axis * 0.070f, s3);
            var s5 = Move(new[] { "Pendok" }, -axis * 0.130f, s4);
            SetStages(b, new[] { "Wilah" },
                ("Utuh (tersarung di jagrak)", "Assembled (sheathed, on the stand)", new Dictionary<string, Vector3>()),
                ("Tahap 1: bilah dihunus dari warangka", "Step 1: blade drawn from the sheath", s1),
                ("Tahap 2: hulu dan selut dilepas", "Step 2: hilt and selut removed", s2),
                ("Tahap 3: ganja dilepas", "Step 3: ganja removed", s3),
                ("Tahap 4: warangka dilepas dari gandar", "Step 4: warangka separated from the gandar", s4),
                ("Tahap 5: pendok dilepas dari gandar", "Step 5: pendok slid off the gandar", s5));
            // Animasi hunus: bilah ditarik ke arah hulu sampai pucuknya lolos dari mulut warangka, lalu naik ke atas sarung.
            b.exploded.stages[1].drawOut = axis * (BladeLength + DrawClearance);

            // Hotspot dihitung pada pose "dihunus". Koordinat ruang Model: x sepanjang keris, y ke atas.
            b.exploded.SnapTo(1);
            float kx = KerisPivot.x, ky = KerisPivot.y, by = ky + DrawnLift;
            var pos = new Dictionary<string, (string, Vector3)>
            {
                ["hulu"] = Front(b, "Hulu", kx + 0.075f, by),
                ["selut"] = Front(b, "Selut", kx + 0.025f, by),
                ["warangka"] = Front(b, "Warangka", kx - 0.015f, ky + 0.050f),
                ["gandar"] = Front(b, "Gandar", kx - 0.130f, ky),
                ["cincin"] = Front(b, "Cincin", kx - 0.2085f, ky),
                ["pendok"] = Front(b, "Pendok", kx - 0.330f, ky),
                ["jagrak"] = Front(b, "Jagrak", 0f, 0.082f),
                ["ganja"] = Front(b, "Ganja", kx + 0.006f, by - 0.020f),
                ["gandik"] = Front(b, "Wilah", kx - 0.030f, by + 0.012f),
                ["pamor"] = Front(b, "Wilah", kx - 0.150f, by),
                ["luk"] = Front(b, "Wilah", kx - 0.260f, by),
            };
            return Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
        }

        const string Source = "Lembar acuan Keris Bali (cetak biru proyek) + glosarium draf PRD v1.1 §6.1 - menunggu validasi kurator";
        const string Note = "Belum divalidasi (model Blender dari lembar acuan).";

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Keris Bali", "Balinese Keris");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Bali", "Bali");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Bali dengan bilah 9 luk berpamor banyu tetes, ganja maswatu berlapis emas, hulu emas berupa figur dewa yang berlutut menyembah, dan warangka Branggah dari kayu pelet dengan pendok emas bertatah permata, dipajang di jagrak ukir bermotif Karang Boma. Model dibuat di Blender dari lembar acuan, bukan dipindai dari spesimen.",
                "A Balinese-style keris with a 9-wave banyu tetes patterned blade, a gold-clad ganja maswatu, a gold hilt in the form of a kneeling, worshipping deity, and a Branggah sheath of pelet wood with a jewelled gold pendok, displayed on a stand carved with the Karang Boma motif. The model was made in Blender from a reference sheet, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari lembar acuan)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 mengikuti lembar acuan: bilah 40 cm + pesi 8 cm, hulu ±9,5 cm, warangka Branggah lebar 19 cm, jagrak 45 cm. Bukan hasil pengukuran spesimen.",
                "1:1 scale follows the reference sheet: 40 cm blade + 8 cm tang (pesi), ~9.5 cm hilt, 19 cm wide Branggah sheath top, 45 cm stand. Not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KerisBaliCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari lembar acuan: figur hulu, ukiran, dan motif pamor adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from a reference sheet: the hilt figure, carving and pamor pattern are simplified. It will be replaced by an asset from a curator-validated specimen.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note);
            d.hotspots = new List<HotspotData>
            {
                H("hulu", HotspotStage.Utuh, "Hulu (Danganan)", "Hilt (Danganan)", "Bali: danganan - Jawa: ukiran",
                    new LocalizedString("Pada model: emas poles (perlu dikonfirmasi pada spesimen)", "On the model: polished gold (to be confirmed on the specimen)"),
                    new LocalizedString("Pegangan keris berupa figur dewa bermahkota bertingkat (kruna) yang berlutut dengan tangan menyembah, lengkap dengan kalung, sabuk, dan gelang. Identifikasi figurnya perlu dikonfirmasi kurator.",
                        "The keris handle in the form of a deity with a tiered crown (kruna), kneeling with hands in worship, with necklace, belt and bracelets. The identification of the figure needs curator confirmation.")),
                H("selut", HotspotStage.Utuh, "Selut Bertatah Permata", "Jewelled Selut", "Bali: selut / wewer",
                    new LocalizedString("Pada model: emas dengan ruby, zamrud, dan safir (perlu dikonfirmasi)", "On the model: gold with ruby, emerald and sapphire (to be confirmed)"),
                    new LocalizedString("Cincin cawan di pangkal hulu, dihiasi dua baris butiran emas dan sepuluh permata cabochon berselang warna.",
                        "The cup-shaped collar at the base of the hilt, decorated with two rows of gold beads and ten cabochon gems in alternating colours.")),
                H("warangka", HotspotStage.Utuh, "Warangka Branggah", "Branggah Sheath Top", "Bali: warangka branggah",
                    new LocalizedString("Pada model: kayu pelet berpernis (perlu dikonfirmasi)", "On the model: varnished pelet wood (to be confirmed)"),
                    new LocalizedString("Kepala sarung gaya Bali yang melebar dengan dua tanduk melengkung ke atas. Kayu pelet dikenal dari bercak gelapnya yang khas.",
                        "The wide Balinese-style sheath top with two upswept horns. Pelet wood is known for its characteristic dark mottling.")),
                H("gandar", HotspotStage.Utuh, "Gandar", "Gandar (sheath body)", "",
                    new LocalizedString("Pada model: kayu pelet (perlu dikonfirmasi)", "On the model: pelet wood (to be confirmed)"),
                    new LocalizedString("Batang sarung ramping yang membungkus bilah; bagian atasnya dibiarkan kayu.",
                        "The slender sheath body that covers the blade; its upper part is left as bare wood.")),
                H("cincin", HotspotStage.Utuh, "Cincin Permata", "Jewelled Ring", "",
                    new LocalizedString("Pada model: emas dengan zamrud dan ruby (perlu dikonfirmasi)", "On the model: gold with emerald and ruby (to be confirmed)"),
                    new LocalizedString("Cincin emas di tengah sarung dengan zamrud di tengah diapit dua ruby; cincin serupa ada tepat di bawah warangka.",
                        "A gold ring in the middle of the sheath with an emerald flanked by two rubies; a similar ring sits just below the warangka.")),
                H("pendok", HotspotStage.Utuh, "Pendok Emas", "Gold Pendok", "",
                    new LocalizedString("Pada model: emas berukir patra (perlu dikonfirmasi)", "On the model: gold with patra engraving (to be confirmed)"),
                    new LocalizedString("Selongsong logam berukir sulur Bali (patra) yang membalut setengah bawah gandar. Pada tahap terakhir exploded view pendok dilepas.",
                        "The metal sleeve with Balinese scroll engraving (patra) over the lower half of the gandar. In the last exploded-view step the pendok is slid off.")),
                H("jagrak", HotspotStage.Utuh, "Jagrak Karang Boma", "Karang Boma Stand", "Bali: jagrak / tatakan keris",
                    new LocalizedString("Pada model: kayu jati/suren tua (perlu dikonfirmasi)", "On the model: aged teak/suren wood (to be confirmed)"),
                    new LocalizedString("Dudukan pajang keris dengan ukiran tembus: wajah Boma bermata besar dan bertaring di tengah, diapit sayap dan sulur spiral; kakinya bertingkat dengan pita meander.",
                        "A pierced-carved keris display stand: a large-eyed, fanged Boma face in the centre, flanked by wings and spiral scrolls; the base is stepped with a meander band.")),
                H("ganja", HotspotStage.Bilah, "Ganja Maswatu", "Ganja Maswatu", "",
                    new LocalizedString("Pada model: berlapis emas berukir (perlu dikonfirmasi)", "On the model: engraved gold cladding (to be confirmed)"),
                    new LocalizedString("Bagian melintang di pangkal bilah yang dilapisi emas dan meruncing seperti segitiga di sisi greneng. Dilepas pada tahap 3.",
                        "The cross-piece at the base of the blade, clad in gold and tapering like a triangle on the greneng side. Removed in step 3.")),
                H("gandik", HotspotStage.Bilah, "Gandik & Greneng", "Gandik & Greneng", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Pangkal bilah yang melebar (sor-soran) dengan kembang kacang di depan dan gerigi greneng di belakang.",
                        "The widened blade base (sor-soran) with the kembang kacang at the front and the serrated greneng at the back.")),
                H("pamor", HotspotStage.Bilah, "Pamor Banyu Tetes", "Pamor (banyu tetes)", "",
                    new LocalizedString("Campuran besi, baja, dan nikel (perlu dikonfirmasi)", "Iron, steel and nickel (to be confirmed)"),
                    new LocalizedString("Motif keperakan hasil tempa lipat. Banyu tetes berarti 'tetesan air': bentuk seperti tetes berlapis garis di antara urat yang mengalir. Motif pada model dibuat prosedural.",
                        "The silvery pattern from fold-forging. Banyu tetes means 'dripping water': drop shapes ringed by lines among flowing veins. The pattern on the model is procedural.")),
                H("luk", HotspotStage.Bilah, "Luk 9 & Adeg-adeg", "Luk (9 waves) & Ridge", "9 luk (menurut lembar acuan, perlu divalidasi)",
                    default,
                    new LocalizedString("Lekukan bilah berjumlah ganjil - model ini 9 luk dengan panjang 40 cm. Punggungan di tengah bilah disebut adeg-adeg, bidang rata di kiri-kanannya bilik, dan tepinya kikis.",
                        "The blade waves are always odd in number - this model has 9, 40 cm long. The central ridge is the adeg-adeg, the flat areas beside it the bilik, and the edges the kikis.")),
            };
        }
    }
}
