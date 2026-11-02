// Put this file in Assets/Editor/ next to the other builder scripts.
// Menu items (under "Haunted House"):
//   Create Bottle System  - bottle prefab, ThrowItem on the Player, 3 bottle pickups
//   Create Barricades     - 4 disabled barricades in door gaps, wired into GameManager.barricades
//
// Barricade order (one appears per page collected):
//   1 Hallway <-> Bathroom     2 Bedroom 2 <-> Entrance
//   3 Storage <-> Stairs       4 Kitchen <-> Living Room
// Every room stays reachable after all 4 are closed, and the Living Room block still gives you a loop.
// Safe to run again: it rebuilds what it created.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BuildPhase8
{
    const string BottlePath = "Assets/Prefabs/Bottle.prefab";

    // ---------------- Bottle system ----------------

    static readonly Vector3[] BottlePos =
    {
        new Vector3(-5.0f, 0.15f,  5.5f),   // Bedroom 1
        new Vector3( 5.0f, 0.15f, -4.0f),   // Stairs
        new Vector3(-3.5f, 0.15f, -1.5f),   // Kitchen
    };

    [MenuItem("Haunted House/Create Bottle System")]
    public static void CreateBottleSystem()
    {
        var player = GameObject.Find("Player");
        var camGo  = GameObject.Find("Main Camera");
        if (player == null || camGo == null) { Debug.LogError("Need 'Player' and 'Main Camera' in the scene."); return; }

        var noiseType = System.Type.GetType("NoiseOnImpact, Assembly-CSharp");
        var throwType = System.Type.GetType("ThrowItem, Assembly-CSharp");
        var pickType  = System.Type.GetType("ItemPickup, Assembly-CSharp");
        if (noiseType == null || throwType == null || pickType == null)
        { Debug.LogError("NoiseOnImpact / ThrowItem / ItemPickup scripts not found. Import all scripts first."); return; }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");

        var glass = MakeMat("GB_Bottle", new Color(0.2f, 0.6f, 0.3f));

        // ----- Bottle prefab (thrown) -----
        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        b.name = "Bottle";
        b.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
        b.GetComponent<Renderer>().sharedMaterial = glass;
        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        b.AddComponent(noiseType);
        var prefab = PrefabUtility.SaveAsPrefabAsset(b, BottlePath);
        Object.DestroyImmediate(b);

        // ----- ThrowItem on the Player -----
        var th = player.GetComponent(throwType);
        if (th == null) th = Undo.AddComponent(player, throwType);
        var so = new SerializedObject(th);
        so.FindProperty("bottlePrefab").objectReferenceValue = prefab;
        so.FindProperty("cam").objectReferenceValue = camGo.transform;
        so.FindProperty("bottles").intValue = 1;
        so.ApplyModifiedProperties();

        // ----- Bottle pickups (batteries live in the same "Pickups" root, so only remove bottles) -----
        var pk = GameObject.Find("Pickups");
        if (pk == null)
        {
            pk = new GameObject("Pickups");
            Undo.RegisterCreatedObjectUndo(pk, "Create Pickups");
        }
        for (int i = pk.transform.childCount - 1; i >= 0; i--)
        {
            var c = pk.transform.GetChild(i);
            if (c.name.StartsWith("BottlePickup")) Undo.DestroyObjectImmediate(c.gameObject);
        }

        for (int i = 0; i < BottlePos.Length; i++)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "BottlePickup_" + (i + 1);
            p.transform.SetParent(pk.transform, false);
            p.transform.position = BottlePos[i];
            p.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
            p.GetComponent<Renderer>().sharedMaterial = glass;
            Undo.RegisterCreatedObjectUndo(p, "Create bottle pickup");

            var comp = Undo.AddComponent(p, pickType);
            var pso = new SerializedObject(comp);
            var t = pso.FindProperty("type");
            if (t != null) t.enumValueIndex = 1;   // Bottle
            pso.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Bottle system ready. Press G to throw (you start with 1).");
    }

    // ---------------- Barricades ----------------

    // center of the door gap, and whether the wall runs along Z (x is fixed) or along X (z is fixed)
    static readonly (string name, Vector3 pos, bool wallAlongZ)[] Doors =
    {
        ("Barricade_1_HallwayBathroom", new Vector3( 2f, 1.1f,  4.5f), true),
        ("Barricade_2_Bedroom2Entrance",new Vector3(-2f, 1.1f, -5.0f), true),
        ("Barricade_3_StorageStairs",   new Vector3( 4f, 1.1f, -3.0f), false),
        ("Barricade_4_KitchenLiving",   new Vector3(-2f, 1.1f, -0.5f), true),
    };

    [MenuItem("Haunted House/Create Barricades")]
    public static void CreateBarricades()
    {
        var gmType = System.Type.GetType("GameManager, Assembly-CSharp");
        var gm = GameObject.Find("GameManager");

        var root = GameObject.Find("Barricades");
        if (root == null)
        {
            root = new GameObject("Barricades");
            Undo.RegisterCreatedObjectUndo(root, "Create Barricades");
        }
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
        root.transform.position = Vector3.zero;

        var mat = MakeMat("GB_Barricade", new Color(0.30f, 0.20f, 0.12f));
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        var made = new GameObject[Doors.Length];
        for (int i = 0; i < Doors.Length; i++)
        {
            var d = Doors[i];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = d.name;
            go.transform.SetParent(root.transform, false);
            go.transform.position = d.pos;
            // fills the 1.4 m door gap (slightly oversized so there is no slit) and the full 2.2 m door height
            go.transform.localScale = d.wallAlongZ ? new Vector3(0.6f, 2.2f, 1.5f)
                                                   : new Vector3(1.5f, 2.2f, 0.6f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (obstacleLayer >= 0) go.layer = obstacleLayer;   // blocks the enemy's line of sight too
            Undo.RegisterCreatedObjectUndo(go, "Create barricade");

            // makes the enemy's NavMesh path update when the barricade appears
            var ob = Undo.AddComponent<NavMeshObstacle>(go);
            ob.shape = NavMeshObstacleShape.Box;
            ob.center = Vector3.zero;
            ob.size = Vector3.one;      // relative to the cube's scale
            ob.carving = true;

            go.SetActive(false);        // GameManager turns them on, one per page
            made[i] = go;
        }

        if (gm != null && gmType != null)
        {
            var comp = gm.GetComponent(gmType);
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                var prop = so.FindProperty("barricades");
                prop.arraySize = made.Length;
                for (int i = 0; i < made.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = made[i];
                so.ApplyModifiedProperties();
            }
        }
        else Debug.LogWarning("GameManager not found: drag the barricades into its Barricades list by hand.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Created " + made.Length + " barricades (disabled; one turns on per page).");
    }

    static Material MakeMat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { m.color = c; return m; }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        m = new Material(shader);
        m.color = c;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
