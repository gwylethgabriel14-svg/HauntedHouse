using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RemoveMissingScripts
{
    [MenuItem("Tools/Remove Missing Scripts/In Open Scene")]
    static void InScene()
    {
        int total = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int n = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            if (n > 0) { total += n; Debug.Log($"Removed {n} missing script(s) from {go.name}", go); }
        }
        if (total > 0) EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Scene: {total} missing script(s) removed.");
    }

    [MenuItem("Tools/Remove Missing Scripts/In All Prefabs")]
    static void InPrefabs()
    {
        int total = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                total += n;
                Debug.Log($"Prefab {path}: removed {n}");
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"Prefabs: {total} missing script(s) removed.");
    }
}