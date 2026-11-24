using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static NusantaraAR.EditorTools.GlbArtifact;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Golok Betawi (jenis sorenan) dari <c>Art/Golok/golok.glb</c>: bilah baja 30 cm punggung lurus bermata
    /// melengkung halus, salut kuningan pelindung jari, hulu jengkolan tanduk kerbau hitam berpaku kuningan, serta
    /// sarung tanduk kerbau belang berlilit 14 simpay kuningan dan cantelan pinggang (sangkep).
    /// Exploded view 4 tahap: hunus -> lepas hulu + paku -> lepas salut -> pisah kepala sarung, sangkep & simpay.
    /// Menu: Nusantara AR / Build Golok Betawi (juga dipanggil dari Setup Everything).
    /// </summary>
    public static class GolokBetawiBuilder
    {
        public const string Id = "GOLOK_BETAWI_01";
        const string GlbPath = Root + "/Art/Golok/golok.glb";
        const string ArtDir = Root + "/Art/Golok";
        const string ContentDir = Root + "/Content/" + Id;
        public const string DataPath = ContentDir + "/" + Id + ".asset";
        public const string ThumbPath = ContentDir + "/" + Id + "_thumb.png";
        const string MusicPath = ContentDir + "/Music/" + Id + "_music.mp3";

        /// <summary>Panjang bilah dari pangkal di bawah salut sampai ujung runcing (30 cm).</summary>
        public const float BladeLength = 0.300f;
        /// <summary>Geser bilah ke samping kanan (+X) saat dihunus agar tampil berdampingan dengan sarung.</summary>
        static readonly Vector3 Drawn = new Vector3(0.145f, 0f, 0f);
        /// <summary>Jarak ekstra di atas mulut sarung sebelum bilah melengkung ke samping (animasi hunus).</summary>
        const float DrawClearance = 0.080f;

        [MenuItem("Nusantara AR/Build Golok Betawi")]
        public static void BuildMenu()
        {
            Build();
            ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
            Debug.Log("[NusantaraAR] Golok Betawi selesai dibuat.");
        }

        /// <summary>Batch: -executeMethod NusantaraAR.EditorTools.GolokBetawiBuilder.BuildFullBatch</summary>
        public static void BuildFullBatch()
        {
            try
            {
                Build();
                BuildStoryFromJson(Environment.GetEnvironmentVariable("GOLOK_STORY_JSON"));
                AttachMusic();
                ProjectSetup.RenderThumbnail(DataPath, ThumbPath);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static ArtifactData Build()
        {
            var partOf = new Dictionary<string, string>
            {
                ["Bilah"] = "Bilah",
                ["Salut"] = "Salut",
                ["Hulu"] = "Hulu",
                ["Paku_Hulu"] = "Paku",
                ["Sarung_Badan"] = "Sarung",
                ["Sarung_Kepala"] = "KepalaSarung",
                ["Sarung_Simpay"] = "Simpay",
                ["Sarung_Sangkep"] = "Sangkep",
            };
            var pivots = new Dictionary<string, Vector3>();
            foreach (var p in new[] { "Sarung", "KepalaSarung", "Simpay", "Sangkep", "Bilah", "Salut", "Hulu", "Paku" })
                pivots[p] = Vector3.zero;

            // Sangkep berpusat di Blender +X dan -Y (muka depan), sehingga rightPart="Sangkep" menjamin muka depan di -Z Unity.
            var b = Load(Id, GlbPath, partOf, pivots, "Sangkep", "Sarung");
            UseCompressedTextures(b, ArtDir + "/Textures", ArtDir);

            var weapon = new[] { "Bilah", "Salut", "Hulu", "Paku" };
            var s1 = Move(weapon, Drawn);
            var s2 = Move(new[] { "Paku" }, new Vector3(0f, 0.045f, -0.055f),
                     Move(new[] { "Hulu" }, Vector3.up * 0.095f, s1));
            var s3 = Move(new[] { "Salut" }, Vector3.up * 0.068f, s2);
            var s4 = Move(new[] { "KepalaSarung" }, Vector3.up * 0.058f,
                     Move(new[] { "Sangkep" }, new Vector3(0f, 0f, -0.060f),
                     Move(new[] { "Simpay" }, new Vector3(-0.060f, 0f, 0f), s3)));

            SetStages(b, new[] { "Bilah" },
                ("Utuh (tersarung)", "Assembled (sheathed)", new Dictionary<string, Vector3>()),
                ("Tahap 1: bilah dihunus dari sarung", "Step 1: blade drawn from the sheath", s1),
                ("Tahap 2: hulu dan paku dilepas dari puting", "Step 2: hilt and pin removed from the tang", s2),
                ("Tahap 3: salut kuningan dilepas", "Step 3: brass guard removed", s3),
                ("Tahap 4: kepala sarung, sangkep, dan simpay dipisah", "Step 4: sheath throat, belt hook and bands separated", s4));

            // Animasi hunus: bilah ditarik lurus ke atas sampai ujung lolos mulut sarung, lalu mengayun ke samping.
            b.exploded.stages[1].drawOut = Vector3.up * (BladeLength + DrawClearance);

            // Hotspot dihitung pada pose "dihunus" (tahap 1), kecuali puting pada tahap 2 (saat hulu sudah terangkat).
            b.exploded.SnapTo(1);
            float d = Drawn.x;
            var pos = new Dictionary<string, (string, Vector3)>
            {
                ["hulu"] = Front(b, "Hulu", d - 0.004f, 0.085f),
                ["paku"] = Front(b, "Paku", d + 0.0003f, 0.0252f),
                ["salut"] = Front(b, "Salut", d - 0.006f, 0.0045f),
                ["kepala_sarung"] = Front(b, "KepalaSarung", -0.006f, -0.006f),
                ["sangkep"] = Front(b, "Sangkep", 0.0005f, -0.054f),
                ["simpay"] = Front(b, "Simpay", 0.0f, -0.088f),
                ["sarung"] = Front(b, "Sarung", 0.0f, -0.190f),
                ["bilah"] = Front(b, "Bilah", d - 0.004f, -0.145f),
                ["cap_pande"] = Front(b, "Bilah", d + 0.005f, -0.030f),
            };
            b.exploded.SnapTo(2);
            pos["puting"] = Front(b, "Bilah", d + 0.002f, 0.030f);

            var saved = Save(b, ContentDir, (data, prefab) => Fill(data, prefab, pos));
            AttachMusic();
            return saved;
        }

        [Serializable] class StoryJson { public string voiceID, voiceEN; public StoryChapterJson[] chapters; }
        [Serializable] class StoryChapterJson { public string key, focus, titleID, titleEN, id, en, voiceIDPath, voiceENPath; public int stage = -1; public StoryCue[] cuesID, cuesEN; }

        static void BuildStoryFromJson(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) return;
            var parsed = JsonUtility.FromJson<StoryJson>(File.ReadAllText(jsonPath));
            if (parsed == null || parsed.chapters == null) throw new InvalidOperationException("JSON kisah Golok Betawi tidak valid.");

            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(DataPath);
            if (data == null) throw new InvalidOperationException("ArtifactData Golok Betawi belum dibuat.");

            string storyDir = ContentDir + "/Story";
            Directory.CreateDirectory(storyDir);
            string storyPath = storyDir + "/" + Id + "_Story.asset";
            var story = AssetDatabase.LoadAssetAtPath<ArtifactStory>(storyPath);
            if (story == null)
            {
                story = ScriptableObject.CreateInstance<ArtifactStory>();
                AssetDatabase.CreateAsset(story, storyPath);
            }
            story.voiceCredit = "Microsoft Edge TTS: " + parsed.voiceID + " / " + parsed.voiceEN;
            story.curatorValidated = false;
            story.chapters = new List<StoryChapter>();

            AssetDatabase.Refresh();
            foreach (var c in parsed.chapters)
            {
                story.chapters.Add(new StoryChapter
                {
                    key = c.key,
                    title = new LocalizedString(c.titleID, c.titleEN),
                    text = new LocalizedString(c.id, c.en),
                    stage = c.stage,
                    focusHotspot = c.focus ?? "",
                    voiceID = ImportVoice(c.voiceIDPath),
                    voiceEN = ImportVoice(c.voiceENPath),
                    cuesID = c.cuesID != null ? new List<StoryCue>(c.cuesID) : new List<StoryCue>(),
                    cuesEN = c.cuesEN != null ? new List<StoryCue>(c.cuesEN) : new List<StoryCue>(),
                });
            }
            EditorUtility.SetDirty(story);
            data.story = story;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NusantaraAR] Kisah Golok Betawi: {story.chapters.Count} bab dipasang.");
        }

        static AudioClip ImportVoice(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = imp.defaultSampleSettings;
            imp.forceToMono = true;
            imp.loadInBackground = true;
            settings.preloadAudioData = false;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.5f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = settings;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static void AttachMusic()
        {
            if (!File.Exists(MusicPath)) return;
            AssetDatabase.ImportAsset(MusicPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (AudioImporter)AssetImporter.GetAtPath(MusicPath);
            if (imp != null)
            {
                var settings = imp.defaultSampleSettings;
                imp.forceToMono = false;
                imp.loadInBackground = true;
                settings.preloadAudioData = false;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.4f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                imp.defaultSampleSettings = settings;
                imp.SaveAndReimport();
            }
            var data = AssetDatabase.LoadAssetAtPath<ArtifactData>(DataPath);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            if (data != null && clip != null)
            {
                data.backgroundMusic = clip;
                data.musicCredit = "\"Nusantara - Epic traditional music\" - Pocketbeats (Pixabay, Pixabay Content License)";
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }

        const string Source = "Model: rekaan Blender dari foto acuan Golok Betawi tanduk kerbau & data bersumber";
        const string Note = GolokBetawiRefs.Checked;

        static readonly LocalizedString BetawiHistory = new LocalizedString(
            "Dalam sejarah masyarakat Betawi di Batavia, golok melekat erat dengan keseharian warga dan para jawara. Fungsinya terbagi dua: golok gablong (bendo) untuk kerja tani dan dapur, serta golok sorenan yang diselipkan di pinggang untuk menjaga diri dan melengkapi pakaian adat, termasuk dalam tradisi Palang Pintu. Pada tahun 2021, Kemendikbudristek menetapkan Golok Betawi sebagai Warisan Budaya Takbenda (WBTb) Indonesia dari DKI Jakarta.",
            "In the history of Jakarta's Betawi community, the golok was closely tied to daily life and local martial artists (jawara). It serves two roles: the gablong (bendo) for farming and household work, and the sorenan worn at the waist for self-defense and traditional dress, notably in the Palang Pintu wedding ritual. In 2021, Indonesia's Ministry of Education and Culture designated the Golok Betawi as an Intangible Cultural Heritage of DKI Jakarta.");

        static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> p)
        {
            d.artifactId = Id;
            d.displayName = new LocalizedString("Golok Betawi", "Betawi Golok");
            d.localName = "Golok Sorenan";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("DKI Jakarta (Betawi)", "Jakarta (Betawi)");
            d.era = new LocalizedString("Tradisi Betawi; ditetapkan WBTb Indonesia 2021", "Betawi tradition; Intangible Cultural Heritage 2021");
            d.summary = new LocalizedString(
                "Golok sorenan khas Betawi dengan bilah baja 30 cm berpunggung lurus dan mata melengkung halus, salut pelindung kuningan, hulu jengkolan dari tanduk kerbau hitam berpaku kuningan, serta sarung tanduk kerbau belang berlilit 14 simpay dan sangkep kuningan. Model dibuat di Blender dari foto acuan dan data bersumber, bukan dipindai dari spesimen.",
                "A traditional Betawi sorenan golok with a 30 cm straight-spined steel blade sweeping cleanly to the point, a flared brass guard, a curved black water-buffalo horn hilt with a brass pin, and a striated buffalo-horn sheath bound with 14 brass simpay rings and a belt hook. Made in Blender from reference photos and sourced data, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari foto acuan & data bersumber)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 direka dari proporsi golok sorenan Betawi: bilah 30 cm (lebar perut ±4,1 cm, tebal punggung 4,4 mm), hulu 13,3 cm, total tersarung ±45 cm. Bukan hasil pengukuran spesimen museum.",
                "1:1 scale estimated from Betawi sorenan golok proportions: 30 cm blade (~4.1 cm belly width, 4.4 mm spine thickness), 13.3 cm hilt, ~45 cm overall sheathed. Not measured from a museum specimen.");
            d.markerCode = Marker.MarkerPattern.GolokBetawiCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari foto acuan: corak tanduk belang, ukiran simpay, dan cap pande dibuat prosedural. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from reference photos: the striated horn grain, simpay engraving and smith's mark are procedural. It will be replaced by a curator-validated specimen asset.");

            HotspotData H(string id, HotspotStage st, string tID, string tEN, string reg, LocalizedString mat, LocalizedString craft,
                LocalizedString philosophy = default, LocalizedString history = default, params string[] refs) =>
                Hotspot(p, id, st, tID, tEN, reg, mat, craft, Source, Note, philosophy, history, refs);

            d.hotspots = new List<HotspotData>
            {
                H("hulu", HotspotStage.Utuh, "Hulu (Gagang Jengkolan)", "Curved Horn Hilt (Hulu)", "Betawi: hulu, ulu (gaya jengkolan / eluk)",
                    new LocalizedString("Tanduk kerbau hitam dipoles mengkilap dengan cincin ukir geometris (pada model)", "Polished black water-buffalo horn with carved geometric collar (on the model)"),
                    new LocalizedString("Gagang melengkung ergonomis dari tanduk kerbau hitam padat yang kuat dan tahan lama. Bagian leher bersegi dihiasi pita ukir bergerigi, sementara ujung bongkot melengkung ke sisi jari agar genggaman kokoh dan tidak mudah terlepas saat beraktivitas maupun berlatih silat.",
                        "An ergonomic curved grip shaped from dense, durable black water-buffalo horn. Its faceted neck bears a carved fluted collar band, while the curved pommel hooks toward the fingers for a secure hold that resists slipping during use or silat practice."),
                    new LocalizedString("Ukuran gagang tradisional disesuaikan genggaman pemiliknya (lazim disebut sejengkal tiga jari), melambangkan keserasian antara senjata, kendali diri, dan karakter pemiliknya.",
                        "Traditional hilt dimensions are tailored to the owner's grip, symbolizing harmony between the blade, self-control, and the bearer's character."),
                    BetawiHistory, GolokBetawiRefs.Wbtb2021, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.AbdulChaer),

                H("paku", HotspotStage.Utuh, "Paku Hulu", "Hilt Pin (Paku)", "Betawi: paku / pasak gagang",
                    new LocalizedString("Kuningan poles (pada model)", "Polished brass (on the model)"),
                    new LocalizedString("Paku keling kuningan yang menembus leher hulu dan tangkai puting bilah di dalamnya. Fungsinya mengunci gagang secara mekanis agar tidak melonggar saat golok diayunkan.",
                        "A brass rivet passing through the horn neck and the internal blade tang. It mechanically locks the hilt in place so the handle never loosens during use."),
                    default, default, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.SetuBabakan),

                H("salut", HotspotStage.Utuh, "Salut (Pelindung Jari)", "Brass Guard (Salut)", "Betawi/Sunda: salut, tadah",
                    new LocalizedString("Kuningan muda poles ber-bevel (pada model)", "Beveled polished pale brass (on the model)"),
                    new LocalizedString("Pelindung logam di antara bilah dan gagang yang melebar melengkung ke sisi mata bilah. Salut memperkuat pangkal tanduk agar tidak pecah sekaligus menahan jari agar tidak tergelincir ke sisi tajam.",
                        "A metal bolster between blade and hilt that flares gracefully toward the cutting edge. It reinforces the base of the horn grip against cracking while shielding the fingers from sliding onto the sharp edge."),
                    default, default, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.BudayaID),

                H("kepala_sarung", HotspotStage.Utuh, "Kepala Sarung (Kuping)", "Sheath Throat (Kepala Sarung)", "Betawi: kepala sarung, godong",
                    new LocalizedString("Tanduk kerbau hitam mengkilap (pada model)", "Glossy black water-buffalo horn (on the model)"),
                    new LocalizedString("Tudung mulut sarung dari tanduk kerbau hitam yang melebar asimetris membentuk kuping di sisi mata bilah. Bagian ini membimbing bilah masuk dengan mulus dan melindungi bibir atas sarung dari sayatan.",
                        "A black buffalo-horn throat cap flaring asymmetrically into a wing on the edge side. It guides the blade smoothly into the scabbard and protects the sheath mouth from being nicked."),
                    default, default, GolokBetawiRefs.SetuBabakan, GolokBetawiRefs.BudayaID),

                H("sangkep", HotspotStage.Utuh, "Sangkep (Cantelan Pinggang)", "Belt Hook (Sangkep)", "Betawi: sangkep, cantelan (tradisi nyoren)",
                    new LocalizedString("Kuningan poles melengkung (pada model)", "Curved polished brass (on the model)"),
                    new LocalizedString("Pengait logam melengkung di muka atas sarung untuk menyisipkan golok pada sabuk atau ikat pinggang. Cara membawa golok di pinggang ini dikenal masyarakat Betawi sebagai nyoren, sehingga golok jenis ini disebut golok sorenan.",
                        "A curved metal hook mounted on the upper front of the sheath for clipping onto a belt or sash. Carrying a golok at the waist is known as nyoren in Betawi, giving the golok sorenan its name."),
                    new LocalizedString("Menyandang golok di pinggang melambangkan kesiapsiagaan menjaga keselamatan keluarga dan kampung, serta kehormatan seorang pendekar dalam tradisi Betawi.",
                        "Wearing the golok at the waist symbolizes readiness to safeguard family and neighborhood, as well as a martial artist's honor in Betawi tradition."),
                    new LocalizedString("Dalam upacara pernikahan adat Betawi, golok sorenan yang disisipkan di pinggang menjadi perlengkapan ikonik para pendekar saat memperagakan atraksi pencak silat dan berbalas pantun Palang Pintu.",
                        "In traditional Betawi weddings, the waist-worn golok sorenan is an iconic part of the martial artists' attire during the Palang Pintu silat and rhyming-verse duel."),
                    GolokBetawiRefs.SetuBabakan, GolokBetawiRefs.AbdulChaer, GolokBetawiRefs.JakartaGo),

                H("simpay", HotspotStage.Utuh, "Simpay Kuningan", "Brass Bands (Simpay)", "Betawi/Sunda: simpay, sumpai",
                    new LocalizedString("Kuningan muda berukir motif tumpal/chevron & pita polos (pada model)", "Pale brass with engraved chevron motifs and smooth straps (on the model)"),
                    new LocalizedString("Empat belas lilitan pita kuningan — terdiri dari dua pita lebar berukir di bagian atas, sepuluh pita tipis sepanjang batang, dan sepasang pita di ujung bawah. Simpay mengikat belahan sarung agar tetap rapat dan tidak pecah terkena benturan.",
                        "Fourteen brass wraps — two wide engraved bands near the throat, ten slender straps along the shaft, and a twin pair near the tip. Simpay bands bind the sheath halves tightly so they resist cracking under impact."),
                    new LocalizedString("Secara filosofis, lilitan simpay yang saling mengunci melambangkan ikatan kekeluargaan, persatuan, dan keselarasan hidup bermasyarakat.",
                        "Philosophically, the interlocking simpay rings represent family ties, unity, and harmony within the community."),
                    default, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.Wbtb2021),

                H("sarung", HotspotStage.Utuh, "Sarung Tanduk Belang", "Striated Horn Sheath (Sarung)", "Betawi: sarung, kerosok",
                    new LocalizedString("Tanduk kerbau belang dipoles mengkilap (pada model)", "Polished striated water-buffalo horn (on the model)"),
                    new LocalizedString("Badan sarung pelindung bilah yang meruncing halus ke buntut bawah. Selain dari kayu keras seperti kayu jambu, rambutan, atau sonokeling, golok pusaka dan sorenan pilihan sering memakai tanduk kerbau belang yang bercorak alami gelap dan abu-abu asap.",
                        "The tapering sheath body enclosing the blade. Beyond hardwoods such as guava, rambutan, or rosewood, prized sorenan goloks often use striated buffalo horn with natural dark and smoky-grey bands."),
                    new LocalizedString("Sarung yang menutup rapat sisi tajam mencerminkan pengendalian diri: kekuatan dan keberanian harus tersimpan tenang dan hanya dikeluarkan pada saat yang benar.",
                        "A sheath enclosing the sharp edge reflects self-restraint: strength and courage should remain calm and be drawn only when truly justified."),
                    default, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.SetuBabakan, GolokBetawiRefs.BudayaID),

                H("bilah", HotspotStage.Bilah, "Bilah Golok Sorenan", "Sorenan Blade (Bilah)", "Betawi: bilah golok sorenan",
                    new LocalizedString("Baja tempa poles satin (pada model)", "Satin-polished forged steel (on the model)"),
                    new LocalizedString("Bilah bermata satu sepanjang 30 cm dengan punggung lurus (tebal 4,4 mm menipis ke ujung) dan mata melengkung halus ke ujung runcing. Berbeda dari golok kerja (gablong/bendo) yang berat untuk menebas kayu, golok sorenan dibuat lebih ramping, seimbang, dan lincah untuk bela diri.",
                        "A 30 cm single-edged blade with a straight spine (4.4 mm tapering distally) and a smooth convex belly sweeping to a sharp point. Unlike heavy utility goloks (gablong/bendo) for chopping wood, a sorenan blade is sleeker, balanced, and agile for self-defense."),
                    new LocalizedString("Bilah yang kokoh melambangkan ketegasan menegakkan kebenaran. Dalam falsafah Betawi, pendekar tidak bangga melukai orang lain, tetapi bangga menjaga martabat dan membela yang lemah.",
                        "The sturdy blade stands for firmness in upholding truth. Betawi martial wisdom holds that a warrior takes no pride in harming others, only in defending dignity and the vulnerable."),
                    BetawiHistory, GolokBetawiRefs.Wbtb2021, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.WikiID),

                H("cap_pande", HotspotStage.Bilah, "Cap Pande", "Blacksmith's Mark (Cap Pande)", "Betawi: cap pande / tanda tempa",
                    new LocalizedString("Cap tempa pada baja (prosedural pada model)", "Forged stamp on steel (procedural on the model)"),
                    new LocalizedString("Tanda khusus yang dipukulkan pandai besi tradisional saat bilah masih membara. Cap pande menjadi penanda asal bengkel tempa sekaligus jaminan mutu pengerjaan baja.",
                        "A distinctive mark struck by a traditional blacksmith while the steel is still glowing hot. It identifies the forge and serves as a hallmark of the blade's craftsmanship."),
                    default,
                    new LocalizedString("Keberlanjutan pandai besi tradisional Betawi dan sekitarnya menjadi salah satu pilar pelestarian Golok Betawi sebagai Warisan Budaya Takbenda Indonesia.",
                        "The continuity of traditional Betawi blacksmithing is a key pillar in preserving the Golok Betawi as Indonesian Intangible Cultural Heritage."),
                    GolokBetawiRefs.Wbtb2021, GolokBetawiRefs.JakartaGo),

                H("puting", HotspotStage.Bilah, "Puting (Tangkai Bilah)", "Tang (Puting)", "Betawi/Melayu: puting",
                    new LocalizedString("Baja tempa menyatu dengan bilah (pada model)", "Forged steel integral with the blade (on the model)"),
                    new LocalizedString("Tangkai baja di pangkal bilah yang tertanam ke dalam gagang tanduk dan dikunci paku kuningan. Pada model ini puting sepanjang 5,4 cm dan baru tampak jelas ketika hulu dilepas pada tahap 2.",
                        "The steel tang extending from the blade base into the horn hilt and locked by the brass pin. On this model the tang measures 5.4 cm and becomes visible when the hilt separates in step 2."),
                    default, default, GolokBetawiRefs.JakartaGo, GolokBetawiRefs.BudayaID),
            };
        }
    }
}
