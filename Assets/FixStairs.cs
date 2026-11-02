#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Makes the two staircases smooth to walk on (up AND down).
//
// The steps are solid boxes. The CharacterController bumps and sticks on them when you go down.
// This tool switches off the colliders of the steps (they stay visible) and adds one invisible
// sloped collider on top of them, like a ramp. Walking on it feels like walking on a normal stair.
//
//   Haunted House > Fix Stairs (smooth, no sticking)
//
// Safe to run again.
public static class FixStairs
{
    [MenuItem("Haunted House/Fix Stairs (smooth, no sticking)")]
    public static void Run()
    {
        // index every transform once
        var byName = new Dictionary<string, Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.scene.IsValid() && (t.name.StartsWith("Stairs") || t.name.StartsWith("StairRamp_")))
                byName[t.name] = t;

        int fixedCount = 0;
        foreach (string group in new[] { "Stairs1", "Stairs2" })
            if (Fix(group, byName)) fixedCount++;

        if (fixedCount > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Fix Stairs: " + fixedCount + " staircase(s) fixed.");
    }

    static bool Fix(string group, Dictionary<string, Transform> byName)
    {
        var steps = new List<Transform>();
        for (int i = 0; i < 64; i++)
        {
            Transform t;
            if (!byName.TryGetValue(group + "_step" + i, out t)) break;
            steps.Add(t);
        }
        if (steps.Count < 3)
        {
            Debug.LogWarning("Fix Stairs: could not find the steps of " + group +
                             " (named " + group + "_step0, _step1 ...). Skipped.");
            return false;
        }

        // remove an older ramp
        Transform old;
        if (byName.TryGetValue("StairRamp_" + group, out old) && old != null)
            Object.DestroyImmediate(old.gameObject);

        Transform first = steps[0], last = steps[steps.Count - 1];
        Vector3 fs = first.lossyScale, ls = last.lossyScale;

        // the ramp goes through the inside corners of the steps:
        // from the floor in front of the first step, to the top of the last step
        float startX = first.position.x - fs.x / 2f;
        float startY = first.position.y - fs.y / 2f;        // bottom of step 0 = floor level
        float endX = last.position.x + ls.x / 2f;
        float endY = last.position.y + ls.y / 2f;           // top of the last step
        float zCenter = first.position.z;
        float width = fs.z;

        float dx = endX - startX, dy = endY - startY;
        float len = Mathf.Sqrt(dx * dx + dy * dy);
        float theta = Mathf.Atan2(dy, dx);

        Vector2 mid = new Vector2((startX + endX) / 2f, (startY + endY) / 2f);
        Vector2 n = new Vector2(-Mathf.Sin(theta), Mathf.Cos(theta));
        Vector2 center = mid - n * 0.15f;                    // top of the box lies on the line

        var go = new GameObject("StairRamp_" + group);
        go.transform.SetParent(first.parent, false);
        go.transform.position = new Vector3(center.x, center.y, zCenter);
        go.transform.rotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(len, 0.3f, width);

        // the steps stay visible but no longer collide
        foreach (var s in steps)
        {
            var col = s.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        return true;
    }
}
#endif
