
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// ==========================================================
// THE LAST LULLABY - MUSIC NOTE PAGE SPAWNER
//
// - Exactly 3 torn Music Notes per new game
// - Uses approved spawn markers only
// - Each note goes to a different marker
// - Tries to keep the notes far apart
// - No random positions outside the markers
// - Excludes awkward attic/basement/bathroom spots
// - Does not touch Eleanor's Diary Pages
//
// UNITY EDITOR MENU:
// The Last Lullaby > 17 - Fix Music Note Spawn Spots
//
// IMPORTANT:
// Put the spawn markers exactly on the furniture
// where the notes should appear.
//
// Existing Page.cs does not need to change.
// ==========================================================

[DisallowMultipleComponent]
public class PageSpawner : MonoBehaviour
{
    [Header("Music Note Prefab")]
    public GameObject pagePrefab;

    [Header("Approved Spawn Locations")]
    public Transform[] candidateSpots;

    [Header("Number of Music Notes")]
    [Min(3)]
    public int pagesToSpawn = 3;

    [Header("Placement")]
    [Tooltip(
        "Small height offset above the assigned marker."
    )]
    public float spawnHeightOffset = 0.06f;

    public bool keepSpotRotation = true;

    [Header("Distribution")]
    [Tooltip(
        "Preferred minimum 3D distance between notes. " +
        "If the house is smaller, the script automatically " +
        "uses the farthest available spots."
    )]
    [Min(0f)]
    public float preferredMinDistance = 7f;

    [Header("Debug")]
    public bool logSpawnPositions = true;

    [SerializeField]
    private List<GameObject> spawnedPages =
        new List<GameObject>();

    // Approved markers visible in the House scene.
    // These are names, NOT hard-coded world positions.
    //
    // Their actual Transform positions are used,
    // so the notes stay on the assigned furniture.

    private static readonly string[] RecommendedNames =
    {
        "Page_LivingSofa",
        "Page_KitchenTable",
        "Page_KitchenCounter",
        "Page_LibraryShelf",
        "Page_StudyDesk",
        "Page_BedroomPillow",
        "Page_GuestBed",
        "Page_NurseryDresser"
    };

    // ======================================================
    // START
    // ======================================================

    private void Start()
    {
        if (SaveSystem.LoadingFromSave)
        {
            if (logSpawnPositions)
            {
                Debug.Log(
                    "[MUSIC NOTES] Continue detected. " +
                    "Skipping new random page generation. " +
                    "Existing save-restoration logic must " +
                    "restore uncollected pages.",
                    this);
            }

            return;
        }

        SpawnPages();
    }

    // ======================================================
    // SPAWN THREE PAGES
    // ======================================================

    public void SpawnPages()
    {
        if (pagePrefab == null)
        {
            Debug.LogError(
                "[MUSIC NOTES] Page Prefab is missing! " +
                "Assign the prefab containing Page.cs.",
                this);

            return;
        }

        Page prefabPage =
            pagePrefab.GetComponent<Page>();

        if (prefabPage == null)
        {
            Debug.LogError(
                "[MUSIC NOTES] The assigned Page Prefab " +
                "does not have a Page component.",
                this);

            return;
        }

        // Clear only pages created by THIS spawner
        // during the current runtime session.

        foreach (GameObject oldPage in spawnedPages)
        {
            if (oldPage != null)
                Destroy(oldPage);
        }

        spawnedPages.Clear();

        List<Transform> validSpots =
            GetValidSpots();

        const int required = 3;

        if (validSpots.Count < required)
        {
            Debug.LogError(
                "[MUSIC NOTES] Need at least 3 approved " +
                "spawn markers. Found only " +
                validSpots.Count +
                ". Run the setup menu in Unity.",
                this);

            return;
        }

        // Pick distant markers, without duplicates.
        List<Transform> chosen =
            ChooseSpreadOutSpots(
                validSpots,
                required);

        for (int i = 0; i < chosen.Count; i++)
        {
            Transform marker = chosen[i];

            int pieceIndex = i + 1;

            Vector3 spawnPosition =
                marker.position +
                Vector3.up * spawnHeightOffset;

            Quaternion spawnRotation =
                keepSpotRotation
                    ? marker.rotation
                    : Quaternion.identity;

            GameObject spawned = Instantiate(
                pagePrefab,
                spawnPosition,
                spawnRotation);

            spawned.name =
                "MusicNote_" +
                pieceIndex +
                "_Spawned";

            Page page =
                spawned.GetComponent<Page>();

            if (page != null)
                page.SetPieceIndex(pieceIndex);

            spawnedPages.Add(spawned);

            if (logSpawnPositions)
            {
                Debug.Log(
                    "[MUSIC NOTES] Piece " +
                    pieceIndex +
                    "/3 spawned at " +
                    marker.name +
                    " | Position: " +
                    spawnPosition,
                    spawned);
            }
        }

        if (logSpawnPositions)
        {
            Debug.Log(
                "[MUSIC NOTES] Successfully spawned " +
                spawnedPages.Count +
                " Music Notes at distinct approved spots.",
                this);
        }
    }

    // ======================================================
    // GET UNIQUE VALID SPOTS
    // ======================================================

    private List<Transform> GetValidSpots()
    {
        List<Transform> valid =
            new List<Transform>();

        if (candidateSpots == null)
            return valid;

        foreach (Transform spot in candidateSpots)
        {
            if (spot == null)
                continue;

            if (!spot.gameObject.scene.IsValid())
                continue;

            if (valid.Contains(spot))
                continue;

            // Only allow the approved marker names.
            if (!IsRecommendedMarker(spot.name))
            {
                if (logSpawnPositions)
                {
                    Debug.LogWarning(
                        "[MUSIC NOTES] Ignoring unapproved " +
                        "spawn marker: " + spot.name,
                        spot);
                }

                continue;
            }

            valid.Add(spot);
        }

        return valid;
    }

    private static bool IsRecommendedMarker(
        string objectName)
    {
        foreach (string approved in RecommendedNames)
        {
            if (objectName == approved)
                return true;
        }

        return false;
    }

    // ======================================================
    // SELECT WELL-SPACED LOCATIONS
    //
    // Random first location, then choose distant
    // locations with a small random variation.
    //
    // Distance uses horizontal X/Z positions.
    // This avoids clustering in one area.
    // ======================================================

    private List<Transform> ChooseSpreadOutSpots(
        List<Transform> available,
        int count)
    {
        List<Transform> pool =
            new List<Transform>(available);

        List<Transform> result =
            new List<Transform>();

        // First marker is random.
        int firstIndex =
            Random.Range(0, pool.Count);

        result.Add(pool[firstIndex]);

        pool.RemoveAt(firstIndex);

        // Choose the remaining spots.
        while (result.Count < count &&
               pool.Count > 0)
        {
            float bestScore =
                float.NegativeInfinity;

            int bestIndex = 0;

            for (int i = 0; i < pool.Count; i++)
            {
                Transform candidate = pool[i];

                float nearest =
                    float.PositiveInfinity;

                foreach (Transform chosen in result)
                {
                    Vector2 a = new Vector2(
                        candidate.position.x,
                        candidate.position.z);

                    Vector2 b = new Vector2(
                        chosen.position.x,
                        chosen.position.z);

                    float distance =
                        Vector2.Distance(a, b);

                    nearest = Mathf.Min(
                        nearest,
                        distance);
                }

                // Prefer enough spacing, but also
                // permit widely distributed locations
                // if every marker is somewhat close.

                float spacingScore = nearest;

                if (nearest >= preferredMinDistance)
                    spacingScore += 2f;

                // Small random variation.
                float randomBonus =
                    Random.Range(0f, 1.5f);

                float score =
                    spacingScore + randomBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            result.Add(pool[bestIndex]);
            pool.RemoveAt(bestIndex);
        }

        return result;
    }

    // ======================================================
    // EDITOR SETUP
    // ======================================================

#if UNITY_EDITOR

    [ContextMenu("Auto Assign Recommended Page Spots")]
    private void AutoAssignRecommendedSpots()
    {
        AssignRecommendedSpots(this, true);
    }

    [MenuItem(
        "The Last Lullaby/17 - Fix Music Note Spawn Spots")]
    private static void FixMusicNoteSpawnSpots()
    {
        PageSpawner[] allSpawners =
            FindObjectsByType<PageSpawner>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (allSpawners.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Music Note Spawner",
                "No PageSpawner component was found " +
                "in the open scene.\n\n" +
                "Open the House scene first.",
                "OK");

            return;
        }

        int successCount = 0;

        foreach (PageSpawner spawner in allSpawners)
        {
            if (spawner == null ||
                !spawner.gameObject.scene.IsValid())
            {
                continue;
            }

            if (AssignRecommendedSpots(
                spawner,
                false))
            {
                successCount++;
            }
        }

        EditorUtility.DisplayDialog(
            "The Last Lullaby",
            successCount > 0
                ? "Music Note spawn spots updated!\n\n" +
                  "Using approved furniture locations.\n" +
                  "Attic, basement and awkward spots " +
                  "are excluded.\n\n" +
                  "Save the House scene."
                : "Could not assign at least 3 " +
                  "recommended markers.\n\n" +
                  "Check the Unity Console.",
            "OK");
    }

    private static bool AssignRecommendedSpots(
        PageSpawner spawner,
        bool showDialog)
    {
        Transform[] allTransforms =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        List<Transform> approved =
            new List<Transform>();

        foreach (string wantedName in RecommendedNames)
        {
            foreach (Transform candidate in allTransforms)
            {
                if (candidate == null)
                    continue;

                if (!candidate.gameObject.scene.IsValid())
                    continue;

                if (candidate.name != wantedName)
                    continue;

                if (!approved.Contains(candidate))
                {
                    approved.Add(candidate);
                }
            }
        }

        if (approved.Count < 3)
        {
            Debug.LogError(
                "[MUSIC NOTES] Found only " +
                approved.Count +
                " approved marker(s). At least 3 " +
                "are required. Check the Hierarchy " +
                "and marker names.",
                spawner);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Music Note Spawner",
                    "Only " + approved.Count +
                    " recommended markers found.\n" +
                    "Need at least 3.",
                    "OK");
            }

            return false;
        }

        Undo.RecordObject(
            spawner,
            "Assign Music Note Spawn Spots");

        spawner.candidateSpots =
            approved.ToArray();

        spawner.pagesToSpawn = 3;
        spawner.spawnHeightOffset = 0.06f;
        spawner.preferredMinDistance = 7f;

        EditorUtility.SetDirty(spawner);

        EditorSceneManager.MarkSceneDirty(
            spawner.gameObject.scene);

        Selection.activeGameObject =
            spawner.gameObject;

        Debug.Log(
            "[MUSIC NOTES] Assigned " +
            approved.Count +
            " approved locations:\n" +
            string.Join(
                "\n",
                approved.ConvertAll(
                    spot => spot.name)),
            spawner);

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Music Note Spawner",
                approved.Count +
                " recommended locations assigned.\n\n" +
                "Save the House scene.",
                "OK");
        }

        return true;
    }

#endif
}
