
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HauntedItems
{
    // =====================================================
    // THE LAST LULLABY - RANDOM ITEM SPAWNER
    //
    // Randomizes on every New Game:
    // - Tranquilizer Guns
    // - Dart Packs
    // - Bear Traps
    //
    // Keeps existing Item Model Factory,
    // PickupItem, TranquilizerGun and TrapItem.
    //
    // FIXES:
    // - Reliable new random seed each game
    // - Correct placement height
    // - Validated positions and item spacing
    // - Better random distribution
    // - Clear debug logs of each spawned item
    // =====================================================

    public class ItemSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject gunPrefab;
        public GameObject dartPackPrefab;
        public GameObject trapPrefab;
        public GameObject dartProjectilePrefab;

        [Header("Item Quantity")]
        [Min(0)] public int gunCount = 1;
        [Min(0)] public int dartPackCount = 3;
        [Min(1)] public int dartsPerPack = 3;
        [Min(0)] public int trapCount = 3;

        [Header("Random Spawn")]
        public bool spawnOnPlay = true;

        [Tooltip(
            "ON = different random seed each New Game."
        )]
        public bool newSeedEveryGame = true;

        public int fixedSeed = 12345;
        public int lastSeed;

        public List<Transform> spawnPoints =
            new List<Transform>();

        [Min(0f)]
        public float spawnRadius = 1.5f;

        [Range(0f, 1f)]
        public float spawnPointBias = 0.5f;

        public bool useProceduralPlacement = true;

        public Vector3 areaCenter =
            new Vector3(15f, 3.5f, 10f);

        public Vector3 areaSize =
            new Vector3(29f, 8f, 19f);

        public float gridStep = 2.5f;
        public int maxGridPoints = 60;

        [Header("Placement Validation")]
        public LayerMask floorMask = ~0;
        public LayerMask obstacleMask = ~0;

        public float clearanceRadius = 0.35f;
        public float minSpacing = 2f;

        public int maxAttemptsPerItem = 180;

        public bool requireNavMesh = true;
        public float navMeshTolerance = 0.6f;

        public Transform reachableFrom;

        public bool requireCeiling = false;
        public float maxCeilingHeight = 6f;

        [Header("Tranquilizer Settings")]
        public int magazineSize = 3;
        public bool gunStartsLoaded = true;

        public float dartStunSeconds = 120f;
        public float reloadSeconds = 1.8f;
        public float fireCooldown = 0.6f;
        public float dartSpeed = 45f;

        [Header("Trap Settings")]
        public float trapStunSeconds = 60f;
        public float trapArmDelay = 1.5f;

        [Header("Debug")]
        public bool logSpawnPositions = true;

        [HideInInspector]
        public Transform container;

        [HideInInspector]
        public Transform spawnPointsRoot;

        [HideInInspector]
        public List<Vector3> previewPoints =
            new List<Vector3>();

        private readonly List<Vector3> placed =
            new List<Vector3>();

        private bool navExists;

        private enum ItemKind
        {
            TranquilizerGun,
            DartPack,
            Trap
        }

        // =================================================
        // START
        // =================================================

        private void Start()
        {
            if (Application.isPlaying && spawnOnPlay)
            {
                Generate();
            }
        }

        // =================================================
        // RANDOM GENERATION
        // =================================================

        [ContextMenu("TEST - Generate Random Items")]
        public void GenerateRandomItems()
        {
            Generate();
        }

        public int Generate(int seed = int.MinValue)
        {
            Clear();

            Physics.SyncTransforms();

            if (seed == int.MinValue)
            {
                seed = newSeedEveryGame
                    ? Guid.NewGuid().GetHashCode()
                    : fixedSeed;
            }

            lastSeed = seed;

            System.Random rng =
                new System.Random(seed);

            GameObject root =
                new GameObject("GeneratedItems");

            root.transform.SetParent(
                transform,
                false);

            // Keep generated items in world space.
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation =
                Quaternion.identity;

            root.transform.localScale = Vector3.one;

            container = root.transform;

            placed.Clear();

            navExists =
                NavMesh.CalculateTriangulation()
                    .vertices.Length > 0;

            if (requireNavMesh && !navExists)
            {
                Debug.LogWarning(
                    "[ItemSpawner] No baked NavMesh " +
                    "found. Placement will still " +
                    "validate floors and obstacles.",
                    this);
            }

            // Randomize which item category is
            // placed first, so there is no fixed
            // priority for the best positions.
            List<ItemKind> queue =
                new List<ItemKind>();

            for (int i = 0; i < gunCount; i++)
                queue.Add(ItemKind.TranquilizerGun);

            for (int i = 0; i < dartPackCount; i++)
                queue.Add(ItemKind.DartPack);

            for (int i = 0; i < trapCount; i++)
                queue.Add(ItemKind.Trap);

            Shuffle(queue, rng);

            int successCount = 0;

            foreach (ItemKind kind in queue)
            {
                if (SpawnOne(rng, kind))
                    successCount++;
            }

            Debug.Log(
                "[ItemSpawner] NEW RANDOM GENERATION\n" +
                "Seed: " + lastSeed + "\n" +
                "Items placed: " + successCount +
                "/" + queue.Count + "\n" +
                "Guns: " + gunCount + "\n" +
                "Dart Packs: " + dartPackCount + "\n" +
                "Traps: " + trapCount,
                this);

            return successCount;
        }

        private static void Shuffle<T>(
            List<T> list,
            System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);

                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        // =================================================
        // CLEAR PREVIOUS GENERATED ITEMS
        // =================================================

        public void Clear()
        {
            for (int i = transform.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    transform.GetChild(i);

                if (child.name != "GeneratedItems")
                    continue;

                child.gameObject.SetActive(false);

                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            container = null;
            placed.Clear();
        }

        // =================================================
        // SPAWN ONE ITEM
        // =================================================

        private bool SpawnOne(
            System.Random rng,
            ItemKind kind)
        {
            int attempts =
                Mathf.Max(1, maxAttemptsPerItem);

            for (int a = 0; a < attempts; a++)
            {
                Vector3 position;

                if (!TryFindPoint(
                    rng,
                    true,
                    out position))
                {
                    continue;
                }

                GameObject spawned =
                    Create(kind, position, rng);

                if (spawned == null)
                    continue;

                placed.Add(position);

                if (logSpawnPositions)
                {
                    Debug.Log(
                        "[ItemSpawner] " +
                        kind +
                        " spawned at " +
                        spawned.transform.position
                            .ToString("F2") +
                        " | Seed: " + lastSeed,
                        spawned);
                }

                return true;
            }

            Debug.LogWarning(
                "[ItemSpawner] No valid position " +
                "for " + kind +
                " after " + attempts +
                " attempts. Check spawn area, " +
                "floor/obstacle masks and NavMesh.",
                this);

            return false;
        }

        // =================================================
        // CREATE PREFAB
        // =================================================

        private GameObject Create(
            ItemKind kind,
            Vector3 point,
            System.Random rng)
        {
            GameObject prefab = null;

            switch (kind)
            {
                case ItemKind.TranquilizerGun:
                    prefab = gunPrefab;
                    break;

                case ItemKind.DartPack:
                    prefab = dartPackPrefab;
                    break;

                case ItemKind.Trap:
                    prefab = trapPrefab;
                    break;
            }

            GameObject go;

            if (prefab != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    go = (GameObject)
                        PrefabUtility.InstantiatePrefab(
                            prefab,
                            container);
                }
                else
                {
                    go = Instantiate(prefab, container);
                }
#else
                go = Instantiate(prefab, container);
#endif
            }
            else
            {
                switch (kind)
                {
                    case ItemKind.TranquilizerGun:
                        go = ItemModelFactory.BuildGun();
                        break;

                    case ItemKind.DartPack:
                        go = ItemModelFactory.BuildDartPack();
                        break;

                    default:
                        go = ItemModelFactory.BuildTrap();
                        break;
                }

                if (go != null)
                    go.transform.SetParent(
                        container,
                        true);
            }

            if (go == null)
                return null;

            float yaw =
                (float)rng.NextDouble() * 360f;

            Quaternion rotation =
                Quaternion.Euler(0f, yaw, 0f);

            if (kind == ItemKind.TranquilizerGun)
            {
                rotation *=
                    Quaternion.Euler(0f, 0f, 90f);
            }

            go.transform.SetPositionAndRotation(
                point,
                rotation);

            // FIX:
            // Point.y is already the detected
            // floor height. Only offset the item
            // by the difference to its mesh bottom.
            // Do NOT add point.y a second time.
            Bounds bounds =
                ItemModelFactory.GetBounds(go);

            float bottomOffset =
                point.y + 0.035f -
                bounds.min.y;

            go.transform.position +=
                Vector3.up * bottomOffset;

            // Existing gameplay component settings.
            switch (kind)
            {
                case ItemKind.TranquilizerGun:
                {
                    TranquilizerGun gun =
                        go.GetComponent<TranquilizerGun>();

                    if (gun != null)
                    {
                        gun.magazineSize =
                            magazineSize;

                        gun.ammoInMagazine =
                            gunStartsLoaded
                                ? magazineSize
                                : 0;

                        gun.stunSeconds =
                            dartStunSeconds;

                        gun.reloadSeconds =
                            reloadSeconds;

                        gun.fireCooldown =
                            fireCooldown;

                        gun.dartSpeed =
                            dartSpeed;

                        gun.dartPrefab =
                            dartProjectilePrefab;
                    }

                    break;
                }

                case ItemKind.DartPack:
                {
                    PickupItem pickup =
                        go.GetComponent<PickupItem>();

                    if (pickup != null)
                    {
                        pickup.amount =
                            dartsPerPack;
                    }

                    break;
                }

                case ItemKind.Trap:
                {
                    TrapItem trap =
                        go.GetComponent<TrapItem>();

                    if (trap != null)
                    {
                        trap.stunDuration =
                            trapStunSeconds;

                        trap.armDelay =
                            trapArmDelay;
                    }

                    break;
                }
            }

            return go;
        }

        // =================================================
        // RANDOM LOCATION
        // =================================================

        private bool TryFindPoint(
            System.Random rng,
            bool checkSpacing,
            out Vector3 result)
        {
            result = Vector3.zero;

            List<Transform> validSpawnPoints =
                new List<Transform>();

            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                    validSpawnPoints.Add(point);
            }

            bool useSpawnPoint =
                validSpawnPoints.Count > 0 &&
                (!useProceduralPlacement ||
                 rng.NextDouble() < spawnPointBias);

            Vector3 xz;
            float rayStartY;
            bool firstOnly;

            if (useSpawnPoint)
            {
                Transform spawn =
                    validSpawnPoints[
                        rng.Next(validSpawnPoints.Count)];

                double angle =
                    rng.NextDouble() * Math.PI * 2.0;

                double radius =
                    Math.Sqrt(rng.NextDouble()) *
                    spawnRadius;

                xz = spawn.position +
                    new Vector3(
                        (float)(
                            Math.Cos(angle) * radius),
                        0f,
                        (float)(
                            Math.Sin(angle) * radius));

                rayStartY =
                    spawn.position.y + 1.5f;

                firstOnly = true;
            }
            else if (useProceduralPlacement)
            {
                xz = areaCenter +
                    new Vector3(
                        (float)(
                            rng.NextDouble() - 0.5) *
                        areaSize.x,

                        0f,

                        (float)(
                            rng.NextDouble() - 0.5) *
                        areaSize.z);

                rayStartY =
                    areaCenter.y +
                    areaSize.y * 0.5f;

                firstOnly = false;
            }
            else
            {
                return false;
            }

            List<Vector3> valid =
                ValidFloors(
                    xz,
                    rayStartY,
                    checkSpacing,
                    firstOnly);

            if (valid.Count == 0)
                return false;

            result =
                valid[rng.Next(valid.Count)];

            return true;
        }

        // =================================================
        // FIND VALID FLOOR SURFACES
        // =================================================

        private List<Vector3> ValidFloors(
            Vector3 xz,
            float startY,
            bool checkSpacing,
            bool firstOnly)
        {
            List<Vector3> valid =
                new List<Vector3>();

            RaycastHit[] hits =
                Physics.RaycastAll(
                    new Vector3(
                        xz.x,
                        startY,
                        xz.z),
                    Vector3.down,
                    200f,
                    floorMask,
                    QueryTriggerInteraction.Ignore);

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.normal.y < 0.7f)
                    continue;

                if (Accept(hit.point, checkSpacing))
                    valid.Add(hit.point);

                if (firstOnly)
                    break;
            }

            return valid;
        }

        // =================================================
        // PLACEMENT VALIDATION
        // =================================================

        private bool Accept(
            Vector3 point,
            bool checkSpacing)
        {
            float radius =
                Mathf.Max(0.05f, clearanceRadius);

            // Avoid detecting the actual floor
            // when checking the item clearance.
            Vector3 center =
                point +
                Vector3.up *
                (radius + 0.08f);

            if (Physics.CheckSphere(
                center,
                radius,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (Physics.Raycast(
                point + Vector3.up * 0.05f,
                Vector3.up,
                1.2f,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (requireCeiling)
            {
                if (!Physics.Raycast(
                    point + Vector3.up * 0.1f,
                    Vector3.up,
                    maxCeilingHeight,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
            }

            if (checkSpacing)
            {
                foreach (Vector3 previous in placed)
                {
                    Vector3 difference =
                        previous - point;

                    float horizontal =
                        new Vector2(
                            difference.x,
                            difference.z).sqrMagnitude;

                    if (Mathf.Abs(difference.y) < 2f &&
                        horizontal < minSpacing * minSpacing)
                    {
                        return false;
                    }
                }
            }

            if (requireNavMesh && navExists)
            {
                NavMeshHit navHit;

                if (!NavMesh.SamplePosition(
                    point,
                    out navHit,
                    navMeshTolerance,
                    NavMesh.AllAreas))
                {
                    return false;
                }

                // Avoid sampling a different floor.
                if (Mathf.Abs(
                    navHit.position.y - point.y) >
                    navMeshTolerance)
                {
                    return false;
                }

                if (reachableFrom != null)
                {
                    NavMeshHit originHit;

                    if (!NavMesh.SamplePosition(
                        reachableFrom.position,
                        out originHit,
                        2f,
                        NavMesh.AllAreas))
                    {
                        return false;
                    }

                    NavMeshPath path =
                        new NavMeshPath();

                    if (!NavMesh.CalculatePath(
                        originHit.position,
                        navHit.position,
                        NavMesh.AllAreas,
                        path))
                    {
                        return false;
                    }

                    if (path.status !=
                        NavMeshPathStatus.PathComplete)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // =================================================
        // PREVIEW POINTS
        // =================================================

        public void PreviewPoints()
        {
            Physics.SyncTransforms();

            navExists =
                NavMesh.CalculateTriangulation()
                    .vertices.Length > 0;

            placed.Clear();
            previewPoints.Clear();

            int seed = newSeedEveryGame
                ? Guid.NewGuid().GetHashCode()
                : fixedSeed;

            System.Random rng =
                new System.Random(seed);

            int total =
                gunCount +
                dartPackCount +
                trapCount;

            for (int i = 0; i < total; i++)
            {
                for (int attempt = 0;
                     attempt < maxAttemptsPerItem;
                     attempt++)
                {
                    Vector3 point;

                    if (TryFindPoint(
                        rng,
                        true,
                        out point))
                    {
                        placed.Add(point);
                        previewPoints.Add(point);
                        break;
                    }
                }
            }

            placed.Clear();
        }

        // =================================================
        // GENERATE EDITOR SPAWN POINTS
        // =================================================

        public int GenerateSpawnPoints()
        {
            Physics.SyncTransforms();

            navExists =
                NavMesh.CalculateTriangulation()
                    .vertices.Length > 0;

            placed.Clear();

            List<Vector3> found =
                new List<Vector3>();

            float startY =
                areaCenter.y +
                areaSize.y * 0.5f;

            for (float x = -areaSize.x * 0.5f;
                 x <= areaSize.x * 0.5f;
                 x += Mathf.Max(0.5f, gridStep))
            {
                for (float z = -areaSize.z * 0.5f;
                     z <= areaSize.z * 0.5f;
                     z += Mathf.Max(0.5f, gridStep))
                {
                    Vector3 sample =
                        areaCenter +
                        new Vector3(x, 0f, z);

                    found.AddRange(
                        ValidFloors(
                            sample,
                            startY,
                            false,
                            false));
                }
            }

            System.Random rng =
                new System.Random(1234);

            Shuffle(found, rng);

            if (found.Count > maxGridPoints)
            {
                found.RemoveRange(
                    maxGridPoints,
                    found.Count - maxGridPoints);
            }

            if (spawnPointsRoot == null)
            {
                Transform existing =
                    transform.Find("SpawnPoints");

                spawnPointsRoot =
                    existing != null
                        ? existing
                        : new GameObject(
                            "SpawnPoints").transform;

                spawnPointsRoot.SetParent(
                    transform,
                    false);
            }

            int added = 0;

            foreach (Vector3 position in found)
            {
                GameObject point =
                    new GameObject(
                        "SpawnPoint_" +
                        (spawnPoints.Count + 1));

                point.transform.SetParent(
                    spawnPointsRoot,
                    false);

                point.transform.position = position;

                spawnPoints.Add(point.transform);
                added++;
            }

            return added;
        }

        // =================================================
        // CLEAR EDITOR SPAWN POINTS
        // =================================================

        public void ClearSpawnPoints()
        {
            spawnPoints.Clear();

            Transform root =
                transform.Find("SpawnPoints");

            if (root != null)
            {
                if (Application.isPlaying)
                    Destroy(root.gameObject);
                else
                    DestroyImmediate(root.gameObject);
            }

            spawnPointsRoot = null;
        }

        // =================================================
        // GIZMOS
        // =================================================

        private void OnDrawGizmosSelected()
        {
            if (useProceduralPlacement)
            {
                Gizmos.color =
                    new Color(1f, 0.6f, 0f, 0.9f);

                Gizmos.DrawWireCube(
                    areaCenter,
                    areaSize);
            }

            Gizmos.color =
                new Color(0.2f, 0.8f, 1f, 0.9f);

            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                    continue;

                Gizmos.DrawSphere(
                    point.position + Vector3.up * 0.1f,
                    0.12f);

                Gizmos.DrawWireSphere(
                    point.position,
                    spawnRadius);
            }

            Gizmos.color = Color.green;

            foreach (Vector3 point in previewPoints)
            {
                Gizmos.DrawWireSphere(
                    point + Vector3.up * 0.2f,
                    0.25f);
            }
        }
    }
}
