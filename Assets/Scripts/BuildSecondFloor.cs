#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Adds a 2nd floor + attic on top of the BIG House (run "Build BIG House" first).
//
// Stairs 1 (ground -> 2nd floor): inside the Living Room, along the north wall.
// Stairs 2 (2nd floor -> attic):  inside the Guest Room, along the south wall.
//
// 2nd floor: Study (stairs), Guest Room (attic stairs), Library, Playroom, Hallway
// Attic:     one big dark maze-like room with trunks, crates and a locker
//
// Menu: Haunted House/Build 2ND FLOOR + ATTIC
// Running it again rebuilds only the upper floors (safe to repeat).
public static class BuildSecondFloor
{
    const float H = HouseDims.H;        // room height (change it in HouseDims.cs)
    const float HA = HouseDims.HA;       // attic height
    const float T = 0.3f;        // wall thickness
    const float DOOR_H = 2.4f;
    const float F2 = HouseDims.F2;       // top of 2nd floor (= top of ground ceiling slab)
    const float F3 = HouseDims.F3;       // top of attic floor

    static int obstacleLayer;
    static Material mWall, mFloor, mCeil, mWood, mBed, mProp, mAttic;

    class Door
    {
        public float c, w, h;
        public Door(float c, float w, float h = DOOR_H) { this.c = c; this.w = w; this.h = h; }
    }

    static readonly string[] UpRoots = { "BigUpstairs", "BigUpLamps", "BigUpLockers", "BigUpWaypoints", "BigUpPageSpots", "BigUpKeySpots" };

    [MenuItem("Haunted House/Build 2ND FLOOR + ATTIC")]
    public static void Build()
    {
        var bigHouse = FindRoot("BigHouse");
        if (bigHouse == null)
        {
            EditorUtility.DisplayDialog("2nd floor", "BigHouse not found. Open the House scene and run Haunted House > Build BIG House first.", "OK");
            return;
        }

        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) obstacleLayer = 6;
        LoadMats();

        // clean previous run
        foreach (var n in UpRoots) DestroyRoot(n);
        var oldCeil = bigHouse.transform.Find("Ceiling");
        if (oldCeil != null) Object.DestroyImmediate(oldCeil.gameObject);

        var up = new GameObject("BigUpstairs").transform;
        up.SetParent(bigHouse.transform, false);

        var enemy = Object.FindFirstObjectByType<EnemyAI>();

        // stairs: where the opening in the slab must start so you never hit your head
        float stairRise = (H + 0.3f) / 14f;
        int stepsUnderSlab = Mathf.Max(1, Mathf.FloorToInt((H - 2.1f) / stairRise));
        float hole1X0 = 0.5f + stepsUnderSlab * 0.6f;
        float hole2X0 = 16.5f + stepsUnderSlab * 0.6f;

        // ---------------- ground ceiling = 2nd floor slab, with the stair opening
        SlabWithHole(up, "Ceiling", H + 0.15f, T, -0.3f, 30.3f, -0.3f, 20.3f, hole1X0, 8.9f, 5.9f, 7.85f, mFloor);

        // ---------------- stairs 1: Living Room, along the north wall, rising east
        Stairs(up, "Stairs1", 0.5f, 0f, 5.9f, 7.85f);
        Rail(up, "Rail1_S", new Vector3((hole1X0 + 8.9f) / 2f, F2 + 0.5f, 5.9f), new Vector3(8.9f - hole1X0, 1.0f, 0.1f));
        Rail(up, "Rail1_W", new Vector3(hole1X0, F2 + 0.5f, 6.875f), new Vector3(0.1f, 1.0f, 1.95f));

        // ---------------- 2nd floor walls
        float y0 = F2;
        WallX(up, "F2_Outer_S", 0f, 0f, 30f, y0, H, mWall);
        WallX(up, "F2_Outer_N", 20f, 0f, 30f, y0, H, mWall);
        WallZ(up, "F2_Outer_W", 0f, 0f, 20f, y0, H, mWall);
        WallZ(up, "F2_Outer_E", 30f, 0f, 20f, y0, H, mWall);
        WallX(up, "F2_Hall_S", 8f, 0f, 30f, y0, H, mWall, new Door(12f, 1.8f), new Door(24f, 1.8f));
        WallX(up, "F2_Hall_N", 12f, 0f, 30f, y0, H, mWall, new Door(7f, 1.8f), new Door(22f, 1.8f));
        WallZ(up, "F2_Div_S", 15f, 0f, 8f, y0, H, mWall, new Door(4f, 1.6f));
        WallZ(up, "F2_Div_N", 15f, 12f, 20f, y0, H, mWall, new Door(16f, 1.6f));

        // ---------------- 2nd floor ceiling = attic floor slab, with the attic stair opening
        SlabWithHole(up, "AtticFloor", F2 + H + 0.15f, T, -0.3f, 30.3f, -0.3f, 20.3f, hole2X0, 24.9f, 0.2f, 2.6f, mFloor);

        // ---------------- stairs 2: Guest Room, along the south wall, rising east
        Stairs(up, "Stairs2", 16.5f, F2, 0.2f, 2.6f);
        Rail(up, "Rail2_N", new Vector3((hole2X0 + 24.9f) / 2f, F3 + 0.5f, 2.6f), new Vector3(24.9f - hole2X0, 1.0f, 0.1f));
        Rail(up, "Rail2_W", new Vector3(hole2X0, F3 + 0.5f, 1.4f), new Vector3(0.1f, 1.0f, 2.4f));

        // ---------------- attic
        float a0 = F3;
        WallX(up, "F3_Outer_S", 0f, 0f, 30f, a0, HA, mAttic);
        WallX(up, "F3_Outer_N", 20f, 0f, 30f, a0, HA, mAttic);
        WallZ(up, "F3_Outer_W", 0f, 0f, 20f, a0, HA, mAttic);
        WallZ(up, "F3_Outer_E", 30f, 0f, 20f, a0, HA, mAttic);
        WallZ(up, "F3_Part1", 10f, 0f, 14f, a0, HA, mAttic);   // forces a detour north
        WallZ(up, "F3_Part2", 20f, 6f, 20f, a0, HA, mAttic);   // forces a detour south
        Box(up, "AtticRoof", new Vector3(15f, a0 + HA + 0.15f, 10f), new Vector3(30.6f, T, 20.6f), mCeil);
        for (int i = 0; i < 4; i++)  // old beams
            Box(up, "Beam_" + i, new Vector3(5f + i * 7f, a0 + HA / 2f, 10f), new Vector3(0.4f, HA, 0.4f), mWood, obstacleLayer);

        BuildFurniture(up);

        // ---------------- lamps / lockers
        var lamps = new GameObject("BigUpLamps").transform;
        Color warm = new Color(1f, 0.78f, 0.5f), cold = new Color(0.6f, 0.75f, 1f);
        Lamp(lamps, "Lamp_Study", new Vector3(7f, F2 + H - 0.5f, 4f), warm, 1.3f, enemy);
        Lamp(lamps, "Lamp_Guest", new Vector3(22f, F2 + H - 0.5f, 4f), warm, 1.3f, enemy);
        Lamp(lamps, "Lamp_Hall2A", new Vector3(5f, F2 + H - 0.5f, 10f), warm, 1.2f, enemy);
        Lamp(lamps, "Lamp_Hall2B", new Vector3(15f, F2 + H - 0.5f, 10f), warm, 1.2f, enemy);
        Lamp(lamps, "Lamp_Hall2C", new Vector3(25f, F2 + H - 0.5f, 10f), warm, 1.2f, enemy);
        Lamp(lamps, "Lamp_Library", new Vector3(7f, F2 + H - 0.5f, 16f), warm, 1.3f, enemy);
        Lamp(lamps, "Lamp_Playroom", new Vector3(22f, F2 + H - 0.5f, 16f), cold, 1.3f, enemy);
        Lamp(lamps, "Lamp_Attic1", new Vector3(5f, F3 + HA - 0.5f, 5f), cold, 1.6f, enemy);
        Lamp(lamps, "Lamp_Attic2", new Vector3(15f, F3 + HA - 0.5f, 10f), cold, 1.6f, enemy);
        Lamp(lamps, "Lamp_Attic3", new Vector3(25f, F3 + HA - 0.5f, 14f), cold, 1.6f, enemy);

        var lockers = new GameObject("BigUpLockers").transform;
        Locker(lockers, "Locker_Library", new Vector3(14.2f, F2, 19.4f), 0f);
        Locker(lockers, "Locker_Attic", new Vector3(0.8f, F3, 10f), 270f);

        // ---------------- waypoints / spots
        var wps = new List<Transform>();
        var wpRoot = new GameObject("BigUpWaypoints").transform;
        Vector3[] pts =
        {
            new Vector3(4.65f, F2 / 2f, 6.9f),                       // stairs 1 (middle)
            new Vector3(3, F2 + 0.05f, 10), new Vector3(10, F2 + 0.05f, 10), new Vector3(17, F2 + 0.05f, 10),
            new Vector3(24, F2 + 0.05f, 10), new Vector3(28, F2 + 0.05f, 10),
            new Vector3(4, F2 + 0.05f, 3), new Vector3(11, F2 + 0.05f, 4),
            new Vector3(20, F2 + 0.05f, 5), new Vector3(27, F2 + 0.05f, 5.5f),
            new Vector3(5, F2 + 0.05f, 16), new Vector3(11, F2 + 0.05f, 15),
            new Vector3(20, F2 + 0.05f, 16), new Vector3(27, F2 + 0.05f, 15),
            new Vector3(20.65f, (F2 + F3) / 2f, 1.4f),                      // stairs 2 (middle)
            new Vector3(25, F3 + 0.05f, 4), new Vector3(15, F3 + 0.05f, 3), new Vector3(15, F3 + 0.05f, 10),
            new Vector3(15, F3 + 0.05f, 17), new Vector3(5, F3 + 0.05f, 17), new Vector3(5, F3 + 0.05f, 6),
            new Vector3(25, F3 + 0.05f, 12), new Vector3(27, F3 + 0.05f, 18),
        };
        for (int i = 0; i < pts.Length; i++) wps.Add(Marker(wpRoot, "WP2_" + (i + 1), pts[i]));

        var pages = new List<Transform>();
        var pgRoot = new GameObject("BigUpPageSpots").transform;
        pages.Add(Marker(pgRoot, "Page_StudyDesk", new Vector3(3f, F2 + 0.85f, 2f)));
        pages.Add(Marker(pgRoot, "Page_GuestBed", new Vector3(27.5f, F2 + 0.65f, 3f)));
        pages.Add(Marker(pgRoot, "Page_LibraryShelf", new Vector3(7f, F2 + 1.3f, 18.8f)));
        pages.Add(Marker(pgRoot, "Page_PlayroomToybox", new Vector3(22f, F2 + 0.75f, 17f)));
        pages.Add(Marker(pgRoot, "Page_AtticTrunk", new Vector3(6f, F3 + 0.75f, 17f)));
        pages.Add(Marker(pgRoot, "Page_AtticWall", new Vector3(0.9f, F3 + 1.2f, 15f)));
        pages.Add(Marker(pgRoot, "Page_AtticCrate", new Vector3(15f, F3 + 1.05f, 14f)));

        var keys = new List<Transform>();
        var kyRoot = new GameObject("BigUpKeySpots").transform;
        keys.Add(Marker(kyRoot, "Key_AtticCrate", new Vector3(27f, F3 + 1.05f, 18f)));
        keys.Add(Marker(kyRoot, "Key_StudyRug", new Vector3(11f, F2 + 0.2f, 2f)));
        keys.Add(Marker(kyRoot, "Key_PlayroomFloor", new Vector3(28f, F2 + 0.2f, 13.5f)));

        // ---------------- rewire (adds to the existing lists)
        if (enemy != null) AppendArray(enemy, "waypoints", wps);
        var spawner = Object.FindFirstObjectByType<PageSpawner>();
        if (spawner != null) AppendArray(spawner, "candidateSpots", pages);
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null) AppendArray(gm, "keySpawnPoints", keys);

        // things from the ground floor that would now be inside the stairs
        Move("Battery_1", 6f, 0.5f, 3f);
        Move("Sofa", 4f, 0.4f, 4.7f);
        Move("CoffeeTable", 4f, 0.25f, 2.9f);
        Move("Page_LivingSofa", 4f, 0.95f, 4.7f);

        FixStairs.Run();   // smooth ramp collider on the stairs (no bumping or sticking)

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = bigHouse;
        Debug.Log("2nd floor + attic built. NEXT: select BigHouse > Nav Mesh Surface > Bake, then Ctrl+S.");
    }

    // ------------------------------------------------------------------ furniture

    static void BuildFurniture(Transform p)
    {
        // study
        Box(p, "Desk", new Vector3(3f, F2 + 0.4f, 2f), new Vector3(2.2f, 0.8f, 1f), mWood);
        Box(p, "StudyShelf", new Vector3(12f, F2 + 1.1f, 0.6f), new Vector3(4f, 2.2f, 0.5f), mWood, obstacleLayer);
        // guest room
        Box(p, "GuestBed", new Vector3(27.5f, F2 + 0.3f, 3f), new Vector3(2f, 0.6f, 3f), mBed);
        Box(p, "GuestWardrobe", new Vector3(29.2f, F2 + 1.1f, 6.5f), new Vector3(1f, 2.2f, 2f), mWood, obstacleLayer);
        // library
        Box(p, "LibraryShelves", new Vector3(7.5f, F2 + 1.1f, 19.6f), new Vector3(12f, 2.2f, 0.5f), mWood, obstacleLayer);
        Box(p, "LibraryTable", new Vector3(8f, F2 + 0.4f, 15f), new Vector3(2f, 0.8f, 1f), mWood);
        // playroom
        Box(p, "Toybox", new Vector3(22f, F2 + 0.35f, 17f), new Vector3(1.2f, 0.7f, 0.8f), mBed);
        Box(p, "PlayRug", new Vector3(22f, F2 + 0.02f, 15f), new Vector3(4f, 0.04f, 4f), mBed, 0);
        // attic
        Box(p, "AtticTrunk", new Vector3(6f, F3 + 0.35f, 17f), new Vector3(1.4f, 0.7f, 0.8f), mWood);
        Box(p, "AtticCrate1", new Vector3(15f, F3 + 0.5f, 14f), Vector3.one, mWood, obstacleLayer);
        Box(p, "AtticCrate2", new Vector3(27f, F3 + 0.5f, 18f), Vector3.one, mWood, obstacleLayer);
        Box(p, "SheetFurniture1", new Vector3(24f, F3 + 0.7f, 10f), new Vector3(1f, 1.4f, 1.6f), mProp, obstacleLayer);
        Box(p, "SheetFurniture2", new Vector3(5f, F3 + 0.7f, 3f), new Vector3(1.6f, 1.4f, 1f), mProp, obstacleLayer);
    }

    // ------------------------------------------------------------------ stairs, slabs, rails

    // 14 solid steps (rise 0.25, run 0.6) rising toward +X
    static void Stairs(Transform p, string n, float x0, float baseY, float zA, float zB)
    {
        const int N = 14; const float run = 0.6f;
        float rise = (H + 0.3f) / N;
        for (int i = 0; i < N; i++)
        {
            float h = (i + 1) * rise;
            Box(p, n + "_step" + i, new Vector3(x0 + (i + 0.5f) * run, baseY + h / 2f, (zA + zB) / 2f),
                new Vector3(run, h, zB - zA), mWood, 0);
        }
    }

    static void Rail(Transform p, string n, Vector3 c, Vector3 size)
    {
        Box(p, n, c, size, mWood, obstacleLayer);
    }

    static void SlabWithHole(Transform p, string n, float y, float th, float x0, float x1, float z0, float z1,
                             float hx0, float hx1, float hz0, float hz1, Material m)
    {
        Rect(p, n + "_A", x0, hx0, z0, z1, y, th, m);
        Rect(p, n + "_B", hx1, x1, z0, z1, y, th, m);
        Rect(p, n + "_C", hx0, hx1, z0, hz0, y, th, m);
        Rect(p, n + "_D", hx0, hx1, hz1, z1, y, th, m);
    }

    static void Rect(Transform p, string n, float xa, float xb, float za, float zb, float y, float th, Material m)
    {
        if (xb - xa < 0.01f || zb - za < 0.01f) return;
        Box(p, n, new Vector3((xa + xb) / 2f, y, (za + zb) / 2f), new Vector3(xb - xa, th, zb - za), m);
    }

    // ------------------------------------------------------------------ lamps, lockers

    static void Lamp(Transform p, string n, Vector3 pos, Color col, float inten, EnemyAI enemy)
    {
        var g = new GameObject(n);
        g.transform.SetParent(p, false);
        g.transform.position = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.intensity = inten;
        l.range = 10f;
        l.shadows = LightShadows.None;
        var f = g.AddComponent<FlickerLight>();
        if (enemy != null) SetRef(f, "enemy", enemy, enemy.transform);
        SetFloat(f, "triggerRange", 9f);
        SetFloat(f, "baseIntensity", inten);
    }

    static void Locker(Transform p, string n, Vector3 pos, float yaw)
    {
        var root = new GameObject(n);
        root.transform.SetParent(p, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        BoxL(root.transform, "Body", new Vector3(0f, 1f, 0f), new Vector3(0.9f, 2f, 0.9f), mWood);
        BoxL(root.transform, "Door", new Vector3(0f, 1f, -0.47f), new Vector3(0.75f, 1.8f, 0.06f), mAttic);

        var hide = new GameObject("hidePoint").transform;
        hide.SetParent(root.transform, false);
        hide.localPosition = new Vector3(0f, 1f, 0f);
        var exit = new GameObject("exitPoint").transform;
        exit.SetParent(root.transform, false);
        exit.localPosition = new Vector3(0f, 1f, -1.5f);

        var hs = root.AddComponent<HidingSpot>();
        SetRef(hs, "hidePoint", hide, hide.gameObject);
        SetRef(hs, "exitPoint", exit, exit.gameObject);
    }

    // ------------------------------------------------------------------ helpers

    static void LoadMats()
    {
        mWall = GetMat("GB_Wall", new Color(0.55f, 0.52f, 0.48f));
        mFloor = GetMat("GB_Floor", new Color(0.30f, 0.26f, 0.22f));
        mCeil = GetMat("Big_Ceiling", new Color(0.12f, 0.11f, 0.10f));
        mWood = GetMat("Big_Wood", new Color(0.30f, 0.18f, 0.10f));
        mBed = GetMat("Big_Fabric", new Color(0.45f, 0.30f, 0.30f));
        mProp = GetMat("Big_Prop", new Color(0.75f, 0.75f, 0.72f));
        mAttic = GetMat("Big_Attic", new Color(0.22f, 0.16f, 0.12f));
    }

    static Material GetMat(string name, Color c)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, int layer = 0)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position = center;
        g.transform.localScale = size;
        g.layer = layer;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void BoxL(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
    }

    static void WallX(Transform p, string n, float z, float x0, float x1, float y0, float h, Material m, params Door[] doors)
    {
        System.Array.Sort(doors, (a, b) => a.c.CompareTo(b.c));
        float cur = x0; int i = 0;
        foreach (var d in doors)
        {
            SegX(p, n + "_" + i++, z, cur, d.c - d.w / 2f, y0, h, m);
            if (d.h < h - 0.01f)
                Box(p, n + "_lintel" + i, new Vector3(d.c, y0 + d.h + (h - d.h) / 2f, z), new Vector3(d.w, h - d.h, T), m, obstacleLayer);
            cur = d.c + d.w / 2f;
        }
        SegX(p, n + "_" + i, z, cur, x1, y0, h, m);
    }

    static void SegX(Transform p, string n, float z, float a, float b, float y0, float h, Material m)
    {
        if (b - a < 0.01f) return;
        Box(p, n, new Vector3((a + b) / 2f, y0 + h / 2f, z), new Vector3(b - a, h, T), m, obstacleLayer);
    }

    static void WallZ(Transform p, string n, float x, float z0, float z1, float y0, float h, Material m, params Door[] doors)
    {
        System.Array.Sort(doors, (a, b) => a.c.CompareTo(b.c));
        float cur = z0; int i = 0;
        foreach (var d in doors)
        {
            SegZ(p, n + "_" + i++, x, cur, d.c - d.w / 2f, y0, h, m);
            if (d.h < h - 0.01f)
                Box(p, n + "_lintel" + i, new Vector3(x, y0 + d.h + (h - d.h) / 2f, d.c), new Vector3(T, h - d.h, d.w), m, obstacleLayer);
            cur = d.c + d.w / 2f;
        }
        SegZ(p, n + "_" + i, x, cur, z1, y0, h, m);
    }

    static void SegZ(Transform p, string n, float x, float a, float b, float y0, float h, Material m)
    {
        if (b - a < 0.01f) return;
        Box(p, n, new Vector3(x, y0 + h / 2f, (a + b) / 2f), new Vector3(T, h, b - a), m, obstacleLayer);
    }

    static Transform Marker(Transform parent, string name, Vector3 pos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        return g.transform;
    }

    static void Move(string name, float x, float y, float z)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) { t.position = new Vector3(x, y, z); return; }
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void DestroyRoot(string name)
    {
        // the container may sit under BigHouse (BigUpstairs) or at the root (others)
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == name) { Object.DestroyImmediate(go); continue; }
            if (go.name == "BigHouse")
            {
                var c = go.transform.Find(name);
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
        }
    }

    static void SetRef(Object target, string prop, params Object[] candidates)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning(target.GetType().Name + "." + prop + " not found"); return; }
        foreach (var c in candidates)
        {
            p.objectReferenceValue = c;
            if (p.objectReferenceValue != null) break;
        }
        so.ApplyModifiedProperties();
    }

    static void SetFloat(Object target, string prop, float v)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) return;
        p.floatValue = v;
        so.ApplyModifiedProperties();
    }

    // keeps the existing entries (drops broken ones) and adds the new ones
    static void AppendArray(Object target, string prop, List<Transform> items)
    {
        var so = new SerializedObject(target);
        var arr = so.FindProperty(prop);
        if (arr == null || !arr.isArray) { Debug.LogWarning(target.GetType().Name + "." + prop + " array not found"); return; }

        for (int i = arr.arraySize - 1; i >= 0; i--)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null)
                arr.DeleteArrayElementAtIndex(i);

        int start = arr.arraySize;
        arr.arraySize = start + items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(start + i);
            e.objectReferenceValue = items[i];
            if (e.objectReferenceValue == null) e.objectReferenceValue = items[i].gameObject;
        }
        so.ApplyModifiedProperties();
    }
}
#endif