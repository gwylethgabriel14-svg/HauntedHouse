#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Haunted House > Enemies > PLACE enemies in my house
// Put this file in Assets/Editor/ (next to HauntedEnemiesMenu.cs).
//
// Places the enemies at fixed spots of the BIG House (30 x 20 m ground floor + 2nd floor + attic):
//   - NURSERY DOLLS : ground floor Nursery (x 12..21, z 12..20), 5 dolls on the floor
//   - BLACK SLIME   : ground floor Bathroom (CR)  (x 21..30, z 12..20)
//   - BLACK SLIME   : Attic, the closed west pocket (the locker side)
//
// It first deletes every BlackSlime / DollNursery already in the scene, so running it again just moves them.
// Run "Build BIG House" and "Build 2ND FLOOR + ATTIC" first (the attic slime needs the attic to exist).
public static class PlaceEnemiesInHouse
{
    [MenuItem("Haunted House/Enemies/PLACE enemies in my house")]
    public static void Place()
    {
        Physics.SyncTransforms();
        int removed = Clear();

        var root = new GameObject("BigEnemies");
        Undo.RegisterCreatedObjectUndo(root, "Place enemies");

        PlaceDolls(root.transform);
        PlaceBathroomSlime(root.transform);
        bool attic = PlaceAtticSlime(root.transform);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        Debug.Log("Enemies placed: 5 dolls in the Nursery, slime in the Bathroom"
                  + (attic ? ", slime in the Attic" : " (attic slime skipped: attic not found)")
                  + ". " + removed + " old enemy objects removed.");
    }

    // ------------------------------------------------------------------ clear old ones

    static int Clear()
    {
        int n = 0;
        foreach (var s in Object.FindObjectsByType<BlackSlime>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(s.gameObject);
            n++;
        }
        foreach (var d in Object.FindObjectsByType<DollNursery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.DestroyObjectImmediate(d.gameObject);
            n++;
        }
        // an empty BigEnemies left from the last run
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "BigEnemies") { Undo.DestroyObjectImmediate(go); n++; }
        return n;
    }

    // ------------------------------------------------------------------ dolls

    static void PlaceDolls(Transform parent)
    {
        // Nursery interior: x 12.15..20.85, z 12.15..19.85 (walls at x=12, x=21, z=12, z=20), floor y = 0
        var zone = new GameObject("NurseryDolls");
        zone.transform.SetParent(parent, false);
        zone.transform.position = new Vector3(16.5f, 0f, 16f);

        var nursery = zone.AddComponent<DollNursery>();
        nursery.size = new Vector3(8.7f, 3f, 7.7f);   // 3 m tall on purpose: the 2nd floor above must not trigger it
        nursery.porcelainMat = HauntedEnemiesMenu.Mat("DollPorcelain", new Color(0.86f, 0.80f, 0.72f), 0.55f, 0f, null);
        nursery.dressMat = HauntedEnemiesMenu.Mat("DollDress", new Color(0.38f, 0.07f, 0.09f), 0.1f, 0f, null);
        nursery.hairMat = HauntedEnemiesMenu.Mat("DollHair", new Color(0.05f, 0.03f, 0.03f), 0.35f, 0f, null);
        nursery.eyeMat = HauntedEnemiesMenu.Mat("DollEye", new Color(0.01f, 0.01f, 0.01f), 0.95f, 0f, null);
        nursery.mouthMat = HauntedEnemiesMenu.Mat("DollMouth", new Color(0.30f, 0.02f, 0.03f), 0.2f, 0f, null);

        // on the floor, away from the doors (x=12,z=16 / x=21,z=16 / z=12,x=16.5), the crib and the dresser
        Vector2[] spots =
        {
            new Vector2(13.7f, 13.4f),   // near the hallway door, left
            new Vector2(19.2f, 13.6f),   // near the hallway door, right
            new Vector2(13.9f, 16.6f),   // next to the bedroom door
            new Vector2(19.3f, 18.3f),   // behind the crib, east
            new Vector2(15.0f, 19.2f),   // north wall, next to the crib
        };
        float[] yaw = { 20f, 140f, 250f, 75f, 200f };

        for (int i = 0; i < spots.Length; i++)
        {
            var d = new GameObject("Doll_" + (i + 1));
            d.transform.SetParent(zone.transform, false);
            d.transform.position = new Vector3(spots[i].x, 0f, spots[i].y);
            d.transform.rotation = Quaternion.Euler(0f, yaw[i], 0f);
            d.AddComponent<HorrorDoll>();
        }
    }

    // ------------------------------------------------------------------ slimes

    static void PlaceBathroomSlime(Transform parent)
    {
        // Bathroom interior: x 21.15..29.85, z 12.15..19.85. Bathtub is on the far east (x>27.8), sink on the north wall.
        Vector3 p = Floor(new Vector3(25f, 1.5f, 16f), 0f);
        MakeSlime("BlackSlime_CR", p, 1.3f, parent);
    }

    static bool PlaceAtticSlime(Transform parent)
    {
        // the attic roof is built as "AtticRoof": shoot a ray down from just under it to find the attic floor
        var roof = FindByName("AtticRoof");
        if (roof == null)
        {
            Debug.LogWarning("PlaceEnemiesInHouse: 'AtticRoof' not found. Run Build 2ND FLOOR + ATTIC first. Attic slime skipped.");
            return false;
        }

        // west pocket of the attic (closed by the x=10 partition), between the locker and the sheet-covered furniture
        Vector3 from = new Vector3(5f, roof.position.y - 0.4f, 6.5f);
        RaycastHit hit;
        if (!Physics.Raycast(from, Vector3.down, out hit, 20f))
        {
            Debug.LogWarning("PlaceEnemiesInHouse: could not find the attic floor under (5, 6.5). Attic slime skipped.");
            return false;
        }

        MakeSlime("BlackSlime_Attic", hit.point, 1.8f, parent);
        return true;
    }

    static void MakeSlime(string name, Vector3 pos, float wander, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var s = go.AddComponent<BlackSlime>();
        s.bodyMat = HauntedEnemiesMenu.Mat("SlimeBlack", new Color(0.015f, 0.015f, 0.02f), 0.95f, 0.1f, null);
        s.eyeMat = HauntedEnemiesMenu.Mat("SlimeEye", new Color(0.9f, 0.9f, 0.6f), 0.2f, 0f, new Color(1.6f, 1.5f, 0.8f));
        s.wanderRadius = wander;
    }

    // ------------------------------------------------------------------ helpers

    static Vector3 Floor(Vector3 from, float fallbackY)
    {
        RaycastHit hit;
        if (Physics.Raycast(from, Vector3.down, out hit, 10f)) return hit.point;
        return new Vector3(from.x, fallbackY, from.z);
    }

    static Transform FindByName(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t;
        return null;
    }
}
#endif
