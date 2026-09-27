using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Konten DRAF untuk Keris Bali prosedural (dari lembar acuan/cetak biru). Deskripsi hanya menyebut apa yang
    /// terlihat pada model dan istilah umum; filosofi & sejarah sengaja dibiarkan sebagai penanda untuk kurator.
    /// </summary>
    static class KerisBaliContent
    {
        const string PendingID = "[Draf] Diisi kurator setelah spesimen asli ditetapkan.";
        const string PendingEN = "[Draft] To be written by the curator once the real specimen is chosen.";
        static readonly LocalizedString Pending = new LocalizedString(PendingID, PendingEN);
        const string Source = "Lembar acuan Keris Bali (cetak biru proyek) + glosarium draf PRD v1.1 §6.1 - menunggu validasi kurator";

        public static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> positions)
        {
            d.artifactId = KerisBaliBuilder.Id;
            d.displayName = new LocalizedString("Keris Bali (Model Prosedural)", "Balinese Keris (Procedural Model)");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Bali", "Bali");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Bali dengan bilah 11 luk berpamor, hulu figur, mendak berpermata, dan warangka kayu berbentuk perahu di atas dudukan. Model ini disusun dari lembar acuan, bukan dipindai dari spesimen.",
                "A Balinese-style keris with an 11-wave patterned blade, a figural hilt, a jewelled mendak and a boat-shaped wooden sheath on a stand. The model is built from a reference sheet, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model prosedural milik proyek (dibuat dari lembar acuan)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 memakai asumsi panjang bilah 42 cm (keris Bali umumnya lebih panjang dari keris Jawa), bukan hasil pengukuran spesimen.",
                "1:1 scale assumes a 42 cm blade (Balinese keris tend to be longer than Javanese ones); not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KerisBaliCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model prosedural dari lembar acuan: bentuk, ukiran, dan motif pamor adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Procedural model from a reference sheet: the shapes, carving and pamor pattern are simplified. It will be replaced by an asset from a curator-validated specimen.");

            d.hotspots = new List<HotspotData>
            {
                Make(positions, "hulu", HotspotStage.Utuh, "Hulu", "Hilt", "Bali: danganan - Jawa: ukiran",
                    new LocalizedString("Pada model: logam/batu keabu-abuan (perlu dikonfirmasi pada spesimen)", "On the model: greyish metal/stone (to be confirmed on the specimen)"),
                    new LocalizedString("Pegangan keris yang dipasang pada pesi. Pada lembar acuan hulu berbentuk figur duduk bergaya Ganesha di atas alas teratai; identifikasi figurnya perlu dikonfirmasi kurator.",
                        "The keris handle, fitted onto the pesi. On the reference sheet the hilt is a seated Ganesha-style figure on a lotus base; the identification of the figure needs curator confirmation.")),
                Make(positions, "mendak", HotspotStage.Utuh, "Mendak", "Mendak (ring)", "",
                    new LocalizedString("Pada model: emas dengan permata merah (perlu dikonfirmasi)", "On the model: gold with red gems (to be confirmed)"),
                    new LocalizedString("Cincin logam di antara hulu dan ganja. Pada lembar acuan dihiasi deretan permata merah dan butiran emas.",
                        "A metal ring between the hilt and the ganja. On the reference sheet it is set with a row of red gems and gold beads.")),
                Make(positions, "warangka", HotspotStage.Utuh, "Warangka", "Warangka (sheath top)", "",
                    new LocalizedString("Pada model: kayu (bertekstur jati, perlu dikonfirmasi)", "On the model: wood (teak-like grain, to be confirmed)"),
                    new LocalizedString("Bagian atas sarung yang melebar, tempat ganja bertumpu. Pada lembar acuan berbentuk perahu dengan ujung kanan memanjang naik dan ujung kiri melengkung pendek.",
                        "The wide upper part of the sheath, where the ganja rests. On the reference sheet it is boat-shaped, with a long upswept right end and a short curled left end.")),
                Make(positions, "gandar", HotspotStage.Utuh, "Gandar", "Gandar (sheath body)", "",
                    new LocalizedString("Pada model: kayu (perlu dikonfirmasi)", "On the model: wood (to be confirmed)"),
                    new LocalizedString("Badan sarung yang membungkus bilah; sebagian besar tertutup pendok.",
                        "The sheath body that covers the blade; most of it is covered by the pendok.")),
                Make(positions, "pendok", HotspotStage.Utuh, "Pendok", "Pendok (sheath sleeve)", "",
                    new LocalizedString("Pada model: logam berwarna emas berukir (perlu dikonfirmasi)", "On the model: engraved gold-coloured metal (to be confirmed)"),
                    new LocalizedString("Selongsong logam pelapis gandar. Pada lembar acuan berukir, dengan panel gelap di tengah dan roset emas. Pada tahap terakhir exploded view pendok dilepas dari gandar.",
                        "The metal sleeve over the gandar. On the reference sheet it is engraved, with a dark central panel and a gold rosette. In the last exploded-view step the pendok is slid off the gandar.")),
                Make(positions, "ganja", HotspotStage.Bilah, "Ganja", "Ganja", "",
                    new LocalizedString("Besi/baja tempa berpamor (perlu dikonfirmasi)", "Forged, patterned iron/steel (to be confirmed)"),
                    new LocalizedString("Bagian melintang di pangkal bilah. Pada model ini dibuat terpisah dari bilah dan dilepas pada tahap 3.",
                        "The cross-piece at the base of the blade. On this model it is a separate piece, removed in step 3.")),
                Make(positions, "gandik", HotspotStage.Bilah, "Gandik & Greneng", "Gandik & Greneng", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Pangkal bilah yang melebar (sor-soran). Takik-takik kecil di sisi belakang pada model disebut greneng; bentuk ricikan pada spesimen ditetapkan kurator.",
                        "The widened blade base (sor-soran). The small notches on the back edge of the model are called greneng; the exact features on the specimen are set by the curator.")),
                Make(positions, "pamor", HotspotStage.Bilah, "Pamor", "Pamor", "",
                    new LocalizedString("Besi dengan bahan pamor, mis. nikel atau besi meteorit (perlu dikonfirmasi)", "Iron with pamor material, e.g. nickel or meteoric iron (to be confirmed)"),
                    new LocalizedString("Motif pada permukaan bilah, hasil tempa lipat besi dengan bahan pamor. Motif pada model dibuat prosedural; nama motif ditetapkan kurator dari spesimen.",
                        "The pattern on the blade surface, made by fold-forging iron with pamor material. The pattern on the model is procedural; the motif name is set by the curator from the specimen.")),
                Make(positions, "luk", HotspotStage.Bilah, "Luk", "Luk (blade waves)", "11 luk (menurut lembar acuan, perlu divalidasi)",
                    default,
                    new LocalizedString("Lekukan pada bilah. Jumlah luk selalu ganjil; model ini dibuat dengan 11 luk sesuai lembar acuan.",
                        "The waves of the blade. The number of luk is always odd; this model has 11, following the reference sheet.")),
            };
        }

        static HotspotData Make(Dictionary<string, (string part, Vector3 local)> positions, string id, HotspotStage stage,
            string titleID, string titleEN, string regional, LocalizedString material, LocalizedString craft)
        {
            var (part, local) = positions[id];
            return new HotspotData
            {
                hotspotId = id,
                partName = part,
                localPosition = local,
                visibleFrom = stage,
                title = new LocalizedString(titleID, titleEN),
                regionalTerm = regional,
                material = material,
                philosophy = Pending,
                craft = craft,
                history = Pending,
                transcript = craft,
                sources = new List<string> { Source },
                curatorValidated = false,
                curatorNote = "Belum divalidasi (model prosedural dari lembar acuan)."
            };
        }
    }
}
