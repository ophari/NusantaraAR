using System;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace NusantaraAR
{
    /// <summary>Izin kamera (PRD §4.1): cek, minta, dan buka pengaturan aplikasi bila ditolak permanen.</summary>
    public static class CameraPermission
    {
        public static bool IsGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
                return true; // iOS: ditangani ARKit + NSCameraUsageDescription
#endif
            }
        }

        /// <summary>Meminta izin. Callback: true = diizinkan.</summary>
        public static void Request(Action<bool> onResult)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                onResult?.Invoke(true);
                return;
            }

            bool settled = false;
            void Finish(bool granted)
            {
                if (settled) return;
                settled = true;
                onResult?.Invoke(granted);
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => Finish(true);
            callbacks.PermissionDenied += _ => Finish(false);
#pragma warning disable CS0618 // tetap didaftarkan bersama guard idempoten agar kompatibel di Android API 26-36
            callbacks.PermissionDeniedAndDontAskAgain += _ => Finish(false);
#pragma warning restore CS0618
            Permission.RequestUserPermission(Permission.Camera, callbacks);
#else
            onResult?.Invoke(true);
#endif
        }

        /// <summary>Membuka halaman info aplikasi di Pengaturan Android.</summary>
        public static void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NusantaraAR] Gagal membuka pengaturan aplikasi: " + e.Message);
            }
#else
            Debug.Log("[NusantaraAR] OpenAppSettings hanya tersedia di Android.");
#endif
        }
    }
}
