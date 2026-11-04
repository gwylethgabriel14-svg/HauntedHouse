using UnityEngine;

namespace HorrorItems
{
    /// <summary>Small helpers shared by runtime + editor code (works across Unity versions).</summary>
    public static class ItemUtil
    {
        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>();
#endif
        }

        public static T FindFirst<T>() where T : UnityEngine.Object
        {
            var a = FindAll<T>();
            return a.Length > 0 ? a[0] : null;
        }

        public static void DestroySmart(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
