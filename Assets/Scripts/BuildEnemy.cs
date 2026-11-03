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
        RebuildEnemyAndWaypoints();
    }

    [MenuItem("Haunted House/Rebuild Enemy Safe")]
    public static void RebuildEnemySafe()
    {
        RebuildEnemyAndWaypoints();
    }

    static void RebuildEnemyAndWaypoints()
    {
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("No object named 'Player' in the scene.");
            return;
        }

        var aiType = System.Type.GetType("EnemyAI, Assembly-CSharp");
        if (aiType == null)
        {
            Debug.LogError("EnemyAI.cs not found. Import all scripts first.");
            return;
        }

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

        // ---- Enemy root ----
        var oldEnemy = GameObject.Find("Enemy");
        if (oldEnemy != null)
            Undo.DestroyObjectImmediate(oldEnemy);

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

        // ---- Visual child (WomanModel) ----
        var visualPrefab = FindEnemyVisualPrefab();
        GameObject visual = null;

        if (visualPrefab != null)
        {
            visual = PrefabUtility.InstantiatePrefab(visualPrefab, enemy.transform) as GameObject;
            if (visual == null)
                visual = Object.Instantiate(visualPrefab, enemy.transform);

            visual.name = "WomanModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            if (visual.GetComponentsInChildren<Renderer>().Length > 0)
            {
                AlignModelToEnemy(visual, enemy.transform, agent.baseOffset, 1.78f);
            }

            ApplyModelColor(visual, new Color(0.62f, 0.68f, 0.72f, 1f));
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "WomanModel";
            visual.transform.SetParent(enemy.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            var col = visual.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = MakeMat("GB_Enemy", new Color(0.62f, 0.68f, 0.72f, 1f));

            Debug.LogWarning("No woman model prefab was found in the project. A fallback capsule was used so the enemy still exists.");
        }

        var animator = visual != null ? visual.GetComponent<Animator>() : null;
        if (animator == null && visual != null)
            animator = visual.GetComponentInChildren<Animator>();

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/WomanController.controller");
        var riggedWomanAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Animations/Woman.fbx");

        if (animator == null && controller != null && visual != null)
        {
            animator = Undo.AddComponent<Animator>(visual);
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            if (controller != null)
                animator.runtimeAnimatorController = controller;

            if (animator.avatar == null && riggedWomanAvatar != null)
                animator.avatar = riggedWomanAvatar;
        }

        if (ai != null && animator != null)
        {
            var so = new SerializedObject(ai);
            so.FindProperty("anim").objectReferenceValue = animator;
            so.ApplyModifiedProperties();
        }

        // ---- EnemyAI refs ----
        var enemySo = new SerializedObject(ai);
        enemySo.FindProperty("player").objectReferenceValue = player.transform;

        var wpProp = enemySo.FindProperty("waypoints");
        wpProp.arraySize = wps.Length;
        for (int i = 0; i < wps.Length; i++)
            wpProp.GetArrayElementAtIndex(i).objectReferenceValue = wps[i];

        var obstacles = LayerMask.NameToLayer("Obstacle");
        if (obstacles < 0)
            Debug.LogWarning("Layer 'Obstacle' not found: the enemy will see through walls until you add it and rebuild the house.");

        var maskProp = enemySo.FindProperty("obstacleMask.m_Bits");
        if (maskProp != null)
            maskProp.intValue = obstacles >= 0 ? (1 << obstacles) : 0;

        enemySo.ApplyModifiedProperties();

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
        else
        {
            Debug.LogWarning("GameManager object or script not found: assign the Enemy slot by hand.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = enemy;
        Debug.Log("Enemy rebuilt with a root/child hierarchy and " + wps.Length + " waypoints. Check hierarchy: Enemy > WomanModel");
    }

    static GameObject FindEnemyVisualPrefab()
    {
        string[] candidates =
        {
            "Assets/Animations/Woman.fbx",
            "Assets/Animations/Woman.prefab",
            "Assets/GhostlyWoman/Womann.prefab",
            "Assets/GhostlyWoman/Womann.obj"
        };

        foreach (var path in candidates)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) return go;
        }

        return null;
    }

    static void AlignModelToEnemy(GameObject model, Transform enemyRoot, float baseOffset, float targetHeight)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float height = Mathf.Max(0.01f, bounds.size.y);
        float scale = targetHeight / height;
        model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(scale, scale, scale));

        renderers = model.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 feet = new Vector3(enemyRoot.position.x, enemyRoot.position.y - baseOffset, enemyRoot.position.z);
        Vector3 delta = feet - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.transform.position += delta;
        model.transform.localPosition = new Vector3(model.transform.localPosition.x, 0f, model.transform.localPosition.z);
    }

    static void ApplyModelColor(GameObject model, Color color)
    {
        if (model == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        var mat = MakeMat("M_EnemyGhost", color);
        foreach (var renderer in renderers)
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            renderer.sharedMaterials = mats;
        }
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
