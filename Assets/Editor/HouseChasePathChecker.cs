
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class HouseChasePathChecker : EditorWindow
{
    private GameObject bigHouse;
    private Vector2 scroll;

    private float stairClearance = 0.65f;
    private float detectionHeight = 2.0f;

    private readonly List<string> warnings =
        new List<string>();

    [MenuItem(
        "The Last Lullaby/14 - Enemy Chase Path Checker")]
    public static void Open()
    {
        HouseChasePathChecker window =
            GetWindow<HouseChasePathChecker>(
                "Enemy Chase Path");

        window.minSize = new Vector2(470, 480);
        window.Scan();
        window.Show();
    }

    private void OnEnable()
    {
        Scan();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "THE LAST LULLABY",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField(
            "ENEMY CHASE PATH CHECKER",
            EditorStyles.centeredGreyMiniLabel);

        GUILayout.Space(12);

        EditorGUILayout.HelpBox(
            "Checks furniture near the staircases " +
            "and helps clear the Living Room " +
            "stair approach without rebuilding " +
            "the house.",
            MessageType.Info);

        GUILayout.Space(10);

        bigHouse = (GameObject)EditorGUILayout.ObjectField(
            "BigHouse",
            bigHouse,
            typeof(GameObject),
            true);

        stairClearance = EditorGUILayout.Slider(
            "Clearance Around Stairs",
            stairClearance,
            0.3f,
            1.5f);

        detectionHeight = EditorGUILayout.Slider(
            "Obstacle Check Height",
            detectionHeight,
            0.5f,
            3f);

        GUILayout.Space(12);

        if (GUILayout.Button(
            "1 - SCAN STAIRCASE OBSTACLES",
            GUILayout.Height(36)))
        {
            Scan();
        }

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "SCAN RESULTS",
            EditorStyles.boldLabel);

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No staircase clearance warnings " +
                "detected in this scan.",
                MessageType.Info);
        }
        else
        {
            foreach (string warning in warnings)
            {
                EditorGUILayout.HelpBox(
                    warning,
                    MessageType.Warning);
            }
        }

        GUILayout.Space(16);

        EditorGUILayout.HelpBox(
            "Moves the original Sofa collider and " +
            "its generated visual together.\n\n" +
            "Sofa: Z 4.7 -> 3.8\n" +
            "CoffeeTable: Z 2.9 -> 1.3\n\n" +
            "Also moves the Living Sofa page marker " +
            "to match the sofa.",
            MessageType.None);

        GUI.backgroundColor =
            new Color(0.65f, 0.8f, 0.66f);

        if (GUILayout.Button(
            "2 - CLEAR LIVING ROOM STAIRS",
            GUILayout.Height(42)))
        {
            ClearLivingStairs();
        }

        GUI.backgroundColor = Color.white;

        GUILayout.Space(12);

        if (GUILayout.Button(
            "3 - REBUILD BIGHOUSE NAVMESH",
            GUILayout.Height(38)))
        {
            RebuildNavMesh();
        }

        GUILayout.Space(12);

        EditorGUILayout.HelpBox(
            "After rebuilding the NavMesh, test " +
            "whether Eleanor can chase the player " +
            "from the ground floor to the second " +
            "floor and attic.\n\n" +
            "This is a clearance audit, not a " +
            "guarantee that all NavMesh paths work.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    // ==================================================
    // FIND OBJECTS
    // ==================================================

    private static Transform FindByName(
        Transform root,
        string targetName)
    {
        if (root == null)
            return null;

        foreach (Transform t in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == targetName)
                return t;
        }

        return null;
    }

    private GameObject FindBigHouse()
    {
        if (bigHouse != null)
            return bigHouse;

        foreach (GameObject root in
            UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects())
        {
            Transform found =
                FindByName(root.transform, "BigHouse");

            if (found != null)
            {
                bigHouse = found.gameObject;
                return bigHouse;
            }
        }

        return null;
    }

    // ==================================================
    // SCAN
    // ==================================================

    private void Scan()
    {
        warnings.Clear();

        GameObject house = FindBigHouse();

        if (house == null)
        {
            warnings.Add(
                "BigHouse was not found in this scene.");
            Repaint();
            return;
        }

        CheckStaircase(
            house.transform,
            "Stairs1");

        CheckStaircase(
            house.transform,
            "Stairs2");

        if (FindByName(
            house.transform, "BigDecor") == null)
        {
            warnings.Add(
                "BigDecor is missing. The house may " +
                "not have its generated decorations.");
        }

        Repaint();
    }

    private void CheckStaircase(
        Transform house,
        string staircaseName)
    {
        List<Collider> stairColliders =
            new List<Collider>();

        foreach (Collider collider in
                 house.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
                continue;

            if (collider.name.StartsWith(
                staircaseName + "_step",
                StringComparison.OrdinalIgnoreCase))
            {
                stairColliders.Add(collider);
            }
        }

        if (stairColliders.Count == 0)
        {
            warnings.Add(
                staircaseName +
                " was not found. Check BigUpstairs " +
                "and the stair builder.");

            return;
        }

        Bounds stairs =
            stairColliders[0].bounds;

        foreach (Collider col in stairColliders)
            stairs.Encapsulate(col.bounds);

        // Check the stair footprint and a
        // clearance margin surrounding it.
        Bounds inspection = stairs;

        inspection.Expand(
            new Vector3(
                stairClearance * 2f,
                0f,
                stairClearance * 2f));

        // Only consider objects near the staircase
        // vertical level, not furniture on other floors.
        inspection.size = new Vector3(
            inspection.size.x,
            stairs.size.y + detectionHeight,
            inspection.size.z);

        Transform decor = FindByName(house, "BigDecor");

        if (decor == null)
            return;

        HashSet<string> reported =
            new HashSet<string>();

        foreach (Collider furniture in
                 house.GetComponentsInChildren<Collider>(true))
        {
            if (furniture == null ||
                !furniture.enabled ||
                !furniture.gameObject.activeInHierarchy ||
                furniture.isTrigger)
                continue;

            if (stairColliders.Contains(furniture))
                continue;

            if (furniture.transform.IsChildOf(decor))
                continue;

            // Ignore structural objects like floors,
            // walls and railing; this audit focuses
            // on furniture and movable clutter.
            string name = furniture.gameObject.name;

            if (!IsFurniture(name))
                continue;

            if (!inspection.Intersects(furniture.bounds))
                continue;

            if (reported.Add(name))
            {
                warnings.Add(
                    staircaseName +
                    ": Furniture near stair clearance - " +
                    name);
            }
        }
    }

    private bool IsFurniture(string name)
    {
        string[] terms =
        {
            "Sofa", "CoffeeTable", "Chair",
            "Armchair", "Bench", "Desk",
            "Table", "Wardrobe", "Shelf",
            "Cabinet", "Trunk", "Crate",
            "Box", "Sideboard", "Toybox",
            "Bed", "Dresser", "Fridge"
        };

        foreach (string term in terms)
        {
            if (name.IndexOf(
                term,
                StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    // ==================================================
    // MOVE SOFA AND COFFEE TABLE
    // ==================================================

    private void ClearLivingStairs()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Stop Play Mode before changing " +
                "furniture positions.",
                "OK");
            return;
        }

        GameObject house = FindBigHouse();

        if (house == null)
        {
            EditorUtility.DisplayDialog(
                "BigHouse Missing",
                "Open the scene containing BigHouse.",
                "OK");
            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Clear Staircase Approach",
                "Move Sofa and CoffeeTable farther " +
                "from Stairs1?\n\n" +
                "This updates their positions and " +
                "the sofa's generated visual.\n\n" +
                "You can Undo with Ctrl+Z.",
                "MOVE FURNITURE",
                "CANCEL");

        if (!confirmed)
            return;

        Transform root = house.transform;

        bool movedSofa =
            MoveFurniture(
                root,
                "Sofa",
                new Vector3(4f, 0.4f, 3.8f));

        bool movedTable =
            MoveFurniture(
                root,
                "CoffeeTable",
                new Vector3(4f, 0.25f, 1.3f));

        Transform pageSpot =
            FindByName(root, "Page_LivingSofa");

        if (pageSpot != null)
        {
            Undo.RecordObject(
                pageSpot,
                "Move Living Sofa Page Spot");

            Vector3 p = pageSpot.position;
            pageSpot.position =
                new Vector3(4f, p.y, 3.8f);
        }

        EditorSceneManager.MarkSceneDirty(
            house.scene);

        Scan();

        Debug.Log(
            "[CHASE PATH] Furniture adjusted. " +
            "Sofa moved: " + movedSofa +
            ", Table moved: " + movedTable +
            ". Rebuild the NavMesh next.");

        EditorUtility.DisplayDialog(
            "Furniture Updated",
            "Living Room furniture adjusted.\n\n" +
            "NEXT: Click REBUILD BIGHOUSE NAVMESH.\n" +
            "Then save the scene and test Eleanor.",
            "OK");
    }

    private bool MoveFurniture(
        Transform house,
        string objectName,
        Vector3 newWorldPosition)
    {
        Transform objectTransform =
            FindByName(house, objectName);

        if (objectTransform == null)
        {
            Debug.LogWarning(
                "[CHASE PATH] " + objectName +
                " was not found.");
            return false;
        }

        // Preserve the original bottom height.
        Undo.RecordObject(
            objectTransform,
            "Move " + objectName);

        objectTransform.position =
            newWorldPosition;

        // DecorateHouse creates a second generated
        // visual while leaving the original collider.
        // Align that visual with the moved collider.
        Transform visual =
            FindByName(
                house,
                objectName + "_Visual");

        if (visual != null)
        {
            Undo.RecordObject(
                visual,
                "Move " + objectName + " Visual");

            MeshRenderer renderer =
                objectTransform.GetComponent<MeshRenderer>();

            Collider col =
                objectTransform.GetComponent<Collider>();

            if (renderer != null)
            {
                Bounds b = renderer.bounds;

                visual.position = new Vector3(
                    b.center.x,
                    b.min.y,
                    b.center.z);
            }
            else if (col != null)
            {
                Bounds b = col.bounds;

                visual.position = new Vector3(
                    b.center.x,
                    b.min.y,
                    b.center.z);
            }
            else
            {
                visual.position = newWorldPosition;
            }
        }

        return true;
    }

    // ==================================================
    // REBUILD NAVMESH
    // ==================================================

    private void RebuildNavMesh()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Stop Play Mode before rebaking.",
                "OK");
            return;
        }

        GameObject house = FindBigHouse();

        if (house == null)
        {
            EditorUtility.DisplayDialog(
                "Missing BigHouse",
                "No BigHouse found in this scene.",
                "OK");
            return;
        }

        NavMeshSurface surface =
            house.GetComponent<NavMeshSurface>();

        if (surface == null)
        {
            surface =
                house.GetComponentInChildren<
                    NavMeshSurface>(true);
        }

        if (surface == null)
        {
            EditorUtility.DisplayDialog(
                "NavMeshSurface Missing",
                "Add a NavMeshSurface to BigHouse " +
                "before rebaking.",
                "OK");
            return;
        }

        try
        {
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(
                house.scene);

            Debug.Log(
                "[CHASE PATH] BigHouse NavMesh " +
                "rebuilt successfully.");

            EditorUtility.DisplayDialog(
                "NavMesh Rebuilt",
                "NavMesh rebuild finished.\n\n" +
                "Save the scene with Ctrl+S, " +
                "then test Eleanor's chase.",
                "OK");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[CHASE PATH] NavMesh rebuild failed: " +
                e.Message);
        }
    }
}

#endif
