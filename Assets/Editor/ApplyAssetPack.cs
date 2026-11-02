// Put this file in Assets/Editor/ and the HauntedAssets folder (models + textures) anywhere in Assets.
// Then use the menu:  Haunted House > APPLY ASSET PACK
//
// What it does (safe to run again):
//   * builds URP materials from the pack textures (Assets/HauntedAssets/Materials)
//   * textures the greybox: wallpaper walls, plank floor, wood ceiling, rusty lockers, wooden barricades
//   * swaps the front-door wall for WallForDoor_1 and hangs Door_1 + DoorWodCover_1 in it
//   * adds corner pillars, beds, tables, night stands, a rusty cage and a coat stand (House/PackProps)
//   * moves the page/key spots onto the new furniture so nothing floats in mid-air
//   * gives the enemy a dark body, glowing eyes and the bloody knife
//   * rebuilds the NavMesh so the enemy walks around the furniture
//
// Needs the earlier builders to have been run first (House, Player, Enemy, lockers, ...).
// Missing pieces are skipped with a warning, nothing is deleted except what this script made.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public static class ApplyAssetPack
{
    // ---------------- things you may want to tweak ----------------
    const float WallH = 3f;            // must match BuildGreyboxHouse
    const float WallT = 0.2f;
    const float WallTile = 1.5f;       // metres covered by one wallpaper repeat
    const float FloorTile = 2.5f;      // metres covered by one floor-plank repeat
    const float DoorYaw = 0f;          // set to 180 if the door faces the wrong way
    const float KnifeHeldYaw = 90f;    // rotate if the held knife points sideways/backwards
    const float KnifeHeldLength = 0.6f;
    const float KnifeTableLength = 0.38f;

    const string OutDir = "Assets/HauntedAssets/Materials";

    static readonly string[] ModelNames =
    {
        "bed_1", "Corner_1", "Cube_011", "Door_1", "DoorWodCover_1", "DressHolder_1",
        "Floor_1", "Knife", "NightStand_1", "Roof_1", "Table_1", "Wall_1", "WallForDoor_1"
    };

    static float packScale = 0.5f;     // measured at runtime (pack walls are 6 m tall, ours are 3 m)
    static int obstacleLayer;
    static Transform house, props;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly List<string> missing = new List<string>();
    static int placed, moved;

    [MenuItem("Haunted House/APPLY ASSET PACK")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("Stop Play mode first, then run Haunted House > APPLY ASSET PACK."); return; }

        var houseT = FindDeep("House");
        if (houseT == null || houseT.Find("Walls") == null)
        { Debug.LogError("No 'House' with 'Walls' in this scene. Open the House scene (or run Build Greybox House) first."); return; }
        house = houseT;
        obstacleLayer = LayerMask.NameToLayer("Obstacle");

        mats.Clear(); missing.Clear(); placed = 0; moved = 0;
        bool baked = false;

        try
        {
            Step("Building materials", 0.05f);   BuildMaterials();
            Step("Linking model materials", 0.15f); RemapModelMaterials();
            packScale = MeasurePackScale();

            Step("Front door wall", 0.30f);      FrontDoorWall();
            Step("Texturing greybox", 0.45f);    RetextureGreybox();
            Step("Placing furniture", 0.60f);    BuildProps();
            Step("Styling the enemy", 0.80f);    StyleEnemy();
            Step("Baking NavMesh", 0.90f);       baked = Rebake();
            Step("Fixing enemy & waypoints", 0.95f);
            if (baked) SnapToNavMesh();
            WireEnemy();
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { EditorUtility.ClearProgressBar(); }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        string msg = "Asset pack applied: " + placed + " props placed, " + moved + " page/key spots moved onto furniture, " +
                     "NavMesh " + (baked ? "rebaked" : "NOT rebaked (select House > NavMesh Surface > Bake)") + ". Press Ctrl+S to save the scene.";
        if (missing.Count > 0) msg += "\nMissing files (skipped): " + string.Join(", ", missing);
        Debug.Log(msg);
    }

    static void Step(string label, float t) { EditorUtility.DisplayProgressBar("Applying asset pack", label, t); }

    // ======================================================================
    //  Materials
    // ======================================================================

    static void BuildMaterials()
    {
        EnsureFolder(OutDir);
        Make("wall",  "WallPaint",                          new Color(0.85f, 0.80f, 0.80f), 0.10f);
        Make("floor", "WoodGround",                         new Color(0.90f, 0.90f, 0.90f), 0.15f);
        Make("ceil",  "weathered_brown_planks_diff_1k",     new Color(0.70f, 0.70f, 0.70f), 0.05f);
        Make("wood",  "weathered_brown_planks_diff_1k",     Color.white,                    0.15f);
        Make("rust",  "RustTexture",                        Color.white,                    0.30f);
        Make("door",  "OldDoorPhoto",                       Color.white,                    0.20f);
        Make("bed",   "bed",                                Color.white,                    0.10f);
        Make("knife", "knife",                              Color.white,                    0.60f);
        Make("enemy", null,                                 new Color(0.03f, 0.03f, 0.04f), 0.25f);
        var eye = Make("eye", null,                         new Color(0.4f, 0f, 0f),        0.9f);
        if (eye != null)
        {
            eye.EnableKeyword("_EMISSION");
            eye.SetColor("_EmissionColor", new Color(3.5f, 0.15f, 0.1f));
            EditorUtility.SetDirty(eye);
        }
    }

    static Material Make(string key, string textureName, Color tint, float smooth)
    {
        string path = OutDir + "/M_" + key + ".mat";
        Texture2D tex = null;
        if (textureName != null)
        {
            tex = FindTexture(textureName);
            if (tex == null) missing.Add(textureName);
        }

        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        if (tex != null) m.mainTexture = tex;
        m.color = tint;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        mats[key] = m;
        return m;
    }

    static Material Mat(string key)
    {
        Material m;
        return mats.TryGetValue(key, out m) ? m : null;
    }

    // Copy of a base material with the texture repeated tx x ty times (cached as an asset)
    static Material Tiled(string key, float tx, float ty)
    {
        var b = Mat(key);
        if (b == null) return null;
        tx = Mathf.Max(0.1f, Mathf.Round(tx * 10f) / 10f);
        ty = Mathf.Max(0.1f, Mathf.Round(ty * 10f) / 10f);
        string name = b.name + "_t" + tx.ToString("0.#", CultureInfo.InvariantCulture) + "x" + ty.ToString("0.#", CultureInfo.InvariantCulture);
        string folder = OutDir + "/Tiled";
        EnsureFolder(folder);
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(b);
            AssetDatabase.CreateAsset(m, path);
        }
        m.CopyPropertiesFromMaterial(b);
        m.mainTextureScale = new Vector2(tx, ty);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Which of our materials goes with an FBX material name
    static string KeyFor(string rawName)
    {
        string n = (rawName ?? "").ToLowerInvariant();
        if (n.StartsWith("m_"))
        {
            string k = n.Substring(2);
            if (mats.ContainsKey(k)) return k;
        }
        if (n.Contains("wallpaint")) return "wall";
        if (n.Contains("door")) return "door";
        if (n.Contains("rust")) return "rust";
        if (n.Contains("floor")) return "floor";
        if (n.Contains("roof")) return "ceil";
        if (n.Contains("bed")) return "bed";
        if (n == "material" || n.StartsWith("material")) return "knife";
        return "wood";
    }

    // Make the FBX files themselves use our materials (so dragging a model into the scene looks right too)
    static void RemapModelMaterials()
    {
        foreach (var model in ModelNames)
        {
            try
            {
                var go = FindModel(model);
                if (go == null) continue;
                string path = AssetDatabase.GetAssetPath(go);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;

                bool changed = false;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var m = o as Material;
                    if (m == null) continue;
                    var target = Mat(KeyFor(m.name));
                    if (target == null) continue;
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(m), target);
                    changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
            catch (Exception e) { Debug.LogWarning("Could not remap materials on " + model + ": " + e.Message); }
        }
    }

    static void ApplyMats(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var arr = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < arr.Length; i++)
            {
                var m = Mat(KeyFor(arr[i] != null ? arr[i].name : ""));
                if (m != null) { arr[i] = m; changed = true; }
            }
            if (changed) r.sharedMaterials = arr;
        }
    }

    // ======================================================================
    //  Greybox: front door wall + retexturing
    // ======================================================================

    // Floor and Ceiling may be a single cube or a container of pieces (the big house splits
    // the upper slab around the stair opening), so texture every renderer underneath.
    static void TextureAll(Transform t, string matKey)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            var s = r.transform.lossyScale;
            r.sharedMaterial = Tiled(matKey, Mathf.Max(s.x, s.z) / FloorTile, s.z / FloorTile);
        }
    }

    static void FrontDoorWall()
    {
        var walls = house.Find("Walls");

        // remove the generated south wall pieces (this script's earlier output included)
        for (int i = walls.childCount - 1; i >= 0; i--)
        {
            var c = walls.GetChild(i);
            if (c.name.StartsWith("Outer_South") || c.name == "DoorWall_Pack")
                Undo.DestroyObjectImmediate(c.gameObject);
        }

        var panel = Spawn("WallForDoor_1", walls, "DoorWall_Pack");
        float leftEnd = -0.7f, rightStart = 0.7f;

        if (panel != null)
        {
            FitSize(panel, new Vector3(3f, WallH, WallT));
            PlaceBounds(panel, new Vector3(0f, WallH / 2f, -7f), false);

            float cx;
            if (OpeningCenterX(panel, out cx))
                panel.transform.position += new Vector3(-cx, 0f, 0f);   // centre the opening on x = 0

            var b = GetBounds(panel);
            leftEnd = b.min.x;
            rightStart = b.max.x;

            SetLayerAndStatic(panel);
            var mf = panel.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
            }
        }
        else
        {
            // fallback: simple lintel above the gap
            Cube("Outer_South_Lintel", walls, new Vector3(0f, 2.2f + (WallH - 2.2f) / 2f, -7f), new Vector3(1.4f, WallH - 2.2f, WallT), "wall");
        }

        Cube("Outer_South_L", walls, new Vector3((-6f + leftEnd) / 2f, WallH / 2f, -7f),
             new Vector3(leftEnd + 6f, WallH, WallT), "wall");
        Cube("Outer_South_R", walls, new Vector3((rightStart + 6f) / 2f, WallH / 2f, -7f),
             new Vector3(6f - rightStart, WallH, WallT), "wall");

        // ---- the actual door, hung in the opening ----
        var exit = house.Find("ExitDoor");
        var old = house.Find("ExitDoorModel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var holder = new GameObject("ExitDoorModel");
        Undo.RegisterCreatedObjectUndo(holder, "Asset pack");
        holder.transform.SetParent(house, false);

        var cover = Spawn("DoorWodCover_1", holder.transform, "DoorFrame");
        var door = Spawn("Door_1", holder.transform, "DoorLeaf");
        if (cover != null)
        {
            cover.transform.localScale = Vector3.one * packScale;
            cover.transform.rotation = Quaternion.Euler(0f, DoorYaw, 0f);
            PlaceBounds(cover, new Vector3(0f, 0f, -7f), true);
            if (door != null)
            {
                door.transform.localScale = Vector3.one * packScale;
                door.transform.rotation = cover.transform.rotation;
                door.transform.position = cover.transform.position;   // both were modelled around the same origin
            }
            // the invisible ExitDoor box stays: it is what you interact with
            if (exit != null)
            {
                var r = exit.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }
        else if (door != null)
        {
            door.transform.localScale = Vector3.one * packScale;
            door.transform.rotation = Quaternion.Euler(0f, DoorYaw, 0f);
            PlaceBounds(door, new Vector3(0f, 0f, -7f), true);
            if (exit != null)
            {
                var r = exit.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }
        // If neither model was found the grey ExitDoor box simply stays visible.
    }

    static void RetextureGreybox()
    {
        var walls = house.Find("Walls");
        foreach (Transform t in walls)
        {
            var r = t.GetComponent<Renderer>();
            if (r == null || t.name == "DoorWall_Pack") continue;
            var s = t.localScale;
            r.sharedMaterial = Tiled("wall", Mathf.Max(s.x, s.z) / WallTile, s.y / WallTile);
        }

        var floor = house.Find("Floor");
        if (floor != null)
            floor.GetComponent<Renderer>().sharedMaterial = Tiled("floor", floor.localScale.x / FloorTile, floor.localScale.z / FloorTile);
        TextureAll(house.Find("Ceiling"), "ceil");
        var block = house.Find("Centerpiece");
        if (block != null)
            block.GetComponent<Renderer>().sharedMaterial = Tiled("wood", block.localScale.x / WallTile, block.localScale.y / WallTile);

        // lockers (HidingSpots/Locker_*/Body + Door)
        var hs = FindDeep("HidingSpots");
        if (hs != null)
        {
            foreach (var r in hs.GetComponentsInChildren<Renderer>(true))
            {
                var s = r.transform.localScale;
                r.sharedMaterial = Tiled("rust", Mathf.Max(s.x, s.z) / 1.2f, s.y / 1.2f);
            }
        }

        // barricades
        foreach (var t in AllTransforms())
        {
            if (!t.name.StartsWith("Barricade_")) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            var s = t.localScale;
            r.sharedMaterial = Tiled("wood", Mathf.Max(s.x, s.z) / 1.2f, s.y / 1.2f);
        }
    }

    // ======================================================================
    //  Furniture
    // ======================================================================

    static void BuildProps()
    {
        var old = house.Find("PackProps");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject("PackProps");
        Undo.RegisterCreatedObjectUndo(go, "Asset pack");
        go.transform.SetParent(house, false);
        props = go.transform;

        // ---- corner trim: 4 outer corners + 4 inner wall junctions ----
        var posts = new[]
        {
            new Vector2(-6f, -7f), new Vector2(6f, -7f), new Vector2(-6f, 7f), new Vector2(6f, 7f),
            new Vector2(-2f,  2f), new Vector2(2f,  2f), new Vector2(-2f, -3f), new Vector2(2f, -3f)
        };
        for (int i = 0; i < posts.Length; i++)
        {
            var p = Spawn("Corner_1", props, "Pillar_" + (i + 1));
            if (p == null) break;
            FitSize(p, new Vector3(0.26f, WallH, 0.26f));
            PlaceBounds(p, new Vector3(posts[i].x, 0f, posts[i].y), true);
            placed++;
        }

        // ---- Bedroom 1 ----
        var bed = Prop("bed_1", "Bed_Bedroom1", -3.9f, 6.0f, 0f);
        if (bed != null) { var b = GetBounds(bed); Move("Page_Bedroom1Pillow", new Vector3(b.center.x, b.min.y + 0.55f, b.center.z)); }
        var ns = Prop("NightStand_1", "NightStand_Bedroom1", -5.45f, 6.45f, 0f);
        if (ns != null) Move("Key_UnderBed", TopOf(ns));

        // ---- Kitchen ----
        var table = Prop("Table_1", "Table_Kitchen", -5.1f, 0f, 0f);
        if (table != null)
        {
            var top = TopOf(table);
            Move("Page_KitchenTable", top);
            PropFit("Knife", "Knife_Kitchen", top.x + 0.1f, top.y - 0.06f, top.z + 0.35f, 25f, KnifeTableLength);
        }
        ns = Prop("NightStand_1", "Drawer_Kitchen", -5.45f, -2.0f, 0f);
        if (ns != null) Move("Key_KitchenDrawer", TopOf(ns));

        // ---- Living room ----
        Prop("DressHolder_1", "CoatStand_Living", -1.5f, 1.5f, 0f);

        // ---- Bedroom 2 ----
        ns = Prop("NightStand_1", "Dresser_Bedroom2", -5.45f, -6.45f, 0f);
        if (ns != null) Move("Page_Bedroom2Dresser", TopOf(ns));
        Prop("DressHolder_1", "CoatStand_Bedroom2", -5.6f, -4.6f, 0f);

        // ---- Bathroom ----
        ns = Prop("NightStand_1", "Cabinet_Bathroom", 5.45f, 6.45f, 0f);
        if (ns != null) Move("Page_Bathroom", TopOf(ns));

        // ---- Storage ----
        var cage = Prop("Cube_011", "RustyCage_Storage", 5.15f, -2.1f, 0f);
        if (cage != null) Move("Key_StorageCrate", TopOf(cage));
        ns = Prop("NightStand_1", "Crate_Storage", 5.45f, 1.45f, 0f);
        if (ns != null) Move("Page_StorageCloset", TopOf(ns));

        // ---- Stairs ----
        var table2 = Prop("Table_1", "Table_Stairs", 4.9f, -6.0f, 0f);
        if (table2 != null) Move("Page_StairsShelf", TopOf(table2));
    }

    // A furniture piece at pack scale, standing on the floor at (x, z)
    static GameObject Prop(string model, string name, float x, float z, float yaw)
    {
        var go = Spawn(model, props, name);
        if (go == null) return null;
        go.transform.localScale = Vector3.one * packScale;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        PlaceBounds(go, new Vector3(x, 0f, z), true);
        AddBoxCollider(go);
        placed++;
        return go;
    }

    // A small decorative piece scaled so its longest side is `length` metres, bottom at height y, no collider
    static GameObject PropFit(string model, string name, float x, float y, float z, float yaw, float length)
    {
        var go = Spawn(model, props, name);
        if (go == null) return null;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var b = GetBounds(go);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) go.transform.localScale = Vector3.one * (length / longest);
        PlaceBounds(go, new Vector3(x, y, z), true);
        placed++;
        return go;
    }

    static Vector3 TopOf(GameObject go)
    {
        var b = GetBounds(go);
        return new Vector3(b.center.x, b.max.y + 0.06f, b.center.z);
    }

    // Moves an existing page/key spot (made by the earlier builders) onto the new furniture
    static void Move(string spotName, Vector3 pos)
    {
        var t = FindDeep(spotName);
        if (t == null) return;
        Undo.RecordObject(t, "Move spot");
        t.position = pos;
        moved++;
    }

    static void AddBoxCollider(GameObject go)
    {
        var mf = go.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        var bc = Undo.AddComponent<BoxCollider>(mf.gameObject);
        bc.center = mf.sharedMesh.bounds.center;
        bc.size = mf.sharedMesh.bounds.size;
    }

    // ======================================================================
    //  Enemy
    // ======================================================================

    static void StyleEnemy()
    {
        var e = FindDeep("Enemy");
        if (e == null) { Debug.LogWarning("No 'Enemy' in the scene (run Create Enemy & Waypoints first) - skipped."); return; }

        var r = e.GetComponent<Renderer>();
        if (r != null && Mat("enemy") != null) r.sharedMaterial = Mat("enemy");

        foreach (var n in new[] { "Eye_L", "Eye_R", "Knife_Held" })
        {
            var c = e.Find(n);
            if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
        }

        foreach (float sx in new[] { -0.22f, 0.22f })
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = sx < 0f ? "Eye_L" : "Eye_R";
            Object.DestroyImmediate(eye.GetComponent<Collider>());
            eye.transform.SetParent(e, false);
            eye.transform.localPosition = new Vector3(sx, 0.55f, 0.44f);
            eye.transform.localScale = new Vector3(0.16f, 0.10f, 0.10f);
            if (Mat("eye") != null) eye.GetComponent<Renderer>().sharedMaterial = Mat("eye");
            Undo.RegisterCreatedObjectUndo(eye, "Asset pack");
        }

        var knife = Spawn("Knife", e, "Knife_Held");
        if (knife != null)
        {
            knife.transform.localRotation = Quaternion.Euler(0f, KnifeHeldYaw, 0f);
            var b = GetBounds(knife);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) knife.transform.localScale *= KnifeHeldLength / longest;
            PlaceBounds(knife, e.TransformPoint(new Vector3(0.55f, -0.1f, 0.45f)), false);
        }
    }

    // ======================================================================
    //  NavMesh
    // ======================================================================

    static bool Rebake()
    {
        try
        {
            var t = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (t == null) return false;
            var s = Object.FindFirstObjectByType(t);
            if (s == null) return false;
            var m = t.GetMethod("BuildNavMesh", Type.EmptyTypes);
            if (m == null) return false;
            m.Invoke(s, null);
            return true;
        }
        catch (Exception e) { Debug.LogWarning("NavMesh bake failed: " + e.Message); return false; }
    }

    // Furniture now fills some floor space: move waypoints and the enemy's spawn onto walkable NavMesh
    static void SnapToNavMesh()
    {
        try
        {
            var wps = FindDeep("Waypoints");
            if (wps != null)
            {
                foreach (Transform w in wps)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(w.position, out hit, 2f, NavMesh.AllAreas) &&
                        (hit.position - w.position).sqrMagnitude > 0.01f)
                    {
                        Undo.RecordObject(w, "Snap waypoint");
                        w.position = new Vector3(hit.position.x, 0.1f, hit.position.z);
                    }
                }
            }

            var e = FindDeep("Enemy");
            if (e != null)
            {
                var ag = e.GetComponent<NavMeshAgent>();
                NavMeshHit h;
                // free floor in Bedroom 1, away from the bed
                if (NavMesh.SamplePosition(new Vector3(-3.5f, 0f, 3.5f), out h, 3f, NavMesh.AllAreas))
                {
                    Undo.RecordObject(e, "Move enemy spawn");
                    e.position = h.position + Vector3.up * (ag != null ? ag.baseOffset : 1f);
                }
            }
        }
        catch (Exception ex) { Debug.LogWarning("Could not snap to NavMesh: " + ex.Message); }
    }

    // GameManager.enemy must point at the Enemy (an empty slot caused the CollectPage error)
    static void WireEnemy()
    {
        try
        {
            var e = FindDeep("Enemy");
            var gm = FindDeep("GameManager");
            if (e == null || gm == null) return;
            var ai = e.GetComponent("EnemyAI");
            var comp = gm.GetComponent("GameManager");
            if (ai == null || comp == null) return;
            var so = new SerializedObject(comp);
            var p = so.FindProperty("enemy");
            if (p == null) return;
            p.objectReferenceValue = ai;
            so.ApplyModifiedProperties();
        }
        catch (Exception ex) { Debug.LogWarning("Could not wire GameManager.enemy: " + ex.Message); }
    }

    // ======================================================================
    //  Helpers
    // ======================================================================

    static GameObject Spawn(string model, Transform parent, string name)
    {
        var prefab = FindModel(model);
        if (prefab == null) { if (!missing.Contains(model + ".fbx")) missing.Add(model + ".fbx"); return null; }
        var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (go == null) return null;
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "Asset pack");
        ApplyMats(go);
        return go;
    }

    static GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 size, string matKey)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var m = Tiled(matKey, Mathf.Max(size.x, size.z) / WallTile, size.y / WallTile);
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        if (obstacleLayer >= 0) go.layer = obstacleLayer;
        go.isStatic = true;
        Undo.RegisterCreatedObjectUndo(go, "Asset pack");
        return go;
    }

    static void SetLayerAndStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (obstacleLayer >= 0) t.gameObject.layer = obstacleLayer;
            t.gameObject.isStatic = true;
        }
    }

    static float MeasurePackScale()
    {
        var prefab = FindModel("Wall_1");
        if (prefab == null) return 0.5f;
        var tmp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (tmp == null) return 0.5f;
        var b = GetBounds(tmp);
        Object.DestroyImmediate(tmp);
        float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        return biggest > 0.01f ? WallH / biggest : 0.5f;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // Moves go so its bounds centre sits at target (x, z) and either the bottom (bottom=true) or the centre (y) is at target.y
    static void PlaceBounds(GameObject go, Vector3 target, bool bottom)
    {
        var b = GetBounds(go);
        float dy = bottom ? target.y - b.min.y : target.y - b.center.y;
        go.transform.position += new Vector3(target.x - b.center.x, dy, target.z - b.center.z);
    }

    // Stretch go so its world bounds become `size`
    static void FitSize(GameObject go, Vector3 size)
    {
        var b = GetBounds(go).size;
        var s = go.transform.localScale;
        go.transform.localScale = new Vector3(
            b.x > 0.0001f ? s.x * size.x / b.x : s.x,
            b.y > 0.0001f ? s.y * size.y / b.y : s.y,
            b.z > 0.0001f ? s.z * size.z / b.z : s.z);
    }

    // The door panel's hole is not centred; find the middle of the two inner bottom vertices (world x)
    static bool OpeningCenterX(GameObject panel, out float cx)
    {
        cx = 0f;
        var mf = panel.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        var vs = mf.sharedMesh.vertices;
        var tr = mf.transform;
        var ws = new Vector3[vs.Length];
        float minY = float.MaxValue;
        for (int i = 0; i < vs.Length; i++)
        {
            ws[i] = tr.TransformPoint(vs[i]);
            if (ws[i].y < minY) minY = ws[i].y;
        }
        var xs = new List<float>();
        foreach (var w in ws)
        {
            if (Mathf.Abs(w.y - minY) > 0.01f) continue;
            bool dup = false;
            foreach (var x in xs) if (Mathf.Abs(x - w.x) < 0.01f) { dup = true; break; }
            if (!dup) xs.Add(w.x);
        }
        xs.Sort();
        if (xs.Count < 4) return false;
        cx = (xs[1] + xs[2]) / 2f;
        return true;
    }

    static GameObject FindModel(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:GameObject"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    static Texture2D FindTexture(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.StartsWith(OutDir)) continue;
            if (Path.GetFileNameWithoutExtension(p).Equals(name, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    static Transform[] AllTransforms()
    {
        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    static Transform FindDeep(string name)
    {
        foreach (var t in AllTransforms())
            if (t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
