
using System.Collections.Generic;
using UnityEngine;

// Spawns one Music Box at a random location
// at the beginning of each New Game.
//
// Spawn markers should be positioned
// on top of tables, shelves, or cabinets.

public class MusicBoxManager : MonoBehaviour
{
    [Header("Music Box")]
    public GameObject musicBoxPrefab;
    public Transform[] spots;

    [Header("Placement")]
    public float minDistanceFromPlayer = 12f;

    [Tooltip("Small gap between box and furniture.")]
    public float surfaceGap = 0.01f;

    [Tooltip("Checks for furniture below the spawn point.")]
    public bool snapToSurface = true;

    [Tooltip("Maximum distance to search downward.")]
    public float surfaceCheckDistance = 1.5f;

    [Header("Legacy Compatibility")]
    public int triggerPages = 3;
    public bool lullabyAttractsEnemy = false;

    public string spawnHint =
        "A music box is hidden somewhere in the house.";

    [Header("Debug")]
    public bool showDebugLogs = true;

    public bool Spawned { get; private set; }

    private GameObject activeMusicBox;

    void Start()
    {
        // SaveSystem will restore the existing box
        // when continuing a saved game.
        if (SaveSystem.LoadingFromSave)
            return;

        SpawnRandom();
    }

    public void SpawnRandom()
    {
        if (Spawned)
            return;

        if (musicBoxPrefab == null)
        {
            Debug.LogError(
                "[MusicBoxManager] Music Box " +
                "Prefab is missing.", this);

            return;
        }

        if (spots == null || spots.Length == 0)
        {
            Debug.LogError(
                "[MusicBoxManager] Assign " +
                "spawn locations.", this);

            return;
        }

        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        List<Transform> valid =
            new List<Transform>();

        List<Transform> far =
            new List<Transform>();

        foreach (Transform spot in spots)
        {
            if (spot == null)
                continue;

            valid.Add(spot);

            if (player == null ||
                Vector3.Distance(
                    spot.position,
                    player.transform.position)
                    >= minDistanceFromPlayer)
            {
                far.Add(spot);
            }
        }

        if (valid.Count == 0)
            return;

        List<Transform> choices =
            far.Count > 0 ? far : valid;

        Transform selected =
            choices[Random.Range(
                0, choices.Count)];

        Vector3 spawnPosition = selected.position;

        if (snapToSurface)
        {
            Vector3 rayStart =
                selected.position +
                Vector3.up * 0.15f;

            RaycastHit[] hits = Physics.RaycastAll(
                rayStart,
                Vector3.down,
                surfaceCheckDistance + 0.15f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float highest = float.NegativeInfinity;

            foreach (RaycastHit hit in hits)
            {
                // Ignore surfaces above the marker.
                if (hit.point.y >
                    rayStart.y + 0.001f)
                    continue;

                if (hit.point.y > highest)
                    highest = hit.point.y;
            }

            if (!float.IsNegativeInfinity(highest))
            {
                spawnPosition.y = highest;
            }
        }

        activeMusicBox = Instantiate(
            musicBoxPrefab,
            spawnPosition,
            selected.rotation);

        AlignBottom(
            activeMusicBox,
            spawnPosition.y + surfaceGap);

        Spawned = true;

        if (showDebugLogs)
        {
            Debug.Log(
                "[MusicBoxManager] Music Box at " +
                selected.name +
                " | Position: " +
                activeMusicBox.transform.position);
        }
    }

    void AlignBottom(
        GameObject box,
        float desiredBottomY)
    {
        Renderer[] renderers =
            box.GetComponentsInChildren<Renderer>();

        float lowest = float.PositiveInfinity;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            lowest = Mathf.Min(
                lowest,
                renderer.bounds.min.y);
        }

        if (float.IsInfinity(lowest))
            return;

        float difference =
            desiredBottomY - lowest;

        box.transform.position +=
            Vector3.up * difference;
    }

    // Existing SaveSystem compatibility.
    public void RestoreFromSave(
        bool found,
        bool exists,
        Vector3 position,
        Vector3 euler)
    {
        if (activeMusicBox != null)
        {
            Destroy(activeMusicBox);
            activeMusicBox = null;
        }

        Spawned = found || exists;

        if (!exists || musicBoxPrefab == null)
            return;

        // Saved position is preserved exactly.
        activeMusicBox = Instantiate(
            musicBoxPrefab,
            position,
            Quaternion.Euler(euler));
    }

    public Transform GetActiveMusicBox()
    {
        if (activeMusicBox != null)
            return activeMusicBox.transform;

        MusicBoxItem existing =
            FindFirstObjectByType<MusicBoxItem>();

        return existing != null
            ? existing.transform
            : null;
    }
}
