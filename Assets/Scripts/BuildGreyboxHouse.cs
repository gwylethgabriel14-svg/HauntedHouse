// Put this file in Assets/Editor/ (create the "Editor" folder if it doesn't exist).
// Then use the menu:  Haunted House > Build Greybox House
//
// Layout (top view, 12 x 14 m, +Z is "up"):
//
//   z=+7  +-----------+-----------+-----------+
//         | Bedroom 1 |  Hallway  | Bathroom  |
//   z=+2  +-----------+-----------+-----------+
//         |  Kitchen  | Living Rm |  Storage  |   <- center block to run around
//   z=-3  +-----------+-----------+-----------+
//         | Bedroom 2 | Entrance  |  Stairs   |
//   z=-7  +-----------+---[EXIT]---+-----------+
//       x=-6        x=-2         x=2         x=6
//
// Safe to run again: it rebuilds the generated parts and leaves your other objects alone.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildGreyboxHouse
{
    const float WallH = 3f;     // wall height
    const float WallT = 0.2f;   // wall thickness
    const float DoorW = 1.4f;   // door gap width (wide enough for the enemy's NavMesh agent)
    const float DoorH = 2.2f;   // door gap height

    static Transform root;
    static int obstacleLayer;
    static Material wallMat, floorMat, ceilMat, blockMat, doorMat;

    [MenuItem("Haunted House/Build Greybox House")]
    public static void Build()
    {
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
            Debug.LogWarning("Layer 'Obstacle' not found. Add it in Edit > Project Settings > Tags and Layers, " +
                             "then run Haunted House > Build Greybox House again so the walls get that layer.");

        // Materials
        wallMat  = MakeMat("GB_Wall",   new Color(0.55f, 0.55f, 0.58f));
        floorMat = MakeMat("GB_Floor",  new Color(0.25f, 0.22f, 0.20f));
        ceilMat  = MakeMat("GB_Ceiling", new Color(0.35f, 0.35f, 0.37f));
        blockMat = MakeMat("GB_Block",  new Color(0.40f, 0.30f, 0.25f));
        doorMat  = MakeMat("GB_Door",   new Color(0.45f, 0.25f, 0.12f));

        // Root object: reuse "House" if it exists, remove only what this script generated before
        var existing = GameObject.Find("House");
        if (existing != null)
        {
            root = existing.transform;
            foreach (var n in new[] { "Floor", "Ceiling", "Walls", "Centerpiece", "ExitDoor" })
            {
                var c = root.Find(n);
                if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }
        else
        {
            var go = new GameObject("House");
            Undo.RegisterCreatedObjectUndo(go, "Build greybox house");
            root = go.transform;
        }
        root.position = Vector3.zero;

        var oldLights = GameObject.Find("TempLights");
        if (oldLights != null) Undo.DestroyObjectImmediate(oldLights);

        // Floor and ceiling (12 x 14 m)
        Box("Floor",   root, new Vector3(0, -0.5f, 0),         new Vector3(12, 1, 14), floorMat, false);
        Box("Ceiling", root, new Vector3(0, WallH + 0.5f, 0),  new Vector3(12, 1, 14), ceilMat,  false);

        var walls = new GameObject("Walls");
        walls.transform.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(walls, "Build greybox house");
        var w = walls.transform;

        // ---- Outer walls ----
        WallAlongX(w, "Outer_North", 7f,  -6f, 6f, new float[] { });
        WallAlongX(w, "Outer_South", -7f, -6f, 6f, new float[] { 0f });          // gap at x=0 = front door
        WallAlongZ(w, "Outer_West", -6f, -7.1f, 7.1f, new float[] { });
        WallAlongZ(w, "Outer_East",  6f, -7.1f, 7.1f, new float[] { });

        // ---- Interior walls running east-west (between the 3 rows) ----
        // z=2 : Bedroom1<->Kitchen (x=-4), Hallway<->Living (x=0), Bathroom<->Storage (x=4)
        WallAlongX(w, "Inner_Row1", 2f,  -6f, 6f, new float[] { -4f, 0f, 4f });
        // z=-3: Kitchen<->Bedroom2 (x=-4), Living<->Entrance (x=0), Storage<->Stairs (x=4)
        WallAlongX(w, "Inner_Row2", -3f, -6f, 6f, new float[] { -4f, 0f, 4f });

        // ---- Interior walls running north-south (between the 3 columns) ----
        // x=-2: Bedroom1<->Hallway (z=4.5), Kitchen<->Living (z=-0.5), Bedroom2<->Entrance (z=-5)
        WallAlongZ(w, "Inner_ColA", -2f, -7f, 7f, new float[] { -5f, -0.5f, 4.5f });
        // x=2 : Hallway<->Bathroom (z=4.5), Living<->Storage (z=-0.5), Entrance<->Stairs (z=-5)
        WallAlongZ(w, "Inner_ColB",  2f, -7f, 7f, new float[] { -5f, -0.5f, 4.5f });

        // ---- Center block in the Living Room so you can run in circles around it ----
        var block = Box("Centerpiece", root, new Vector3(0, WallH / 2f, -0.5f), new Vector3(1.4f, WallH, 1.4f), blockMat, true);
        block.name = "Centerpiece";

        // ---- Front door (locked exit) ----
        var door = Box("ExitDoor", root, new Vector3(0, DoorH / 2f, -7f), new Vector3(DoorW, DoorH, WallT), doorMat, false);
        var exitType = System.Type.GetType("ExitDoor, Assembly-CSharp");
        if (exitType != null && door.GetComponent(exitType) == null)
            Undo.AddComponent(door, exitType);

        // ---- Temporary lights so you can see inside (delete "TempLights" later) ----
        var lightsRoot = new GameObject("TempLights");
        Undo.RegisterCreatedObjectUndo(lightsRoot, "Build greybox house");
        var lightSpots = new[]
        {
            new Vector3(0, 2.6f, 4.5f),  new Vector3(0, 2.6f, 1.2f),  new Vector3(0, 2.6f, -5f),
            new Vector3(-4, 2.6f, 0),    new Vector3(4, 2.6f, 0),
            new Vector3(-4, 2.6f, 4.5f), new Vector3(4, 2.6f, 4.5f),
            new Vector3(-4, 2.6f, -5f),  new Vector3(4, 2.6f, -5f)
        };
        foreach (var p in lightSpots)
        {
            var g = new GameObject("TempLight");
            g.transform.SetParent(lightsRoot.transform, false);
            g.transform.position = p;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 7f;
            l.intensity = 1.2f;
            l.shadows = LightShadows.None;
        }

        // ---- Tidy up the Phase 4 test objects ----
        var player = GameObject.Find("Player");
        if (player != null) player.transform.position = new Vector3(0, 1f, -5f);   // start in the Entrance
        var testFloor = GameObject.Find("TestFloor");
        if (testFloor != null) testFloor.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Greybox house built. Next: select 'House', Add Component > NavMesh Surface, click Bake.");
    }

    // ---------- helpers ----------

    // Wall along the X axis at a fixed Z, with door gaps at the given X positions.
    static void WallAlongX(Transform parent, string name, float z, float x0, float x1, float[] gapCenters)
    {
        System.Array.Sort(gapCenters);
        float cursor = x0;
        int i = 0;
        foreach (float g in gapCenters)
        {
            float a = g - DoorW / 2f;
            if (a - cursor > 0.01f)
                Box(name + "_" + i++, parent, new Vector3((cursor + a) / 2f, WallH / 2f, z),
                    new Vector3(a - cursor, WallH, WallT), wallMat, true);
            Box(name + "_Lintel" + i++, parent, new Vector3(g, DoorH + (WallH - DoorH) / 2f, z),
                new Vector3(DoorW, WallH - DoorH, WallT), wallMat, true);
            cursor = g + DoorW / 2f;
        }
        if (x1 - cursor > 0.01f)
            Box(name + "_" + i, parent, new Vector3((cursor + x1) / 2f, WallH / 2f, z),
                new Vector3(x1 - cursor, WallH, WallT), wallMat, true);
    }

    // Wall along the Z axis at a fixed X, with door gaps at the given Z positions.
    static void WallAlongZ(Transform parent, string name, float x, float z0, float z1, float[] gapCenters)
    {
        System.Array.Sort(gapCenters);
        float cursor = z0;
        int i = 0;
        foreach (float g in gapCenters)
        {
            float a = g - DoorW / 2f;
            if (a - cursor > 0.01f)
                Box(name + "_" + i++, parent, new Vector3(x, WallH / 2f, (cursor + a) / 2f),
                    new Vector3(WallT, WallH, a - cursor), wallMat, true);
            Box(name + "_Lintel" + i++, parent, new Vector3(x, DoorH + (WallH - DoorH) / 2f, g),
                new Vector3(WallT, WallH - DoorH, DoorW), wallMat, true);
            cursor = g + DoorW / 2f;
        }
        if (z1 - cursor > 0.01f)
            Box(name + "_" + i, parent, new Vector3(x, WallH / 2f, (cursor + z1) / 2f),
                new Vector3(WallT, WallH, z1 - cursor), wallMat, true);
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool obstacle)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (obstacle && obstacleLayer >= 0) go.layer = obstacleLayer;
        go.isStatic = true;
        Undo.RegisterCreatedObjectUndo(go, "Build greybox house");
        return go;
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
