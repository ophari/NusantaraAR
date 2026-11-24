using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Bali (luk 9, pamor banyu tetes, danganan togogan figur dewa, warangka kayu timoho) di atas dudukan ukir Karang Boma,
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
        /// Tarikan ekstra di luar panjang bilah sebelum bilah diangkat (animasi hunus): tanduk warangka sesrengatan
        /// menjorok ±5,5 cm melewati pangkal bilah; bilah yang mulai diangkat harus tetap di atas pucuk tanduk.
        /// </summary>
        const float DrawClearance = 0.13f;

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
                ["Warangka_Sesrengatan"] = "Warangka", ["Warangka_Celah"] = "Warangka", ["Gandar"] = "Gandar",
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
                ("Utuh (tersarung di dudukan)", "Assembled (sheathed, on the stand)", new Dictionary<string, Vector3>()),
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
                ["warangka"] = Front(b, "Warangka", kx - 0.008f, ky + 0.045f),
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

        const string Source = "Model: lembar acuan Keris Bali (cetak biru proyek)";
        const string Note = KerisRefs.Checked;

        /// <summary>Sejarah umum keris + keris dalam budaya Bali (tab Sejarah pada hulu).</summary>
        static readonly LocalizedString BaliHistory = new LocalizedString(
            "Keris tertua yang diketahui berasal dari abad ke-10; relief Candi Borobudur (abad ke-9) sudah menggambarkan senjata mirip keris. Dari Jawa, keris menyebar ke seluruh Nusantara dan Asia Tenggara lewat diplomasi dan perdagangan. UNESCO menetapkannya sebagai Karya Agung Warisan Budaya Lisan dan Takbenda Kemanusiaan pada 2005 (masuk Daftar Representatif 2008). Di Bali, keris diberikan saat upacara potong gigi (mepandes) dan pernikahan sebagai tanda kedewasaan, dan setiap Tumpek Landep (Saniscara Kliwon wuku Landep, tiap 210 hari) keris diupacarai untuk memuja Sang Hyang Siwa Pasupati.",
            "The earliest known keris date from the 10th century; a 9th-century relief at Borobudur already shows a keris-like weapon. From Java, the keris spread across the archipelago and Southeast Asia through diplomacy and trade. UNESCO proclaimed it a Masterpiece of the Oral and Intangible Heritage of Humanity in 2005 (Representative List, 2008). In Bali, a keris is given at the tooth-filing rite (mepandes) and at weddings as a sign of adulthood, and on every Tumpek Landep (Saniscara Kliwon of wuku Landep, every 210 days) keris are blessed in worship of Sang Hyang Siwa Pasupati.");

        /// <summary>Cara pamor dimunculkan (tab Sejarah pada pamor).</summary>
        static readonly LocalizedString PamorMaking = new LocalizedString(
            "Empu menempa lapisan beberapa jenis besi dengan nikel atau besi meteor, dilipat puluhan hingga ratusan kali. Bilah lalu diwarangi: warangan (air jeruk nipis yang difermentasi dicampur batu arsenik) membuat besi dan baja menghitam, sedangkan nikel/meteor tetap putih keperakan - itulah pamor. Bahan pamor yang terkenal adalah meteor yang jatuh di dekat Candi Prambanan pada akhir abad ke-18 dan disimpan Keraton Surakarta.",
            "The empu forges layers of several kinds of iron with nickel or meteoric iron, folded dozens to hundreds of times. The blade is then treated with warangan (fermented lime juice mixed with arsenic ore): the iron and steel turn black while the nickel or meteorite stays silvery white - that is the pamor. A famous pamor source is the meteorite that fell near Prambanan temple at the end of the 18th century, kept by the Surakarta palace.");

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Keris Bali", "Balinese Keris");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Bali", "Bali");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Bali dengan bilah 9 luk berpamor banyu tetes, ganja berlapis emas, hulu (danganan) emas jenis togogan berupa figur dewa yang berlutut menyembah, dan warangka gaya sesrengatan dari kayu timoho berpelet dengan pendok emas bertatah permata, dipajang di dudukan ukir bermotif Karang Boma. Model dibuat di Blender dari lembar acuan, bukan dipindai dari spesimen.",
                "A Balinese-style keris with a 9-wave banyu tetes patterned blade, a gold-clad ganja, a gold togogan-type hilt (danganan) in the form of a kneeling, worshipping deity, and a sesrengatan-style sheath of pelet-figured timoho wood with a jewelled gold pendok, displayed on a stand carved with the Karang Boma motif. The model was made in Blender from a reference sheet, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari lembar acuan)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 mengikuti lembar acuan: bilah 40 cm + pesi 8 cm, hulu ±9,5 cm, warangka sesrengatan lebar 18 cm, dudukan 45 cm. Bukan hasil pengukuran spesimen. (Keris Bali pasca-Majapahit umumnya 35-45 cm.)",
                "1:1 scale follows the reference sheet: 40 cm blade + 8 cm tang (pesi), ~9.5 cm hilt, 18 cm wide sesrengatan sheath top, 45 cm stand. Not measured from a specimen. (Post-Majapahit Balinese keris are typically 35-45 cm.)");
            d.markerCode = Marker.MarkerPattern.KerisBaliCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari lembar acuan: figur hulu, ukiran, dan motif pamor adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from a reference sheet: the hilt figure, carving and pamor pattern are simplified. It will be replaced by an asset from a curator-validated specimen.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy = default, LocalizedString history = default, params string[] refs) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);
            d.hotspots = new List<HotspotData>
            {
                H("hulu", HotspotStage.Utuh, "Hulu (Danganan)", "Hilt (Danganan)", "Bali: danganan - Jawa: ukiran",
                    new LocalizedString("Pada model: emas poles (perlu dikonfirmasi pada spesimen)", "On the model: polished gold (to be confirmed on the specimen)"),
                    new LocalizedString("Pegangan keris berupa figur dewa bermahkota bertingkat (kruna) yang berlutut dengan tangan menyembah, lengkap dengan kalung, sabuk, dan gelang. Hulu berbentuk figur manusia atau dewa seperti ini di Bali disebut jenis togogan. Identifikasi figurnya perlu dikonfirmasi kurator.",
                        "The keris handle in the form of a deity with a tiered crown (kruna), kneeling with hands in worship, with necklace, belt and bracelets. In Bali, hilts shaped as human or divine figures like this are called togogan. The identification of the figure needs curator confirmation."),
                    new LocalizedString("Hulu keris Bali terkenal figuratif: sosok dewa atau raksasa, sering berlapis emas dan bertatah batu mulia seperti ruby. Hulu berbentuk Dewa Ganesha termasuk jenis danganan yang paling populer di Bali.",
                        "Balinese hilts are famously figurative - gods or demons, often gold-clad and set with gems such as rubies. Hilts in the form of Ganesha are among the most popular danganan in Bali."),
                    BaliHistory, KerisRefs.Danganan, KerisRefs.WikiKris, KerisRefs.Unesco, KerisRefs.Borobudur, KerisRefs.KerisBali, KerisRefs.TumpekLandep),
                H("selut", HotspotStage.Utuh, "Selut Bertatah Permata", "Jewelled Selut", "Bali: selut",
                    new LocalizedString("Pada model: emas dengan ruby, zamrud, dan safir (perlu dikonfirmasi)", "On the model: gold with ruby, emerald and sapphire (to be confirmed)"),
                    new LocalizedString("Cincin cawan di pangkal hulu, dihiasi dua baris butiran emas dan sepuluh permata cabochon berselang warna. Keris Bali memang kerap dihias emas, perak, dan batu mulia.",
                        "The cup-shaped collar at the base of the hilt, decorated with two rows of gold beads and ten cabochon gems in alternating colours. Balinese keris are often adorned with gold, silver and precious stones."),
                    default, default, KerisRefs.WikiKris, KerisRefs.KerisBali),
                H("warangka", HotspotStage.Utuh, "Warangka Sesrengatan", "Sesrengatan Warangka", "Jenis warangka Bali: sesrengatan, kojongan, kekandikan, batun poh, beblatungan",
                    new LocalizedString("Pada model: kayu timoho berpelet, berpernis (perlu dikonfirmasi)", "On the model: varnished, pelet-figured timoho wood (to be confirmed)"),
                    new LocalizedString("Kepala sarung gaya sesrengatan: bagian atasnya datar tempat ganja bertumpu, ujung belakangnya pendek dan tumpul membulat, sedangkan ujung depannya menjulang menjadi tanduk runcing. Bali mengenal lima jenis warangka: sesrengatan, kojongan (trapesium tinggi), kekandikan (balok datar), batun poh atau gegodoan (membulat), dan beblatungan (melengkung seperti kait). Sesrengatan dianggap yang terbaik oleh komunitas keris Bali. Siluet model mengikuti foto sampel warangka Bali dalam jurnal; detail ukiran disederhanakan.",
                        "A sesrengatan-style sheath top: a flat top where the ganja rests, a short, blunt, rounded rear end, and a front end that sweeps up into a pointed horn. Bali has five warangka types: sesrengatan, kojongan (tall trapezoid), kekandikan (flat block), batun poh or gegodoan (rounded) and beblatungan (hook-shaped). Sesrengatan is regarded as the finest by the Balinese keris community. The model's silhouette follows photos of Balinese warangka samples in a journal; the carving is simplified."),
                    default,
                    new LocalizedString("Kayu timoho (Kleinhovia hospita) berwarna kuning pucat dengan serat hitam tak beraturan yang disebut pelet. Pelet yang indah sulit didapat sehingga mahal; timoho berpelet sangat digemari untuk warangka di Yogyakarta, Bali, dan Madura. Selain kayu, warangka Bali juga dibuat dari gading, tulang, dan logam.",
                        "Timoho wood (Kleinhovia hospita) is pale yellow with irregular black streaks called pelet. Fine pelet is rare and costly; pelet-figured timoho is highly prized for sheaths in Yogyakarta, Bali and Madura. Besides wood, Balinese sheaths are also made of ivory, bone and metal."),
                    KerisRefs.WarangkaBali, KerisRefs.KerisBali, KerisRefs.Timoho),
                H("gandar", HotspotStage.Utuh, "Gandar", "Gandar (sheath body)", "",
                    new LocalizedString("Pada model: kayu timoho berpelet (perlu dikonfirmasi)", "On the model: pelet-figured timoho wood (to be confirmed)"),
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
                H("jagrak", HotspotStage.Utuh, "Dudukan Karang Boma", "Karang Boma Stand", "",
                    new LocalizedString("Pada model: kayu jati/suren tua (perlu dikonfirmasi)", "On the model: aged teak/suren wood (to be confirmed)"),
                    new LocalizedString("Dudukan pajang keris dengan ukiran tembus: wajah Boma bermata besar dan bertaring di tengah, diapit sayap dan sulur spiral; kakinya bertingkat dengan pita meander.",
                        "A pierced-carved keris display stand: a large-eyed, fanged Boma face in the centre, flanked by wings and spiral scrolls; the base is stepped with a meander band."),
                    new LocalizedString("Karang Boma adalah ornamen Bali berupa wajah raksasa yang biasanya dipahat di atas gerbang pura atau puri (paduraksa/candi kurung). Wajahnya garang, tetapi Boma dipandang sebagai penjaga yang memancarkan energi positif dan melindungi dari kekuatan jahat.",
                        "Karang Boma is a Balinese ornament of a giant's face, usually carved above the gateways of temples and palaces (paduraksa/candi kurung). Its face is fierce, but Boma is seen as a guardian that radiates positive energy and protects against evil forces."),
                    new LocalizedString("Dalam mitologi, Boma (Narakasura) adalah putra Dewa Wisnu dan Dewi Pertiwi; namanya berasal dari bahasa Sanskerta yang berarti 'putra Bumi'.",
                        "In mythology, Boma (Narakasura) is the son of Wisnu and Pertiwi, the Earth goddess; his name comes from Sanskrit for 'son of the Earth'."),
                    KerisRefs.KarangBoma),
                H("ganja", HotspotStage.Bilah, "Ganja", "Ganja (base plate)", "",
                    new LocalizedString("Pada model: berlapis emas berukir (perlu dikonfirmasi)", "On the model: engraved gold cladding (to be confirmed)"),
                    new LocalizedString("Bagian melintang di pangkal bilah; pada model dilapisi emas dan meruncing di sisi greneng. Ganja yang dibuat menyatu dengan bilah disebut ganja iras. Dilepas pada tahap 3.",
                        "The cross-piece at the base of the blade; on the model it is gold-clad and tapers on the greneng side. A ganja forged in one piece with the blade is called ganja iras. Removed in step 3."),
                    default, default, KerisRefs.KerisMelayu),
                H("gandik", HotspotStage.Bilah, "Gandik & Greneng", "Gandik & Greneng", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Pangkal bilah yang melebar (sor-soran) dengan kembang kacang di depan - lengkung yang menyerupai bunga kacang - dan gerigi greneng di belakang.",
                        "The widened blade base (sor-soran) with the kembang kacang at the front - a curl resembling a bean flower - and the serrated greneng at the back."),
                    default, default, KerisRefs.Ricikan),
                H("pamor", HotspotStage.Bilah, "Pamor Banyu Tetes", "Pamor (banyu tetes)", "",
                    new LocalizedString("Campuran besi, baja, dan nikel (perlu dikonfirmasi)", "Iron, steel and nickel (to be confirmed)"),
                    new LocalizedString("Motif keperakan hasil tempa lipat. Banyu tetes (disebut juga tirto tumetes atau tetesing warih) berarti 'tetesan air': lingkaran-lingkaran kecil tak sama besar, kadang bersusun hingga tiga lapis, di antara garis pamor. Motif pada model dibuat prosedural.",
                        "The silvery pattern from fold-forging. Banyu tetes (also tirto tumetes or tetesing warih) means 'dripping water': small circles of uneven size, sometimes ringed up to three layers, among the pamor lines. The pattern on the model is procedural."),
                    new LocalizedString("Tetesan air dimaknai sebagai rezeki yang datang sedikit demi sedikit dari berbagai arah namun terus mengalir, sekaligus pengingat ketekunan: tetesan air pun dapat melubangi batu. Pamor ini tergolong tidak pemilih - cocok untuk siapa saja.",
                        "The water drops stand for fortune that arrives little by little from many directions yet keeps flowing, and for perseverance: even dripping water can hollow out stone. This pamor is considered non-selective - suitable for anyone."),
                    PamorMaking, KerisRefs.BanyuTetes, KerisRefs.Unesco, KerisRefs.Warangan, KerisRefs.WikiKris),
                H("luk", HotspotStage.Bilah, "Luk 9 & Adeg-adeg", "Luk (9 waves) & Ridge", "9 luk (menurut lembar acuan, perlu divalidasi)",
                    default,
                    new LocalizedString("Lekukan bilah disebut luk dan jumlahnya selalu ganjil, umumnya 3 sampai 13 (ada yang sampai 29) - model ini 9 luk dengan panjang 40 cm. Punggungan di tengah bilah disebut adeg-adeg, bidang rata di kiri-kanannya bilik, dan tepinya kikis.",
                        "The blade waves are called luk and are always odd in number, usually 3 to 13 (some up to 29) - this model has 9, 40 cm long. The central ridge is the adeg-adeg, the flat areas beside it the bilik, and the edges the kikis."),
                    default,
                    new LocalizedString("Keris Bali pasca-Majapahit umumnya berukuran 35-45 cm dengan bilah yang tegak, tegas, dan tebal - lebih besar daripada keris Bali yang masih berpengaruh Jawa (33-35 cm). Bilah model ini 40 cm.",
                        "Post-Majapahit Balinese keris are usually 35-45 cm long with upright, bold, thick blades - larger than the earlier Javanese-influenced Balinese keris (33-35 cm). This model's blade is 40 cm."),
                    KerisRefs.WikiKris, KerisRefs.KerisBali),
            };
        }
    }
}
