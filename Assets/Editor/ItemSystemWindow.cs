using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HauntedItems.EditorTools
{
    public class ItemSystemWindow : EditorWindow
    {
        const string Root = "Assets/HauntedItems";
        ItemSpawner spawner; SerializedObject so; Vector2 scroll;
        readonly bool[] fold = { true, true, true, true, true, true };
        int seed = -1; GameObject playerOverride;

        [MenuItem("Haunted House/Item System/Item Tool")]
        static void Open() { GetWindow<ItemSystemWindow>("Item System"); }

        void OnEnable() { spawner = ItemUtil.FindSpawner(); }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Item & Trap System", EditorStyles.boldLabel);

            spawner = (ItemSpawner)EditorGUILayout.ObjectField("Item Spawner", spawner, typeof(ItemSpawner), true);
            if (spawner == null)
            {
                EditorGUILayout.HelpBox("Walang ItemSpawner sa scene.", MessageType.Info);
                if (GUILayout.Button("Create 'ItemSystem' object"))
                {
                    var go = new GameObject("ItemSystem");
                    spawner = Undo.AddComponent<ItemSpawner>(go);
                    Undo.RegisterCreatedObjectUndo(go, "Create ItemSystem");
                }
                EditorGUILayout.EndScrollView();
                return;
            }
            if (so == null || so.targetObject != spawner) so = new SerializedObject(spawner);
            so.Update();

            // 1. MODELS
            if (fold[0] = EditorGUILayout.Foldout(fold[0], "1. Item Model Generator", true))
            {
                Props("gunPrefab", "dartPackPrefab", "trapPrefab", "dartProjectilePrefab");
                if (GUILayout.Button("Generate / Update Item Prefabs")) { Apply(); ItemPrefabBuilder.Build(spawner); so.Update(); }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Show Model Preview in Scene")) ShowModelPreview();
                if (GUILayout.Button("Remove Model Preview")) RemoveModelPreview();
                EditorGUILayout.EndHorizontal();
            }
            // 2. SPAWN POINTS
            if (fold[1] = EditorGUILayout.Foldout(fold[1], "2. Item Spawn Point Generator", true))
            {
                Props("spawnPoints", "areaCenter", "areaSize", "gridStep", "maxGridPoints", "reachableFrom");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Generate Spawn Points (grid)")) { Apply(); Undo.RegisterCompleteObjectUndo(spawner, "Spawn points"); int n = spawner.GenerateSpawnPoints(); Debug.Log("Generated " + n + " spawn points."); Dirty(); so.Update(); }
                if (GUILayout.Button("Add Point at Scene Camera")) AddPointAtCamera();
                if (GUILayout.Button("Clear Points")) { Apply(); spawner.ClearSpawnPoints(); Dirty(); so.Update(); }
                EditorGUILayout.EndHorizontal();
            }
            // 3. RANDOM
            if (fold[2] = EditorGUILayout.Foldout(fold[2], "3. Random Spawn Settings", true))
                Props("spawnOnPlay", "newSeedEveryGame", "fixedSeed", "lastSeed", "spawnRadius", "spawnPointBias", "useProceduralPlacement",
                      "floorMask", "obstacleMask", "clearanceRadius", "minSpacing", "maxAttemptsPerItem",
                      "requireNavMesh", "navMeshTolerance", "requireCeiling", "maxCeilingHeight");
            // 4. QUANTITY
            if (fold[3] = EditorGUILayout.Foldout(fold[3], "4. Item Quantity Settings", true))
                Props("gunCount", "dartPackCount", "dartsPerPack", "trapCount");
            // 5. TRAP
            if (fold[4] = EditorGUILayout.Foldout(fold[4], "5. Trap Settings", true))
                Props("trapStunSeconds", "trapArmDelay");
            // 6. TRANQUILIZER
            if (fold[5] = EditorGUILayout.Foldout(fold[5], "6. Tranquilizer Settings", true))
                Props("magazineSize", "gunStartsLoaded", "dartStunSeconds", "reloadSeconds", "fireCooldown", "dartSpeed");

            Apply();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField(new GUIContent("Seed (-1 = random)"), seed);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(.6f, 1f, .6f);
            if (GUILayout.Button("Generate Items", GUILayout.Height(30)))
            {
                spawner.Generate(seed < 0 ? int.MinValue : seed);
                if (spawner.container != null) Undo.RegisterCreatedObjectUndo(spawner.container.gameObject, "Generate Items");
                Dirty();
            }
            GUI.backgroundColor = new Color(1f, .6f, .6f);
            if (GUILayout.Button("Clear Generated Items", GUILayout.Height(30))) { spawner.Clear(); Dirty(); }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Spawn Points")) { spawner.PreviewPoints(); SceneView.RepaintAll(); }
            if (GUILayout.Button("Clear Preview")) { spawner.previewPoints.Clear(); SceneView.RepaintAll(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            playerOverride = (GameObject)EditorGUILayout.ObjectField("Player (optional)", playerOverride, typeof(GameObject), true);
            if (GUILayout.Button("Setup Player (add PlayerItemInteractor)")) SetupPlayer();
            if (GUILayout.Button("Add StunnableEnemy to Selected Objects")) SetupEnemies();

            EditorGUILayout.EndScrollView();
        }

        void Props(params string[] names)
        {
            foreach (var n in names)
            {
                var p = so.FindProperty(n);
                if (p != null) EditorGUILayout.PropertyField(p, true);
            }
        }
        void Apply() { so.ApplyModifiedProperties(); }
        void Dirty() { if (spawner != null) EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene); }

        void AddPointAtCamera()
        {
            var sv = SceneView.lastActiveSceneView; if (sv == null) return;
            Apply();
            if (spawner.spawnPointsRoot == null)
            {
                var existing = spawner.transform.Find("SpawnPoints");
                spawner.spawnPointsRoot = existing != null ? existing : new GameObject("SpawnPoints").transform;
                spawner.spawnPointsRoot.SetParent(spawner.transform, false);
            }
            var g = new GameObject("SpawnPoint_" + (spawner.spawnPoints.Count + 1));
            g.transform.SetParent(spawner.spawnPointsRoot, false);
            Vector3 pos = sv.pivot;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 20f)) pos = hit.point;
            g.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(g, "Add spawn point");
            spawner.spawnPoints.Add(g.transform);
            EditorUtility.SetDirty(spawner); Dirty(); so.Update();
        }

        void ShowModelPreview()
        {
            RemoveModelPreview();
            var sv = SceneView.lastActiveSceneView;
            Vector3 c = sv != null ? sv.pivot : Vector3.zero;
            var root = new GameObject("ItemModelPreview");
            GameObject[] prefabs = { spawner.gunPrefab, spawner.dartPackPrefab, spawner.trapPrefab };
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], root.transform);
                g.transform.position = c + new Vector3(i * 1.2f - 1.2f, 0, 0);
            }
            Undo.RegisterCreatedObjectUndo(root, "Model preview");
        }
        void RemoveModelPreview()
        {
            var old = GameObject.Find("ItemModelPreview");
            if (old != null) DestroyImmediate(old);
        }

        void SetupPlayer()
        {
            var player = playerOverride != null ? playerOverride : GameObject.FindWithTag("Player");
            if (player == null) { Debug.LogWarning("Walang Player. I-drag ang player sa field."); return; }
            var it = player.GetComponent<PlayerItemInteractor>();
            if (it == null) it = Undo.AddComponent<PlayerItemInteractor>(player);
            it.playerCamera = player.GetComponentInChildren<Camera>() != null ? player.GetComponentInChildren<Camera>() : Camera.main;
            it.settings = spawner; it.trapPrefab = spawner.trapPrefab;
            EditorUtility.SetDirty(it); Dirty();
        }

        void SetupEnemies()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
                if (go.GetComponent<StunnableEnemy>() == null) { Undo.AddComponent<StunnableEnemy>(go); n++; }
            Debug.Log("StunnableEnemy added to " + n + " object(s).");
        }
    }

    public static class ItemPrefabBuilder
    {
        const string Root = "Assets/HauntedItems";
        static readonly Dictionary<Object, Object> done = new Dictionary<Object, Object>();

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void Build(ItemSpawner sp)
        {
            EnsureFolder(Root + "/Prefabs"); EnsureFolder(Root + "/Materials"); EnsureFolder(Root + "/Meshes");
            done.Clear(); ItemMaterials.ClearCache();

            sp.dartProjectilePrefab = Save(ItemModelFactory.BuildDart(), "TranquilizerDart");
            sp.gunPrefab = Save(ItemModelFactory.BuildGun(), "TranquilizerGun");
            sp.dartPackPrefab = Save(ItemModelFactory.BuildDartPack(), "TranquilizerDartPack");
            sp.trapPrefab = Save(ItemModelFactory.BuildTrap(), "BearTrap");

            // i-link ang dart prefab sa gun prefab
            var gunComp = sp.gunPrefab.GetComponent<TranquilizerGun>();
            gunComp.dartPrefab = sp.dartProjectilePrefab;
            EditorUtility.SetDirty(sp.gunPrefab);

            EditorUtility.SetDirty(sp);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[ItemTool] Prefabs saved sa " + Root);
        }

        static GameObject Save(GameObject go, string name)
        {
            Persist(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void Persist(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = PersistMaterial(mats[i]);
                r.sharedMaterials = mats;
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("HI_")) mf.sharedMesh = PersistMesh(mf.sharedMesh);
        }

        static Material PersistMaterial(Material m)
        {
            if (m == null || !m.name.StartsWith("HI_")) return m;
            if (done.TryGetValue(m, out var d)) return (Material)d;
            string path = Root + "/Materials/" + m.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material result;
            if (existing != null) { EditorUtility.CopySerialized(m, existing); result = existing; }
            else { AssetDatabase.CreateAsset(m, path); result = m; }
            done[m] = result; return result;
        }

        static Mesh PersistMesh(Mesh m)
        {
            if (done.TryGetValue(m, out var d)) return (Mesh)d;
            string path = Root + "/Meshes/" + m.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh result;
            if (existing != null) { EditorUtility.CopySerialized(m, existing); result = existing; }
            else { AssetDatabase.CreateAsset(m, path); result = m; }
            done[m] = result; return result;
        }
    }
}