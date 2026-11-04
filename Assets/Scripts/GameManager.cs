using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Objective")]
    public int totalPages = 4;
    public GameObject keyPrefab;
    public Transform[] keySpawnPoints;

    [Header("The House Changes")]
    public GameObject[] barricades;   // disabled in the scene; one turns on per page

    [Header("Music box")]
    public bool requireMusicBox = true;   // the key only appears after the music box is found

    [Header("Saving")]
    public bool autoSaveOnProgress = true;   // saves after each page, the key and the music box (makes CONTINUE work)

    [Header("Refs")]
    public EnemyAI enemy;

    [Header("UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    public bool HasKey { get; private set; }
    public bool IsEnded => ended;       // true during the capture / death sequence and on the end screens
    public int Pages => pages;          // used by the save system
    public bool MusicBoxFound { get; private set; }

    int pages;
    bool ended;
    bool keySpawned;
    GameOverSequence sequence;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyAI>();   // fixes an empty Enemy slot

        // the capture / death sequence (created automatically if you didn't add it)
        sequence = FindFirstObjectByType<GameOverSequence>();
        if (sequence == null) sequence = new GameObject("GameOverSequence").AddComponent<GameOverSequence>();
    }

    public void CollectPage()
    {
        pages++;
        if (enemy != null) enemy.Enrage(pages);

        // The House Changes: block one route per page
        int i = pages - 1;
        if (barricades != null && i < barricades.Length && barricades[i] != null)
            barricades[i].SetActive(true);

        if (pages >= totalPages)
        {
            if (!requireMusicBox || MusicBoxFound) SpawnKey();
        }

        AutoSaveSoon();
    }

    // called by the save system after "Continue"
    public void RestoreState(int savedPages, bool hasKey)
    {
        pages = savedPages;
        HasKey = hasKey;
        keySpawned = hasKey;
        if (enemy != null) enemy.Enrage(pages);

        if (barricades != null)
            for (int i = 0; i < pages && i < barricades.Length; i++)
                if (barricades[i] != null) barricades[i].SetActive(true);
    }

    // called by the music box when the player takes it
    public void CollectMusicBox()
    {
        MusicBoxFound = true;
        if (pages >= totalPages && !keySpawned) SpawnKey();

        AutoSaveSoon();
    }

    // used by the save system
    public void RestoreMusicBox(bool found) { MusicBoxFound = found; }
    public void MarkKeySpawned() { keySpawned = true; }

    void SpawnKey()
    {
        if (keySpawned) return;
        if (keySpawnPoints == null || keySpawnPoints.Length == 0 || keyPrefab == null)
        {
            Debug.LogWarning("[GameManager] Walang key spawn points o key prefab.", this);
            return;
        }
        Transform p = keySpawnPoints[Random.Range(0, keySpawnPoints.Length)];
        Instantiate(keyPrefab, p.position, p.rotation);
        keySpawned = true;
    }

    public void CollectKey()
    {
        HasKey = true;
        AutoSaveSoon();
    }

    // saves a moment later, so the page / key you just took is already gone from the floor
    void AutoSaveSoon()
    {
        if (!autoSaveOnProgress || ended) return;
        CancelInvoke(nameof(DoAutoSave));
        Invoke(nameof(DoAutoSave), 0.3f);
    }

    void DoAutoSave()
    {
        if (!ended) SaveSystem.Save();
    }

    // Iniwan ko ito para hindi masira ang ibang script na tumatawag ng ShowHint.
    // Wala na itong ipinapakita sa screen.
    public void ShowHint(string msg, float seconds = 3f) { }

    public void Win() { End(winPanel); }

    // the main enemy caught the player
    public void Lose()
    {
        if (ended) return;
        if (sequence != null && enemy != null) { ended = true; sequence.PlayCapture(enemy); }
        else End(losePanel);
    }

    // the player lost the last life
    public void PlayerDied()
    {
        if (ended) return;
        if (sequence != null) { ended = true; sequence.PlayDeath(); }
        else End(losePanel);
    }

    void End(GameObject panel)
    {
        if (ended) return;
        ended = true;
        if (panel == winPanel) { SaveSystem.DeleteSave(); PlayerHealth.ResetPersisted(); }   // you escaped: start fresh next time
        if (panel != null) panel.SetActive(true);
        else Debug.LogWarning("[GameManager] Walang naka-assign na Win/Lose Panel sa inspector.", this);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hook this to the Restart button's OnClick
    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}