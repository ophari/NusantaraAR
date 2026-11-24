using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Keris Sumatra (luk 7, pamor wos wutah) dari <c>Tools/blender/keris_sumatra.py</c> -> <c>Art/KerisSumatra/keris_sumatra.glb</c>.
    /// Keris berdiri tersarung di dudukan kayu; pivot root di dasar dudukan, muka menghadap -Z.
    /// Exploded view 4 tahap: hunus -> lepas hulu + pendongkok (mendak) -> lepas ganja -> lepas sampir (warangka).
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
                ("Tahap 1: bilah dihunus dari sarung", "Step 1: blade drawn from the sheath", s1),
                ("Tahap 2: hulu dan pendongkok dilepas", "Step 2: hilt and pendongkok removed", s2),
                ("Tahap 3: ganja dilepas", "Step 3: ganja removed", s3),
                ("Tahap 4: sampir dilepas dari batang", "Step 4: sampir separated from the sheath body", s4));
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

        const string Source = "Model: cetak biru Keris Sumatra (proyek)";
        const string Note = KerisRefs.Checked;

        /// <summary>Sejarah umum keris + keris di dunia Melayu/Sumatra (tab Sejarah pada hulu).</summary>
        static readonly LocalizedString SumatraHistory = new LocalizedString(
            "Keris tertua yang diketahui berasal dari abad ke-10 di Jawa; relief Candi Borobudur (abad ke-9) sudah menggambarkan senjata mirip keris. Lewat diplomasi dan perdagangan, keris menyebar ke Sumatra, Semenanjung Melayu, Thailand selatan, hingga Filipina. UNESCO menetapkannya sebagai Karya Agung Warisan Budaya Lisan dan Takbenda Kemanusiaan pada 2005 (masuk Daftar Representatif 2008). Di dunia Melayu, keris menjadi alat kebesaran dan kelengkapan pakaian adat.",
            "The earliest known keris date from 10th-century Java; a 9th-century relief at Borobudur already shows a keris-like weapon. Through diplomacy and trade the keris spread to Sumatra, the Malay Peninsula, southern Thailand and the Philippines. UNESCO proclaimed it a Masterpiece of the Oral and Intangible Heritage of Humanity in 2005 (Representative List, 2008). In the Malay world, the keris became regalia and part of traditional dress.");

        static readonly LocalizedString PamorMaking = new LocalizedString(
            "Empu menempa lapisan beberapa jenis besi dengan nikel atau besi meteor, dilipat puluhan hingga ratusan kali. Bilah lalu diwarangi: warangan (air jeruk nipis yang difermentasi dicampur batu arsenik) membuat besi dan baja menghitam, sedangkan nikel/meteor tetap putih keperakan - itulah pamor.",
            "The empu forges layers of several kinds of iron with nickel or meteoric iron, folded dozens to hundreds of times. The blade is then treated with warangan (fermented lime juice mixed with arsenic ore): the iron and steel turn black while the nickel or meteorite stays silvery white - that is the pamor.");

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Keris Sumatra", "Sumatran Keris");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Sumatra", "Sumatra");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Sumatra (Melayu) dengan bilah 7 luk berpamor wos wutah, hulu kayu burl berukir yang melengkung seperti kepala burung, pendongkok kuningan, sampir bulan sabit, dan pendok kuningan berukir. Model dibuat di Blender dari cetak biru, bukan dipindai dari spesimen.",
                "A Sumatran (Malay) style keris with a 7-wave wos wutah patterned blade, a carved burl-wood hilt curving like a bird's head, a brass pendongkok, a crescent sampir and engraved brass pendok. The model was made in Blender from a blueprint, not scanned from a specimen.");
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

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy = default, LocalizedString history = default, params string[] refs) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);
            d.hotspots = new List<HotspotData>
            {
                H("hulu", HotspotStage.Utuh, "Hulu", "Hilt (hulu)", "Melayu/Sumatra: hulu (ulu) - Jawa: ukiran, deder",
                    new LocalizedString("Pada model: kayu burl (jati berpola) berpernis (sesuai cetak biru, perlu dikonfirmasi)", "On the model: varnished burl (figured teak) wood (per the blueprint, to be confirmed)"),
                    new LocalizedString("Pegangan keris yang dipasang pada puting (pesi). Bentuknya melengkung seperti pistol dengan kepala bergaya paruh burung; bagian bawahnya diukir motif sulur dan bunga. Hulu keris Melayu biasa dibuat dari kayu, tanduk, gading, atau tulang; di Palembang dahulu dipakai kayu berharga seperti tembesu dan limar.",
                        "The keris handle, fitted onto the tang (pesi). It curves like a pistol grip with a stylised bird-beak head; the lower part is carved with scroll and flower motifs. Malay hilts are usually made of wood, horn, ivory or bone; in Palembang, precious woods such as tembesu and limar were once used."),
                    new LocalizedString("Di Palembang dikenal hulu berbentuk burung atau anak ayam, misalnya hulu ludai yang menyerupai kepala burung enggang laut dan hulu jawa demam anak ayam. Menurut perajin keris Palembang, dilihat dari belakang lekuknya tetap menyerupai orang yang duduk tahiyat dalam salat.",
                        "Palembang knows bird- and chick-shaped hilts, such as the hulu ludai resembling a sea hornbill's head and the jawa demam anak ayam. According to a Palembang keris maker, seen from behind their curve still resembles a person seated in the tahiyat posture of prayer."),
                    SumatraHistory, KerisRefs.Palembang, KerisRefs.KerisMelayu, KerisRefs.Unesco, KerisRefs.WikiKris, KerisRefs.Borobudur),
                H("mendak", HotspotStage.Utuh, "Pendongkok", "Pendongkok (hilt ring)", "Melayu: pendongkok, dokok, memendak - Jawa: mendak",
                    new LocalizedString("Pada model: kuningan timbul (perlu dikonfirmasi)", "On the model: embossed brass (to be confirmed)"),
                    new LocalizedString("Cincin logam bertingkat di pangkal hulu yang menyarungi puting hingga ke ganja. Dalam tradisi Melayu dibuat dari tembaga, perak, atau emas, berbentuk seperti bunga, kadang bertatah permata. Pada model dihiasi deretan butiran dan kelopak.",
                        "The stepped metal ring at the base of the hilt, sleeving the tang down to the ganja. In Malay tradition it is made of copper, silver or gold, shaped like a flower and sometimes set with gems. On the model it is decorated with a row of beads and petals."),
                    default, default, KerisRefs.KerisMelayu),
                H("warangka", HotspotStage.Utuh, "Sampir Bulan Sabit", "Crescent Sampir", "Melayu/Sumatra: sampir - Jawa: warangka",
                    new LocalizedString("Pada model: kayu burl berpernis (perlu dikonfirmasi)", "On the model: varnished burl wood (to be confirmed)"),
                    new LocalizedString("Bagian atas sarung yang lebar dan tebal, tempat ganja masuk. Pada model berbentuk bulan sabit; kedua ujungnya melengkung ke atas dan satu sisi meruncing lebih panjang.",
                        "The broad, thick top of the sheath that receives the ganja. On the model it is crescent-shaped; both ends curve upward and one side tapers longer."),
                    new LocalizedString("Di Palembang dikenal dua bentuk sampir: sampir perahu, lambang budaya sungai Palembang dan Sungai Musi, serta sampir bulan sabit seperti pada model ini.",
                        "Palembang knows two sampir forms: the boat-shaped sampir perahu, a symbol of Palembang's river culture and the Musi River, and the crescent sampir bulan sabit, as on this model."),
                    new LocalizedString("Di Minangkabau, penghulu adat menyelipkan keris di pinggang sebelah kiri. Keris itu melambangkan tempat bertumpu bagi anak kemenakan dan tempat mencurahkan masalah, sekaligus kekuatan dan perlindungan: penghulu siap membela masyarakatnya.",
                        "Among the Minangkabau, a customary leader (penghulu) wears the keris tucked at the left side of the waist. It symbolises a pillar for his kin to lean on and bring their troubles to, as well as strength and protection: the penghulu stands ready to defend his community."),
                    KerisRefs.Palembang, KerisRefs.KerisMelayu, KerisRefs.Minangkabau),
                H("pendok", HotspotStage.Utuh, "Pendok", "Pendok (sheath sleeves)", "",
                    new LocalizedString("Pada model: kuningan/perunggu 85/15 berpatina (perlu dikonfirmasi)", "On the model: patinated 85/15 brass/bronze (to be confirmed)"),
                    new LocalizedString("Selongsong logam repoussé berukir sulur yang membalut batang sarung; pada model terbagi dua: di bawah sampir dan di ujung sarung.",
                        "Repoussé metal sleeves with scroll decoration over the sheath body; on the model they come in two parts: below the sampir and at the sheath tip."),
                    default,
                    new LocalizedString("Pada keris Palembang, lapisan logam luar sarung dibuat dari kuningan, perak, suasa, atau emas, sering berukir halus dan bertatah batu mulia - menambah nilai seni sekaligus nilai ekonominya.",
                        "On Palembang keris, the outer metal casing of the sheath is made of brass, silver, suasa or gold, often finely engraved and set with gems - adding both artistic and economic value."),
                    KerisRefs.Palembang),
                H("gandar", HotspotStage.Utuh, "Batang (Gandar)", "Batang (sheath body)", "Melayu: batang - Jawa: gandar",
                    new LocalizedString("Pada model: kayu nangka (perlu dikonfirmasi)", "On the model: jackwood (to be confirmed)"),
                    new LocalizedString("Badan sarung yang panjang dan membungkus bilah, meruncing ke bawah; dalam tradisi Melayu biasanya dari kayu atau gading. Pada model bagian tengahnya dibiarkan kayu.",
                        "The long sheath body that covers the blade, tapering downward; in Malay tradition usually made of wood or ivory. On the model its middle is left as bare wood."),
                    default, default, KerisRefs.KerisMelayu),
                H("ganja", HotspotStage.Bilah, "Ganja", "Ganja (base plate)", "Melayu: ganja - Jawa: ganja, gonjo",
                    new LocalizedString("Besi/baja tempa berpamor (perlu dikonfirmasi)", "Forged, patterned iron/steel (to be confirmed)"),
                    new LocalizedString("Bantalan besi melintang di pangkal bilah, berlubang untuk puting; satu sisinya tebal dan tumpul, sisi lain meruncing tipis. Pada model dibuat terpisah dan dilepas pada tahap 3.",
                        "The iron cross-piece at the base of the blade, pierced for the tang; one side is thick and blunt, the other tapers thin. On this model it is a separate piece, removed in step 3."),
                    new LocalizedString("Dalam tradisi Melayu, ganja berfungsi melindungi buku jari dari mata keris lawan saat bertikam.",
                        "In Malay tradition, the ganja protects the knuckles from an opponent's blade in a fight."),
                    default, KerisRefs.KerisMelayu),
                H("gandik", HotspotStage.Bilah, "Gandik & Belalai Gajah", "Gandik & Belalai Gajah", "Melayu (Palembang, Riau, Semenanjung): belalai gajah - Jawa: kembang kacang",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Pangkal depan bilah yang tebal, dengan lengkung kecil menyerupai bunga kacang atau belalai gajah - di Palembang, Riau, dan Semenanjung Melayu disebut belalai gajah, di Jawa kembang kacang. Sisi belakang bergerigi disebut greneng.",
                        "The thick front base of the blade, with a small curl resembling a bean flower or an elephant's trunk - called belalai gajah in Palembang, Riau and the Malay Peninsula, and kembang kacang in Java. The serrated back edge is the greneng."),
                    default, default, KerisRefs.Ricikan, KerisRefs.KerisMelayu),
                H("pamor", HotspotStage.Bilah, "Pamor Wos Wutah", "Pamor (wos wutah)", "",
                    new LocalizedString("Baja lipat dengan bahan pamor nikel (perlu dikonfirmasi)", "Fold-forged steel with nickel pamor (to be confirmed)"),
                    new LocalizedString("Urat perak di permukaan bilah hasil tempa lipat. Wos wutah (beras wutah) berarti 'beras tumpah' - motif yang tidak beraturan, tersebar acak di permukaan bilah. Motif pada model dibuat prosedural.",
                        "The silvery veins on the blade surface from fold-forging. Wos wutah (beras wutah) means 'spilled rice' - an irregular pattern scattered randomly over the blade. The pattern on the model is procedural."),
                    new LocalizedString("Beras yang tumpah dari wadah yang penuh melambangkan rasa syukur atas berkah dan buah kerja keras menghidupi keluarga. Pamor ini tergolong tidak pemilih - cocok untuk siapa saja.",
                        "Rice spilling from an overflowing store stands for gratitude for blessings and the fruit of hard work in providing for one's family. This pamor is considered non-selective - suitable for anyone."),
                    PamorMaking, KerisRefs.WosWutah, KerisRefs.Unesco, KerisRefs.Warangan),
                H("luk", HotspotStage.Bilah, "Luk 7", "Luk (7 waves)", "7 luk (menurut cetak biru, perlu divalidasi)",
                    default,
                    new LocalizedString("Lekukan pada bilah disebut luk (Melayu: lok). Jumlahnya selalu ganjil, umumnya 3 sampai 13 (ada yang sampai 29); model ini dibuat dengan 7 luk dan panjang bilah 36 cm sesuai cetak biru.",
                        "The waves of the blade are called luk (Malay: lok). Their number is always odd, usually 3 to 13 (some up to 29); this model has 7, with a 36 cm blade, following the blueprint."),
                    default, default, KerisRefs.WikiKris),
            };
        }
    }
}
