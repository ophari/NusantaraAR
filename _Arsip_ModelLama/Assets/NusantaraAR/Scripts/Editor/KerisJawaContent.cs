using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Konten DRAF untuk Keris Jawa (model Blender dari cetak biru). Deskripsi hanya menyebut apa yang tertera pada
    /// cetak biru dan istilah umum; filosofi & sejarah sengaja dibiarkan sebagai penanda untuk kurator.
    /// </summary>
    static class KerisJawaContent
    {
        const string PendingID = "[Draf] Diisi kurator setelah spesimen asli ditetapkan.";
        const string PendingEN = "[Draft] To be written by the curator once the real specimen is chosen.";
        static readonly LocalizedString Pending = new LocalizedString(PendingID, PendingEN);
        const string Source = "Cetak biru Keris Jawa (proyek) + glosarium draf PRD v1.1 §6.1 - menunggu validasi kurator";

        public static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> positions)
        {
            d.artifactId = KerisJawaBuilder.Id;
            d.displayName = new LocalizedString("Keris Jawa (Model Blender)", "Javanese Keris (Blender Model)");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Jawa", "Java");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris gaya Jawa dengan bilah lurus berpamor, hulu Jawa Demam dari kayu sonokeling bersalut kuningan, dan warangka sampir perahu kandang dari kayu kemuning. Model ini dibuat di Blender dari cetak biru, bukan dipindai dari spesimen.",
                "A Javanese-style keris with a straight patterned blade, a Jawa Demam hilt of sonokeling wood with brass fittings, and a boat-shaped (sampir perahu kandang) sheath of kemuning wood. The model was made in Blender from a blueprint, not scanned from a specimen.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model Blender milik proyek (dibuat dari cetak biru)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 mengikuti cetak biru: bilah 34,5 cm + peksi 7,5 cm, hulu 11 cm (lebar 4 cm), warangka lebar 18 cm. Bukan hasil pengukuran spesimen.",
                "1:1 scale follows the blueprint: 34.5 cm blade + 7.5 cm tang (peksi), 11 cm hilt (4 cm wide), 18 cm wide sheath top. Not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KerisJawaCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model Blender dari cetak biru: ukiran, motif pamor, dan serat kayu adalah penyederhanaan. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Blender model from a blueprint: the carving, pamor pattern and wood grain are simplified. It will be replaced by an asset from a curator-validated specimen.");

            d.hotspots = new List<HotspotData>
            {
                Make(positions, "hulu", HotspotStage.Utuh, "Hulu (Jawa Demam)", "Hilt (Jawa Demam)", "Jawa: ukiran / jejeran",
                    new LocalizedString("Pada model: kayu sonokeling dengan tutup kuningan (sesuai cetak biru, perlu dikonfirmasi)", "On the model: sonokeling wood with a brass cap (per the blueprint, to be confirmed)"),
                    new LocalizedString("Pegangan keris yang dipasang pada peksi. Bentuk Jawa Demam membungkuk ke depan; pada cetak biru tingginya 11 cm dengan ukiran utu (sulur spiral) di muka depan.",
                        "The keris handle, fitted onto the peksi. The Jawa Demam form leans forward; on the blueprint it is 11 cm tall with utu carving (paired spirals) on the front.")),
                Make(positions, "selut", HotspotStage.Utuh, "Selut", "Selut (hilt collar)", "",
                    new LocalizedString("Pada model: kuningan berukir (perlu dikonfirmasi)", "On the model: engraved brass (to be confirmed)"),
                    new LocalizedString("Cincin logam di pangkal hulu, di atas mendak. Pada cetak biru berupa kuningan dengan deretan butiran dan bunga.",
                        "The metal collar at the base of the hilt, above the mendak. On the blueprint it is brass with rows of beads and flowers.")),
                Make(positions, "mendak", HotspotStage.Utuh, "Mendak", "Mendak (ring)", "",
                    new LocalizedString("Pada model: kuningan (perlu dikonfirmasi)", "On the model: brass (to be confirmed)"),
                    new LocalizedString("Cincin logam di antara hulu dan ganja. Pada cetak biru berupa cincin kuningan polos.",
                        "A metal ring between the hilt and the ganja. On the blueprint it is a plain brass ring.")),
                Make(positions, "warangka", HotspotStage.Utuh, "Warangka (Sampir Perahu Kandang)", "Warangka (boat-shaped sheath top)", "Jawa: sampir / ladrang",
                    new LocalizedString("Pada model: kayu kemuning (sesuai cetak biru, perlu dikonfirmasi)", "On the model: kemuning wood (per the blueprint, to be confirmed)"),
                    new LocalizedString("Bagian atas sarung yang melebar, tempat ganja bertumpu. Pada cetak biru lebarnya 18 cm, berbentuk perahu dengan tanduk kanan menjulang lebih tinggi.",
                        "The wide upper part of the sheath, where the ganja rests. On the blueprint it is 18 cm wide and boat-shaped, with the right horn rising higher.")),
                Make(positions, "gandar", HotspotStage.Utuh, "Gandar", "Gandar (sheath body)", "",
                    new LocalizedString("Pada model: kayu kemuning (perlu dikonfirmasi)", "On the model: kemuning wood (to be confirmed)"),
                    new LocalizedString("Badan sarung yang membungkus bilah. Pada cetak biru meruncing ke bawah dengan penampang berbentuk perisai.",
                        "The sheath body that covers the blade. On the blueprint it tapers downward with a shield-shaped cross-section.")),
                Make(positions, "ganja", HotspotStage.Bilah, "Ganja", "Ganja", "",
                    new LocalizedString("Besi/baja tempa berpamor (perlu dikonfirmasi)", "Forged, patterned iron/steel (to be confirmed)"),
                    new LocalizedString("Bagian melintang di pangkal bilah. Pada model ini dibuat terpisah dari bilah dan dilepas pada tahap 3.",
                        "The cross-piece at the base of the blade. On this model it is a separate piece, removed in step 3.")),
                Make(positions, "gandik", HotspotStage.Bilah, "Gandik", "Gandik", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Bagian tebal di pangkal depan bilah. Bentuk ricikan pada spesimen ditetapkan kurator.",
                        "The thickened front part at the base of the blade. The exact features on the specimen are set by the curator.")),
                Make(positions, "pamor", HotspotStage.Bilah, "Pamor", "Pamor", "",
                    new LocalizedString("Besi dengan bahan pamor, mis. nikel atau besi meteorit (perlu dikonfirmasi)", "Iron with pamor material, e.g. nickel or meteoric iron (to be confirmed)"),
                    new LocalizedString("Motif keperakan pada permukaan bilah, hasil tempa lipat besi dengan bahan pamor. Motif pada model dibuat prosedural; nama motif ditetapkan kurator dari spesimen.",
                        "The silvery pattern on the blade surface, made by fold-forging iron with pamor material. The pattern on the model is procedural; the motif name is set by the curator from the specimen.")),
                Make(positions, "ada_ada", HotspotStage.Bilah, "Ada-ada & Bilah Lurus", "Ada-ada & Straight Blade", "Bilah lurus (dapur keris lurus, perlu divalidasi)",
                    default,
                    new LocalizedString("Ada-ada adalah tulang tengah yang memanjang di permukaan bilah. Bilah pada cetak biru lurus (tanpa luk), panjang 35-38 cm.",
                        "The ada-ada is the raised central ridge running along the blade. The blade on the blueprint is straight (no luk), 35-38 cm long.")),
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
                curatorNote = "Belum divalidasi (model Blender dari cetak biru)."
            };
        }
    }
}
