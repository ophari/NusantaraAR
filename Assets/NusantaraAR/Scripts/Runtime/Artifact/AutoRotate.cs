using System;
using UnityEngine;

namespace NusantaraAR
{
    /// <summary>Turntable 360° lambat (PRD FR-15). Berhenti otomatis saat pengguna menyentuh objek.</summary>
    public class AutoRotate : MonoBehaviour
    {
        public float degreesPerSecond = 18f;
        bool active;

        public event Action<bool> ActiveChanged;

        public bool Active
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                ActiveChanged?.Invoke(active);
            }
        }

        void Update()
        {
            if (active) transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
    }
}
