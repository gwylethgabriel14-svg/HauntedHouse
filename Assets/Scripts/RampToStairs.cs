#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Replaces the sloped ramp to the basement (east end of the hallway) with real stairs.
// It works on the scene you already have: no need to rebuild the house.
//
// The old ramp had slanted side walls and a slanted ceiling. Slanted boxes leave open wedges
// (the black gaps) where they meet vertical walls. So this script rebuilds the whole stair
// tunnel from straight vertical blocks, one column per step:
//   - the step itself (solid block down to the basement floor)
//   - a side wall on each side
//   - a ceiling piece (the ceiling steps down along with the stairs)
// Everything touches, so nothing can be seen through.
//
// Menu:  Haunted House/Ramp To Stairs
//
// After running it:  select BigHouse > Nav Mesh Surface > Bake,  then run Apply Asset Pack again,  then Ctrl+S.
// (If you ever rebuild the house with Build BIG House, run this again. Running it twice is safe.)
//
// Put this file in Assets/Editor/.
public static class RampToStairs
{
    // Same footprint as the old ramp: starts at the hallway end wall and ends at the basement.
    const float TOP_X = 30.15f;       // where the stairs begin (east end of the hallway)
    const int STEPS = 14;
    const float RISE = 0.25f;         // height of one step   (14 x 0.25 = 3.5 m drop)
    const float RUN = 0.6f;           // depth of one step    (14 x 0.6  = 8.4 m long)
    const float CENTER_Z = 10f;
    const float HALF_WALK = 2.0f;     // half of the 4 m wide walkway
    const float WALL_T = 0.3f;        // thickness of the side walls and ceiling
    const float BOTTOM_Y = -3.65f;    // blocks go down to just under the basement floor

    const float CEIL_START = 3.2f;    // underside of the ceiling at the top (same as the hallway ceiling)
    const float CEIL_END = -0.3f;     // underside of the ceiling at the bottom (same as the basement ceiling)

    static readonly string[] OLD_PREFIXES = { "Ramp_Step_", "Ramp_WallL", "Ramp_WallR", "Ramp_Ceiling" };

    [MenuItem("Haunted House/Ramp To Stairs")]
    public static void Convert()
    {
        var big = FindRoot("BigHouse");
        if (big == null)
        {
            EditorUtility.DisplayDialog("Stairs",
                "BigHouse not found in the open scene. Open the House scene and run Build BIG House first.", "OK");
            return;
        }

        // remember the materials and layer of the old pieces, then remove them
        Material stepMat = null, wallMat = null, ceilMat = null;
        int wallLayer = -1;
        int removed = 0;

        for (int i = big.transform.childCount - 1; i >= 0; i--)
        {
            var c = big.transform.GetChild(i);
            string n = c.name;

            bool old = n == "Ramp";
            foreach (var p in OLD_PREFIXES) if (n.StartsWith(p)) old = true;
            if (!old) continue;

            var r = c.GetComponent<Renderer>();
            if (r != null)
            {
                if (n == "Ramp" || n.StartsWith("Ramp_Step_")) { if (stepMat == null) stepMat = r.sharedMaterial; }
                else if (n.StartsWith("Ramp_Wall")) { if (wallMat == null) wallMat = r.sharedMaterial; if (wallLayer < 0) wallLayer = c.gameObject.layer; }
                else if (n.StartsWith("Ramp_Ceiling")) { if (ceilMat == null) ceilMat = r.sharedMaterial; }
            }

            Object.DestroyImmediate(c.gameObject);
            removed++;
        }

        if (removed == 0)
        {
            EditorUtility.DisplayDialog("Stairs",
                "The ramp was not found under BigHouse (objects named Ramp, Ramp_WallL, Ramp_WallR, Ramp_Ceiling). Nothing was changed.", "OK");
            return;
        }

        if (stepMat == null) stepMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GB_Floor.mat");
        if (wallMat == null) wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Big_Basement.mat");
        if (ceilMat == null) ceilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Big_Ceiling.mat");

        if (wallLayer < 0)
        {
            wallLayer = LayerMask.NameToLayer("Obstacle");
            if (wallLayer < 0) wallLayer = 6;
        }

        float wallZ = HALF_WALK + WALL_T / 2f;               // center of each side wall (z offset from the middle)
        float stepWidth = 2f * (HALF_WALK + WALL_T);         // steps run under the side walls too
        float ceilWidth = 2f * (HALF_WALK + WALL_T);
        float ceilDrop = (CEIL_START - CEIL_END) / (STEPS - 1);

        for (int i = 0; i < STEPS; i++)
        {
            float x = TOP_X + (i + 0.5f) * RUN;

            // step: solid block from the basement floor up to the step top
            float stepTop = -(i + 1) * RISE;
            Block(big.transform, "Ramp_Step_" + i,
                  new Vector3(x, (stepTop + BOTTOM_Y) / 2f, CENTER_Z),
                  new Vector3(RUN, stepTop - BOTTOM_Y, stepWidth), stepMat, 0);

            // ceiling piece: its underside steps down along the stairs
            float ceilBottom = CEIL_START - i * ceilDrop;
            float ceilTop = ceilBottom + WALL_T;
            Block(big.transform, "Ramp_Ceiling_" + i,
                  new Vector3(x, (ceilBottom + ceilTop) / 2f, CENTER_Z),
                  new Vector3(RUN, WALL_T, ceilWidth), ceilMat, 0);

            // side walls: vertical, from below the step up to the top of the ceiling piece
            float wallHeight = ceilTop - BOTTOM_Y;
            float wallY = (ceilTop + BOTTOM_Y) / 2f;
            Block(big.transform, "Ramp_WallL_" + i,
                  new Vector3(x, wallY, CENTER_Z - wallZ),
                  new Vector3(RUN, wallHeight, WALL_T), wallMat, wallLayer);
            Block(big.transform, "Ramp_WallR_" + i,
                  new Vector3(x, wallY, CENTER_Z + wallZ),
                  new Vector3(RUN, wallHeight, WALL_T), wallMat, wallLayer);
        }

        // the enemy waypoint that sat on the middle of the ramp: put it on the matching step
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "WP_16" && t.gameObject.scene.IsValid())
            {
                float wx = t.position.x;
                int idx = Mathf.Clamp(Mathf.FloorToInt((wx - TOP_X) / RUN), 0, STEPS - 1);
                t.position = new Vector3(wx, -(idx + 1) * RISE + 0.05f, t.position.z);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = big;
        Debug.Log("Basement stairs rebuilt (" + STEPS + " steps, side walls and stepped ceiling, no gaps). " +
                  "NEXT: BigHouse > Nav Mesh Surface > Bake, run Apply Asset Pack again, then Ctrl+S.");
    }

    static void Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, true);
        g.transform.position = center;
        g.transform.localScale = size;
        g.layer = layer;

        if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;

        Undo.RegisterCreatedObjectUndo(g, "Create basement stairs");
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }
}
#endif