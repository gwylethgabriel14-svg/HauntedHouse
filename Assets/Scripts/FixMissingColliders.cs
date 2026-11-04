#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Put this file in Assets/Editor/.
//
// Menu: Haunted House > Colliders > SCAN missing colliders (report only)
//       Haunted House > Colliders > FIX missing colliders (add them)
//
// It looks at every active object in the open scene that has a mesh (MeshFilter + MeshRenderer) and no collider,
// and prints the list in the Console (click a line to jump to the object). SCAN also selects them in the Hierarchy.
// FIX adds a collider to each one: BoxCollider on cubes, Sphere/Capsule on those primitives, MeshCollider on any other mesh.
// Ctrl+Z undoes the fix.
//
// It does NOT touch (edit the lists below if you want something different):
//   - the Bed (you wake up inside it on purpose), rugs, pages, keys, batteries, bottles and other small pickups
//   - the stairs steps (FixStairs gives the stairs a smooth ramp collider instead)
//   - tiny objects (smaller than MinSize), the player, the enemy, the slimes and the dolls
//   - meshes that already sit inside a parent that has a solid collider
// Colliders that exist but are turned OFF, or are Triggers on big objects, are only reported (they may be on purpose).
public static class FixMissingColliders
{
    const float MinSize = 0.4f;   // the biggest side of the object must be at least this many meters

    static readonly string[] SkipExact = { "Bed", "Rug", "PlayRug" };
    static readonly string[] SkipContains = { "Page", "Key", "Battery", "Bottle", "Pickup", "Decal", "Blood", "Paper", "Note" };

    class Result
    {
        public List<GameObject> missing = new List<GameObject>();
        public List<GameObject> disabled = new List<GameObject>();
        public List<GameObject> triggers = new List<GameObject>();
    }

    [MenuItem("Haunted House/Colliders/SCAN missing colliders (report only)")]
    public static void Scan()
    {
        var r = Find();
        Report(r);

        var all = new List<Object>();
        foreach (var g in r.missing) all.Add(g);
        foreach (var g in r.disabled) all.Add(g);
        foreach (var g in r.triggers) all.Add(g);
        Selection.objects = all.ToArray();
    }

    [MenuItem("Haunted House/Colliders/FIX missing colliders (add them)")]
    public static void Fix()
    {
        var r = Find();
        int added = 0;

        foreach (var go in r.missing)
        {
            var mf = go.GetComponent<MeshFilter>();
            string mesh = mf.sharedMesh.name;

            if (mesh == "Cube") Undo.AddComponent<BoxCollider>(go);
            else if (mesh == "Sphere") Undo.AddComponent<SphereCollider>(go);
            else if (mesh == "Capsule") Undo.AddComponent<CapsuleCollider>(go);
            else
            {
                var mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = mf.sharedMesh;
            }
            added++;
        }

        if (added > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("FIX done: added " + added + " colliders. Press Ctrl+S to save the scene.");

        if (r.disabled.Count > 0 || r.triggers.Count > 0)
            Debug.LogWarning("Still to check by hand: " + r.disabled.Count + " objects with a collider that is turned OFF, "
                             + r.triggers.Count + " big objects whose collider is a Trigger. Use SCAN to list them.");
    }

    // ------------------------------------------------------------------ search

    static Result Find()
    {
        var r = new Result();

        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var go = mf.gameObject;
            if (!go.scene.IsValid() || mf.sharedMesh == null) continue;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;
            if (ShouldSkip(go, mr)) continue;
            if (InsideSolidParent(go.transform)) continue;

            var cols = go.GetComponents<Collider>();
            if (cols.Length == 0)
            {
                r.missing.Add(go);
                continue;
            }

            bool solid = false, off = false, trig = false;
            foreach (var c in cols)
            {
                if (!c.enabled) off = true;
                else if (c.isTrigger) trig = true;
                else solid = true;
            }
            if (solid) continue;
            if (off) r.disabled.Add(go);
            else if (trig) r.triggers.Add(go);
        }
        return r;
    }

    static bool ShouldSkip(GameObject go, MeshRenderer mr)
    {
        Vector3 s = mr.bounds.size;
        if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < MinSize) return true;

        string n = go.name;
        foreach (var e in SkipExact) if (n == e) return true;
        foreach (var c in SkipContains) if (n.Contains(c)) return true;

        // stairs: FixStairs handles their collision
        if (n.StartsWith("Step_") || n.Contains("_step")) return true;
        for (var t = go.transform.parent; t != null; t = t.parent)
            if (t.name.StartsWith("Stairs")) return true;

        // player, enemy, slimes, dolls
        if (go.GetComponentInParent<PlayerController>() != null) return true;
        if (go.GetComponentInParent<EnemyAI>() != null) return true;
        if (go.GetComponentInParent<BlackSlime>() != null) return true;
        if (go.GetComponentInParent<HorrorDoll>() != null) return true;
        return false;
    }

    // true if a parent already has a solid collider that covers this mesh
    static bool InsideSolidParent(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
            foreach (var c in p.GetComponents<Collider>())
                if (c.enabled && !c.isTrigger) return true;
        return false;
    }

    // ------------------------------------------------------------------ report

    static void Report(Result r)
    {
        const int Max = 80;
        int shown = 0;

        foreach (var g in r.missing)
            if (shown++ < Max) Debug.Log("NO COLLIDER: " + PathOf(g), g);
        foreach (var g in r.disabled)
            if (shown++ < Max) Debug.LogWarning("COLLIDER IS TURNED OFF: " + PathOf(g), g);
        foreach (var g in r.triggers)
            if (shown++ < Max) Debug.LogWarning("COLLIDER IS A TRIGGER (you can walk through): " + PathOf(g), g);

        int total = r.missing.Count + r.disabled.Count + r.triggers.Count;
        if (total > Max) Debug.Log("... and " + (total - Max) + " more (they are all selected in the Hierarchy).");

        Debug.Log("SCAN done: " + r.missing.Count + " without collider, " + r.disabled.Count
                  + " with collider turned off, " + r.triggers.Count + " with a Trigger collider. "
                  + (r.missing.Count > 0 ? "Use FIX to add the missing ones." : "Nothing to add."));
    }

    static string PathOf(GameObject g)
    {
        string p = g.name;
        for (var t = g.transform.parent; t != null; t = t.parent) p = t.name + "/" + p;
        return p;
    }
}
#endif
