using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR.EditorTools
{
    /// <summary>
    /// Konten DRAF untuk model keris sementara, diambil dari glosarium PRD v1.1 §6.1.
    /// Semua hotspot ditandai belum divalidasi; bagian filosofi & sejarah sengaja dibiarkan
    /// sebagai penanda agar tidak ada klaim budaya yang dikarang sebelum diisi kurator.
    /// </summary>
    static class PlaceholderContent
    {
        const string PendingID = "[Draf] Diisi kurator setelah spesimen asli ditetapkan.";
        const string PendingEN = "[Draft] To be written by the curator once the real specimen is chosen.";
        static readonly LocalizedString Pending = new LocalizedString(PendingID, PendingEN);
        const string Source = "Glosarium draf PRD v1.1 §6.1 - menunggu validasi kurator";

        public static void Fill(ArtifactData d, GameObject prefab, Dictionary<string, (string part, Vector3 local)> positions)
        {
            d.artifactId = "KERIS_PLACEHOLDER_01";
            d.displayName = new LocalizedString("Keris (Model Sementara)", "Keris (Temporary Model)");
            d.localName = "Keris";
            d.category = ArtifactCategory.KerisSenjata;
            d.region = new LocalizedString("Asal daerah belum ditentukan", "Region not yet determined");
            d.era = new LocalizedString("Era belum ditentukan", "Era not yet determined");
            d.summary = new LocalizedString(
                "Keris adalah senjata tikam khas nusantara yang diakui UNESCO sebagai warisan budaya takbenda (2005). Model ini menampilkan keris beserta warangka di atas dudukan.",
                "The keris is an Indonesian dagger recognised by UNESCO as intangible cultural heritage (2005). This model shows a keris with its sheath on a display stand.");
            d.prefab = prefab;
            d.specimenOwner = "";
            d.collectionNumber = "";
            d.license = "Model sementara - rekonstruksi dari satu foto (proyek keris3d)";
            d.measurementNote = new LocalizedString(
                "Skala 1:1 memakai asumsi panjang bilah 35 cm (bilah keris lazim 33-38 cm), bukan hasil pengukuran spesimen.",
                "1:1 scale assumes a 35 cm blade (typical keris blades are 33-38 cm); not measured from a specimen.");
            d.markerCode = Marker.MarkerPattern.KerisPlaceholderCode;
            d.isPlaceholder = true;
            d.placeholderNote = new LocalizedString(
                "Model sementara hasil rekonstruksi dari satu foto: sisi samping dan belakang adalah perkiraan, dan warangka/gandar/pendok belum dipisah sesuai struktur aslinya. Akan diganti aset dari spesimen asli yang divalidasi kurator.",
                "Temporary model reconstructed from a single photo: the sides and back are estimated, and the sheath parts are not yet separated as in the real object. It will be replaced by an asset from a curator-validated specimen.");

            d.hotspots = new List<HotspotData>
            {
                Make(positions, "hulu", HotspotStage.Utuh, "Hulu", "Hilt", "Jawa: ukiran - Bali: danganan",
                    new LocalizedString("Kayu (perlu dikonfirmasi pada spesimen)", "Wood (to be confirmed on the specimen)"),
                    new LocalizedString("Pegangan keris, dipasang pada pesi (tangkai bilah). Bentuk dan motif ukirannya berbeda menurut gaya daerah.",
                        "The keris handle, fitted onto the pesi (blade tang). Its shape and carving differ by regional style.")),
                Make(positions, "mendak", HotspotStage.Utuh, "Mendak", "Mendak (ring)", "",
                    new LocalizedString("Logam (perlu dikonfirmasi)", "Metal (to be confirmed)"),
                    new LocalizedString("Cincin logam di antara hulu dan ganja; sering diberi hiasan.",
                        "A metal ring between the hilt and the ganja, often decorated.")),
                Make(positions, "warangka", HotspotStage.Utuh, "Warangka", "Warangka (sheath top)", "",
                    new LocalizedString("Kayu (perlu dikonfirmasi)", "Wood (to be confirmed)"),
                    new LocalizedString("Bagian atas sarung yang melebar; tempat ganja bertumpu saat keris disarungkan.",
                        "The wide upper part of the sheath, where the ganja rests when the keris is sheathed.")),
                Make(positions, "gandar", HotspotStage.Utuh, "Gandar & Pendok", "Gandar & Pendok (sheath body)", "",
                    new LocalizedString("Kayu dengan selongsong logam (perlu dikonfirmasi)", "Wood with a metal sleeve (to be confirmed)"),
                    new LocalizedString("Gandar adalah badan sarung yang membungkus bilah; pendok adalah selongsong logam pelapisnya. Pada model sementara ini keduanya masih satu objek.",
                        "The gandar is the sheath body that covers the blade; the pendok is its metal sleeve. In this temporary model they are still one object.")),
                Make(positions, "ganja", HotspotStage.Bilah, "Ganja", "Ganja", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Bagian melintang di pangkal bilah. Bisa berupa bagian terpisah, atau menyatu dengan bilah (ganja iras).",
                        "The cross-piece at the base of the blade. It can be a separate piece, or one with the blade (ganja iras).")),
                Make(positions, "gandik", HotspotStage.Bilah, "Gandik", "Gandik", "",
                    new LocalizedString("Besi/baja tempa (perlu dikonfirmasi)", "Forged iron/steel (to be confirmed)"),
                    new LocalizedString("Bagian tebal di pangkal depan bilah. Pada banyak keris terdapat kembang kacang (lengkungan) di sini. Posisi titik pada model sementara masih perkiraan.",
                        "The thick front part of the blade base. Many keris have a kembang kacang (curl) here. The point position on this temporary model is approximate.")),
                Make(positions, "pamor", HotspotStage.Bilah, "Pamor", "Pamor", "",
                    new LocalizedString("Besi dengan bahan pamor, mis. nikel atau besi meteorit (perlu dikonfirmasi)", "Iron with pamor material, e.g. nickel or meteoric iron (to be confirmed)"),
                    new LocalizedString("Motif pada permukaan bilah, hasil tempa lipat besi dengan bahan pamor. Nama motif ditetapkan kurator dari spesimen.",
                        "The pattern on the blade surface, made by fold-forging iron with pamor material. The motif name is set by the curator from the specimen.")),
                Make(positions, "luk", HotspotStage.Bilah, "Luk", "Luk (blade waves)", "",
                    default,
                    new LocalizedString("Lekukan pada bilah. Jumlah luk selalu ganjil; jumlah pada spesimen dihitung dan divalidasi kurator.",
                        "The waves of the blade. The number of luk is always odd; the count on the specimen is verified by the curator.")),
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
                curatorNote = "Belum divalidasi (model sementara)."
            };
        }
    }
}
