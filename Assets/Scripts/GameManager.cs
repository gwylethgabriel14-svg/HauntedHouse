
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Objective - Three Music Notes")]
    public int totalPages = 3;
    public GameObject keyPrefab;
    public Transform[] keySpawnPoints;

    [Header("The House Changes")]
    public GameObject[] barricades;

    [Header("Music Box Quest")]
    public bool requireMusicBox = true;

    [Header("Saving")]
    public bool autoSaveOnProgress = true;

    [Header("Refs")]
    public EnemyAI enemy;

    [Header("Enemy Difficulty")]
    [Tooltip(
        "Temporarily disable to avoid the " +
        "EnemyAI.Enrage NullReferenceException."
    )]
    public bool enrageEnemyOnPageCollection = false;

    [Header("UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    public bool HasKey { get; private set; }

    public bool IsEnded => ended;

    public int Pages => pages;

    public bool MusicBoxFound { get; private set; }

    public bool HasAllMusicNotes =>
        pages >= Mathf.Max(1, totalPages);

    private int pages;
    private bool ended;
    private bool keySpawned;

    private GameOverSequence sequence;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (enemy == null)
        {
            enemy = FindFirstObjectByType<EnemyAI>();
        }

        sequence =
            FindFirstObjectByType<GameOverSequence>();

        if (sequence == null)
        {
            sequence =
                new GameObject("GameOverSequence")
                .AddComponent<GameOverSequence>();
        }
    }

    // =========================================
    // MUSIC NOTES COLLECTION
    // =========================================

    public void CollectPage()
    {
        if (ended || pages >= totalPages)
            return;

        pages++;

        Debug.Log(
            "[GameManager] Music Notes: " +
            pages + "/" + totalPages
        );

        // Temporary protection against the
        // existing EnemyAI.Enrage error.
        ApplyEnemyDifficulty();

        // The house changes after collecting notes.
        int index = pages - 1;

        if (barricades != null &&
            index >= 0 &&
            index < barricades.Length &&
            barricades[index] != null)
        {
            barricades[index].SetActive(true);
        }

        // Key should NOT spawn automatically
        // when the Music Box is required.
        if (HasAllMusicNotes &&
            !requireMusicBox &&
            !keySpawned)
        {
            SpawnKey();
        }

        AutoSaveSoon();
    }

    // =========================================
    // ENEMY DIFFICULTY
    // =========================================

    private void ApplyEnemyDifficulty()
    {
        if (!enrageEnemyOnPageCollection)
            return;

        if (enemy == null)
            return;

        // This method must eventually be fixed
        // inside EnemyAI1.cs.
        enemy.Enrage(pages);
    }

    // =========================================
    // RESTORE SAVED PROGRESS
    // =========================================

    public void RestoreState(
        int savedPages,
        bool hasKey)
    {
        pages = Mathf.Clamp(
            savedPages,
            0,
            Mathf.Max(1, totalPages)
        );

        HasKey = hasKey;

        keySpawned = hasKey;

        ApplyEnemyDifficulty();

        if (barricades != null)
        {
            for (int i = 0; i < pages &&
                 i < barricades.Length; i++)
            {
                if (barricades[i] != null)
                {
                    barricades[i].SetActive(true);
                }
            }
        }

        Debug.Log(
            "[GameManager] Restored " +
            pages + "/" + totalPages +
            " music notes."
        );
    }

    // =========================================
    // MUSIC BOX ACTIVATION
    // =========================================

    public bool ActivateMusicBox(
        Vector3 keyPosition,
        Quaternion keyRotation)
    {
        if (ended ||
            MusicBoxFound ||
            HasKey ||
            keySpawned)
        {
            return false;
        }

        if (!HasAllMusicNotes)
        {
            Debug.LogWarning(
                "[MusicBox] Not enough music notes: " +
                pages + "/" + totalPages
            );

            ShowHint(
                "Find all 3 music notes.",
                3f
            );

            return false;
        }

        if (keyPrefab == null)
        {
            Debug.LogWarning(
                "[GameManager] Key Prefab is missing!",
                this
            );

            return false;
        }

        // Spawn the key at the Music Box,
        // not at a random fireplace location.
        GameObject spawnedKey = Instantiate(
            keyPrefab,
            keyPosition,
            keyRotation
        );

        if (spawnedKey == null)
        {
            Debug.LogError(
                "[GameManager] Key failed to spawn!"
            );

            return false;
        }

        keySpawned = true;
        MusicBoxFound = true;

        Debug.Log(
            "[GameManager] Music Box activated! " +
            "Exit Key spawned at " + keyPosition
        );

        AutoSaveSoon();

        return true;
    }

    // =========================================
    // OLD MUSIC BOX COMPATIBILITY
    // =========================================

    public void CollectMusicBox()
    {
        if (!HasAllMusicNotes || MusicBoxFound)
            return;

        if (keySpawned || HasKey)
        {
            MusicBoxFound = true;
            AutoSaveSoon();
            return;
        }

        if (SpawnKey())
        {
            MusicBoxFound = true;
            AutoSaveSoon();
        }
    }

    public void RestoreMusicBox(bool found)
    {
        MusicBoxFound = found;
    }

    public void MarkKeySpawned()
    {
        keySpawned = true;
    }

    // =========================================
    // FALLBACK KEY SPAWNING
    // =========================================

    private bool SpawnKey()
    {
        if (keySpawned || HasKey)
            return false;

        if (keyPrefab == null ||
            keySpawnPoints == null ||
            keySpawnPoints.Length == 0)
        {
            Debug.LogWarning(
                "[GameManager] No Key Prefab " +
                "or Key Spawn Points assigned.",
                this
            );

            return false;
        }

        // Find a valid spawn point.
        Transform selectedPoint = null;

        foreach (Transform point in keySpawnPoints)
        {
            if (point != null)
            {
                selectedPoint = point;
                break;
            }
        }

        if (selectedPoint == null)
        {
            Debug.LogWarning(
                "[GameManager] All Key Spawn Points " +
                "are missing."
            );

            return false;
        }

        GameObject key = Instantiate(
            keyPrefab,
            selectedPoint.position,
            selectedPoint.rotation
        );

        if (key == null)
            return false;

        keySpawned = true;

        return true;
    }

    // =========================================
    // KEY COLLECTION
    // =========================================

    public void CollectKey()
    {
        if (HasKey)
            return;

        HasKey = true;
        keySpawned = true;

        Debug.Log(
            "[GameManager] Exit Key collected! " +
            "Player can now escape."
        );

        AutoSaveSoon();
    }

    // =========================================
    // AUTO SAVE
    // =========================================

    private void AutoSaveSoon()
    {
        if (!autoSaveOnProgress || ended)
            return;

        CancelInvoke(nameof(DoAutoSave));

        Invoke(nameof(DoAutoSave), 0.3f);
    }

    private void DoAutoSave()
    {
        if (!ended)
        {
            SaveSystem.Save();
        }
    }

    // Kept for compatibility with existing scripts.
    public void ShowHint(
        string message,
        float seconds = 3f)
    {
        // Existing UI hook.
        // No visual message until a hint UI is connected.
    }

    // =========================================
    // WIN / LOSE SYSTEM
    // =========================================

    public void Win()
    {
        End(winPanel);
    }

    public void Lose()
    {
        if (ended)
            return;

        if (sequence != null && enemy != null)
        {
            ended = true;
            sequence.PlayCapture(enemy);
        }
        else
        {
            End(losePanel);
        }
    }

    public void PlayerDied()
    {
        if (ended)
            return;

        if (sequence != null)
        {
            ended = true;
            sequence.PlayDeath();
        }
        else
        {
            End(losePanel);
        }
    }

    private void End(GameObject panel)
    {
        if (ended)
            return;

        ended = true;

        if (panel == winPanel)
        {
            SaveSystem.DeleteSave();
            PlayerHealth.ResetPersisted();
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[GameManager] Win/Lose Panel " +
                "is not assigned!",
                this
            );
        }

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // =========================================
    // RESTART
    // =========================================

    public void Restart()
    {
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}
