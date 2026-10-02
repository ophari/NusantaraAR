using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Candi Borobudur dari <c>Tools/blender/candi_borobudur.py</c> -> <c>Art/CandiBorobudur/candi_borobudur.glb</c>
    /// (skala asli 123 x 123 x 35 m; data & sumber di <c>Docs/Borobudur_Data.md</c>).
    /// Satu bagian per tingkat: kaki, 5 teras persegi, 3 teras melingkar, stupa induk. Langkan, relung-arca, tangga, dan
    /// gapura ikut tingkat tempatnya berdiri. Exploded view 4 tahap memisahkan tingkat dari atas ke bawah mengikuti tiga
    /// alam kosmologi Buddha (Arupadhatu, Rupadhatu, Kamadhatu).
    /// Prefab diperkecil 1:200 pada child "Model" (root tetap skala 1 agar pas di AR/kartu). Pivot root di pusat kaki,
    /// muka timur (pintu utama) menghadap -Z. Menu: Nusantara AR / Build Candi Borobudur (juga dari Setup Everything).
    /// </summary>
    public static class CandiBorobudurBuilder
    {
        public const string Id = "BOROBUDUR_01";
        const string GlbPath = Root + "/Art/CandiBorobudur/candi_borobudur.glb";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";

        /// <summary>Skala prefab 1:200: tapak 123 m -> 61,5 cm, tinggi 35 m -> 17,5 cm.</summary>
        public const float Scale = 1f / 200f;
        /// <summary>Jarak antartingkat saat dibongkar (m, ruang Model sebelum diskalakan).</summary>
        const float Gap = 6f;

        /// <summary>Tingkat dari bawah ke atas, dengan z dasar (m, Blender) dan objek Blender penyusunnya.</summary>
        static readonly (string part, float z, string[] objects)[] Levels =
        {
            ("Kaki", 0f, new[] { "Kaki_Candi", "Tangga_Kaki", "Langkan_1", "Arca_Langkan_1", "Gapura_1" }),
            ("Teras1", 4.0f, new[] { "Teras_1", "Tangga_1", "Langkan_2", "Arca_Langkan_2", "Gapura_2" }),
            ("Teras2", 8.4f, new[] { "Teras_2", "Tangga_2", "Langkan_3", "Arca_Langkan_3", "Gapura_3" }),
            ("Teras3", 12.4f, new[] { "Teras_3", "Tangga_3", "Langkan_4", "Arca_Langkan_4", "Gapura_4" }),
            ("Teras4", 16.2f, new[] { "Teras_4", "Tangga_4", "Langkan_5", "Arca_Langkan_5", "Gapura_5" }),
            ("Teras5", 19.8f, new[] { "Teras_5", "Tangga_5" }),
            ("Lingkar1", 20.8f, new[] { "Teras_Melingkar_1", "Tangga_Melingkar_1", "Stupa_Terawang_1", "Arca_Stupa_1" }),
            ("Lingkar2", 22.2f, new[] { "Teras_Melingkar_2", "Tangga_Melingkar_2", "Stupa_Terawang_2", "Arca_Stupa_2" }),
            ("Lingkar3", 23.6f, new[] { "Teras_Melingkar_3", "Tangga_Melingkar_3", "Stupa_Terawang_3", "Arca_Stupa_3" }),
            ("StupaInduk", 25.0f, new[] { "Stupa_Induk" }),
        };

        [MenuItem("Nusantara AR/Build Candi Borobudur")]
        static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Candi Borobudur selesai dibuat.");
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>();
            var pivots = new Dictionary<string, Vector3>();
            foreach (var (part, z, objects) in Levels)
            {
                pivots[part] = B(0f, 0f, z);
                foreach (var o in objects) partOf[o] = part;
            }
            // Candi simetris empat arah: pemeriksa arah +X tidak berarti (rightPart = null).
            var b = Load(Id, GlbPath, partOf, pivots, null, "Kaki");
            UseCompressedTextures(b, "Tools/blender/candi_borobudur_textures", Root + "/Art/CandiBorobudur");

            // Memisahkan batas di bawah tingkat k = mengangkat tingkat k dan semua di atasnya sejauh Gap.
            var names = Levels.Select(l => l.part).ToArray();
            Dictionary<string, Vector3> Split(int k, Dictionary<string, Vector3> basis) =>
                Move(names.Skip(k), Vector3.up * Gap, basis);
            var s1 = Split(9, null);
            var s2 = Split(6, Split(7, Split(8, s1)));
            var s3 = Split(3, Split(4, Split(5, s2)));
            var s4 = Split(1, Split(2, s3));
            SetStages(b, new string[0],
                ("Utuh", "Assembled", new Dictionary<string, Vector3>()),
                ("Tahap 1: stupa induk diangkat", "Step 1: main stupa lifted", s1),
                ("Tahap 2: tiga teras melingkar (Arupadhatu) dipisah", "Step 2: the three circular terraces (Arupadhatu) separated", s2),
                ("Tahap 3: teras persegi 3-5 (Rupadhatu atas) dipisah", "Step 3: square terraces 3-5 (upper Rupadhatu) separated", s3),
                ("Tahap 4: teras persegi 1-2 dipisah dari kaki candi (Kamadhatu)", "Step 4: square terraces 1-2 separated from the base (Kamadhatu)", s4));

            // Hotspot pada pose utuh; koordinat ruang Model (x, y) = (x, z) Blender dalam meter. Sinar dari 100 m di depan
            // candi; titik 0,6 m di depan permukaan = 3 mm setelah diskalakan 1:200.
            b.exploded.SnapTo(0);
            (string, Vector3) F(string part, float x, float y) => Front(b, part, x, y, 100f, 0.003f / Scale);
            var pos = new Dictionary<string, (string, Vector3)>
            {
                ["kaki"] = F("Kaki", -30f, 2.0f),
                ["langkan"] = F("Kaki", 32f, 4.5f),                  // dinding pagar langkan 1 (di bawah relung), sisi kanan agar tak menumpuk titik kaki
                ["gapura"] = F("Kaki", -1.65f, 6.0f),                // tiang gapura timur di celah langkan 1
                ["relief"] = F("Teras1", -20f, 6.2f),                // pita relief dinding lorong pertama
                ["andesit"] = F("Teras2", 20f, 10.4f),
                ["rupadhatu"] = F("Teras3", -20f, 14.3f),
                ["pemugaran"] = F("Teras4", 20f, 18.0f),
                ["arupadhatu"] = F("Lingkar1", -12f, 21.5f),         // dinding teras melingkar pertama
                ["stupa_terawang"] = F("Lingkar1", -2.372f, 24.8f),  // kubah padat stupa terdepan (sudut 264,375°)
                ["stupa_induk"] = F("StupaInduk", 0f, 29.0f),
            };

            // Arca & kisi stupa (±220 ribu segitiga) tidak perlu collider: oklusi hotspot dan ketuk-model cukup memakai
            // teras. Mengurangi pemrosesan collider saat model dimuat di HP.
            foreach (var r in b.parts.Values.SelectMany(p => p.renderers))
                if (r.name.StartsWith("Arca_") || r.name.StartsWith("Stupa_Terawang_"))
                    Object.DestroyImmediate(r.GetComponent<MeshCollider>());

            // Perkecil 1:200 di child "Model"; root tetap skala 1 (dipakai AR, Scan QR, dan slider skala).
            b.model.localPosition *= Scale;
            b.model.localScale = Vector3.one * Scale;
            return Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
        }

        const string Source = "Model: Blender dari data ukuran bersumber + perkiraan (Tools/blender/candi_borobudur.py, Docs/Borobudur_Data.md)";
        const string Note = CandiRefs.Checked;

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Candi Borobudur", "Borobudur Temple");
            d.localName = "Borobudur";
            d.category = ArtifactCategory.Candi;
            d.region = new LocalizedString("Magelang, Jawa Tengah", "Magelang, Central Java");
            d.era = new LocalizedString("Abad ke-9, Wangsa Syailendra", "9th century, Sailendra dynasty");
            d.summary = new LocalizedString(
                "Candi Buddha terbesar di dunia: sembilan tingkat (enam persegi dan tiga melingkar) yang dimahkotai stupa induk, disusun dari sekitar 55.000 m³ batu andesit tanpa semen. Dindingnya memuat sekitar 2.672 panel relief, dengan 504 arca Buddha di relung langkan dan di dalam 72 stupa terawang. Model dibuat di Blender dari data ukuran bersumber; ukuran yang tidak ditemukan sumbernya adalah perkiraan.",
                "The world's largest Buddhist temple: nine levels (six square and three circular) crowned by a main stupa, built from about 55,000 m³ of andesite stone without mortar. Its walls carry some 2,672 relief panels, with 504 Buddha statues in the balustrade niches and inside 72 perforated stupas. The model was made in Blender from sourced dimensions; dimensions without a source are estimates.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari data ukuran bersumber)";
            d.measurementNote = new LocalizedString(
                "Skala 1:200. Denah 123 x 123 m dan tinggi 35 m sampai puncak stupa induk (tanpa chattra) bersumber; tinggi tiap teras, diameter teras melingkar, dan ukuran stupa adalah perkiraan (lihat Docs/Borobudur_Data.md).",
                "1:200 scale. The 123 x 123 m plan and the 35 m height to the top of the main stupa (without the chattra) are sourced; terrace heights, circular terrace diameters and stupa sizes are estimates (see Docs/Borobudur_Data.md).");
            d.markerCode = Marker.MarkerPattern.BorobudurCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender rekonstruksi: relief memakai tekstur bergaya (bukan relief asli), arca disederhanakan tanpa mudra, jaladwara dan arca singa belum dimodelkan. Akan divalidasi kurator.",
                "Reconstructed Blender model: the reliefs are a stylised texture (not the real reliefs), the statues are simplified without mudras, and the water spouts and lion statues are not modelled yet. To be validated by a curator.");

            HotspotData H(string id, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy, LocalizedString history, params string[] refs) =>
                Hotspot(p, id, HotspotStage.Utuh, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);
            var andesit = new LocalizedString("Batu andesit (seluruh candi ±55.000 m³)", "Andesite stone (±55,000 m³ for the whole temple)");
            d.hotspots = new List<HotspotData>
            {
                H("kaki", "Kaki Candi", "Temple Base", "Kamadhatu - alam keinginan", andesit,
                    new LocalizedString("Kaki candi berdenah 123 x 123 m. Kaki yang tampak sekarang adalah kaki tambahan: di baliknya tersembunyi kaki asli berhias 160 panel relief Karmawibhangga tentang hukum sebab-akibat (karma).",
                        "The base measures 123 x 123 m. The base seen today is an added one: behind it lies the original base, decorated with 160 Karmavibhangga relief panels on the law of cause and effect (karma)."),
                    new LocalizedString("Kaki candi melambangkan Kamadhatu, alam keinginan - tingkat terbawah dari tiga alam dalam kosmologi Buddha yang dilalui peziarah menuju puncak.",
                        "The base stands for Kamadhatu, the realm of desire - the lowest of the three realms of Buddhist cosmology that pilgrims pass through on their way to the top."),
                    new LocalizedString("Kaki asli ditutup karena cacat struktur yang muncul saat candi masih dibangun. Relief tersembunyi itu ditemukan tidak sengaja pada 1885 oleh insinyur Belanda Jan Willem IJzerman.",
                        "The original base was covered because of structural defects that appeared while the temple was being built. The hidden reliefs were discovered by chance in 1885 by the Dutch engineer Jan Willem IJzerman."),
                    CandiRefs.WikiEN, CandiRefs.WikiID),
                H("langkan", "Langkan & Relung Arca", "Balustrades & Buddha Niches", "Relung arca Dhyani Buddha",
                    new LocalizedString("Batu andesit; arca Buddha dipahat dari batu yang sama", "Andesite; the Buddha statues are carved from the same stone"),
                    new LocalizedString("Di atas lima tingkat langkan berderet 432 relung, masing-masing berisi arca Buddha duduk: 104, 104, 88, 72, dan 64 relung dari langkan pertama sampai kelima. Pada model, relung dan arca disederhanakan.",
                        "Along the tops of the five balustrade levels stand 432 niches, each holding a seated Buddha: 104, 104, 88, 72 and 64 niches from the first to the fifth balustrade. On the model the niches and statues are simplified."),
                    new LocalizedString("Sikap tangan (mudra) arca mengikuti arah mata angin: timur bhumisparsa mudra (Aksobya), selatan wara mudra (Ratnasambhawa), barat dhyana mudra (Amitabha), utara abhaya mudra (Amoghasiddhi). Di langkan kelima semua arca bersikap witarka mudra (Wairocana).",
                        "The statues' hand gestures (mudras) follow the compass: east bhumisparsa mudra (Akshobhya), south vara mudra (Ratnasambhava), west dhyana mudra (Amitabha), north abhaya mudra (Amoghasiddhi). On the fifth balustrade all statues show vitarka mudra (Vairocana)."),
                    new LocalizedString("Bersama 72 arca di dalam stupa terawang, Borobudur semula memiliki 504 arca Buddha.",
                        "Together with the 72 statues inside the perforated stupas, Borobudur originally had 504 Buddha statues."),
                    CandiRefs.Kompas, CandiRefs.WikiEN, CandiRefs.Britannica),
                H("gapura", "Tangga & Gapura", "Stairways & Gateways", "Pintu utama di sisi timur",
                    andesit,
                    new LocalizedString("Tangga naik tepat di tengah keempat sisi candi dan melewati gapura di setiap tingkat. Pintu masuk utama berada di sisi timur. Pada model, gapura disederhanakan tanpa ukiran kepala kala.",
                        "Stairways rise at the centre of all four sides and pass through a gateway at every level. The main entrance is on the east side. On the model the gateways are simplified, without the carved kala heads."),
                    new LocalizedString("Peziarah naik dari timur lalu berjalan searah jarum jam (pradaksina) mengelilingi setiap tingkat sebelum naik ke tingkat berikutnya - perjalanan simbolis dari alam keinginan menuju alam tanpa bentuk.",
                        "Pilgrims climb from the east and walk clockwise (pradakshina) around each level before going up to the next - a symbolic journey from the realm of desire to the formless realm."),
                    new LocalizedString("Candi ini juga dihiasi 32 arca singa dan 100 jaladwara (pancuran air di sudut-sudut), masing-masing jaladwara berukiran unik. Keduanya belum dimodelkan.",
                        "The temple is also adorned with 32 lion statues and 100 water spouts at the corners, each spout with a unique carving. Neither is modelled yet."),
                    CandiRefs.WikiEN),
                H("relief", "Relief Lorong", "Gallery Reliefs", "2.672 panel relief",
                    new LocalizedString("Pahatan relief pada batu andesit (tekstur model bergaya, bukan relief asli)", "Reliefs carved in andesite (the model uses a stylised texture, not the real reliefs)"),
                    new LocalizedString("Borobudur memuat sekitar 2.672 panel relief - 1.460 naratif dan 1.212 dekoratif - seluas kira-kira 2.500 m². Relief naratif tersusun dalam 11 seri sepanjang sekitar 3.000 m di kaki tertutup dan empat lorong pertama.",
                        "Borobudur carries about 2,672 relief panels - 1,460 narrative and 1,212 decorative - covering some 2,500 m². The narrative reliefs are arranged in 11 series over about 3,000 m on the hidden base and the first four galleries."),
                    new LocalizedString("Relief dibaca sambil berjalan searah jarum jam. Seri Lalitavistara (120 panel) mengisahkan hidup Sang Buddha; Jataka dan Avadana mengisahkan kehidupan-kehidupan sebelumnya; Gandavyuha (372 panel di lorong 2-4) mengisahkan pengembaraan Sudhana mencari kebijaksanaan sampai ia mencapai Kebijaksanaan Sempurna.",
                        "The reliefs are read while walking clockwise. The Lalitavistara series (120 panels) tells the life of the Buddha; the Jataka and Avadana tell his former lives; the Gandavyuha (372 panels in galleries 2-4) follows Sudhana's wandering search until he attains Perfect Wisdom."),
                    new LocalizedString("Seluruh relief naratif berjumlah 1.460 panel: 160 Karmawibhangga di kaki tertutup, 120 Lalitavistara dan 120 Jataka/Avadana di dinding lorong pertama, 600 Jataka/Avadana di langkan lorong 1-2, dan 372 Gandavyuha.",
                        "The narrative reliefs total 1,460 panels: 160 Karmavibhangga on the hidden base, 120 Lalitavistara and 120 Jataka/Avadana on the first gallery wall, 600 Jataka/Avadana on the balustrades of galleries 1-2, and 372 Gandavyuha."),
                    CandiRefs.WikiEN),
                H("andesit", "Batu Andesit Tanpa Semen", "Mortarless Andesite", "",
                    andesit,
                    new LocalizedString("Batu andesit dipotong sesuai ukuran, diangkut ke lokasi, lalu disusun tanpa semen. Sambungan antarbatu dikunci dengan tonjolan, lekukan, dan pasak ekor burung.",
                        "The andesite was cut to size, carried to the site and laid without mortar. The stones are locked together with knobs, indentations and dovetails."),
                    new LocalizedString("Proporsi tinggi kaki, badan, dan kepala candi disebut mengikuti rasio 4 : 6 : 9.",
                        "The heights of the base, body and head of the temple are said to follow a 4 : 6 : 9 ratio."),
                    new LocalizedString("Menurut cerita rakyat Jawa, arsiteknya bernama Gunadharma; nama ini tidak tercatat dalam prasasti.",
                        "According to Javanese folk tales its architect was Gunadharma; the name is not recorded in any inscription."),
                    CandiRefs.WikiEN, CandiRefs.Musacchio),
                H("rupadhatu", "Teras Persegi (Rupadhatu)", "Square Terraces (Rupadhatu)", "Rupadhatu - alam berbentuk",
                    andesit,
                    new LocalizedString("Di atas kaki berdiri lima teras persegi yang makin ke atas makin kecil. Teras pertama mundur 7 m dari tepi kaki; di antara dinding dan langkan terbentuk lorong sempit tempat relief dipahat. Denah sisinya berlekuk-lekuk di sudut.",
                        "Above the base stand five square terraces, each smaller than the one below. The first terrace is set back 7 m from the edge of the base; between the walls and the balustrades run narrow galleries lined with reliefs. The corners of the plan are stepped."),
                    new LocalizedString("Lima teras persegi (badan candi) melambangkan Rupadhatu, alam berbentuk - tingkat tengah di antara alam keinginan (kaki) dan alam tanpa bentuk (puncak).",
                        "The five square terraces (the body) stand for Rupadhatu, the world of forms - the middle level between the realm of desire (the base) and the formless world (the top)."),
                    new LocalizedString("Prasasti Karangtengah (824) menyebut bangunan suci bernama Jinalaya yang diresmikan Pramodhawardhani, putri Samaratungga; bangunan ini sering dikaitkan dengan Borobudur. Tahap pembangunan kelima diperkirakan selesai sekitar 833.",
                        "The Karangtengah inscription (824) mentions a sacred building named Jinalaya inaugurated by Pramodhawardhani, daughter of Samaratungga; it is often linked to Borobudur. The fifth construction stage is estimated to have been completed around 833."),
                    CandiRefs.WikiEN, CandiRefs.FactsDetails),
                H("pemugaran", "Penemuan & Pemugaran", "Rediscovery & Restoration", "",
                    andesit,
                    new LocalizedString("Pemugaran pertama dipimpin Theodoor van Erp pada 1907-1911. Pada pemugaran besar 1975-1982 (biaya US$6.901.243), lebih dari satu juta batu dibongkar lalu diidentifikasi, dicatat, dan dibersihkan satu per satu seperti kepingan puzzle raksasa; fondasi distabilkan dan saluran air ditanam di dalam candi.",
                        "The first restoration was led by Theodoor van Erp in 1907-1911. In the major 1975-1982 restoration (cost US$6,901,243), over one million stones were dismantled, then individually identified, catalogued and cleaned like pieces of a giant jigsaw puzzle; the foundation was stabilised and drainage channels were embedded in the monument."),
                    new LocalizedString("Borobudur tetap menjadi tempat ibadah yang hidup: setiap tahun saat Waisak (purnama Mei atau Juni), umat Buddha Indonesia berjalan mengelilingi candi searah jarum jam (pradaksina) dan bermeditasi.",
                        "Borobudur remains a living place of worship: every year at Vesak (the full moon in May or June), Indonesian Buddhists walk clockwise around the temple (pradakshina) and meditate."),
                    new LocalizedString("Pada 1814 Thomas Stamford Raffles mengutus insinyur H.C. Cornelius, yang bersama 200 orang membersihkan candi dari semak selama dua bulan. Borobudur ditetapkan sebagai Warisan Dunia UNESCO pada 1991. Pada 21 Januari 1985 sembilan stupa rusak parah akibat bom.",
                        "In 1814 Thomas Stamford Raffles sent the engineer H.C. Cornelius, who with 200 men cleared the temple of vegetation over two months. Borobudur was inscribed as a UNESCO World Heritage Site in 1991. On 21 January 1985 nine stupas were badly damaged by bombs."),
                    CandiRefs.WikiEN),
                H("arupadhatu", "Teras Melingkar (Arupadhatu)", "Circular Terraces (Arupadhatu)", "Arupadhatu - alam tanpa bentuk",
                    andesit,
                    new LocalizedString("Tiga teras melingkar tanpa relief berdiri di atas teras persegi, masing-masing dikelilingi satu lingkar stupa terawang: 32, 24, dan 16 stupa.",
                        "Three circular terraces without reliefs rise above the square terraces, each ringed by perforated stupas: 32, 24 and 16 stupas."),
                    new LocalizedString("Tiga teras melingkar dan stupa induk melambangkan Arupadhatu, alam tanpa bentuk. Karena itu arsitekturnya berubah: dari dinding penuh relief di teras persegi menjadi polos di teras melingkar.",
                        "The three circular terraces and the main stupa stand for Arupadhatu, the formless world. The architecture changes accordingly: from walls full of reliefs on the square terraces to plain surfaces on the circular ones."),
                    new LocalizedString("Sembilan tingkat Borobudur terdiri atas enam persegi dan tiga melingkar. Tinggi dan diameter teras melingkar pada model adalah perkiraan.",
                        "Borobudur's nine levels are six square and three circular. The heights and diameters of the circular terraces on the model are estimates."),
                    CandiRefs.WikiEN, CandiRefs.WikiID),
                H("stupa_terawang", "Stupa Terawang", "Perforated Stupas", "72 stupa berlubang",
                    andesit,
                    new LocalizedString("Stupa berbentuk genta dengan lubang-lubang terawang. Lubangnya berbentuk belah ketupat di dua teras bawah dan persegi di teras teratas. Di dalam setiap stupa duduk arca Buddha.",
                        "Bell-shaped stupas pierced with openings: diamond-shaped on the two lower terraces and square on the top terrace. A Buddha statue sits inside each stupa."),
                    new LocalizedString("Ke-72 arca di dalam stupa adalah Dhyani Buddha Vajrasattva dengan sikap tangan dharmacakra mudra (memutar roda dharma).",
                        "The 72 statues inside the stupas are Dhyani Buddha Vajrasattva with the dharmachakra mudra (turning the wheel of the dharma)."),
                    new LocalizedString("Dulu banyak pengunjung percaya menyentuh arca di dalam stupa membawa keberuntungan (mitos Kunto Bimo). Kebiasaan ini sudah lama dilarang demi pelestarian candi.",
                        "Visitors once believed that touching a statue inside a stupa brings luck (the Kunto Bimo myth). The practice has long been banned to protect the temple."),
                    CandiRefs.WikiID, CandiRefs.Kompas),
                H("stupa_induk", "Stupa Induk", "Main Stupa", "",
                    new LocalizedString("Batu andesit", "Andesite stone"),
                    new LocalizedString("Stupa terbesar di puncak candi; titik tertingginya 35 m di atas tanah. Dengan chattra (payung bersusun tiga), tinggi candi dahulu mencapai 42 m. Pada model, diameter dan profil stupa adalah perkiraan.",
                        "The largest stupa, at the summit; its top is 35 m above the ground. With the chattra (three-tiered parasol) the temple once reached 42 m. On the model the stupa's diameter and profile are estimates."),
                    new LocalizedString("Stupa induk adalah puncak perjalanan dari alam keinginan menuju alam tanpa bentuk. Bagian dalamnya kosong.",
                        "The main stupa is the culmination of the journey from the realm of desire to the formless realm. Its interior is empty."),
                    new LocalizedString("Van Erp sempat menyusun kembali chattra di puncak stupa, lalu membongkarnya karena batu aslinya tidak cukup. Chattra itu kini disimpan di museum bersama arca Buddha yang belum selesai.",
                        "Van Erp rebuilt the chattra on top of the stupa, then dismantled it because there were not enough original stones. The chattra is now kept in a museum together with the Unfinished Buddha."),
                    CandiRefs.WikiEN, CandiRefs.WikiID),
            };
        }
    }
}
