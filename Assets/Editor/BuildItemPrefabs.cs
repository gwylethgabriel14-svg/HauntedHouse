using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildItemPrefabs
{
    const string Folder = "Assets/Prefabs/Items";

    class Slot
    {
        public string label;            // pangalan ng field sa Inspector
        public string fileName;
        public Func<MethodInfo, bool> match;
        public string[] components;
        public bool addCollider;
    }

    static readonly Slot[] Slots =
    {
        new Slot { label = "Gun Prefab", fileName = "TranquilizerGun",
            match = m => m.Name.Contains("Gun") && !m.Name.Contains("Shotgun"),
            components = new[] { "TranquilizerGun", "PickupItem" }, addCollider = true },
        new Slot { label = "Dart Pack Prefab", fileName = "DartPack",
            match = m => m.Name.Contains("Dart") && (m.Name.Contains("Pack") || m.Name.Contains("Box") || m.Name.Contains("Ammo") || m.Name.Contains("Clip")),
            components = new[] { "PickupItem" }, addCollider = true },
        new Slot { label = "Trap Prefab", fileName = "Trap",
            match = m => m.Name.Contains("Trap"),
            components = new[] { "TrapItem" }, addCollider = true },
        new Slot { label = "Dart Projectile Prefab", fileName = "DartProjectile",
            match = m => m.Name == "BuildDart",
            components = new[] { "TranquilizerDart" }, addCollider = false },
    };

    [MenuItem("Tools/Items/Build Item Prefabs")]
    static void Build()
    {
        EnsureFolder(Folder);
        EnsureFolder(Folder + "/Materials");
        EnsureFolder(Folder + "/Meshes");

        var factory = FindType("ItemModelFactory");
        if (factory == null) { Debug.LogError("Hindi makita ang ItemModelFactory."); return; }

        var methods = factory.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.ReturnType == typeof(GameObject) && m.GetParameters().All(p => p.IsOptional || p.HasDefaultValue))
            .ToList();
        Debug.Log("ItemModelFactory methods na nakita: " + string.Join(", ", methods.Select(m => m.Name)));

        var made = new Dictionary<string, GameObject>();

        foreach (var s in Slots)
        {
            var method = methods.FirstOrDefault(s.match);
            if (method == null)
            {
                Debug.LogWarning($"[{s.label}] walang nakitang method sa factory. Tingnan ang listahan sa taas.");
                continue;
            }

            var args = method.GetParameters().Select(p => p.DefaultValue is DBNull ? null : p.DefaultValue).ToArray();
            var go = (GameObject)method.Invoke(null, args);
            if (go == null) { Debug.LogWarning($"[{s.label}] {method.Name} ay nag-return ng null."); continue; }

            go.name = s.fileName;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;

            MakePersistent(go, s.fileName);

            foreach (var cn in s.components)
            {
                var t = FindType(cn);
                if (t == null) { Debug.LogWarning($"[{s.label}] walang type na {cn}."); continue; }
                if (go.GetComponent(t) == null) go.AddComponent(t);
            }

            if (s.addCollider && go.GetComponentInChildren<Collider>(true) == null) AddFittedCollider(go);

            string path = $"{Folder}/{s.fileName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            made[s.label] = prefab;
            Debug.Log($"[{s.label}] nagawa gamit ang {method.Name} -> {path}", prefab);
        }

        // ilagay sa ItemSpawner at PlayerItemInteractor
        var spawner = FindInScene("ItemSpawner");
        var interactor = FindInScene("PlayerItemInteractor");
        foreach (var kv in made)
        {
            if (spawner != null) Assign(spawner, kv.Key, kv.Value);
            if (interactor != null && kv.Key == "Trap Prefab") Assign(interactor, kv.Key, kv.Value);
        }
        if (spawner == null) Debug.LogWarning("Walang ItemSpawner sa scene; i-drag mo na lang ang mga prefab sa Assets/Prefabs/Items.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("Tapos na. I-save ang scene (Ctrl+S).");
    }

    // ---- helpers ----

    static Type FindType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == name);

    static UnityEngine.Object FindInScene(string typeName)
    {
        var t = FindType(typeName);
        return t == null ? null : UnityEngine.Object.FindFirstObjectByType(t, FindObjectsInactive.Include);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // Ang mga material/mesh na ginawa sa code ay mawawala sa prefab kapag hindi na-save bilang asset
    static void MakePersistent(GameObject go, string name)
    {
        int i = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
                    AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Materials/{name}_{i++}.mat"));
        }
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mf.sharedMesh)))
                AssetDatabase.CreateAsset(mf.sharedMesh, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Meshes/{name}_{i++}.asset"));
        foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true))
            if (mc.sharedMesh != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mc.sharedMesh)))
                AssetDatabase.CreateAsset(mc.sharedMesh, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Meshes/{name}_{i++}.asset"));
    }

    static void AddFittedCollider(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) { go.AddComponent<BoxCollider>(); return; }
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = b.size;
    }

    static string Norm(string s) => s.Replace(" ", "").ToLowerInvariant();

    static void Assign(UnityEngine.Object target, string label, GameObject prefab)
    {
        var so = new SerializedObject(target);
        var it = so.GetIterator();
        while (it.NextVisible(true))
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && Norm(it.displayName) == Norm(label))
            {
                it.objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Debug.Log($"{target.GetType().Name}.{label} <- {prefab.name}", target);
                return;
            }
        }
    }
}