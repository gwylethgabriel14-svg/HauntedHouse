// Put this file in Assets/Editor/ next to the other builder scripts.
// Menu:  Haunted House > Create Enemy & Waypoints
//
// Requirements: the NavMesh is baked, and the scene has "Player" and "GameManager" objects.
//
// Creates:
//   - "Waypoints": 11 patrol points (one or two per room, none inside the center block)
//   - "Enemy": a dark-red capsule with NavMeshAgent + EnemyAI, spawned in Bedroom 1,
//     far from the player's start in the Entrance
// and wires up EnemyAI (player, waypoints, obstacle mask) and GameManager.enemy.
// Safe to run again: it rebuilds the Enemy and Waypoints.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BuildEnemy
{
    static readonly Vector3[] WaypointPos =
    {
        new Vector3(-4f, 0.1f,  4.5f),   // Bedroom 1
        new Vector3( 0f, 0.1f,  4.5f),   // Hallway
        new Vector3( 4f, 0.1f,  4.5f),   // Bathroom
        new Vector3(-4f, 0.1f,  0.0f),   // Kitchen
        new Vector3( 0f, 0.1f,  1.2f),   // Living room (north of the block)
        new Vector3( 0f, 0.1f, -2.2f),   // Living room (south of the block)
        new Vector3( 4f, 0.1f,  0.0f),   // Storage
        new Vector3(-4f, 0.1f, -5.0f),   // Bedroom 2
        new Vector3( 0f, 0.1f, -5.0f),   // Entrance
        new Vector3( 4f, 0.1f, -5.0f),   // Stairs
        new Vector3(-4f, 0.1f,  2.5f),   // Bedroom 1 / Kitchen doorway side
    };

    static readonly Vector3 EnemySpawn = new Vector3(-4f, 1f, 5.5f);

    [MenuItem("Haunted House/Create Enemy & Waypoints")]
    public static void Build()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("No object named 'Player' in the scene."); return; }

        var aiType = System.Type.GetType("EnemyAI, Assembly-CSharp");
        if (aiType == null) { Debug.LogError("EnemyAI.cs not found. Import all scripts first."); return; }

        // ---- Waypoints ----
        var wpRoot = GameObject.Find("Waypoints");
        if (wpRoot == null)
        {
            wpRoot = new GameObject("Waypoints");
            Undo.RegisterCreatedObjectUndo(wpRoot, "Create waypoints");
        }
        for (int i = wpRoot.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(wpRoot.transform.GetChild(i).gameObject);

        var wps = new Transform[WaypointPos.Length];
        for (int i = 0; i < WaypointPos.Length; i++)
        {
            var g = new GameObject("WP_" + (i + 1));
            g.transform.SetParent(wpRoot.transform, false);
            g.transform.position = WaypointPos[i];
            Undo.RegisterCreatedObjectUndo(g, "Create waypoint");
            wps[i] = g.transform;
        }

        // ---- Enemy ----
        var old = GameObject.Find("Enemy");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var enemy = new GameObject("Enemy");
        enemy.transform.position = EnemySpawn;
        enemy.transform.rotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(enemy, "Create enemy");

        var agent = Undo.AddComponent<NavMeshAgent>(enemy);
        agent.speed = 2f;
        agent.angularSpeed = 240f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0f;
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.baseOffset = 1f;

        var ai = Undo.AddComponent(enemy, aiType);

        // Rebuild the actual character visual so the enemy keeps the full Woman design
        // and the Animator sits on the child model, which is what EnemyAI expects.
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Animations/Woman.fbx");
        if (visualPrefab != null)
        {
            var visual = PrefabUtility.InstantiatePrefab(visualPrefab, enemy.transform) as GameObject;
            if (visual == null) visual = Object.Instantiate(visualPrefab, enemy.transform);
            visual.name = "WomanModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.GetComponentInChildren<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(visual);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/WomanController.controller");
            if (controller != null) animator.runtimeAnimatorController = controller;

            if (ai != null)
            {
                var so = new SerializedObject(ai);
                so.FindProperty("anim").objectReferenceValue = animator;
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "WomanModel";
            fallback.transform.SetParent(enemy.transform, false);
            fallback.transform.localPosition = Vector3.zero;
            fallback.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            var col = fallback.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var rend = fallback.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = MakeMat("GB_Enemy", new Color(0.45f, 0.05f, 0.05f));
        }

        var so = new SerializedObject(ai);
        so.FindProperty("player").objectReferenceValue = player.transform;

        var wpProp = so.FindProperty("waypoints");
        wpProp.arraySize = wps.Length;
        for (int i = 0; i < wps.Length; i++)
            wpProp.GetArrayElementAtIndex(i).objectReferenceValue = wps[i];

        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0) Debug.LogWarning("Layer 'Obstacle' not found: the enemy will see through walls until you add it and rebuild the house.");
        var maskProp = so.FindProperty("obstacleMask.m_Bits");
        if (maskProp != null) maskProp.intValue = layer >= 0 ? (1 << layer) : 0;
        so.ApplyModifiedProperties();

        // ---- GameManager.enemy ----
        var gm = GameObject.Find("GameManager");
        var gmType = System.Type.GetType("GameManager, Assembly-CSharp");
        if (gm != null && gmType != null)
        {
            var gmComp = gm.GetComponent(gmType);
            if (gmComp != null)
            {
                var gso = new SerializedObject(gmComp);
                var p = gso.FindProperty("enemy");
                if (p != null) p.objectReferenceValue = ai;
                gso.ApplyModifiedProperties();
            }
        }
        else Debug.LogWarning("GameManager object or script not found: assign the Enemy slot by hand.");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = enemy;
        Debug.Log("Enemy and " + wps.Length + " waypoints created. Press Play.");
    }

    static Material MakeMat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { m.color = c; return m; }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        m = new Material(shader);
        m.color = c;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
