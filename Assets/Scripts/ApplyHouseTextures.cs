#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts your ASSET PACK materials (M_wall, M_floor, M_ceil, M_wood, M_rust) on the BIG House
// (ground floor, basement, 2nd floor, attic, lockers, barricades).
//
// Decides what each part is from the OBJECT NAME made by the Build scripts
// (Outer_S, Hall_N, F2_Outer_W, F3_Part1, B_Floor, Ramp_WallL, AtticRoof ...), so it does not
// matter what material the part has now. Walls you painted by hand with WallMat are replaced too.
//
// NO STRETCHING: instead of material tiling (which stretches on cubes of different sizes),
// the script rewrites each part's UVs in WORLD METERS (box projection). Every face gets the same
// texture scale, and the pattern continues across neighbouring walls, above doors and around corners.
//
// Menu:  Haunted House/Apply Asset Pack to BIG House
//        Haunted House/Reset BIG House Textures   (back to the plain gray materials)
//
// Order:  Build BIG House  >  Build 2ND FLOOR + ATTIC  >  Bake NavMesh  >  Apply Asset Pack
// (The Build scripts recreate the plain materials and meshes, so run Apply again after rebuilding.)
//
// Put this file in Assets/Editor/.
public static class ApplyHouseTextures
{
    // ---- which pack material goes where (file names in Assets/HauntedAssets/Materials) ----
    // Each entry lists names to try, in order. Change them here if you want a different look.
    static readonly string[] WALL_MAT     = { "M_wall" };                       // ground floor + 2nd floor walls
    static readonly string[] FLOOR_MAT    = { "M_floor" };                      // floors and basement floor
    static readonly string[] CEIL_MAT     = { "M_ceil", "M_ceiling", "M_cell" };
    static readonly string[] BASEMENT_MAT = { "M_rust" };                       // basement + ramp walls
    static readonly string[] ATTIC_MAT    = { "M_wood" };                       // attic walls
    static readonly string[] WOOD_MAT     = { "M_wood" };                       // furniture, stairs, crates, barricades
    static readonly bool APPLY_WOOD = true;                                     // false = leave furniture as it is

    // ---- how big one texture tile is in the world (meters, width of the tile) ----
    // Smaller number = the pattern repeats more often (looks smaller).
    const float WALL_TILE = 2.0f;
    const float FLOOR_TILE = 2.0f;
    const float CEIL_TILE = 2.0f;
    const float BASEMENT_TILE = 2.5f;
    const float ATTIC_TILE = 2.0f;
    const float WOOD_TILE = 1.5f;

    const string MAT_DIR = "Assets/Materials";
    const string GEN_DIR = "Assets/Materials/Generated";

    static readonly string[] ROOTS = { "BigHouse", "BigLockers", "BigUpLockers", "BigBarricades" };

    [MenuItem("Haunted House/Apply Asset Pack to BIG House")]
    public static void Apply()
    {
        var wall = FindMat(WALL_MAT);
        var floor = FindMat(FLOOR_MAT);
        var ceil = FindMat(CEIL_MAT);
        var basement = FindMat(BASEMENT_MAT);
        var attic = FindMat(ATTIC_MAT);
        var wood = FindMat(WOOD_MAT);

        var missing = new List<string>();
        if (wall == null) missing.Add(string.Join(" / ", WALL_MAT));
        if (floor == null) missing.Add(string.Join(" / ", FLOOR_MAT));
        if (ceil == null) missing.Add(string.Join(" / ", CEIL_MAT));
        if (basement == null) missing.Add(string.Join(" / ", BASEMENT_MAT));
        if (attic == null) missing.Add(string.Join(" / ", ATTIC_MAT));
        if (APPLY_WOOD && wood == null) missing.Add(string.Join(" / ", WOOD_MAT));
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Asset pack",
                "These materials were not found in Assets/HauntedAssets/Materials:\n\n" + string.Join("\n", missing) +
                "\n\nCheck the exact names in that folder and edit the lists at the top of ApplyHouseTextures.cs.",
                "OK");
            return;
        }

        if (FindRoot("BigHouse") == null)
        {
            EditorUtility.DisplayDialog("Asset pack",
                "BigHouse not found in the open scene. Open the House scene and run Build BIG House first.", "OK");
            return;
        }

        EnsureFolder(MAT_DIR);
        EnsureFolder(GEN_DIR);

        var cache = new Dictionary<string, Material>();
        var skipped = new Dictionary<string, int>();
        int count = 0, total = 0;

        foreach (var rootName in ROOTS)
        {
            var root = FindRoot(rootName);
            if (root == null) continue;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                total++;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length != 1 || mats[0] == null) continue;

                string matBase = BaseName(mats[0].name);
                string n = r.name;
                string cls = Classify(n, matBase);

                if (cls == "")
                {
                    string key = n + "  (" + mats[0].name + ")";
                    int c;
                    skipped.TryGetValue(key, out c);
                    skipped[key] = c + 1;
                    continue;
                }

                Material pack;
                float tileW;

                switch (cls)
                {
                    case "wall":     pack = wall;     tileW = WALL_TILE;     break;
                    case "floor":    pack = floor;    tileW = FLOOR_TILE;    break;
                    case "ceil":     pack = ceil;     tileW = CEIL_TILE;     break;
                    case "basement": pack = basement; tileW = BASEMENT_TILE; break;
                    case "attic":    pack = attic;    tileW = ATTIC_TILE;    break;
                    case "wood":
                        if (!APPLY_WOOD) continue;
                        pack = wood; tileW = WOOD_TILE;
                        break;
                    default: continue;
                }

                // keep the texture's proportions (a tile is tileW wide and tileW * aspect tall)
                float aspect = 1f;
                if (pack.mainTexture != null && pack.mainTexture.width > 0)
                    aspect = (float)pack.mainTexture.height / pack.mainTexture.width;
                float tileH = tileW * aspect;

                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) ProjectUV(mf, tileW, tileH);

                var variant = GetVariant(cache, cls, pack);
                if (variant == null) continue;

                Undo.RecordObject(r, "Apply asset pack");
                r.sharedMaterials = new Material[] { variant };
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("Asset pack applied to " + count + " of " + total + " parts in the BIG House. Ctrl+S to save the scene.");

        if (skipped.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Left alone (furniture, props, fabric...). If one of these is a wall, floor or ceiling, tell me its name:");
            foreach (var kv in skipped) sb.AppendLine(" - " + kv.Key + (kv.Value > 1 ? "  x" + kv.Value : ""));
            Debug.Log(sb.ToString());
        }
    }

    [MenuItem("Haunted House/Reset BIG House Textures")]
    public static void Reset()
    {
        int count = 0;

        foreach (var rootName in ROOTS)
        {
            var root = FindRoot(rootName);
            if (root == null) continue;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length != 1 || mats[0] == null) continue;

                string n = r.name;
                string cls = Classify(n, BaseName(mats[0].name));
                if (cls == "") continue;

                string plain = PlainFor(cls, n);
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_DIR + "/" + plain + ".mat");
                if (baseMat == null) continue;

                Undo.RecordObject(r, "Reset house textures");
                r.sharedMaterials = new Material[] { baseMat };
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Plain materials restored on " + count + " parts.");
    }

    // ------------------------------------------------------------------ UVs

    // Box projection in WORLD meters. Each vertex is projected along the axis its face points to:
    //   faces looking along X -> (z, y)     faces looking along Y -> (x, z)     faces looking along Z -> (x, y)
    // so every face has the same texture scale and the pattern continues from wall to wall.
    static void ProjectUV(MeshFilter mf, float tileW, float tileH)
    {
        var src = mf.sharedMesh;
        if (src == null || !src.isReadable) return;

        // each part gets its own mesh copy (the built-in cube is shared by every cube)
        Mesh mesh = src.name.StartsWith("UV_") ? src : Object.Instantiate(src);
        mesh.name = "UV_" + mf.gameObject.name;

        var verts = mesh.vertices;
        var normals = mesh.normals;
        if (normals == null || normals.Length != verts.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        var t = mf.transform;
        var uv = new Vector2[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 wp = t.TransformPoint(verts[i]);
            Vector3 wn = t.TransformDirection(normals[i]);

            float ax = Mathf.Abs(wn.x), ay = Mathf.Abs(wn.y), az = Mathf.Abs(wn.z);
            float u, v;

            if (ax >= ay && ax >= az) { u = wp.z; v = wp.y; }
            else if (ay >= az)        { u = wp.x; v = wp.z; }
            else                      { u = wp.x; v = wp.y; }

            uv[i] = new Vector2(u / tileW, v / tileH);
        }

        mesh.uv = uv;

        Undo.RecordObject(mf, "Apply asset pack UVs");
        mf.sharedMesh = mesh;
    }

    // ------------------------------------------------------------------ classification

    // Decides what a part is, mostly from its name (names come from the Build scripts).
    // Returns "wall", "floor", "ceil", "basement", "attic", "wood" or "" (leave alone).
    static string Classify(string n, string matBase)
    {
        if (n == "Ceiling" || n.StartsWith("B_Ceiling") || n.StartsWith("Ramp_Ceiling") || n.StartsWith("AtticRoof"))
            return "ceil";

        // "Ceiling_A..D" are the 2nd floor slab pieces (a floor on top), "AtticFloor_*" the attic floor
        if (n == "Floor" || n.StartsWith("Ceiling_") || n.StartsWith("AtticFloor") || n.StartsWith("B_Floor") || n == "Ramp")
            return "floor";

        if (n.StartsWith("F3_")) return "attic";

        if (n.StartsWith("B_") || n.StartsWith("Ramp_Wall")) return "basement";

        if (n.StartsWith("Outer_") || n.StartsWith("Hall_") || n.StartsWith("LivingKitchen") ||
            n.StartsWith("BedNursery") || n.StartsWith("NurseryBath") || n.StartsWith("F2_"))
            return "wall";

        // fall back to the material (furniture, lockers, barricades, anything else)
        switch (matBase)
        {
            case "GB_Wall":
            case "WallMat":
            case "WallPlainMat":
            case "wall":
                return "wall";

            case "GB_Floor":
            case "FloorMat":
            case "floor":
                return "floor";

            case "Big_Ceiling":
            case "CeilingMat":
            case "ceil":
                return "ceil";

            case "Big_Basement":
                return n.Contains("Floor") ? "floor" : "basement";
            case "basement":
                return "basement";

            case "Big_Attic":
            case "attic":
                return "attic";

            case "Big_Wood":
            case "wood":
                return "wood";
        }

        return "";
    }

    static string PlainFor(string cls, string n)
    {
        switch (cls)
        {
            case "wall": return "GB_Wall";
            case "floor": return n.StartsWith("B_Floor") ? "Big_Basement" : "GB_Floor";
            case "ceil": return "Big_Ceiling";
            case "basement": return "Big_Basement";
            case "attic": return "Big_Attic";
            case "wood": return "Big_Wood";
        }

        return "GB_Wall";
    }

    // ------------------------------------------------------------------ helpers

    // A copy of the pack material (tiling 1, because the UVs already carry the scale).
    // Named  <class>__<pack material>
    static Material GetVariant(Dictionary<string, Material> cache, string cls, Material pack)
    {
        string name = cls + "__" + pack.name;

        Material m;
        if (cache.TryGetValue(name, out m)) return m;

        string path = GEN_DIR + "/" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(pack);
            AssetDatabase.CreateAsset(m, path);
        }

        m.mainTextureScale = Vector2.one;
        m.mainTextureOffset = Vector2.zero;

        EditorUtility.SetDirty(m);
        cache[name] = m;
        return m;
    }

    static string BaseName(string matName)
    {
        int i = matName.IndexOf("__");
        return i >= 0 ? matName.Substring(0, i) : matName;
    }

    // Finds a material from the asset pack by exact file name (ignores the Tiled and Generated folders).
    static Material FindMat(string[] names)
    {
        foreach (var name in names)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name) continue;
                if (path.Contains("/Tiled/") || path.Contains("/Generated/")) continue;
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
        }

        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
#endif