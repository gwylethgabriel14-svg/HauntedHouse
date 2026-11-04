#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Haunted House > Enemies > ...
// Put this file inside a folder named "Editor" (Assets/Editor/HauntedEnemiesMenu.cs).
//
// HOW TO USE
//   Add BLACK SLIME:   click the room (CR, attic, ...) in the Hierarchy, then use the menu.
//                      The slime is placed on the floor in the middle of that room.
//                      Nothing selected = it is placed where the Scene view camera is looking.
//   Add NURSERY DOLLS: click the nursery room in the Hierarchy, then use the menu.
//                      The trigger box is fitted to that room and the dolls are scattered on the floor inside.
//                      You can move each doll afterwards (Doll_1, Doll_2...) and resize the box on DollNursery > Size.
public static class HauntedEnemiesMenu
{
    const string Root = "Assets/HauntedHouse";
    const string MatDir = "Assets/HauntedHouse/Materials";

    // ------------------------------------------------------------------ menu items

    [MenuItem("Haunted House/Enemies/Add BLACK SLIME")]
    static void AddSlime()
    {
        Vector3 pos;
        Bounds b;
        if (RoomBounds(out b)) pos = FloorAt(new Vector3(b.center.x, b.center.y, b.center.z), b.min.y);
        else pos = FloorAt(ViewPivot() + Vector3.up * 1.5f, ViewPivot().y);

        var go = new GameObject("BlackSlime");
        go.transform.position = pos;
        var slime = go.AddComponent<BlackSlime>();
        slime.bodyMat = Mat("SlimeBlack", new Color(0.015f, 0.015f, 0.02f), 0.95f, 0.1f, null);
        slime.eyeMat = Mat("SlimeEye", new Color(0.9f, 0.9f, 0.6f), 0.2f, 0f, new Color(1.6f, 1.5f, 0.8f));

        // a room that is smaller than the default wander area should keep the slime inside it
        if (RoomBounds(out b))
        {
            float room = Mathf.Min(b.size.x, b.size.z) * 0.5f;
            slime.wanderRadius = Mathf.Clamp(room - slime.radius - 0.3f, 0.5f, 3f);
        }

        Finish(go, "Add Black Slime");
    }

    [MenuItem("Haunted House/Enemies/Add NURSERY DOLLS")]
    static void AddDolls()
    {
        Vector3 pos;
        Vector3 size = new Vector3(6f, 3f, 6f);
        Bounds b;
        if (RoomBounds(out b))
        {
            pos = FloorAt(b.center, b.min.y);
            size = new Vector3(b.size.x, Mathf.Clamp(b.size.y, 2f, 3.5f), b.size.z);
        }
        else
        {
            pos = FloorAt(ViewPivot() + Vector3.up * 1.5f, ViewPivot().y);
        }

        var zone = new GameObject("NurseryDolls");
        zone.transform.position = pos;
        var nursery = zone.AddComponent<DollNursery>();
        nursery.size = size;
        nursery.porcelainMat = Mat("DollPorcelain", new Color(0.86f, 0.80f, 0.72f), 0.55f, 0f, null);
        nursery.dressMat = Mat("DollDress", new Color(0.38f, 0.07f, 0.09f), 0.1f, 0f, null);
        nursery.hairMat = Mat("DollHair", new Color(0.05f, 0.03f, 0.03f), 0.35f, 0f, null);
        nursery.eyeMat = Mat("DollEye", new Color(0.01f, 0.01f, 0.01f), 0.95f, 0f, null);
        nursery.mouthMat = Mat("DollMouth", new Color(0.30f, 0.02f, 0.03f), 0.2f, 0f, null);

        // scatter the dolls on the floor inside the box
        int count = 5;
        float hx = Mathf.Max(0.3f, size.x * 0.5f - 0.8f);
        float hz = Mathf.Max(0.3f, size.z * 0.5f - 0.8f);
        var placed = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            Vector3 p = pos;
            for (int attempt = 0; attempt < 25; attempt++)
            {
                p = pos + new Vector3(Random.Range(-hx, hx), 0f, Random.Range(-hz, hz));
                bool ok = true;
                foreach (var q in placed) if ((q - p).magnitude < 0.7f) { ok = false; break; }
                if (ok) break;
            }
            placed.Add(p);

            Vector3 floor = FloorAt(p + Vector3.up * 1.5f, pos.y);
            var d = new GameObject("Doll_" + (i + 1));
            d.transform.SetParent(zone.transform, false);
            d.transform.position = floor;
            d.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            d.AddComponent<HorrorDoll>();
        }

        Finish(zone, "Add Nursery Dolls");
    }

    // ------------------------------------------------------------------ helpers

    static void Finish(GameObject go, string undoName)
    {
        Undo.RegisterCreatedObjectUndo(go, undoName);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }

    // bounds of the selected scene object (the room), if there is one
    static bool RoomBounds(out Bounds b)
    {
        b = new Bounds();
        var sel = Selection.activeGameObject;
        if (sel == null || !sel.scene.IsValid()) return false;

        var rs = sel.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    static Vector3 ViewPivot()
    {
        return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
    }

    static Vector3 FloorAt(Vector3 from, float fallbackY)
    {
        RaycastHit hit;
        if (Physics.Raycast(from, Vector3.down, out hit, 50f)) return hit.point;
        return new Vector3(from.x, fallbackY, from.z);
    }

    // material assets (created once, so they also work in a built game; you can edit them later)
    public static Material Mat(string name, Color c, float smooth, float metal, Color? emission)
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "HauntedHouse");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Root, "Materials");

        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(HorrorMeshes.PipelineShader());
            HorrorMeshes.Configure(m, c, smooth, metal, emission);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssets();
        }
        return m;
    }
}
#endif