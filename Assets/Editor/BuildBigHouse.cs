// Put this file in Assets/Editor/ next to BuildGreyboxHouse.cs.
// Menu:  Haunted House > BUILD BIG HOUSE (shell)
//
// Replaces the old 12 x 14 m greybox with the empty two-storey shell only.
// It does NOT place pages, keys, waypoints, lockers, lamps, pickups or the enemy.
//
// Footprint 20 m (X, -10..10) x 24 m (Z, -12..12) on a 3 x 3 grid:
//
//   z=+12 +-----------+-----------+-----------+   upper (y=3.2):
//         |  Parlor   | Back Hall | Bathroom  |   Master  | Upper Hall | Nursery
//   z=+4  +-----------+-----------+-----------+   ---------+------------+---------
//         | Living Rm | Main Hall |  Pantry   |   Study   | stairwell  | Guest
//   z=-4  +-----------+-----------+-----------+   ---------+------------+---------
//         |  Dining   | Entrance  |  Kitchen  |   Kids    | Upper      | Bathroom2
//   z=-12 +-----------+--[EXIT]---+-----------+   Bedroom   | Landing    |
//       x=-10      x=-3.35    x=+3.35      x=+10
//
// Ground floor y=0, upper floor y=3.2. Wall height 3, wall thickness 0.3,
// door gaps 1.6 wide and 2.4 tall.
//
// Door gaps make one full circle around the central core on each floor
// (8 rooms per loop) plus links into the core, so nothing on the ring is a dead end.
//
// One straight staircase (1.6 m wide, 16 x 0.2 m steps) stands in the Main Hall,
// rises towards -Z and arrives through a doorway in the z=-4 wall on the Upper Landing.
// The upper slab is built from four pieces around the stair opening, with a railing
// on the three closed sides of the hole.
//
// Walls, slabs, roof and railings go on the Obstacle layer and are static;
// the steps stay on Default so the enemy's line-of-sight ray ignores them.
// Existing materials (GB_Wall, GB_Floor, GB_Ceiling, GB_Block, GB_Door) are reused,
// and Floor / Ceiling / Walls / ExitDoor keep their names under the House root.
//
// Safe to run again: it clears everything under "House" first.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class BuildBigHouse
{
    // ---- footprint ----
    const float HalfX = 10f;      // X: -10 .. 10
    const float HalfZ = 12f;      // Z: -12 .. 12
    const float ColX = 3.35f;     // north-south interior wall lines (x = +-3.35)
    const float RowZ = 4f;        // east-west interior wall lines   (z = +-4)

    // ---- sizes ----
    const float WallH = 3f;       // wall height per storey
    const float WallT = 0.3f;     // wall thickness
    const float FloorT = 0.3f;    // ground slab thickness (top at y = 0)
    const float SlabT = 0.2f;     // upper slab fills y = 3.0 .. 3.2
    const float RoofT = 0.2f;     // roof fills y = 6.2 .. 6.4
    const float StoreyH = 3.2f;   // floor to floor
    const float DoorW = 1.6f;     // door gap width
    const float DoorH = 2.4f;     // door gap height

    // ---- stairs (straight, in the Main Hall, rising towards -Z) ----
    const int Steps = 16;
    const float StepH = 0.2f;     // 16 x 0.2 = 3.2 = StoreyH
    const float StepD = 0.3f;     // 16 x 0.3 = 4.8 m run
    const float StairW = 1.6f;
    const float StairEndZ = 0.95f;  // north edge of the bottom step; top step stops at the z=-4 wall

    // ---- door gap centres, per storey ----
    // ground floor
    static readonly float[] GF_RowS = { -6.75f, 2.4f, 6.75f };   // z = -4  (Dining|Entrance|Kitchen wall)
    static readonly float[] GF_RowN = { -6.75f, 2.4f, 6.75f };   // z = +4
    static readonly float[] GF_ColW = { -8f, 2.4f, 8f };         // x = -3.35
    static readonly float[] GF_ColE = { -8f, -2.4f, 8f };        // x = +3.35
    static readonly float[] GF_Front = { 0f };                   // z = -12 (locked front door)
    static readonly float[] NoGaps = new float[0];

    // upper floor
    static readonly float[] UF_RowS = { -6.75f, 0f, 6.75f };     // z = -4  (0 = stair arrival)
    static readonly float[] UF_RowN = { -6.75f, 6.75f };         // z = +4
    static readonly float[] UF_ColW = { -8f, 2.4f, 8f };         // x = -3.35
    static readonly float[] UF_ColE = { -8f, 2.4f, 8f };         // x = +3.35

    static Transform root, walls, ceiling, stairs, railings;
    static int obstacleLayer;
    static int cubes, doorways, stepsMade, railsMade;
    static Material wallMat, floorMat, ceilMat, stairMat, doorMat;

    [MenuItem("Haunted House/BUILD BIG HOUSE (shell)")]
    public static void Build()
    {
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0)
            Debug.LogWarning("Layer 'Obstacle' not found. Add it in Project Settings > Tags and Layers, " +
                             "then run this builder again so the walls and railings get that layer.");

        wallMat  = LoadMat("GB_Wall",    new Color(0.55f, 0.55f, 0.58f));
        floorMat = LoadMat("GB_Floor",   new Color(0.25f, 0.22f, 0.20f));
        ceilMat  = LoadMat("GB_Ceiling", new Color(0.35f, 0.35f, 0.37f));
        stairMat = LoadMat("GB_Block",   new Color(0.40f, 0.30f, 0.25f));
        doorMat  = LoadMat("GB_Door",    new Color(0.45f, 0.25f, 0.12f));

        cubes = 0; doorways = 0; stepsMade = 0; railsMade = 0;

        // ---- House root ----
        var existing = GameObject.Find("House");
        if (existing != null)
        {
            root = existing.transform;
            if (root.position.sqrMagnitude > 0.0001f)
                Debug.LogWarning("'House' is not at the origin (it is at " + root.position +
                                 "). The builder places everything assuming origin (0,0,0).");
        }
        else
        {
            var go = new GameObject("House");
            Undo.RegisterCreatedObjectUndo(go, "Build big house");
            root = go.transform;
            root.position = Vector3.zero;
        }

        // ---- clear everything this shell owns (the whole generated subtree) ----
        int removed = root.childCount;
        for (int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

        // ---- ground slab ----
        Box(root, "Floor", new Vector3(0f, -FloorT * 0.5f, 0f),
            new Vector3(HalfX * 2f, FloorT, HalfZ * 2f), floorMat, true, true);

        // ---- ground ceiling = upper floor slab, built around the stair opening ----
        ceiling = NewChild("Ceiling", root);
        BuildUpperSlab();

        // ---- roof ----
        Box(root, "Roof", new Vector3(0f, StoreyH + WallH + RoofT * 0.5f, 0f),
            new Vector3(HalfX * 2f, RoofT, HalfZ * 2f), ceilMat, true, true);

        // ---- walls ----
        walls = NewChild("Walls", root);
        BuildWalls(0f, "GF");
        BuildWalls(StoreyH, "UF");

        // ---- stairs ----
        stairs = NewChild("Stairs", root);
        BuildStairs();

        // ---- railing around the stair opening ----
        railings = NewChild("Railings", root);
        BuildRailings();

        // ---- locked front door ----
        var door = Box(root, "ExitDoor", new Vector3(0f, DoorH * 0.5f, -HalfZ),
                       new Vector3(DoorW, DoorH, WallT), doorMat, false, false);
        var exitType = System.Type.GetType("ExitDoor, Assembly-CSharp");
        if (exitType != null && door.GetComponent(exitType) == null)
            Undo.AddComponent(door, exitType);
        else if (exitType == null)
            Debug.LogWarning("ExitDoor.cs not found: add the ExitDoor component to 'House/ExitDoor' by hand.");

        // ---- NavMesh ----
        EnsureNavMeshSurface();
        bool baked = BakeNavMesh();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root.gameObject;

        Debug.Log("BIG HOUSE shell built: " + cubes + " cubes (" + stepsMade + " steps, " + railsMade +
                  " railing pieces), " + doorways + " doorways, " + removed + " old House children removed. " +
                  "NavMesh " + (baked ? "baked" : "NOT baked (select House > Nav Mesh Surface > Bake)") + ".");
    }

    // ======================================================================
    //  Walls
    // ======================================================================

    static void BuildWalls(float yBottom, string tag)
    {
        // outer shell (the front door gap only exists on the ground floor)
        WallRun(tag + "_Outer_South", true, -HalfZ, -HalfX, HalfX, tag == "GF" ? GF_Front : NoGaps, yBottom);
        WallRun(tag + "_Outer_North", true, HalfZ, -HalfX, HalfX, NoGaps, yBottom);
        WallRun(tag + "_Outer_West", false, -HalfX, -HalfZ, HalfZ, NoGaps, yBottom);
        WallRun(tag + "_Outer_East", false, HalfX, -HalfZ, HalfZ, NoGaps, yBottom);

        // between the south row and the middle row (z = -4)
        WallRun(tag + "_RowS", true, -RowZ, -HalfX, HalfX, tag == "GF" ? GF_RowS : UF_RowS, yBottom);
        // between the middle row and the north row (z = +4)
        WallRun(tag + "_RowN", true, RowZ, -HalfX, HalfX, tag == "GF" ? GF_RowN : UF_RowN, yBottom);
        // between the west column and the centre column (x = -3.35)
        WallRun(tag + "_ColW", false, -ColX, -HalfZ, HalfZ, tag == "GF" ? GF_ColW : UF_ColW, yBottom);
        // between the centre column and the east column (x = +3.35)
        WallRun(tag + "_ColE", false, ColX, -HalfZ, HalfZ, tag == "GF" ? GF_ColE : UF_ColE, yBottom);
    }

    // A wall line with door gaps: solid pieces between the gaps, plus a lintel above each gap.
    static void WallRun(string name, bool alongX, float fixedCoord, float a0, float a1,
                        float[] gaps, float yBottom)
    {
        System.Array.Sort(gaps);
        float cursor = a0;
        int solid = 0, lintel = 0;

        foreach (float g in gaps)
        {
            float ga = g - DoorW * 0.5f;
            float gb = g + DoorW * 0.5f;

            if (ga - cursor > 0.01f)
                WallPiece(name + "_" + (solid++), alongX, fixedCoord, cursor, ga, yBottom, WallH);

            // header above the opening
            WallPiece(name + "_Lintel" + (lintel++), alongX, fixedCoord, ga, gb,
                      yBottom + DoorH, WallH - DoorH);
            doorways++;

            cursor = gb;
        }

        if (a1 - cursor > 0.01f)
            WallPiece(name + "_" + solid, alongX, fixedCoord, cursor, a1, yBottom, WallH);
    }

    static void WallPiece(string name, bool alongX, float fixedCoord, float a0, float a1,
                          float yBottom, float height)
    {
        float len = a1 - a0;
        Vector3 pos, size;
        if (alongX)
        {
            pos  = new Vector3((a0 + a1) * 0.5f, yBottom + height * 0.5f, fixedCoord);
            size = new Vector3(len, height, WallT);
        }
        else
        {
            pos  = new Vector3(fixedCoord, yBottom + height * 0.5f, (a0 + a1) * 0.5f);
            size = new Vector3(WallT, height, len);
        }
        Box(walls, name, pos, size, wallMat, true, true);
    }

    // ======================================================================
    //  Upper floor slab (four pieces around the stair opening)
    // ======================================================================

    static void BuildUpperSlab()
    {
        float y = StoreyH - SlabT * 0.5f;           // centre of the 3.0 .. 3.2 slab
        float hx0 = -StairW * 0.5f, hx1 = StairW * 0.5f;
        float hz0 = StairEndZ - StepD * Steps, hz1 = StairEndZ;   // -3.85 .. 0.95

        // south of the opening, full width
        SlabPiece("Ceiling_South", -HalfX, HalfX, -HalfZ, hz0, y);
        // north of the opening, full width
        SlabPiece("Ceiling_North", -HalfX, HalfX, hz1, HalfZ, y);
        // west and east strips beside the opening
        SlabPiece("Ceiling_West", -HalfX, hx0, hz0, hz1, y);
        SlabPiece("Ceiling_East", hx1, HalfX, hz0, hz1, y);
    }

    static void SlabPiece(string name, float x0, float x1, float z0, float z1, float yCentre)
    {
        Box(ceiling, name, new Vector3((x0 + x1) * 0.5f, yCentre, (z0 + z1) * 0.5f),
            new Vector3(x1 - x0, SlabT, z1 - z0), ceilMat, true, true);
    }

    // ======================================================================
    //  Stairs
    // ======================================================================

    static void BuildStairs()
    {
        for (int i = 1; i <= Steps; i++)
        {
            float top = StepH * i;                                  // tread height
            float zNew = StairEndZ - StepD * i;                      // south edge
            float zOld = StairEndZ - StepD * (i - 1);                // north edge
            // Solid block from the floor up to the tread: keeps a BoxCollider on every step
            // so the CharacterController (step offset 0.3) and NavMesh agents can climb it.
            // layer stays Default (Box only changes the layer when obstacle == true)
            Box(stairs, "Step_" + i.ToString("00"),
                new Vector3(0f, top * 0.5f, (zNew + zOld) * 0.5f),
                new Vector3(StairW, top, StepD), stairMat, false, false);
            stepsMade++;
        }
    }

    // ======================================================================
    //  Railing around the stair hole (west, east and north sides)
    // ======================================================================

    static void BuildRailings()
    {
        float yBase = StoreyH;
        float hx = StairW * 0.5f;
        float z0 = StairEndZ - StepD * Steps;    // -3.85
        float z1 = StairEndZ;                    //  0.95

        // side rails: run along Z at x = +-0.8
        RailAlongZ(-hx, z0, z1, yBase);
        RailAlongZ(hx, z0, z1, yBase);
        // end rail: runs along X at z = 0.95
        RailAlongX(-hx, hx, z1, yBase);
    }

    static void RailAlongZ(float x, float z0, float z1, float yBase)
    {
        float mid = (z0 + z1) * 0.5f, len = z1 - z0;
        // top rail
        Box(railings, "Rail_Top", new Vector3(x, yBase + 1f, mid),
            new Vector3(0.08f, 0.1f, len), stairMat, true, true); railsMade++;
        // mid rail
        Box(railings, "Rail_Mid", new Vector3(x, yBase + 0.55f, mid),
            new Vector3(0.06f, 0.06f, len), stairMat, true, true); railsMade++;
        // posts at both ends and in the middle
        foreach (float z in new[] { z0 + 0.05f, mid, z1 - 0.05f })
        {
            Box(railings, "Rail_Post", new Vector3(x, yBase + 0.5f, z),
                new Vector3(0.08f, 1f, 0.08f), stairMat, true, true); railsMade++;
        }
    }

    static void RailAlongX(float x0, float x1, float z, float yBase)
    {
        float mid = (x0 + x1) * 0.5f, len = x1 - x0;
        Box(railings, "Rail_Top", new Vector3(mid, yBase + 1f, z),
            new Vector3(len, 0.1f, 0.08f), stairMat, true, true); railsMade++;
        Box(railings, "Rail_Mid", new Vector3(mid, yBase + 0.55f, z),
            new Vector3(len, 0.06f, 0.06f), stairMat, true, true); railsMade++;
        foreach (float x in new[] { x0 + 0.05f, mid, x1 - 0.05f })
        {
            Box(railings, "Rail_Post", new Vector3(x, yBase + 0.5f, z),
                new Vector3(0.08f, 1f, 0.08f), stairMat, true, true); railsMade++;
        }
    }

    // ======================================================================
    //  NavMesh
    // ======================================================================

    static void EnsureNavMeshSurface()
    {
        var surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(root.gameObject);

        // Collect everything (also the steps, which are deliberately not flagged static)
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        EditorUtility.SetDirty(surface);
    }

    static bool BakeNavMesh()
    {
        var surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) return false;
        try
        {
            surface.BuildNavMesh();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("NavMesh bake failed: " + e.Message);
            return false;
        }
    }

    // ======================================================================
    //  Helpers
    // ======================================================================

    static Transform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build big house");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size,
                          Material mat, bool obstacle, bool markStatic)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        if (obstacle && obstacleLayer >= 0) go.layer = obstacleLayer;
        go.isStatic = markStatic;
        Undo.RegisterCreatedObjectUndo(go, "Build big house");
        cubes++;
        return go;
    }

    // Reuse the greybox material if it exists (keeps the look the pack gave it),
    // otherwise create it with the greybox colour.
    static Material LoadMat(string name, Color fallback)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        m = new Material(shader);
        m.color = fallback;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
