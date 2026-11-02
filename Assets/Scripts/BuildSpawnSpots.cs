// Put this file in Assets/Editor/ next to BuildGreyboxHouse.cs.
// Menu:  Haunted House > Create Page & Key Spots
//
// Creates 9 page spots under "PageSpots" and 4 key spots under "KeySpots",
// placed for the greybox layout from BuildGreyboxHouse.cs.
// If PageSpawner.cs / GameManager.cs are already in the project, it adds them
// to the objects and fills in their spot lists for you.
// Safe to run again: it rebuilds the spots.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildSpawnSpots
{
    static readonly (string name, Vector3 pos)[] PageSpots =
    {
        ("Page_KitchenTable",  new Vector3(-4.0f, 0.9f,  0.0f)),
        ("Page_Bedroom1Pillow",new Vector3(-5.0f, 0.6f,  6.0f)),
        ("Page_Bathroom",      new Vector3( 5.0f, 0.9f,  6.0f)),
        ("Page_StairsShelf",   new Vector3( 5.0f, 1.2f, -6.0f)),
        ("Page_BehindSofa",    new Vector3( 1.5f, 0.3f,  1.5f)),
        ("Page_StairStep",     new Vector3( 3.0f, 0.3f, -4.5f)),
        ("Page_StorageCloset", new Vector3( 5.0f, 1.0f,  1.0f)),
        ("Page_Bedroom2Dresser",new Vector3(-5.0f, 0.9f, -6.0f)),
        ("Page_Fireplace",     new Vector3( 0.0f, 1.2f,  0.4f)),
    };

    static readonly (string name, Vector3 pos)[] KeySpots =
    {
        ("Key_UnderBed",       new Vector3(-5.0f, 0.2f,  3.5f)),
        ("Key_StorageCrate",   new Vector3( 4.5f, 0.3f, -2.0f)),
        ("Key_KitchenDrawer",  new Vector3(-5.5f, 0.9f, -2.0f)),
        ("Key_Bedroom2",       new Vector3(-5.0f, 0.3f, -4.0f)),
    };

    [MenuItem("Haunted House/Create Page & Key Spots")]
    public static void Build()
    {
        var pageRoot = FindOrCreate("PageSpots");
        var keyRoot  = FindOrCreate("KeySpots");

        var pages = Fill(pageRoot, PageSpots);
        var keys  = Fill(keyRoot, KeySpots);

        // PageSpawner on PageSpots
        var spawnerType = System.Type.GetType("PageSpawner, Assembly-CSharp");
        if (spawnerType != null)
        {
            var comp = pageRoot.GetComponent(spawnerType);
            if (comp == null) comp = Undo.AddComponent(pageRoot, spawnerType);
            Assign(comp, "candidateSpots", pages);
        }
        else Debug.LogWarning("PageSpawner.cs not found yet. Import all scripts, then run this menu again.");

        // GameManager object with the key spawn points
        var gm = GameObject.Find("GameManager");
        if (gm == null)
        {
            gm = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
        }
        var gmType = System.Type.GetType("GameManager, Assembly-CSharp");
        if (gmType != null)
        {
            var comp = gm.GetComponent(gmType);
            if (comp == null) comp = Undo.AddComponent(gm, gmType);
            Assign(comp, "keySpawnPoints", keys);
        }
        else Debug.LogWarning("GameManager.cs not found yet. Import all scripts, then run this menu again.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Created " + pages.Length + " page spots and " + keys.Length + " key spots.");
    }

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        go.transform.position = Vector3.zero;
        return go;
    }

    static Transform[] Fill(GameObject root, (string name, Vector3 pos)[] spots)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

        var result = new Transform[spots.Length];
        for (int i = 0; i < spots.Length; i++)
        {
            var g = new GameObject(spots[i].name);
            g.transform.SetParent(root.transform, false);
            g.transform.position = spots[i].pos;
            Undo.RegisterCreatedObjectUndo(g, "Create spot");
            result[i] = g.transform;
        }
        return result;
    }

    static void Assign(Component comp, string field, Transform[] items)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning("Field '" + field + "' not found on " + comp.GetType().Name); return; }
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedProperties();
    }
}
