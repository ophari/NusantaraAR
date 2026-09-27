using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>
    /// Exploded view bertahap (PRD FR-07). Tahap 0 = utuh; tahap berikutnya mengikuti urutan fisik
    /// (hunus bilah, lepas hulu + mendak, lepas ganja, lepas warangka/pendok). "Gabung" membalik urutan.
    /// Setiap tahap menyimpan pose lokal lengkap untuk semua bagian yang bergerak.
    /// </summary>
    public class ExplodedViewController : MonoBehaviour
    {
        [Serializable]
        public class PartPose
        {
            public Transform part;
            public Vector3 localPosition;
            public Quaternion localRotation = Quaternion.identity;
        }

        [Serializable]
        public class Stage
        {
            public LocalizedString label;
            public List<PartPose> poses = new List<PartPose>();
            [Tooltip("Renderer yang disembunyikan di tahap ini (mis. bilah di dalam sarung)")]
            public List<Renderer> hiddenRenderers = new List<Renderer>();
            [Tooltip("Jalur 'cabut dari sarung' saat masuk tahap ini (ruang lokal induk bagian): bagian yang bergerak " +
                     "ditarik lurus sejauh vektor ini dulu (sampai pucuk lolos dari mulut sarung), lalu melengkung ke pose tahap. " +
                     "Nol = interpolasi lurus biasa.")]
            public Vector3 drawOut;
        }

        public List<Stage> stages = new List<Stage>();
        [Tooltip("Durasi animasi per tahap (PRD: <= 1 detik)")]
        [Range(0.1f, 1f)] public float secondsPerStage = 0.55f;
        [Tooltip("Durasi animasi mencabut/menyarungkan bilah (tahap dengan drawOut)")]
        [Range(0.3f, 3f)] public float secondsPerDraw = 1.6f;

        int current;
        int target;
        int heading;
        Coroutine running;

        /// <summary>Dipanggil setiap kali sebuah tahap tercapai.</summary>
        public event Action<int> StageChanged;

        public int CurrentStage => current;
        public int StageCount => stages.Count;
        public bool IsAnimating => running != null;
        public int TargetStage => target;
        /// <summary>True bila sudah/sedang menuju keadaan terbongkar penuh (tahap terakhir).</summary>
        public bool IsExplodedOrExploding => stages.Count > 1 && target == stages.Count - 1;
        public bool IsAssembled => current == 0 && target == 0;
        /// <summary>Tahap 1 adalah "bilah dicabut dari sarung" (punya jalur cabut), jadi bisa dipakai tombol Hunus.</summary>
        public bool CanDraw => stages.Count > 1 && stages[1].drawOut != Vector3.zero;

        /// <summary>Label tahap saat ini; selama animasi, label tahap yang sedang dituju.</summary>
        public string CurrentStageLabel
        {
            get
            {
                int i = IsAnimating ? heading : current;
                return i < stages.Count ? stages[i].label.Get() : string.Empty;
            }
        }

        void Awake()
        {
            if (stages.Count > 0) SnapTo(0);
        }

        public void Toggle()
        {
            if (stages.Count < 2) return;
            GoTo(IsExplodedOrExploding ? 0 : stages.Count - 1);
        }

        public void Explode() => GoTo(stages.Count - 1);
        public void Assemble() => GoTo(0);

        /// <summary>Sedang menganimasikan cabut/sarung (tahap 0 &lt;-&gt; 1).</summary>
        public bool IsDrawAnimating => IsAnimating && CanDraw && current + heading == 1;

        /// <summary>
        /// Bounds dunia yang dilalui animasi cabut: seluruh artefak + bagian bergerak di titik "tercabut lurus"
        /// dan di sudut lengkungnya. Dipakai 3D Viewer untuk mundur sementara agar bilah tidak keluar layar.
        /// </summary>
        public bool TryGetDrawSweepBounds(out Bounds bounds)
        {
            bounds = default;
            if (!CanDraw) return false;
            bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return false;
            var draw = stages[1].drawOut;
            for (int i = 0; i < stages[0].poses.Count && i < stages[1].poses.Count; i++)
            {
                var p0 = stages[0].poses[i];
                var p1 = stages[1].poses[i];
                if (p0.part == null || p0.localPosition == p1.localPosition) continue;
                var parent = p0.part.parent;
                foreach (var target in new[] { p0.localPosition + draw, p1.localPosition + draw })
                {
                    var shift = parent != null ? parent.TransformVector(target - p0.part.localPosition) : target - p0.part.localPosition;
                    foreach (var r in p0.part.GetComponentsInChildren<Renderer>(true))
                        bounds.Encapsulate(new Bounds(r.bounds.center + shift, r.bounds.size));
                }
            }
            return true;
        }

        /// <summary>Hunus / sarungkan: hanya tahap 0 &lt;-&gt; 1, tanpa membongkar bagian lain.</summary>
        public void ToggleDraw()
        {
            if (!CanDraw) return;
            GoTo(target == 1 ? 0 : 1);
        }

        public void GoTo(int stage)
        {
            stage = Mathf.Clamp(stage, 0, Mathf.Max(0, stages.Count - 1));
            target = stage;
            if (running != null) StopCoroutine(running);
            running = null;
            if (!isActiveAndEnabled || stage == current)
            {
                if (stage != current) SnapTo(stage);
                return;
            }
            running = StartCoroutine(Run(stage));
        }

        /// <summary>Langsung menerapkan pose sebuah tahap tanpa animasi.</summary>
        public void SnapTo(int stage)
        {
            if (stages.Count == 0) return;
            stage = Mathf.Clamp(stage, 0, stages.Count - 1);
            if (running != null) StopCoroutine(running);
            running = null;
            foreach (var p in stages[stage].poses)
            {
                if (p.part == null) continue;
                p.part.localPosition = p.localPosition;
                p.part.localRotation = p.localRotation;
            }
            ApplyVisibility(stage);
            current = target = stage;
            StageChanged?.Invoke(current);
        }

        IEnumerator Run(int goal)
        {
            while (current != goal)
            {
                int next = current + Math.Sign(goal - current);
                heading = next;
                yield return Animate(current, next);
                current = next;
                StageChanged?.Invoke(current);
            }
            running = null;
        }

        IEnumerator Animate(int from, int to)
        {
            var a = stages[from];
            var b = stages[to];

            // Tampilkan bagian yang akan terlihat di tahap tujuan sebelum bergerak.
            foreach (var r in a.hiddenRenderers)
                if (r != null && !b.hiddenRenderers.Contains(r)) r.enabled = true;

            var moves = new List<(Transform t, Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1)>();
            foreach (var pb in b.poses)
            {
                if (pb.part == null) continue;
                moves.Add((pb.part, pb.part.localPosition, pb.part.localRotation, pb.localPosition, pb.localRotation));
            }

            // Mencabut (masuk tahap ber-drawOut) atau menyarungkan (keluar dari tahap itu ke tahap sebelumnya).
            var draw = to > from ? b.drawOut : a.drawOut;
            bool drawPath = draw != Vector3.zero && Mathf.Abs(to - from) == 1;
            bool reverse = to < from;
            float seconds = drawPath ? secondsPerDraw : secondsPerStage;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, seconds));
                float s = Mathf.SmoothStep(0f, 1f, t);
                foreach (var m in moves)
                {
                    if (drawPath && m.p0 != m.p1)
                    {
                        // Jalur didefinisikan dari arah "tersarung -> tercabut"; dibalik untuk menyarungkan.
                        var sheathed = reverse ? m.p1 : m.p0;
                        var drawn = reverse ? m.p0 : m.p1;
                        m.t.localPosition = DrawPath(sheathed, drawn, draw, reverse ? 1f - t : t);
                    }
                    else m.t.localPosition = Vector3.LerpUnclamped(m.p0, m.p1, s);
                    m.t.localRotation = Quaternion.SlerpUnclamped(m.r0, m.r1, s);
                }
                yield return null;
            }

            foreach (var r in b.hiddenRenderers)
                if (r != null) r.enabled = false;
        }

        /// <summary>
        /// Posisi pada jalur cabut (u: 0 = tersarung, 1 = tercabut). Dua gerakan seperti tangan sungguhan, masing-masing
        /// dengan percepatan-perlambatan: (1) tarik lurus sepanjang sumbu sarung sampai pucuk lolos, lalu
        /// (2) lengkung Bezier kubik ke pose tercabut. Kedua titik kendalinya di sudut "tercabut + geser", jadi lengkung
        /// menempel ke sudut: bilah menjauh dari sarung dulu baru turun/kembali, sehingga tidak menembus warangka.
        /// Pembagian waktu sebanding panjang tiap gerakan.
        /// </summary>
        public static Vector3 DrawPath(Vector3 sheathed, Vector3 drawn, Vector3 drawOut, float u)
        {
            var outPoint = sheathed + drawOut;
            var corner = drawn + drawOut;
            float lenA = drawOut.magnitude;
            float lenB = ((corner - outPoint).magnitude + (drawn - corner).magnitude) * 0.85f;
            float split = lenA / Mathf.Max(1e-5f, lenA + lenB);
            u = Mathf.Clamp01(u);
            if (u <= split)
                return Vector3.LerpUnclamped(sheathed, outPoint, Mathf.SmoothStep(0f, 1f, split > 0f ? u / split : 1f));
            float v = Mathf.SmoothStep(0f, 1f, (u - split) / Mathf.Max(1e-5f, 1f - split));
            float w = 1f - v;
            return w * w * w * outPoint + 3f * w * v * corner + v * v * v * drawn;
        }

        void ApplyVisibility(int stage)
        {
            foreach (var s in stages)
                foreach (var r in s.hiddenRenderers)
                    if (r != null) r.enabled = true;
            foreach (var r in stages[stage].hiddenRenderers)
                if (r != null) r.enabled = false;
        }
    }
}
