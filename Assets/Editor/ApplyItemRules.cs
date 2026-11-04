using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyItemRules
{
    const float GunStunSeconds = 120f; // 2 mins
    const float TrapStunSeconds = 60f; // 1 min
    const int TotalDarts = 3;
    const int TotalTraps = 3;

    [MenuItem("Tools/Items/Apply Item Rules")]
    static void Apply()
    {
        // prefabs
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            ForEach(prefab, "TranquilizerGun", c => ApplyGun(c));
            ForEach(prefab, "TrapItem", c => SetFloat(c, "stunDuration", TrapStunSeconds));
        }

        // scene
        foreach (var c in InScene("TranquilizerGun")) ApplyGun(c);
        foreach (var c in InScene("TrapItem")) SetFloat(c, "stunDuration", TrapStunSeconds);
        foreach (var c in InScene("PlayerItemInteractor"))
        {
            SetInt(c, "maxDarts", TotalDarts);
            SetInt(c, "maxTraps", TotalTraps);
            SetInt(c, "darts", 0);
            SetInt(c, "traps", 0);
        }
        foreach (var c in InScene("ItemSpawner")) ApplySpawner(c);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Apply Item Rules tapos na. I-save ang scene (Ctrl+S).");
    }

    static void ApplyGun(Component c)
    {
        SetFloat(c, "stunSeconds", GunStunSeconds);
        SetInt(c, "magazineSize", TotalDarts);
        SetInt(c, "ammoInMagazine", 0); // walang laman sa simula; kailangan pang pumulot ng darts
    }

    static void ApplySpawner(Component c)
    {
        var so = new SerializedObject(c);
        var it = so.GetIterator();
        bool changed = false;
        while (it.NextVisible(true))
        {
            if (it.propertyType != SerializedPropertyType.Integer) continue;
            string n = it.name.ToLowerInvariant();
            bool countLike = n.Contains("count") || n.Contains("amount") || n.Contains("num") || n.Contains("total");
            Debug.Log($"ItemSpawner field: {it.name} = {it.intValue}", c);
            if (!countLike) continue;
            if (n.Contains("trap") && it.intValue != TotalTraps) { Debug.Log($"  -> {it.name}: {it.intValue} to {TotalTraps}"); it.intValue = TotalTraps; changed = true; }
            else if (n.Contains("dart") && it.intValue != TotalDarts) { Debug.Log($"  -> {it.name}: {it.intValue} to {TotalDarts}"); it.intValue = TotalDarts; changed = true; }
        }
        if (changed) { so.ApplyModifiedProperties(); EditorUtility.SetDirty(c); }
    }

    // ---- helpers ----
    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == name);

    static Component[] InScene(string typeName)
    {
        var t = FindType(typeName);
        if (t == null) return new Component[0];
        return UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToArray();
    }

    static void ForEach(GameObject prefab, string typeName, Action<Component> a)
    {
        var t = FindType(typeName);
        if (t == null) return;
        foreach (var c in prefab.GetComponentsInChildren(t, true)) a(c);
    }

    static void SetFloat(Component c, string field, float v)
    {
        var so = new SerializedObject(c); var p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.Float) return;
        p.floatValue = v; so.ApplyModifiedProperties(); EditorUtility.SetDirty(c);
    }

    static void SetInt(Component c, string field, int v)
    {
        var so = new SerializedObject(c); var p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.Integer) return;
        p.intValue = v; so.ApplyModifiedProperties(); EditorUtility.SetDirty(c);
    }
}