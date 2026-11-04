using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Save / load for "The Last Lullaby".
// Saves: pages collected, key, player + enemy position, flashlight battery, bottles,
//        and which pages / key / batteries / bottles are still lying around.
//
// You do NOT put this in a scene. It creates itself when the game loads a saved game.
//   SaveSystem.Save()        -> writes the save file (used by the pause menu)
//   SaveSystem.HasSave()     -> is there a save? (used by Continue)
//   SaveSystem.DeleteSave()  -> used by New Game and when you win
public class SaveSystem : MonoBehaviour
{
    [System.Serializable]
    public class ItemEntry
    {
        public int type;
        public Vector3 pos;
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = 1;
        public string scene;
        public int pages;
        public bool hasKey;

        public Vector3 player;
        public float playerYaw;
        public float battery = -1f;
        public int bottles;
        public int lives = -1;

        public bool hasEnemy;
        public Vector3 enemy;
        public float enemyYaw;

        public bool keyExists;
        public Vector3 keyPos;
        public Vector3 keyRot;

        public bool musicBoxFound;
        public bool musicBoxExists;
        public Vector3 musicBoxPos;
        public Vector3 musicBoxRot;

        public List<Vector3> pagePos = new List<Vector3>();
        public List<Vector3> pageRot = new List<Vector3>();
        public List<ItemEntry> items = new List<ItemEntry>();
    }

    // true from "Continue" until the saved state has been restored
    public static bool LoadingFromSave { get; private set; }

    // true for the whole time the game scene that was opened with "Continue" is running
    // (StartHints uses it to skip the opening lines)
    public static bool Continued { get; private set; }

    static SaveData pending;

    static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "lullaby_save.json"); }
    }

    // ------------------------------------------------------------------ public API

    public static bool HasSave()
    {
        return File.Exists(FilePath);
    }

    public static void DeleteSave()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (System.Exception e) { Debug.LogWarning("Could not delete save: " + e.Message); }
        LoadingFromSave = false;
        Continued = false;
        pending = null;
    }

    // call before loading the game scene; returns the scene name to load
    public static string PrepareContinue(string fallbackScene)
    {
        try
        {
            var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (d == null || string.IsNullOrEmpty(d.scene)) return fallbackScene;
            pending = d;
            LoadingFromSave = true;
            return d.scene;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not read save: " + e.Message);
            LoadingFromSave = false;
            pending = null;
            return fallbackScene;
        }
    }

    public static bool Save()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var gm = GameManager.Instance;
        if (player == null || gm == null)
        {
            Debug.LogWarning("Save failed: Player or GameManager not found in this scene.");
            return false;
        }

        var d = new SaveData();
        d.scene = SceneManager.GetActiveScene().name;
        d.pages = gm.Pages;
        d.hasKey = gm.HasKey;

        Vector3 ppos = player.transform.position;
        if (player.IsHidden) ppos = NearestExitPoint(ppos);   // never save someone inside a locker
        d.player = ppos;
        d.playerYaw = player.transform.eulerAngles.y;

        var fl = FindFirstObjectByType<Flashlight>();
        if (fl != null) d.battery = fl.Battery;

        var th = FindFirstObjectByType<ThrowItem>();
        if (th != null) d.bottles = th.bottles;

        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) d.lives = health.Lives;

        var en = FindFirstObjectByType<EnemyAI>();
        if (en != null)
        {
            d.hasEnemy = true;
            d.enemy = en.transform.position;
            d.enemyYaw = en.transform.eulerAngles.y;
        }

        foreach (var p in FindObjectsByType<Page>(FindObjectsSortMode.None))
        {
            d.pagePos.Add(p.transform.position);
            d.pageRot.Add(p.transform.eulerAngles);
        }

        var key = FindFirstObjectByType<KeyItem>();
        if (key != null)
        {
            d.keyExists = true;
            d.keyPos = key.transform.position;
            d.keyRot = key.transform.eulerAngles;
        }

        d.musicBoxFound = gm.MusicBoxFound;
        var box = FindFirstObjectByType<MusicBoxItem>();
        if (box != null)
        {
            d.musicBoxExists = true;
            d.musicBoxPos = box.transform.position;
            d.musicBoxRot = box.transform.eulerAngles;
        }

        foreach (var it in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
            d.items.Add(new ItemEntry { type = (int)it.type, pos = it.transform.position });

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(d, true));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Save failed: " + e.Message);
            return false;
        }
    }

    // when hidden, use the locker's exit point instead of the position inside the locker
    static Vector3 NearestExitPoint(Vector3 from)
    {
        Vector3 best = from;
        float bestDist = float.MaxValue;
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (m.GetType().Name != "HidingSpot") continue;
            var f = m.GetType().GetField("exitPoint");
            if (f == null) continue;
            var t = f.GetValue(m) as Transform;
            if (t == null) continue;
            float d = (m.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t.position; }
        }
        return best;
    }

    // ------------------------------------------------------------------ restoring a save

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        LoadingFromSave = false;
        Continued = false;
        pending = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // any scene that is not the continued game (menu, restart, new game) resets this
        Continued = false;

        if (LoadingFromSave && pending != null && scene.name == pending.scene)
        {
            Continued = true;
            new GameObject("SaveRestorer").AddComponent<SaveSystem>();
        }
    }

    void Start()
    {
        StartCoroutine(Restore());
    }

    IEnumerator Restore()
    {
        // wait until every other script has finished its Start()
        yield return null;
        yield return new WaitForEndOfFrame();

        var d = pending;
        if (d != null)
        {
            try { Apply(d); }
            catch (System.Exception e) { Debug.LogError("Loading the save failed: " + e); }
        }

        LoadingFromSave = false;
        pending = null;
        Destroy(gameObject);
    }

    void Apply(SaveData d)
    {
        var gm = GameManager.Instance;
        if (gm != null) gm.RestoreState(d.pages, d.hasKey);

        // pages that were still on the floor
        var spawner = FindFirstObjectByType<PageSpawner>();
        if (spawner != null && spawner.pagePrefab != null)
            for (int i = 0; i < d.pagePos.Count; i++)
                Instantiate(spawner.pagePrefab, d.pagePos[i], Quaternion.Euler(d.pageRot[i]));

        // the key, if it had already appeared
        if (d.keyExists && gm != null && gm.keyPrefab != null)
        {
            Instantiate(gm.keyPrefab, d.keyPos, Quaternion.Euler(d.keyRot));
            gm.MarkKeySpawned();
        }

        // the music box
        if (gm != null) gm.RestoreMusicBox(d.musicBoxFound);
        var mgr = FindFirstObjectByType<MusicBoxManager>();
        if (mgr != null) mgr.RestoreFromSave(d.musicBoxFound, d.musicBoxExists, d.musicBoxPos, d.musicBoxRot);

        // batteries / bottles: remove the ones already taken
        foreach (var it in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None))
        {
            bool stillThere = false;
            foreach (var e in d.items)
                if (e.type == (int)it.type && (e.pos - it.transform.position).sqrMagnitude < 0.25f * 0.25f)
                { stillThere = true; break; }
            if (!stillThere) Destroy(it.gameObject);
        }

        // player
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = d.player;
            player.transform.rotation = Quaternion.Euler(0f, d.playerYaw, 0f);
            if (cc != null) cc.enabled = true;
        }

        var fl = FindFirstObjectByType<Flashlight>();
        if (fl != null && d.battery >= 0f) fl.SetBattery(d.battery);

        var th = FindFirstObjectByType<ThrowItem>();
        if (th != null) th.bottles = d.bottles;

        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null && d.lives > 0) health.SetLives(d.lives);

        // enemy
        var en = FindFirstObjectByType<EnemyAI>();
        if (en != null && d.hasEnemy)
        {
            Vector3 pos = d.enemy;

            // never start the enemy right next to the player
            if (player != null && Vector3.Distance(pos, player.transform.position) < 15f
                && en.waypoints != null && en.waypoints.Length > 0)
            {
                float far = -1f;
                foreach (var w in en.waypoints)
                {
                    if (w == null) continue;
                    float dist = Vector3.Distance(w.position, player.transform.position);
                    if (dist > far) { far = dist; pos = w.position; }
                }
            }

            var agent = en.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.Warp(pos);
            else en.transform.position = pos;
            en.transform.rotation = Quaternion.Euler(0f, d.enemyYaw, 0f);
        }
    }
}