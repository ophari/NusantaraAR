using UnityEngine;
using UnityEngine.UI;

namespace NusantaraAR.UI
{
    /// <summary>
    /// Latar scene Main yang digambar kamera (kanvas Screen Space-Camera jauh di belakang keris), bukan oleh UI:
    /// gradasi gading bergumpal terakota & nila + motif kawung samar. Karena ikut ter-render kamera, panel kaca
    /// (katalog, pengaturan, sheet) mem-blurnya. Varian detail meredupkan gumpalan agar keris tetap fokus.
    /// </summary>
    public class Backdrop : MonoBehaviour
    {
        const float MotifTileUnits = 200f;
        const float Oversize = 0.3f; // tepi tidak tampak walau proyeksi kamera digeser (framing di atas sheet)

        RawImage baseImage, motif;
        Texture2D catalogTexture, stageTexture, kawungTexture;
        RectTransform rt;

        public static Backdrop Create(Camera cam, float distance)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Canvas));
            go.layer = 5;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = distance;
            canvas.sortingOrder = -100;
            var b = go.AddComponent<Backdrop>();
            b.rt = (RectTransform)go.transform;
            b.catalogTexture = ProceduralTextures.Backdrop(1f);
            b.stageTexture = ProceduralTextures.Backdrop(0.35f);
            b.kawungTexture = ProceduralTextures.KawungTile();
            b.baseImage = b.Layer("Base", b.catalogTexture, Color.white);
            b.motif = b.Layer("Kawung", b.kawungTexture, Theme.WithAlpha(Theme.Accent, 0.06f));
            return b;
        }

        RawImage Layer(string name, Texture texture, Color color)
        {
            var img = UIKit.Rect(name, rt).gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = new Vector2(-Oversize * 0.5f, -Oversize);
            r.anchorMax = new Vector2(1f + Oversize * 0.5f, 1f + Oversize);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return img;
        }

        /// <param name="detail">true = 3D Viewer (gumpalan & motif diredupkan).</param>
        public void SetDetail(bool detail)
        {
            baseImage.texture = detail ? stageTexture : catalogTexture;
            motif.color = Theme.WithAlpha(Theme.Accent, detail ? 0.035f : 0.06f);
        }

        void Update()
        {
            // Motif berulang dengan ukuran petak tetap (unit kanvas), apa pun rasio layarnya.
            var size = motif.rectTransform.rect.size;
            if (size.x > 1f && size.y > 1f)
                motif.uvRect = new Rect(0f, 0f, size.x / MotifTileUnits, size.y / MotifTileUnits);
        }

        void OnDestroy()
        {
            Destroy(catalogTexture);
            Destroy(stageTexture);
            Destroy(kawungTexture);
        }
    }
}
