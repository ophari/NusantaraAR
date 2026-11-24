using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Komodo (Varanus komodoensis) dari <c>Tools/blender/komodo.py</c> -> <c>Art/Komodo/komodo.glb</c>: diorama komodo
    /// betina (±2,3 m) di savana Pulau Rinca, di depan gundukan sarang burung gosong yang dipotong sehingga telurnya terlihat
    /// (data & sumber di <c>Docs/Komodo_Data.md</c>). Kepala dipotong di garis mulut: Kepala (rahang atas) dan Rahang
    /// (pivot di engsel rahang); Lidah tersimpan di dalam mulut dan disembunyikan saat utuh.
    /// Exploded view 5 tahap: mulut terbuka & lidah menjulur -> telur dikeluarkan dari sarang -> komodo diangkat dari alas
    /// -> rahang bawah dipisah. Prefab diperkecil 1:10 pada child "Model" (seperti Borobudur 1:200); pivot root di pusat
    /// alas, muka depan (sisi kiri komodo) -Z. Menu: Nusantara AR / Build Komodo (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class KomodoBuilder
    {
        public const string Id = "KOMODO_01";
        const string GlbPath = Root + "/Art/Komodo/komodo.glb";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";

        /// <summary>Skala prefab 1:10: komodo ±2,3 m -> ±23 cm, alas diorama 3,3 m -> 33 cm.</summary>
        public const float Scale = 1f / 10f;
        /// <summary>Engsel rahang (ENGSEL di komodo.py, dicetak skrip Blender), ruang Model sebelum diskalakan.</summary>
        public static readonly Vector3 Hinge = B(-0.9611f, -0.0405f, 0.2187f);
        /// <summary>Pangkal lidah (PANGKAL di komodo.py).</summary>
        public static readonly Vector3 TongueRoot = B(-0.8488f, -0.0400f, 0.2457f);
        /// <summary>Arah moncong (HEAD_DIR di komodo.py): maju ke -X, sedikit menunduk.</summary>
        public static readonly Vector3 HeadDir = B(-0.9630f, 0f, -0.2696f);
        /// <summary>Jarak lidah dijulurkan (JULUR di komodo.py, m).</summary>
        public const float TongueOut = 0.28f;
        /// <summary>Sudut rahang dibuka (JAW_OPEN di komodo.py); +Z ruang Model = ujung rahang turun.</summary>
        public const float JawOpenDegrees = 24f;
        /// <summary>Telur dikeluarkan ke depan dari ceruk sarang (EGG_OUT di komodo.py).</summary>
        public const float EggOut = 0.32f;
        /// <summary>Komodo diangkat dari alas pada tahap 3 (m, sebelum diskalakan).</summary>
        public const float Lift = 0.30f;

        static readonly string[] Animal = { "Tubuh", "Kepala", "Rahang", "Lidah" };

        [MenuItem("Nusantara AR/Build Komodo")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Komodo selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>
            {
                ["Alas"] = "Alas", ["Sarang"] = "Sarang", ["Telur"] = "Telur", ["Tubuh"] = "Tubuh",
                ["Kepala"] = "Kepala", ["Rahang"] = "Rahang", ["Lidah"] = "Lidah",
            };
            var pivots = new Dictionary<string, Vector3>
            {
                ["Alas"] = Vector3.zero, ["Sarang"] = B(1.05f, 0.15f, 0f), ["Telur"] = B(1.05f, -0.22f, 0f),
                ["Tubuh"] = Vector3.zero, ["Kepala"] = Vector3.zero, ["Rahang"] = Hinge, ["Lidah"] = TongueRoot,
            };
            // Sarang di sisi +X (kepala komodo ke -X): pemeriksa arah sumbu glTF.
            var b = Load(Id, GlbPath, partOf, pivots, "Sarang", "Alas");
            UseCompressedTextures(b, "Tools/blender/komodo_textures", Root + "/Art/Komodo");

            var none = new Dictionary<string, Vector3>();
            var open = Turn(new[] { "Rahang" }, Quaternion.AngleAxis(JawOpenDegrees, Vector3.forward));
            var s1 = Move(new[] { "Lidah" }, HeadDir.normalized * TongueOut);
            var s2 = Move(new[] { "Telur" }, Vector3.back * EggOut, s1);
            var s3 = Move(Animal, Vector3.up * Lift, s2);
            // Rahang bawah diturunkan dan dimajukan, kembali mendatar, agar barisan gigi tampak dari samping.
            var s4 = Move(new[] { "Rahang" }, new Vector3(-0.10f, -0.15f, 0f), s3);
            SetStages(b, new[] { "Lidah" },
                ("Utuh (betina di depan sarangnya)", "Assembled (a female in front of her nest)", none, null),
                ("Tahap 1: mulut terbuka, lidah bercabang menjulur", "Step 1: mouth open, forked tongue out", s1, open),
                ("Tahap 2: telur dikeluarkan dari sarang", "Step 2: eggs taken out of the nest", s2, open),
                ("Tahap 3: komodo diangkat dari alas", "Step 3: komodo lifted off the base", s3, open),
                ("Tahap 4: rahang bawah dipisah", "Step 4: lower jaw separated", s4, null));

            // Hotspot pada pose tahap 1 (rahang terbuka, lidah terjulur); koordinat ruang Model (x, y) = (x, z) Blender
            // dalam meter, dicetak oleh komodo.py. Sinar dari 5 m di depan; titik 3 cm di depan permukaan = 3 mm setelah 1:10.
            b.exploded.SnapTo(1);
            (string, Vector3) F(string part, float x, float y) => Front(b, part, x, y, 5f, 0.003f / Scale);
            var pos = new Dictionary<string, (string, Vector3)>
            {
                ["kulit"] = F("Tubuh", -0.3351f, 0.3048f),
                ["cakar"] = F("Tubuh", -0.7745f, 0.0120f),
                ["ekor"] = F("Tubuh", 0.4978f, 0.1016f),
                ["mata"] = F("Kepala", -1.0669f, 0.2412f),
                ["sarang"] = F("Sarang", 1.3300f, 0.2500f),
                ["telur"] = F("Telur", 1.0500f, 0.0290f),
                ["alas"] = F("Alas", -1.2800f, 0.0300f),
                ["lidah"] = F("Lidah", -1.4073f, 0.0893f),
                ["gigi"] = F("Rahang", -1.0600f, 0.1500f),
                ["bisa"] = F("Rahang", -1.0499f, 0.1307f),
            };

            // Perkecil 1:10 di child "Model"; root tetap skala 1 (dipakai AR, Scan QR, dan slider skala).
            b.model.localPosition *= Scale;
            b.model.localScale = Vector3.one * Scale;
            return Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
        }

        const string Source = "Model: rekaan Blender dari foto acuan (Wikimedia Commons, C. J. Sharp, CC BY-SA 4.0) & data bersumber";
        const string Note = KomodoRefs.Checked;

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Komodo", "Komodo Dragon");
            d.localName = "Ora";
            d.category = ArtifactCategory.Satwa;
            d.region = new LocalizedString("Taman Nasional Komodo & pesisir Flores, Nusa Tenggara Timur",
                "Komodo National Park & the Flores coast, East Nusa Tenggara");
            d.era = new LocalizedString("Satwa hidup; dideskripsikan secara ilmiah tahun 1912", "Living species; scientifically described in 1912");
            d.summary = new LocalizedString(
                "Komodo (Varanus komodoensis), kadal terbesar di dunia, hanya hidup di alam liar di Taman Nasional Komodo dan pesisir Pulau Flores, Nusa Tenggara Timur. Diorama ini memperlihatkan komodo betina (±2,3 m) di savana Pulau Rinca, di depan gundukan sarang berisi telur. Komodo adalah satwa nasional Indonesia dan berstatus Terancam (Endangered) menurut IUCN. Model dibuat di Blender dari foto acuan dan data bersumber pada skala 1:10, bukan dipindai dari spesimen.",
                "The Komodo dragon (Varanus komodoensis), the world's largest lizard, lives in the wild only in Komodo National Park and on the coast of Flores, East Nusa Tenggara. This diorama shows a female (~2.3 m) on the Rinca Island savanna in front of a mound nest holding eggs. The Komodo dragon is Indonesia's national animal and is listed as Endangered by the IUCN. The model was made in Blender from reference photos and sourced data at 1:10 scale, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari foto acuan & data bersumber)";
            d.measurementNote = new LocalizedString(
                "Skala 1:10. Panjang komodo pada model ±2,3 m (betina dewasa rata-rata ±2,29 m dan 68-73 kg), moncong sampai panggul ±1,13 m. Proporsi tubuh, gundukan sarang (diperkecil menjadi Ø 1 m, tinggi 42 cm), dan ukuran telur (±9 x 6 cm) adalah rekaan. Bukan hasil pengukuran spesimen.",
                "1:10 scale. The komodo on the model is ~2.3 m long (adult females average ~2.29 m and 68-73 kg), snout to hips ~1.13 m. Body proportions, the mound nest (reduced to 1 m across and 42 cm tall) and the egg size (~9 x 6 cm) are estimates. Not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KomodoCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari foto acuan: bentuk tubuh, sisik, dan sarang adalah penyederhanaan. Akan diganti atau divalidasi ahli satwa dan kurator.",
                "Blender model from reference photos: the body shape, scales and nest are simplified. It will be replaced or validated by a wildlife expert and curator.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy = default, LocalizedString history = default, params string[] refs) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);
            d.hotspots = new List<HotspotData>
            {
                H("kulit", HotspotStage.Utuh, "Kulit & Osteoderm", "Skin & osteoderms", "Nama lokal: ora (Komodo), buaya darat, biawak raksasa",
                    new LocalizedString("Sisik di atas osteoderm (keping tulang); pada model berupa tekstur prosedural", "Scales over osteoderms (small bony plates); a procedural texture on the model"),
                    new LocalizedString("Kulit komodo dewasa tertutup sisik kecil yang diperkuat osteoderm, keping-keping tulang kecil di bawah kulit yang membentuk semacam baju zirah. Pemindaian CT menunjukkan osteoderm komodo dewasa hampir menutupi seluruh kepala dan punya empat bentuk berbeda. Anak komodo yang baru menetas belum punya osteoderm; keping tulang ini baru tumbuh menjelang dewasa.",
                        "An adult Komodo dragon's skin is covered in small scales reinforced by osteoderms, tiny bony plates under the skin that form a kind of chain mail. CT scans show that in adults the osteoderms cover almost the whole head and come in four distinct shapes. Hatchlings have none; the plates only grow as the dragon approaches adulthood."),
                    default,
                    new LocalizedString("Karena anak komodo hidup di pohon dan baru turun ke tanah setelah besar, para peneliti menduga zirah ini terutama melindungi komodo dewasa dari sesamanya, yang sering bertarung memperebutkan wilayah dan pasangan.",
                        "Because young dragons live in trees and only come down once they are large, researchers suggest this armour mainly protects adults from each other, as they often fight over territory and mates."),
                    KomodoRefs.Maisano, KomodoRefs.WikiEN, KomodoRefs.WikiID),
                H("cakar", HotspotStage.Utuh, "Kaki & Cakar", "Legs & claws", "",
                    new LocalizedString("Cakar tanduk; pada model berwarna gelap", "Horny claws; dark on the model"),
                    new LocalizedString("Kaki komodo kekar dan terentang ke samping, masing-masing berjari lima dengan cakar tebal yang melengkung. Pada komodo dewasa, cakar terutama dipakai sebagai senjata dan untuk makan, karena tubuhnya terlalu besar untuk memanjat. Pada model, komodo melangkah diagonal: kaki depan kiri dan kaki belakang kanan maju bersamaan.",
                        "A Komodo dragon's legs are stout and sprawl to the sides, each with five toes ending in thick, curved claws. Adults use their claws mainly as weapons and for feeding, since they are too big to climb. On the model the dragon walks with a diagonal gait: front left and hind right legs forward together."),
                    default,
                    new LocalizedString("Anak komodo memakai cakarnya untuk memanjat: komodo yang baru menetas menghabiskan sekitar 98% waktunya di pohon sampai berumur setidaknya satu tahun, menjauhi komodo dewasa yang bisa memangsanya.",
                        "Young dragons use their claws to climb: hatchlings spend about 98% of their time in trees until they are at least a year old, keeping away from adults that may eat them."),
                    KomodoRefs.WikiID, KomodoRefs.Ksdae, KomodoRefs.WikiEN),
                H("ekor", HotspotStage.Utuh, "Ekor", "Tail", "",
                    new LocalizedString("Berotot dan bersisik; pada model ±1,2 m dari panggul (rekaan)", "Muscular and scaly; ~1.2 m from the hips on the model (estimate)"),
                    new LocalizedString("Ekor komodo berotot dan kira-kira sepanjang tubuhnya. Untuk meraih mangsa yang lebih tinggi, komodo dapat berdiri dengan kaki belakang dan memakai ekornya sebagai penopang.",
                        "The Komodo dragon's tail is muscular and about as long as its body. To reach prey higher up, the dragon can stand on its hind legs and use its tail as a prop."),
                    default, default, KomodoRefs.WikiID, KomodoRefs.WikiEN),
                H("mata", HotspotStage.Utuh, "Mata & Kepala", "Eyes & head", "",
                    new LocalizedString("Pada model: mata hitam mengilap, lubang hidung di dekat ujung moncong", "On the model: glossy black eyes, nostrils near the snout tip"),
                    new LocalizedString("Komodo mampu melihat hingga sejauh sekitar 300 m. Namun untuk menemukan mangsa dan bangkai, komodo lebih mengandalkan penciuman lewat lidahnya (lihat Lidah, setelah mulut dibuka).",
                        "Komodo dragons can see as far as about 300 m. To find prey and carrion, however, they rely more on smell through the tongue (see Tongue, once the mouth is open)."),
                    default, default, KomodoRefs.WikiID, KomodoRefs.WikiEN),
                H("sarang", HotspotStage.Utuh, "Sarang Gundukan", "Mound nest", "",
                    new LocalizedString("Gundukan tanah & serasah buatan burung gosong kaki-jingga (Megapodius reinwardt); pada model dipotong dan diperkecil", "A soil-and-litter mound built by the orange-footed scrubfowl (Megapodius reinwardt); cut away and reduced on the model"),
                    new LocalizedString("Komodo betina meletakkan telurnya di lubang tanah, cekungan di lereng bukit, atau gundukan sarang burung gosong kaki-jingga yang telah ditinggalkan. Di Pulau Komodo, sekitar 62% sarang komodo adalah gundukan sarang burung gosong. Pada model, muka depan gundukan dipotong agar telur di dalamnya terlihat.",
                        "Female Komodo dragons lay their eggs in holes in the ground, hollows in hillsides, or abandoned mounds built by the orange-footed scrubfowl. On Komodo Island about 62% of dragon nests are scrubfowl mounds. On the model the front of the mound is cut away so the eggs inside can be seen."),
                    default, default, KomodoRefs.Jessop, KomodoRefs.WikiID, KomodoRefs.WikiEN),
                H("telur", HotspotStage.Utuh, "Telur", "Eggs", "",
                    new LocalizedString("Bercangkang liat; pada model ±9 x 6 cm (rekaan), 7 butir tampak", "Leathery shells; ~9 x 6 cm on the model (estimate), 7 shown"),
                    new LocalizedString("Sarang komodo rata-rata berisi 20 telur. Telur diletakkan sekitar bulan September dan menetas setelah 7-8 bulan, sekitar bulan April. Anak yang baru menetas panjangnya sekitar 46 cm.",
                        "A Komodo nest holds about 20 eggs on average. They are laid around September and hatch after 7-8 months, around April. Hatchlings are about 46 cm long."),
                    new LocalizedString("Menurut legenda Ata Modo (orang Pulau Komodo), Putri Naga melahirkan anak kembar: bayi laki-laki bernama Gerong dan bayi komodo bernama Orah, yang dibesarkan terpisah. Ketika dewasa, Gerong hampir menombak seekor komodo di hutan, tetapi Putri Naga muncul dan berkata bahwa komodo itu saudara kembarnya. Karena itu orang Komodo menyebut komodo sebae (kembar) dan turun-temurun ikut menjaganya.",
                        "According to a legend of the Ata Modo (the people of Komodo Island), Princess Naga gave birth to twins: a boy named Gerong and a baby dragon named Orah, raised apart. As a grown man, Gerong was about to spear a dragon in the forest when Princess Naga appeared and told him the dragon was his twin. That is why the Komodo people call the dragons sebae (twins) and have protected them for generations."),
                    new LocalizedString("Komodo betina juga dapat bertelur tanpa kawin (partenogenesis). Hal ini tercatat pada dua komodo betina di kebun binatang Inggris, London dan Chester; semua anaknya jantan.",
                        "Female Komodo dragons can also lay eggs without mating (parthenogenesis). This was recorded in two females at British zoos, London and Chester; all their offspring were male."),
                    KomodoRefs.WikiEN, KomodoRefs.WikiID, KomodoRefs.KompasLegenda, KomodoRefs.Floresa, KomodoRefs.Watts),
                H("alas", HotspotStage.Utuh, "Savana Pulau Rinca", "Rinca Island savanna", "",
                    new LocalizedString("Pada model: tanah kering, batu, dan rumput savana", "On the model: dry soil, rocks and savanna grass"),
                    new LocalizedString("Komodo hidup di padang rumput terbuka (sabana) dan hutan musim di lembah pesisir. Di Taman Nasional Komodo, yang sebagian besar berupa sabana, komodo tersebar di beberapa pulau, terutama Pulau Komodo dan Rinca; populasinya pada 2024 diperkirakan 3.270 ± 371 ekor. Di pesisir Flores hidup sekitar 701 ± 131 ekor lagi.",
                        "Komodo dragons live in open grassland (savanna) and monsoon forest in coastal valleys. In Komodo National Park, which is mostly savanna, they live on several islands, chiefly Komodo and Rinca; the 2024 population was estimated at 3,270 ± 371. About 701 ± 131 more live on the coast of Flores."),
                    new LocalizedString("Komodo ditetapkan sebagai satwa nasional Indonesia melalui Keputusan Presiden Nomor 4 Tahun 1993, bersama ikan siluk merah (satwa pesona) dan elang jawa (satwa langka).",
                        "The Komodo dragon was named Indonesia's national animal by Presidential Decree No. 4 of 1993, alongside the red arowana (charm animal) and the Javan hawk-eagle (rare animal)."),
                    new LocalizedString("Taman Nasional Komodo didirikan tahun 1980 untuk melindungi komodo dan habitatnya, lalu ditetapkan UNESCO sebagai Situs Warisan Dunia tahun 1991. Sejak 2021 IUCN menggolongkan komodo sebagai Terancam (Endangered), antara lain karena habitatnya diperkirakan menyusut akibat perubahan iklim.",
                        "Komodo National Park was founded in 1980 to protect the dragons and their habitat, and was inscribed by UNESCO as a World Heritage Site in 1991. Since 2021 the IUCN has listed the Komodo dragon as Endangered, partly because climate change is projected to shrink its habitat."),
                    KomodoRefs.Ksdae, KomodoRefs.Keppres, KomodoRefs.TnkWiki, KomodoRefs.Unesco, KomodoRefs.Iucn),
                H("lidah", HotspotStage.Bilah, "Lidah Bercabang", "Forked tongue", "",
                    new LocalizedString("Pada model: kuning, dijulurkan ±28 cm pada tahap 1", "On the model: yellow, extended ~28 cm in step 1"),
                    new LocalizedString("Lidah komodo panjang, kuning, dan bercabang. Komodo menjulurkan lidahnya untuk menangkap partikel bau dari udara, lalu membawanya ke organ Jacobson di langit-langit mulut. Dengan cara ini komodo dapat mendeteksi bangkai dari jarak 4-9,5 km.",
                        "The Komodo dragon's tongue is long, yellow and forked. The dragon flicks it out to pick up scent particles from the air and carries them to the Jacobson's organ in the roof of its mouth. This way it can detect carrion 4-9.5 km away."),
                    default, default, KomodoRefs.WikiID, KomodoRefs.WikiEN),
                H("gigi", HotspotStage.Bilah, "Gigi Berlapis Besi", "Iron-coated teeth", "",
                    new LocalizedString("Gigi bergerigi berujung jingga (lapisan kaya besi); pada model 48 gigi tampak", "Serrated teeth with orange, iron-rich tips; 48 teeth shown on the model"),
                    new LocalizedString("Komodo punya sekitar 60 gigi bergerigi seperti gigi hiu, panjangnya sampai 2,5 cm, yang diganti kira-kira setiap 40 hari. Penelitian tahun 2024 menemukan lapisan kaya besi berwarna jingga di ujung dan gerigi giginya. Lapisan ini menjaga gigi tetap tajam untuk merobek mangsa besar seperti rusa timor, babi hutan, dan kerbau.",
                        "Komodo dragons have about 60 serrated, shark-like teeth up to 2.5 cm long, replaced roughly every 40 days. A 2024 study found an orange, iron-rich coating on the tips and serrations of the teeth. It keeps them sharp for tearing large prey such as Timor deer, wild boar and water buffalo."),
                    default, default, KomodoRefs.LeBlanc, KomodoRefs.WikiEN, KomodoRefs.Ksdae),
                H("bisa", HotspotStage.Bilah, "Kelenjar Bisa", "Venom glands", "",
                    new LocalizedString("Kelenjar di dalam rahang bawah (tidak dimodelkan; titik menandai letaknya)", "Glands inside the lower jaw (not modelled; the point marks their position)"),
                    new LocalizedString("Di rahang bawah komodo terdapat kelenjar bisa. Bisanya menghambat pembekuan darah, menurunkan tekanan darah, dan dapat melumpuhkan otot, sehingga mangsa yang tergigit cepat lemas karena kehilangan darah.",
                        "The Komodo dragon's lower jaw holds venom glands. The venom stops blood from clotting, lowers blood pressure and can paralyse muscles, so a bitten animal quickly weakens from blood loss."),
                    default,
                    new LocalizedString("Temuan kelenjar bisa ini (2009) menggantikan anggapan lama bahwa mangsa komodo mati karena bakteri berbahaya di air liurnya.",
                        "This discovery of venom glands (2009) replaced the old idea that the dragon's prey died from dangerous bacteria in its saliva."),
                    KomodoRefs.Fry, KomodoRefs.WikiEN),
            };
        }
    }
}
