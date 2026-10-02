using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Karambit (kurambiak) Minangkabau jantan dari <c>Tools/blender/karambit.py</c> -> <c>Art/Karambit/karambit.glb</c>.
    /// Karambit tersarung berdiri di dudukan kayu (cincin dijepit dua lengan); pivot root di dasar dudukan, muka -Z.
    /// Punggung bilah adalah busur lingkaran berpusat <see cref="Pusat"/>; sarung ber-pivot di titik itu sehingga
    /// memutarnya = sarung meluncur menyusuri lengkung bilah tanpa menembusnya (bilah cakar tidak bisa dicabut lurus).
    /// Exploded view 5 tahap: sarung dilepas -> pisau diangkat dari dudukan -> hulu dilepas dari puting -> cincin dilepas.
    /// Menu: Nusantara AR / Build Karambit (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KarambitBuilder
    {
        public const string Id = "KARAMBIT_01";
        const string GlbPath = Root + "/Art/Karambit/karambit.glb";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";

        /// <summary>Pusat busur punggung bilah (ruang Model) = PUSAT di karambit.py: (CX, 0, 0) Blender.</summary>
        public static readonly Vector3 Pusat = B(0.041f, 0f, 0f);
        /// <summary>Jari-jari punggung bilah (R_OUT di karambit.py).</summary>
        public const float SpineRadius = 0.052f;
        /// <summary>Putaran sarung saat dilepas: sapuan bilah 120° dari pangkal, mulut sarung di 9°, plus jarak aman.</summary>
        public const float SheathSlideDegrees = 130f;
        /// <summary>Pisau (mata + cincin + hulu) diangkat dari dudukan: naik dan maju agar lolos dari lengan penjepit.</summary>
        static readonly Vector3 Lift = new Vector3(0f, 0.070f, -0.040f);

        [MenuItem("Nusantara AR/Build Karambit")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Karambit selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>
            {
                ["Mata"] = "Mata", ["Cincin"] = "Cincin", ["Hulu"] = "Hulu", ["Sarung"] = "Sarung", ["Dudukan"] = "Dudukan",
            };
            // Pisau ber-pivot di pangkal bilah (titik asal Blender); sarung di pusat busur bilah.
            var pivots = new Dictionary<string, Vector3>
            {
                ["Dudukan"] = Vector3.zero, ["Mata"] = Vector3.zero, ["Cincin"] = Vector3.zero, ["Hulu"] = Vector3.zero,
                ["Sarung"] = Pusat,
            };
            var b = Load(Id, GlbPath, partOf, pivots, "Sarung", "Dudukan");
            UseCompressedTextures(b, "Tools/blender/karambit_textures", Root + "/Art/Karambit");

            var knife = new[] { "Mata", "Cincin", "Hulu" };
            var none = new Dictionary<string, Vector3>();
            // Sudut busur naik dari pangkal (kiri pusat) ke ujung (kanan bawah) = putaran +Z di ruang Model (x, y) = Blender (x, z).
            var slid = Turn(new[] { "Sarung" }, Quaternion.AngleAxis(SheathSlideDegrees, Vector3.forward));
            var s2 = Move(knife, Lift);
            var s3 = Move(new[] { "Hulu" }, Vector3.up * 0.090f, s2);        // puting 4,5 cm lolos dari hulu
            var s4 = Move(new[] { "Cincin" }, Vector3.up * 0.055f, s3);
            // Bilah tidak disembunyikan saat tersarung: celah tipis antara cincin dan mulut sarung tetap tertutup bilah.
            SetStages(b, new string[0],
                ("Utuh (tersarung di dudukan)", "Assembled (sheathed on its stand)", none, null),
                ("Tahap 1: sarung dilepas menyusuri lengkung bilah", "Step 1: sheath slid off along the blade's curve", none, slid),
                ("Tahap 2: karambit diangkat dari dudukan", "Step 2: karambit lifted from the stand", s2, slid),
                ("Tahap 3: hulu dilepas dari puting", "Step 3: hilt removed from the tang", s3, slid),
                ("Tahap 4: cincin dilepas", "Step 4: ferrule removed", s4, slid));

            // Hotspot: koordinat ruang Model (x, y) = (x, z) Blender, dihitung dari rumus karambit.py.
            // Sarung pada pose utuh; bagian lain pada tahap 1 (pisau belum bergerak, sarung sudah menyingkir).
            b.exploded.SnapTo(0);
            var pos = new Dictionary<string, (string, Vector3)> { ["sarung"] = Front(b, "Sarung", 0.019f, -0.038f) };
            b.exploded.SnapTo(1);
            pos["lubang"] = Front(b, "Hulu", -0.0332f, 0.0964f);
            pos["hulu"] = Front(b, "Hulu", -0.0035f, 0.045f);
            pos["cincin"] = Front(b, "Cincin", 0.0f, 0.0035f);
            pos["mata"] = Front(b, "Mata", 0.0196f, -0.0371f);
            pos["gerigi"] = Front(b, "Mata", -0.0026f, -0.0252f);
            pos["ujung"] = Front(b, "Mata", 0.0562f, -0.0468f);
            pos["kaluak"] = Front(b, "Mata", 0.0009f, -0.0140f);
            pos["puting"] = Front(b, "Mata", 0.0f, 0.030f);
            return Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
        }

        const string Source = "Model: rekaan Blender dari foto acuan (Met 36.25.823ab, Kurambiak Minang - Wikimedia) & data bersumber";
        const string Note = KarambitRefs.Checked;

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Karambit Minangkabau", "Minangkabau Karambit");
            d.localName = "Kurambiak";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Minangkabau, Sumatra Barat", "Minangkabau, West Sumatra");
            d.era = new LocalizedString("Tradisi Minangkabau; catatan tertulis tertua 1827", "Minangkabau tradition; earliest written record 1827");
            d.summary = new LocalizedString(
                "Kurambiak, pisau genggam kecil Minangkabau berbilah melengkung seperti cakar harimau, dengan lubang di ujung gagang untuk jari telunjuk. Model ini jenis jantan (7 gerigi di punggung bilah): hulu kayu kemuning berukir, cincin kuningan, dan sarung kayu berukir tinta emas. Model dibuat di Blender dari foto acuan dan data bersumber, bukan dipindai dari spesimen.",
                "The kurambiak, a small Minangkabau hand-held knife whose blade curves like a tiger's claw, with a ring at the end of the grip for the index finger. This model is the male type (7 serrations on the spine): a carved kemuning-wood hilt, a brass ferrule and a wooden sheath carved with gold-ink lines. It was made in Blender from reference photos and sourced data, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari foto acuan & data bersumber)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 direka dari sumber: karambit 10,5 x 15,9 cm tanpa sarung, hulu ±9,7 cm termasuk lubang (Ø dalam 2,2 cm), punggung bilah melengkung 120° sepanjang 10,9 cm, tebal bilah 5 mm. Pembanding: karambit Met Museum 36.25.823ab 14,6 cm. Bukan hasil pengukuran spesimen.",
                "1:1 scale estimated from sources: the karambit is 10.5 x 15.9 cm without its sheath, the hilt ~9.7 cm including the ring (2.2 cm inner diameter), the spine curves 120° over 10.9 cm, the blade is 5 mm thick. For comparison, Met Museum karambit 36.25.823ab is 14.6 cm. Not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KarambitCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari foto acuan: ukiran, motif kaluak paku, dan serat kayu adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from reference photos: the carving, kaluak paku motif and wood grain are simplified. It will be replaced by an asset from a curator-validated specimen.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy = default, LocalizedString history = default, params string[] refs) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);
            d.hotspots = new List<HotspotData>
            {
                H("lubang", HotspotStage.Utuh, "Lubang", "Finger ring (lubang)", "Minangkabau: lubang, lobang (lubang gagang)",
                    new LocalizedString("Pada model: kayu kemuning, menyatu dengan hulu (perlu dikonfirmasi)", "On the model: kemuning wood, one piece with the hilt (to be confirmed)"),
                    new LocalizedString("Lubang di ujung atas gagang tempat jari telunjuk dimasukkan, sehingga lengkung bilah keluar dari bawah kepalan tangan. Ukurannya disesuaikan dengan jari pemiliknya. Karena jari terkait di lubang, karambit sulit dirampas lawan dan tidak mudah lepas dari genggaman.",
                        "The ring at the top of the grip, where the index finger goes, so the curved blade emerges from the bottom of the fist. It is sized to its owner's finger. With the finger hooked through it, the karambit is hard to wrest away and does not slip from the grip."),
                    default, default, KarambitRefs.Jurnal, KarambitRefs.WikiID, KarambitRefs.BeritaMinang),
                H("hulu", HotspotStage.Utuh, "Hulu (Gagang)", "Hilt (hulu)", "Minangkabau: gagang, hulu",
                    new LocalizedString("Pada model: kayu kemuning berukir dengan pita hitam (perlu dikonfirmasi)", "On the model: carved kemuning wood with black bands (to be confirmed)"),
                    new LocalizedString("Gagang dibuat dari kayu, tanduk kerbau, atau gading. Menurut pedoman turun-temurun perajin di Sungai Pua (Agam), gagang idealnya selebar empat buku jari dan sepanjang tiga sampai empat ruas jari telunjuk, lalu disesuaikan dengan pemesan. Kurambiak Minang hanya berwarna di gagangnya, dan hanya dua warna: warna asli kayu dan hitam.",
                        "The grip is made of wood, water-buffalo horn or ivory. By a guideline handed down among the makers of Sungai Pua (Agam), it should ideally be four knuckles wide and three to four index-finger joints long, then adjusted to the customer. A Minang kurambiak is coloured only on its grip, and in just two colours: natural wood and black."),
                    new LocalizedString("Kayu kemuning sering dipilih karena dipercaya bertuah. Sebagian pemilik mengukir gagangnya dengan motif-motif Minangkabau yang memuat falsafah adat.",
                        "Kemuning wood is often chosen because it is believed to carry tuah (spiritual power). Some owners carve the grip with Minangkabau motifs that carry customary philosophy."),
                    new LocalizedString("Dahulu permainan kurambiak hanya diwariskan kepada para datuk atau kalangan raja; tidak sembarang orang boleh menguasainya.",
                        "In the past, the art of the kurambiak was passed down only to datuk (clan leaders) and royalty; not just anyone could master it."),
                    KarambitRefs.BeritaMinang, KarambitRefs.Jurnal, KarambitRefs.WikiEN),
                H("cincin", HotspotStage.Utuh, "Cincin", "Ferrule (cincin)", "",
                    new LocalizedString("Pada model: kuningan dengan dua lis timbul (perlu dikonfirmasi)", "On the model: brass with two raised rims (to be confirmed)"),
                    new LocalizedString("Selongsong logam di pangkal gagang, tempat bilah keluar. Cincin mengikat gagang di sekeliling puting agar kayunya tidak pecah. Pada model dibuat dari kuningan, seperti kurambiak berpangkal logam pada foto acuan.",
                        "The metal collar at the base of the grip, where the blade emerges. It binds the grip around the tang so the wood does not split. On the model it is brass, like the metal-collared kurambiak in the reference photos."),
                    default, default, KarambitRefs.Jurnal, KarambitRefs.Met),
                H("sarung", HotspotStage.Utuh, "Sarung", "Sheath (sarung)", "Minangkabau: sarung, sarang",
                    new LocalizedString("Pada model: kayu berukir bergaris tinta emas, dua pengikat kuningan (perlu dikonfirmasi)", "On the model: carved wood with gold-ink lines and two brass bands (to be confirmed)"),
                    new LocalizedString("Sarung dari kayu atau tanduk kerbau, melengkung mengikuti bilah. Karena bilahnya melengkung, karambit tidak dicabut lurus: sarung dilepas menyusuri lengkung bilah, seperti pada tahap 1. Pada sarung kurambiak Minang terdapat ukiran dari tinta emas.",
                        "The sheath is made of wood or water-buffalo horn and curves with the blade. Because the blade is curved, the karambit is not drawn straight: the sheath slides off along the blade's curve, as in step 1. Minang kurambiak sheaths bear carvings in gold ink."),
                    new LocalizedString("Ornamen klasik adat pada sarung disebut pola pituah adat - hiasan yang membawa petuah (nasihat) adat Minangkabau.",
                        "The classic customary ornament on the sheath is called pola pituah adat - decoration carrying the counsel (petuah) of Minangkabau custom."),
                    default, KarambitRefs.Jurnal, KarambitRefs.WikiEN),
                H("mata", HotspotStage.Bilah, "Mata (Bilah Cakar)", "Blade (mata)", "Minangkabau: kurambiak, karambiak, kurambik",
                    new LocalizedString("Besi/baja tempa; ada pula yang memakai batu meteor (perlu dikonfirmasi)", "Forged iron/steel; some use meteoric iron (to be confirmed)"),
                    new LocalizedString("Bilah bermata satu yang melengkung seperti kuku harimau, dengan sisi tajam di bagian dalam lengkungan. Lengkungnya hampir sembilan puluh derajat; tebalnya sekitar setengah sentimeter di punggung lalu menipis sampai tajam. Pada model, punggung bilah melengkung 120° dengan panjang busur 10,9 cm.",
                        "A single-edged blade curved like a tiger's claw, sharp on the inside of the curve. It bends through almost ninety degrees and is about half a centimetre thick at the spine, thinning to the edge. On the model the spine curves 120° over 10.9 cm."),
                    new LocalizedString("Menurut cerita rakyat, bentuknya meniru cakar harimau Sumatra yang dahulu banyak berkeliaran di hutan - sejalan dengan falsafah Minangkabau alam takambang jadi guru: alam terkembang menjadi guru.",
                        "According to folklore, its shape imitates the claws of the Sumatran tiger that once roamed the forests - in line with the Minangkabau philosophy alam takambang jadi guru: nature unfolded is our teacher."),
                    new LocalizedString("Awalnya karambit adalah alat tani untuk menggaruk akar, mengumpulkan hasil irikan, dan menanam padi, lalu berkembang menjadi senjata silek (silat) Minangkabau, terutama aliran silek harimau.",
                        "The karambit began as a farming tool for raking roots, gathering threshed grain and planting rice, and later became a weapon of Minangkabau silek (silat), especially the tiger style, silek harimau."),
                    KarambitRefs.WikiEN, KarambitRefs.WikiID, KarambitRefs.Jurnal),
                H("gerigi", HotspotStage.Bilah, "Gerigi (Jantan)", "Serrations (male type)", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Kerambit Minangkabau dikenal dalam dua jenis: jantan dan betina. Kerambit jantan punya tujuh gerigi di punggung bawah bilah, sedangkan betina lima. Model ini kerambit jantan.",
                        "Minangkabau karambits come in two types: male (jantan) and female (betina). The male has seven serrations on the lower spine of the blade, the female five. This model is a male karambit."),
                    default,
                    new LocalizedString("Seorang pandai besi di Pudak, Kabupaten Sijunjung, membuat bilah dari baja bekas bar gergaji mesin; bakal bilahnya sepanjang 20 cm dan selebar 3 cm, lalu punggung bawahnya diberi gerigi.",
                        "A blacksmith in Pudak, Sijunjung Regency, makes blades from the steel of used chainsaw bars; the blank is 20 cm long and 3 cm wide, and serrations are then cut into its lower spine."),
                    KarambitRefs.Antara),
                H("ujung", HotspotStage.Bilah, "Ujung", "Point (ujung)", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Ujung runcing yang melengkung ke depan. Karena bilahnya melengkung, karambit bekerja dengan gerakan mengait dan menyayat, bukan menusuk lurus, sehingga dimainkan dalam jarak sangat dekat - idealnya sepasang, di tangan kanan dan kiri.",
                        "The sharp point curving forward. Because the blade is curved, the karambit works with hooking and slicing motions rather than straight thrusts, so it is used at very close range - ideally as a pair, in the right and left hands."),
                    new LocalizedString("Dalam tafsir budaya Minangkabau, lengkung kerambit diibaratkan tulang rusuk yang melindungi organ dalam: senjata yang ampuh berasal dari dalam diri manusia sendiri, seperti tanduk kerbau dan kuku harimau.",
                        "In a Minangkabau cultural reading, the karambit's curve is likened to a rib protecting the inner organs: the mightiest weapon comes from within a person, like the buffalo's horn and the tiger's claw."),
                    default, KarambitRefs.Jurnal),
                H("kaluak", HotspotStage.Bilah, "Motif Kaluak Paku", "Kaluak paku motif", "Minangkabau: kaluak paku (gelung pucuk pakis)",
                    new LocalizedString("Ukiran pada baja (pada model berupa tekstur prosedural)", "Engraved into the steel (a procedural texture on the model)"),
                    new LocalizedString("Ornamen pada bilah kurambiak berupa kaluak paku: pucuk pakis yang bergelung, salah satu motif ukir Minangkabau. Pada model diukir sepasang pilin berhadapan di dekat pangkal bilah.",
                        "The ornament on a kurambiak blade is the kaluak paku: the curled tip of a young fern, one of the Minangkabau carving motifs. On the model a facing pair of spirals is engraved near the base of the blade."),
                    new LocalizedString("Kaluak paku mengisyaratkan kehidupan yang harus diperjuangkan tanpa henti, sambil selalu mengingat yang benar.",
                        "The kaluak paku signifies a life that must be striven for without rest, while always remembering what is right."),
                    default, KarambitRefs.Jurnal),
                H("puting", HotspotStage.Bilah, "Puting", "Tang (puting)", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Tangkai bilah yang masuk ke dalam gagang. Karambit Minangkabau memakai puting separuh (half tang): bilah hanya tertanam sebagian di gagang. Pada model puting sepanjang 4,5 cm dan baru terlihat setelah hulu dilepas pada tahap 3.",
                        "The tang that goes into the grip. The Minangkabau karambit uses a half tang: the blade is only partly embedded in the grip. On the model the tang is 4.5 cm long and only shows once the hilt is removed in step 3."),
                    default,
                    new LocalizedString("Bilah kurambiak dibuat dari besi berkualitas yang ditempa dengan tangan.",
                        "Kurambiak blades are made of good-quality iron forged by hand."),
                    KarambitRefs.BeritaMinang),
            };
        }
    }
}
