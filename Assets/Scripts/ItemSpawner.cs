using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HauntedItems
{
    public class ItemSpawner : MonoBehaviour
    {
        [Header("Prefabs (auto-filled ng Item Tool; kung walang laman, bubuuin sa runtime)")]
        public GameObject gunPrefab, dartPackPrefab, trapPrefab, dartProjectilePrefab;

        [Header("Item Quantity")]
        [Min(0)] public int gunCount = 1;
        [Min(0)] public int dartPackCount = 3;
        [Min(1)] public int dartsPerPack = 3;
        [Min(0)] public int trapCount = 3;

        [Header("Random Spawn")]
        public bool spawnOnPlay = true;
        public bool newSeedEveryGame = true;
        public int fixedSeed = 12345;
        public int lastSeed;
        public List<Transform> spawnPoints = new List<Transform>();
        [Min(0)] public float spawnRadius = 1.5f;
        [Range(0, 1)] public float spawnPointBias = 0.7f;
        public bool useProceduralPlacement = true;
        public Vector3 areaCenter = new Vector3(0, 3f, 0);   // world space; taas ng area = lahat ng floors
        public Vector3 areaSize = new Vector3(30, 8, 20);
        public float gridStep = 2.5f; public int maxGridPoints = 60;

        [Header("Placement Validation")]
        public LayerMask floorMask = ~0;
        public LayerMask obstacleMask = ~0;
        public float clearanceRadius = 0.35f;
        public float minSpacing = 2f;
        public int maxAttemptsPerItem = 120;
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

        [HideInInspector] public Transform container;
        [HideInInspector] public Transform spawnPointsRoot;
        [HideInInspector] public List<Vector3> previewPoints = new List<Vector3>();

        readonly List<Vector3> placed = new List<Vector3>();
        bool navExists;

        void Start() { if (Application.isPlaying && spawnOnPlay) Generate(); }

        // ------------------------------------------------ generate / clear
        public int Generate(int seed = int.MinValue)
        {
            Clear();
            Physics.SyncTransforms();
            if (seed == int.MinValue) seed = newSeedEveryGame ? Guid.NewGuid().GetHashCode() : fixedSeed;
            lastSeed = seed;
            var rng = new System.Random(seed);
            var go = new GameObject("GeneratedItems");
            go.transform.SetParent(transform, false);
            container = go.transform;
            placed.Clear();
            navExists = NavMesh.CalculateTriangulation().vertices.Length > 0;

            int total = Spawn(rng, ItemKind.TranquilizerGun, gunCount)
                      + Spawn(rng, ItemKind.DartPack, dartPackCount)
                      + Spawn(rng, ItemKind.Trap, trapCount);
            Debug.Log("[ItemSpawner] Placed " + total + " items (seed " + seed + ").");
            return total;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var c = transform.GetChild(i);
                if (c.name != "GeneratedItems") continue;
                c.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(c.gameObject); else DestroyImmediate(c.gameObject);
            }
            container = null;
            placed.Clear();
        }

        int Spawn(System.Random rng, ItemKind kind, int count)
        {
            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                bool done = false;
                for (int a = 0; a < maxAttemptsPerItem && !done; a++)
                {
                    if (!TryFindPoint(rng, true, out Vector3 p)) continue;
                    Create(kind, p, rng);
                    placed.Add(p); done = true; ok++;
                }
                if (!done)
                    Debug.LogWarning("[ItemSpawner] Walang valid na lugar para sa " + kind + ". Palakihin ang area/spawn radius o luwagan ang validation.");
            }
            return ok;
        }

        GameObject Create(ItemKind kind, Vector3 point, System.Random rng)
        {
            GameObject prefab = kind == ItemKind.TranquilizerGun ? gunPrefab : kind == ItemKind.DartPack ? dartPackPrefab : trapPrefab;
            GameObject go;
            if (prefab != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                else go = Instantiate(prefab, container);
#else
                go = Instantiate(prefab, container);
#endif
            }
            else
            {
                go = kind == ItemKind.TranquilizerGun ? ItemModelFactory.BuildGun()
                   : kind == ItemKind.DartPack ? ItemModelFactory.BuildDartPack()
                   : ItemModelFactory.BuildTrap();
                go.transform.SetParent(container, false);
            }

            float yaw = (float)rng.NextDouble() * 360f;
            Quaternion rot = Quaternion.Euler(0, yaw, 0) * (kind == ItemKind.TranquilizerGun ? Quaternion.Euler(0, 0, 90) : Quaternion.identity);
            go.transform.SetPositionAndRotation(point, rot);
            var b = ItemModelFactory.GetBounds(go);
            go.transform.position += Vector3.up * (point.y + 0.004f - b.min.y);

            switch (kind)
            {
                case ItemKind.TranquilizerGun:
                    var g = go.GetComponent<TranquilizerGun>();
                    if (g != null)
                    {
                        g.magazineSize = magazineSize;
                        g.ammoInMagazine = gunStartsLoaded ? magazineSize : 0;
                        g.stunSeconds = dartStunSeconds; g.reloadSeconds = reloadSeconds;
                        g.fireCooldown = fireCooldown; g.dartSpeed = dartSpeed;
                        g.dartPrefab = dartProjectilePrefab;
                    }
                    break;
                case ItemKind.DartPack:
                    var p = go.GetComponent<PickupItem>(); if (p != null) p.amount = dartsPerPack;
                    break;
                case ItemKind.Trap:
                    var t = go.GetComponent<TrapItem>();
                    if (t != null) { t.stunDuration = trapStunSeconds; t.armDelay = trapArmDelay; }
                    break;
            }
            return go;
        }

        // ------------------------------------------------ point finding
        bool TryFindPoint(System.Random rng, bool checkSpacing, out Vector3 result)
        {
            result = default;
            bool useSP = spawnPoints.Count > 0 && (!useProceduralPlacement || rng.NextDouble() < spawnPointBias);
            Vector3 xz; float startY; bool firstOnly;
            if (useSP)
            {
                var sp = spawnPoints[rng.Next(spawnPoints.Count)];
                if (sp == null) return false;
                double ang = rng.NextDouble() * Math.PI * 2, rad = Math.Sqrt(rng.NextDouble()) * spawnRadius;
                xz = sp.position + new Vector3((float)(Math.Cos(ang) * rad), 0, (float)(Math.Sin(ang) * rad));
                startY = sp.position.y + 1f; firstOnly = true;
            }
            else if (useProceduralPlacement)
            {
                xz = areaCenter + new Vector3((float)(rng.NextDouble() - .5) * areaSize.x, 0, (float)(rng.NextDouble() - .5) * areaSize.z);
                startY = areaCenter.y + areaSize.y * .5f; firstOnly = false;
            }
            else return false;

            var valid = ValidFloors(xz, startY, checkSpacing, firstOnly);
            if (valid.Count == 0) return false;
            result = valid[rng.Next(valid.Count)];
            return true;
        }

        List<Vector3> ValidFloors(Vector3 xz, float startY, bool checkSpacing, bool firstOnly)
        {
            var list = new List<Vector3>();
            var hits = Physics.RaycastAll(new Vector3(xz.x, startY, xz.z), Vector3.down, 200f, floorMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.normal.y < 0.7f) continue;
                if (Accept(h.point, checkSpacing)) list.Add(h.point);
                if (firstOnly) break;
            }
            return list;
        }

        bool Accept(Vector3 p, bool checkSpacing)
        {
            if (Physics.CheckSphere(p + Vector3.up * (clearanceRadius + 0.06f), clearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, 1.2f, obstacleMask, QueryTriggerInteraction.Ignore)) return false;
            if (requireCeiling && !Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, maxCeilingHeight, obstacleMask, QueryTriggerInteraction.Ignore)) return false;
            if (checkSpacing)
                foreach (var q in placed)
                {
                    Vector3 d = q - p;
                    if (Mathf.Abs(d.y) < 2f && new Vector2(d.x, d.z).sqrMagnitude < minSpacing * minSpacing) return false;
                }
            if (requireNavMesh && navExists)
            {
                if (!NavMesh.SamplePosition(p, out var nh, navMeshTolerance, NavMesh.AllAreas)) return false;
                if (reachableFrom != null)
                {
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(reachableFrom.position, nh.position, NavMesh.AllAreas, path)
                        || path.status != NavMeshPathStatus.PathComplete) return false;
                }
            }
            return true;
        }

        // ------------------------------------------------ editor helpers
        public void PreviewPoints()
        {
            Physics.SyncTransforms();
            navExists = NavMesh.CalculateTriangulation().vertices.Length > 0;
            placed.Clear(); previewPoints.Clear();
            var rng = new System.Random(newSeedEveryGame ? Guid.NewGuid().GetHashCode() : fixedSeed);
            int total = gunCount + dartPackCount + trapCount;
            for (int i = 0; i < total; i++)
                for (int a = 0; a < maxAttemptsPerItem; a++)
                    if (TryFindPoint(rng, true, out var p)) { placed.Add(p); previewPoints.Add(p); break; }
            placed.Clear();
        }

        public int GenerateSpawnPoints()
        {
            Physics.SyncTransforms();
            navExists = NavMesh.CalculateTriangulation().vertices.Length > 0;
            placed.Clear();
            var found = new List<Vector3>();
            float startY = areaCenter.y + areaSize.y * .5f;
            for (float x = -areaSize.x * .5f; x <= areaSize.x * .5f; x += gridStep)
                for (float z = -areaSize.z * .5f; z <= areaSize.z * .5f; z += gridStep)
                    found.AddRange(ValidFloors(areaCenter + new Vector3(x, 0, z), startY, false, false));

            var rng = new System.Random(1234);
            for (int i = found.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = found[i]; found[i] = found[j]; found[j] = tmp; }
            if (found.Count > maxGridPoints) found.RemoveRange(maxGridPoints, found.Count - maxGridPoints);

            if (spawnPointsRoot == null)
            {
                var existing = transform.Find("SpawnPoints");
                spawnPointsRoot = existing != null ? existing : new GameObject("SpawnPoints").transform;
                spawnPointsRoot.SetParent(transform, false);
            }
            for (int i = 0; i < found.Count; i++)
            {
                var g = new GameObject("SpawnPoint_" + (spawnPoints.Count + 1));
                g.transform.SetParent(spawnPointsRoot, false);
                g.transform.position = found[i];
                spawnPoints.Add(g.transform);
            }
            return found.Count;
        }

        public void ClearSpawnPoints()
        {
            spawnPoints.Clear();
            var root = transform.Find("SpawnPoints");
            if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
            spawnPointsRoot = null;
        }

        void OnDrawGizmosSelected()
        {
            if (useProceduralPlacement)
            {
                Gizmos.color = new Color(1f, .6f, 0f, .9f);
                Gizmos.DrawWireCube(areaCenter, areaSize);
            }
            Gizmos.color = new Color(.2f, .8f, 1f, .9f);
            foreach (var sp in spawnPoints)
                if (sp != null) { Gizmos.DrawSphere(sp.position + Vector3.up * .1f, .12f); Gizmos.DrawWireSphere(sp.position, spawnRadius); }
            Gizmos.color = Color.green;
            foreach (var p in previewPoints) Gizmos.DrawWireSphere(p + Vector3.up * .2f, .25f);
        }
    }
}