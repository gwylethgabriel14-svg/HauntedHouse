using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using HauntedItems;

public static class SetupItemSpawner
{
    [MenuItem("Tools/Items/Setup Item Spawner (ayusin + ilagay sa scene)")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("Mag-stop muna bago i-run."); return; }

        // 1) Linisin ang naiwan ng RandomItemPlacer (kung na-run mo dati)
        var old = GameObject.Find("RandomItemPlacer");
        if (old != null) { Undo.DestroyObjectImmediate(old); Debug.Log("Binura ang RandomItemPlacer."); }

        // 2) Hanapin o gumawa ng ItemSpawner
        var sp = Object.FindFirstObjectByType<ItemSpawner>(FindObjectsInactive.Include);
        if (sp == null)
        {
            var go = new GameObject("ItemSpawner");
            sp = go.AddComponent<ItemSpawner>();
            Undo.RegisterCreatedObjectUndo(go, "Create ItemSpawner");
        }
        Undo.RecordObject(sp, "Setup ItemSpawner");
        sp.enabled = true;
        sp.gameObject.SetActive(true);

        // 3) Prefabs
        sp.gunPrefab            = Load("gun");
        sp.dartPackPrefab       = Load("pack");
        sp.trapPrefab           = Load("trap");
        sp.dartProjectilePrefab = Load("projectile");

        // 4) Bilang at rules
        sp.gunCount = 1;
        sp.dartPackCount = 1;
        sp.dartsPerPack = 3;
        sp.trapCount = 3;
        sp.gunStartsLoaded = false;   // walang laman sa simula
        sp.magazineSize = 3;
        sp.dartStunSeconds = 120f;
        sp.trapStunSeconds = 60f;
        sp.spawnOnPlay = true;
        sp.newSeedEveryGame = true;   // iba-iba kada new game

        // 5) I-fit ang area sa NavMesh (kung na-bake na)
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length > 0)
        {
            var b = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (var v in tri.vertices) b.Encapsulate(v);
            sp.areaCenter = new Vector3(b.center.x, b.center.y, b.center.z);
            sp.areaSize = new Vector3(b.size.x + 2f, b.size.y + 8f, b.size.z + 2f);
            Debug.Log("Area na-fit sa NavMesh: " + sp.areaCenter + " / " + sp.areaSize);
        }
        else Debug.LogWarning("Walang NavMesh. Hindi binago ang Area Center/Size; i-check ang orange box sa Scene view.");

        EditorUtility.SetDirty(sp);

        // 6) Ilagay na ang items sa scene ngayon
        int n = sp.Generate();
        EditorSceneManager.MarkSceneDirty(sp.gameObject.scene);
        Debug.Log($"ItemSpawner ready. {n}/5 items nailagay (1 gun, 1 dart pack, 3 traps). I-save ang scene (Ctrl+S).", sp);
        Selection.activeGameObject = sp.gameObject;
    }

    static GameObject Load(string key)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Items" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).ToLower().Contains(key))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        Debug.LogWarning("Hindi nahanap ang prefab na may: " + key);
        return null;
    }
}