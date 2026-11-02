#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Swaps the plain cube furniture in the BIG House for the real models from your asset pack
// (Assets/HauntedAssets/Models). The model is fitted to the size and position of the cube.
// The cube stays in the scene with its collider but its mesh is hidden, so gameplay
// (collisions, hiding spots, page spots, the exit door) keeps working exactly the same.
//
// Menu:  Haunted House/Replace Cubes With Models
//        Haunted House/Restore Cubes (remove models)
//
// Order:  Build BIG House  >  Build 2ND FLOOR + ATTIC  >  Apply Asset Pack  >  Replace Cubes With Models
// (The Build scripts recreate the cubes, so run this again after rebuilding.)
//
// Put this file in Assets/Editor/.
public static class PlaceHouseModels
{
    enum Fit
    {
        Footprint,  // scale evenly so the model fits the cube's floor area (height is free)
        All,        // scale evenly so the model fits inside the whole cube
        Fill        // stretch to the cube's size (only for flat things like doors)
    }

    class Rule
    {
        public string[] objects;       // names of the cube objects made by the Build scripts
        public string[] modelNames;    // model file names to try (matches the start of the name, any case)
        public Fit fit;

        public Rule(string[] objects, string[] modelNames, Fit fit)
        {
            this.objects = objects;
            this.modelNames = modelNames;
            this.fit = fit;
        }
    }

    // ---- what gets replaced. Edit here to change the mapping. ----
    static readonly Rule[] RULES =
    {
        new Rule(new[] { "Bed", "GuestBed" },                                   new[] { "bed_1", "bed" },        Fit.Footprint),
        new Rule(new[] { "Nightstand" },                                        new[] { "Night" },               Fit.Footprint),
        new Rule(new[] { "NurseryDresser" },                                    new[] { "Dress" },               Fit.Footprint),
        new Rule(new[] { "KitchenTable", "CoffeeTable", "Desk", "LibraryTable" }, new[] { "Table_1", "Table" },  Fit.Footprint),
        new Rule(new[] { "Crate_1", "Crate_2", "Crate_3", "Crate_4", "AtticCrate1", "AtticCrate2" },
                                                                                new[] { "Cube_" },               Fit.All),
        new Rule(new[] { "ExitDoor" },                                          new[] { "Door_1" },              Fit.Fill),
    };

    const string MODELS_DIR = "Assets/HauntedAssets/Models";
    const string SUFFIX = "_Model";

    [MenuItem("Haunted House/Replace Cubes With Models")]
    public static void Apply()
    {
        var big = FindRoot("BigHouse");
        if (big == null)
        {
            EditorUtility.DisplayDialog("Models",
                "BigHouse not found in the open scene. Open the House scene and run Build BIG House first.", "OK");
            return;
        }

        RemoveModels(big);   // start clean so running twice never doubles anything

        var prefabs = new Dictionary<Rule, GameObject>();
        var notFound = new List<string>();
        foreach (var rule in RULES)
        {
            var p = FindModel(rule.modelNames);
            prefabs[rule] = p;
            if (p == null) notFound.Add(string.Join(" / ", rule.modelNames) + "  (for " + string.Join(", ", rule.objects) + ")");
        }

        int placed = 0;

        foreach (var mr in big.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.EndsWith(SUFFIX)) continue;

            var rule = RuleFor(mr.name);
            if (rule == null) continue;

            var prefab = prefabs[rule];
            if (prefab == null) continue;

            if (Place(mr, prefab, rule.fit)) placed++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Models placed on " + placed + " pieces. Re-bake the NavMesh (BigHouse > Nav Mesh Surface > Bake), then Ctrl+S.");

        if (notFound.Count > 0)
        {
            Debug.LogWarning("No model found in " + MODELS_DIR + " for:\n - " + string.Join("\n - ", notFound) +
                             "\nCheck the file names in the Models folder and edit the RULES list at the top of PlaceHouseModels.cs.");
        }
    }

    [MenuItem("Haunted House/Restore Cubes (remove models)")]
    public static void Restore()
    {
        var big = FindRoot("BigHouse");
        if (big == null) return;

        int n = RemoveModels(big);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Removed " + n + " models. Cubes are visible again.");
    }

    // ------------------------------------------------------------------ placing

    static bool Place(MeshRenderer cubeR, GameObject prefab, Fit fit)
    {
        Bounds tb = cubeR.bounds;   // the cube's box in world space (read before hiding it)

        var go = Object.Instantiate(prefab);
        go.name = cubeR.name + SUFFIX;
        go.transform.SetParent(cubeR.transform.parent, true);
        go.transform.rotation = Quaternion.identity;

        // the cube keeps the collider; the model must not add its own
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        Bounds mb;
        if (!GetBounds(go, out mb))
        {
            Object.DestroyImmediate(go);
            return false;
        }

        Vector3 baseScale = go.transform.localScale;
        Vector3 size = mb.size;

        // try the model as it is (yaw 0) and turned 90 degrees, keep whichever fits better
        Vector3 w0 = FitScale(fit, tb.size, size);
        Vector3 w90 = FitScale(fit, tb.size, new Vector3(size.z, size.y, size.x));

        bool turn = Quality(fit, w90) > Quality(fit, w0);
        Vector3 w = turn ? w90 : w0;

        // non-uniform scale is applied in local axes, so swap x/z when the model is turned
        Vector3 local = turn ? new Vector3(w.z, w.y, w.x) : w;

        go.transform.rotation = Quaternion.Euler(0f, turn ? 90f : 0f, 0f);
        go.transform.localScale = Vector3.Scale(baseScale, local);

        // sit it on the cube's floor, centered
        Bounds b;
        if (GetBounds(go, out b))
        {
            Vector3 d = new Vector3(tb.center.x - b.center.x, tb.min.y - b.min.y, tb.center.z - b.center.z);
            go.transform.position += d;
        }

        cubeR.enabled = false;
        Undo.RegisterCreatedObjectUndo(go, "Place house model");
        return true;
    }

    // scale per WORLD axis for a model of the given extents inside the target box
    static Vector3 FitScale(Fit fit, Vector3 target, Vector3 ext)
    {
        float rx = target.x / Mathf.Max(0.0001f, ext.x);
        float ry = target.y / Mathf.Max(0.0001f, ext.y);
        float rz = target.z / Mathf.Max(0.0001f, ext.z);

        switch (fit)
        {
            case Fit.Footprint:
                float f = Mathf.Min(rx, rz);
                return new Vector3(f, f, f);

            case Fit.All:
                float a = Mathf.Min(rx, Mathf.Min(ry, rz));
                return new Vector3(a, a, a);

            default:
                return new Vector3(rx, ry, rz);
        }
    }

    // higher = better orientation
    static float Quality(Fit fit, Vector3 w)
    {
        if (fit == Fit.Fill)
        {
            // least distortion between width and depth
            return -Mathf.Abs(Mathf.Log(Mathf.Max(0.0001f, w.x)) - Mathf.Log(Mathf.Max(0.0001f, w.z)));
        }

        return w.x;   // bigger even scale = fills the space better
    }

    static bool GetBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return false;

        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    // ------------------------------------------------------------------ helpers

    static int RemoveModels(GameObject big)
    {
        var models = new List<Transform>();
        foreach (var t in big.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(SUFFIX)) models.Add(t);

        foreach (var t in models)
        {
            if (t == null) continue;

            string baseName = t.name.Substring(0, t.name.Length - SUFFIX.Length);
            var sib = t.parent != null ? t.parent.Find(baseName) : null;
            if (sib != null)
            {
                var mr = sib.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = true;
            }

            Object.DestroyImmediate(t.gameObject);
        }

        return models.Count;
    }

    static Rule RuleFor(string objectName)
    {
        foreach (var rule in RULES)
            foreach (var n in rule.objects)
                if (n == objectName) return rule;

        return null;
    }

    // finds a model in the pack by the start of its file name
    static GameObject FindModel(string[] names)
    {
        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { MODELS_DIR });

        foreach (var name in names)
        {
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith(name, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        return null;
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
#endif
