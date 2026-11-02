#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Builds a big, spacious house (30 x 20 m ground floor + basement) and rewires the game to it.
// Ground floor:  Living Room + Kitchen (south), Hallway (middle), Bedroom + Nursery + Bathroom (north)
// Basement:      reached by a long ramp at the east end of the hallway, two big chambers.
// Menu: Haunted House/Build BIG House   (and Haunted House/Remove BIG House)
public static class BuildBigHouse
{
    const float H = 3.2f;      // room height
    const float T = 0.3f;      // wall thickness
    const float DOOR_H = 2.4f; // default door height

    static int obstacleLayer;
    static Material mWall, mFloor, mCeil, mBase, mWood, mBed, mProp;

    class Door
    {
        public float c, w, h;
        public Door(float c, float w, float h = DOOR_H) { this.c = c; this.w = w; this.h = h; }
    }

    static readonly string[] BigRoots =
        { "BigHouse", "BigWaypoints", "BigPageSpots", "BigKeySpots", "BigLamps", "BigLockers", "BigBarricades" };
    static readonly string[] OldRoots =
        { "House", "Waypoints", "PageSpots", "KeySpots", "RoomLamps", "HidingSpots", "Barricades", "Walls", "Floor", "TestFloor" };

    // ------------------------------------------------------------------ menu

    [MenuItem("Haunted House/Remove BIG House")]
    public static void Remove()
    {
        foreach (var n in BigRoots) DestroyRoot(n);
        foreach (var n in OldRoots)
            if (n != "TestFloor") SetRootActive(n, true);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("BIG House removed. Old house shown again. Re-bake the NavMesh on the old House.");
    }

    [MenuItem("Haunted House/Build BIG House")]
    public static void Build()
    {
        obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer < 0) obstacleLayer = 6;
        LoadMats();

        foreach (var n in BigRoots) DestroyRoot(n);
        foreach (var n in OldRoots) SetRootActive(n, false);

        var house = new GameObject("BigHouse").transform;
        var enemy = Object.FindFirstObjectByType<EnemyAI>();

        BuildGroundFloor(house);
        BuildBasement(house);
        BuildFurniture(house);

        var lamps = new GameObject("BigLamps").transform;
        BuildLamps(lamps, enemy);

        var lockers = new GameObject("BigLockers").transform;
        Locker(lockers, "Locker_Bedroom", new Vector3(7f, 0f, 19.3f), 0f);
        Locker(lockers, "Locker_Kitchen", new Vector3(29.2f, 0f, 2f), 90f);
        Locker(lockers, "Locker_Basement1", new Vector3(43f, -3.5f, 2.7f), 180f);
        Locker(lockers, "Locker_Basement2", new Vector3(57.8f, -3.5f, 14f), 90f);

        var waypoints = BuildWaypoints();
        var pageSpots = BuildPageSpots();
        var keySpots = BuildKeySpots();
        var barricades = BuildBarricades();

        // NavMesh surface (you click Bake once, see the guide)
        var surf = house.gameObject.AddComponent<NavMeshSurface>();
        surf.collectObjects = CollectObjects.Children;

        // ---- rewire the game
        if (enemy != null)
        {
            SetArray(enemy, "waypoints", waypoints);
            enemy.transform.position = new Vector3(53f, -3.4f, 10f);
        }
        else Debug.LogWarning("EnemyAI not found, waypoints not wired.");

        var spawner = Object.FindFirstObjectByType<PageSpawner>();
        if (spawner != null) SetArray(spawner, "candidateSpots", pageSpots);
        else Debug.LogWarning("PageSpawner not found.");

        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            SetArray(gm, "keySpawnPoints", keySpots);
            SetArray(gm, "barricades", barricades);
        }
        else Debug.LogWarning("GameManager not found.");

        MoveParts();
        PlacePlayer();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = house.gameObject;
        Debug.Log("BIG House built. NEXT: select BigHouse > Nav Mesh Surface > click Bake. Then Ctrl+S.");
    }

    // ------------------------------------------------------------------ geometry

    static void BuildGroundFloor(Transform p)
    {
        Box(p, "Floor", new Vector3(15.1f, -0.15f, 10f), new Vector3(30.2f, T, 20f), mFloor);
        Box(p, "Ceiling", new Vector3(15f, H + 0.15f, 10f), new Vector3(30.6f, T, 20.6f), mCeil);

        // outer walls
        WallX(p, "Outer_S", 0f, 0f, 30f, 0f, H, mWall, new Door(8.5f, 2f));      // front door gap
        WallX(p, "Outer_N", 20f, 0f, 30f, 0f, H, mWall);
        WallZ(p, "Outer_W", 0f, 0f, 20f, 0f, H, mWall);
        WallZ(p, "Outer_E", 30f, 0f, 20f, 0f, H, mWall, new Door(10f, 4f, 2.8f)); // to basement ramp

        // hallway walls
        WallX(p, "Hall_S", 8f, 0f, 30f, 0f, H, mWall, new Door(10f, 2.4f), new Door(24f, 2f));
        WallX(p, "Hall_N", 12f, 0f, 30f, 0f, H, mWall, new Door(6f, 1.8f), new Door(16.5f, 1.8f), new Door(25.5f, 1.8f));

        // dividers
        WallZ(p, "LivingKitchen", 17f, 0f, 8f, 0f, H, mWall, new Door(4f, 3.5f, 2.6f)); // wide archway
        WallZ(p, "BedNursery", 12f, 12f, 20f, 0f, H, mWall, new Door(16f, 1.6f));
        WallZ(p, "NurseryBath", 21f, 12f, 20f, 0f, H, mWall, new Door(16f, 1.6f));

        // exit door in the front gap
        var door = Box(p, "ExitDoor", new Vector3(8.5f, 1.2f, 0f), new Vector3(1.9f, DOOR_H, 0.2f), mWood);
        door.AddComponent<ExitDoor>();
    }

    static void BuildBasement(Transform p)
    {
        // ramp from the east end of the hallway, going down to the east
        float topX = 30.15f, botX = 38.5f, drop = 3.5f;
        float dx = botX - topX;
        float len = Mathf.Sqrt(dx * dx + drop * drop);
        float th = Mathf.Atan2(drop, dx);
        Vector3 mid = new Vector3(topX + dx / 2f, -drop / 2f, 10f);
        Vector3 n = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
        Quaternion rot = Quaternion.Euler(0f, 0f, -th * Mathf.Rad2Deg);

        Box(p, "Ramp", mid - n * 0.15f, new Vector3(len + 0.3f, 0.3f, 4f), mFloor, 0, rot);
        Box(p, "Ramp_WallL", mid + n * (H / 2f) + new Vector3(0, 0, -2.15f), new Vector3(len, H, T), mBase, obstacleLayer, rot);
        Box(p, "Ramp_WallR", mid + n * (H / 2f) + new Vector3(0, 0, 2.15f), new Vector3(len, H, T), mBase, obstacleLayer, rot);
        Box(p, "Ramp_Ceiling", mid + n * (H + 0.15f), new Vector3(len, 0.3f, 4.6f), mCeil, 0, rot);

        // basement rooms: x 38.5..58.5, z 2..18, floor y = -3.5
        float y0 = -3.5f;
        Box(p, "B_Floor", new Vector3(48.5f, y0 - 0.15f, 10f), new Vector3(20.6f, T, 16.6f), mBase);
        Box(p, "B_Ceiling", new Vector3(48.5f, y0 + H + 0.15f, 10f), new Vector3(20.6f, T, 16.6f), mCeil);
        WallX(p, "B_S", 2f, 38.5f, 58.5f, y0, H, mBase);
        WallX(p, "B_N", 18f, 38.5f, 58.5f, y0, H, mBase);
        WallZ(p, "B_E", 58.5f, 2f, 18f, y0, H, mBase);
        WallZ(p, "B_W", 38.5f, 2f, 18f, y0, H, mBase, new Door(10f, 4f, 3f));
        WallZ(p, "B_Divider", 48f, 2f, 18f, y0, H, mBase, new Door(6f, 3f, 2.8f), new Door(14f, 3f, 2.8f));

        // crates
        Crate(p, "Crate_1", new Vector3(41f, y0 + 0.5f, 4f));
        Crate(p, "Crate_2", new Vector3(45f, y0 + 0.5f, 15f));
        Crate(p, "Crate_3", new Vector3(50f, y0 + 0.5f, 16.5f));
        Crate(p, "Crate_4", new Vector3(54f, y0 + 0.5f, 3.5f));
    }

    static void BuildFurniture(Transform p)
    {
        // bedroom
        var bed = Box(p, "Bed", new Vector3(1.6f, 0.3f, 16.4f), new Vector3(2f, 0.6f, 3.2f), mBed);
        Object.DestroyImmediate(bed.GetComponent<Collider>()); // you wake up IN the bed
        Box(p, "Nightstand", new Vector3(3.2f, 0.3f, 18.6f), new Vector3(0.7f, 0.6f, 0.7f), mWood);
        Box(p, "Wardrobe", new Vector3(10.6f, 1.1f, 14f), new Vector3(1.0f, 2.2f, 2.4f), mWood, obstacleLayer);

        // nursery
        Box(p, "Crib", new Vector3(16.5f, 0.5f, 17.5f), new Vector3(1.4f, 1.0f, 2.2f), mWood);
        Box(p, "NurseryDresser", new Vector3(13.2f, 0.5f, 18.5f), new Vector3(1.2f, 1.0f, 0.6f), mWood);
        Box(p, "Rug", new Vector3(16.5f, 0.02f, 14.5f), new Vector3(3f, 0.04f, 3f), mBed, 0);

        // bathroom
        Box(p, "Bathtub", new Vector3(28.6f, 0.4f, 18.4f), new Vector3(1.6f, 0.8f, 3f), mProp);
        Box(p, "Sink", new Vector3(24f, 0.45f, 19.6f), new Vector3(1.2f, 0.9f, 0.5f), mProp);

        // kitchen
        Box(p, "KitchenCounter", new Vector3(23.5f, 0.45f, 7.4f), new Vector3(8f, 0.9f, 0.7f), mWood);
        Box(p, "KitchenTable", new Vector3(23.5f, 0.4f, 3.5f), new Vector3(2.4f, 0.8f, 1.2f), mWood);

        // living room
        Box(p, "Sofa", new Vector3(4f, 0.4f, 5.5f), new Vector3(3f, 0.8f, 1.1f), mBed);
        Box(p, "CoffeeTable", new Vector3(4f, 0.25f, 3.6f), new Vector3(1.4f, 0.5f, 0.8f), mWood);
        Box(p, "Fireplace", new Vector3(0.6f, 1f, 4f), new Vector3(0.8f, 2f, 3f), mProp, obstacleLayer);
    }

    static void Crate(Transform p, string n, Vector3 pos)
    {
        Box(p, n, pos, Vector3.one, mWood, obstacleLayer);
    }

    // ------------------------------------------------------------------ lamps, lockers, spots

    static void BuildLamps(Transform parent, EnemyAI enemy)
    {
        Color warm = new Color(1f, 0.78f, 0.5f), cold = new Color(0.6f, 0.75f, 1f);
        Lamp(parent, "Lamp_Living", new Vector3(8.5f, 2.7f, 4f), warm, 1.4f, enemy);
        Lamp(parent, "Lamp_Kitchen", new Vector3(23.5f, 2.7f, 4f), warm, 1.4f, enemy);
        Lamp(parent, "Lamp_Hall1", new Vector3(5f, 2.7f, 10f), warm, 1.2f, enemy);
        Lamp(parent, "Lamp_Hall2", new Vector3(15f, 2.7f, 10f), warm, 1.2f, enemy);
        Lamp(parent, "Lamp_Hall3", new Vector3(25f, 2.7f, 10f), warm, 1.2f, enemy);
        Lamp(parent, "Lamp_Bedroom", new Vector3(6f, 2.7f, 16f), warm, 1.3f, enemy);
        Lamp(parent, "Lamp_Nursery", new Vector3(16.5f, 2.7f, 16f), cold, 1.3f, enemy);
        Lamp(parent, "Lamp_Bathroom", new Vector3(25.5f, 2.7f, 16f), cold, 1.3f, enemy);
        Lamp(parent, "Lamp_Ramp", new Vector3(34f, 0.6f, 10f), warm, 1.2f, enemy);
        Lamp(parent, "Lamp_Basement1", new Vector3(43f, -0.9f, 10f), cold, 1.6f, enemy);
        Lamp(parent, "Lamp_Basement2", new Vector3(53f, -0.9f, 10f), cold, 1.6f, enemy);
    }

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
        BoxL(root.transform, "Door", new Vector3(0f, 1f, -0.47f), new Vector3(0.75f, 1.8f, 0.06f), mBase);

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

    static List<Transform> BuildWaypoints()
    {
        var root = new GameObject("BigWaypoints").transform;
        var list = new List<Transform>();
        Vector3[] pts =
        {
            new Vector3(3, 0.05f, 10), new Vector3(10, 0.05f, 10), new Vector3(17, 0.05f, 10), new Vector3(24, 0.05f, 10), new Vector3(28, 0.05f, 10),
            new Vector3(9, 0.05f, 4), new Vector3(3, 0.05f, 2.5f),
            new Vector3(23.5f, 0.05f, 5.5f), new Vector3(27, 0.05f, 1.5f),
            new Vector3(6, 0.05f, 16), new Vector3(9, 0.05f, 14),
            new Vector3(16.5f, 0.05f, 15), new Vector3(19, 0.05f, 18),
            new Vector3(25.5f, 0.05f, 15.5f), new Vector3(28, 0.05f, 14),
            new Vector3(34, -1.6f, 10),
            new Vector3(42, -3.45f, 10), new Vector3(44, -3.45f, 5), new Vector3(44, -3.45f, 15),
            new Vector3(53, -3.45f, 10), new Vector3(55, -3.45f, 5), new Vector3(55, -3.45f, 15),
        };
        for (int i = 0; i < pts.Length; i++) list.Add(Marker(root, "WP_" + (i + 1), pts[i]));
        return list;
    }

    static List<Transform> BuildPageSpots()
    {
        var root = new GameObject("BigPageSpots").transform;
        var list = new List<Transform>();
        AddSpot(list, root, "Page_LivingSofa", 4f, 0.95f, 5.5f);
        AddSpot(list, root, "Page_Fireplace", 1.2f, 1.1f, 4f);
        AddSpot(list, root, "Page_KitchenTable", 23.5f, 0.95f, 3.5f);
        AddSpot(list, root, "Page_KitchenCounter", 23.5f, 1.0f, 7.4f);
        AddSpot(list, root, "Page_BedroomPillow", 1.6f, 0.7f, 18.2f);
        AddSpot(list, root, "Page_BedroomWardrobe", 11.4f, 1.2f, 14f);
        AddSpot(list, root, "Page_Crib", 16.5f, 1.1f, 17.5f);
        AddSpot(list, root, "Page_NurseryDresser", 13.2f, 1.1f, 18.5f);
        AddSpot(list, root, "Page_Bathtub", 28.6f, 0.9f, 18.4f);
        AddSpot(list, root, "Page_BathSink", 24f, 1.0f, 19.6f);
        AddSpot(list, root, "Page_HallwayEnd", 28.5f, 0.9f, 11.2f);
        AddSpot(list, root, "Page_BasementCrate1", 41f, -2.45f, 4f);
        AddSpot(list, root, "Page_BasementCrate2", 45f, -2.45f, 15f);
        AddSpot(list, root, "Page_BasementBack", 57.5f, -2.8f, 8f);
        return list;
    }

    static List<Transform> BuildKeySpots()
    {
        var root = new GameObject("BigKeySpots").transform;
        var list = new List<Transform>();
        AddSpot(list, root, "Key_UnderBed", 1.6f, 0.2f, 15f);
        AddSpot(list, root, "Key_KitchenDrawer", 21f, 1.0f, 7.4f);
        AddSpot(list, root, "Key_NurseryFloor", 19.5f, 0.2f, 13.2f);
        AddSpot(list, root, "Key_BathCorner", 22f, 0.2f, 13.5f);
        AddSpot(list, root, "Key_BasementCrate", 50f, -2.45f, 16.5f);
        AddSpot(list, root, "Key_BasementCorner", 39.5f, -3.3f, 16.5f);
        return list;
    }

    static List<Transform> BuildBarricades()
    {
        // each one blocks only a redundant path, so the house can never become unwinnable
        var root = new GameObject("BigBarricades").transform;
        var list = new List<Transform>();
        list.Add(Barricade(root, "Barricade_1_LivingKitchenArch", new Vector3(17f, 1.3f, 4f), new Vector3(0.5f, 2.6f, 3.5f)));
        list.Add(Barricade(root, "Barricade_2_BasementDivider", new Vector3(48f, -2.1f, 14f), new Vector3(0.5f, 2.8f, 3f)));
        list.Add(Barricade(root, "Barricade_3_BedroomNursery", new Vector3(12f, 1.2f, 16f), new Vector3(0.4f, 2.4f, 1.6f)));
        list.Add(Barricade(root, "Barricade_4_NurseryBath", new Vector3(21f, 1.2f, 16f), new Vector3(0.4f, 2.4f, 1.6f)));
        return list;
    }

    static Transform Barricade(Transform p, string n, Vector3 pos, Vector3 size)
    {
        var g = Box(p, n, pos, size, mWood, obstacleLayer);
        var o = g.AddComponent<NavMeshObstacle>();
        o.carving = true;
        o.shape = NavMeshObstacleShape.Box;
        o.center = Vector3.zero;
        o.size = Vector3.one; // scale already equals size
        g.SetActive(false);
        return g.transform;
    }

    // ------------------------------------------------------------------ move existing things

    static void MoveParts()
    {
        MovePickup("Battery_1", 4f, 0.5f, 6.5f);
        MovePickup("Battery_2", 27f, 0.5f, 3f);
        MovePickup("Battery_3", 16f, 0.5f, 19f);
        MovePickup("Battery_4", 44f, -3.0f, 16.5f);
        MovePickup("BottlePickup_1", 14f, 0.5f, 9f);
        MovePickup("BottlePickup_2", 24.5f, 0.5f, 17f);
        MovePickup("BottlePickup_3", 53f, -3.0f, 4f);
    }

    static void MovePickup(string name, float x, float y, float z)
    {
        var g = FindAny(name);
        if (g != null) g.transform.position = new Vector3(x, y, z);
    }

    static void PlacePlayer()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) { Debug.LogWarning("Player not found, not moved."); return; }
        var cc = pc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pc.transform.position = new Vector3(1.8f, 1.0f, 16.4f);   // on the bed
        pc.transform.rotation = Quaternion.Euler(0f, 90f, 0f);    // facing east, toward the room
        if (cc != null) cc.enabled = true;
    }

    // ------------------------------------------------------------------ helpers

    static void LoadMats()
    {
        mWall = GetMat("GB_Wall", new Color(0.55f, 0.52f, 0.48f));
        mFloor = GetMat("GB_Floor", new Color(0.30f, 0.26f, 0.22f));
        mCeil = GetMat("Big_Ceiling", new Color(0.12f, 0.11f, 0.10f));
        mBase = GetMat("Big_Basement", new Color(0.28f, 0.28f, 0.30f));
        mWood = GetMat("Big_Wood", new Color(0.30f, 0.18f, 0.10f));
        mBed = GetMat("Big_Fabric", new Color(0.45f, 0.30f, 0.30f));
        mProp = GetMat("Big_Prop", new Color(0.75f, 0.75f, 0.72f));
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

    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m, int layer = 0, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position = center;
        g.transform.rotation = rot ?? Quaternion.identity;
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

    static void AddSpot(List<Transform> list, Transform parent, string name, float x, float y, float z)
    {
        list.Add(Marker(parent, name, new Vector3(x, y, z)));
    }

    static GameObject FindAny(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    static void SetRootActive(string name, bool active)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) go.SetActive(active);
    }

    static void DestroyRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) Object.DestroyImmediate(go);
    }

    // set a reference field by name; tries each candidate until the field accepts one
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

    static void SetArray(Object target, string prop, List<Transform> items)
    {
        var so = new SerializedObject(target);
        var arr = so.FindProperty(prop);
        if (arr == null || !arr.isArray) { Debug.LogWarning(target.GetType().Name + "." + prop + " array not found"); return; }
        arr.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.objectReferenceValue = items[i];
            if (e.objectReferenceValue == null) e.objectReferenceValue = items[i].gameObject;
        }
        so.ApplyModifiedProperties();
    }
}
#endif