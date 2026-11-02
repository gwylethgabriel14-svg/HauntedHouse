using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Objective")]
    public int totalPages = 4;
    public GameObject keyPrefab;
    public Transform[] keySpawnPoints;

    [Header("The House Changes")]
    public GameObject[] barricades;   // disabled in the scene; one turns on per page

    [Header("Refs")]
    public EnemyAI enemy;

    [Header("UI")]
    public TMP_Text pageText;
    public TMP_Text hintText;
    public GameObject winPanel;
    public GameObject losePanel;

    public bool HasKey { get; private set; }
    public int Pages => pages;          // used by the save system

    int pages;
    bool ended;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (enemy == null) enemy = FindFirstObjectByType<EnemyAI>();   // fixes an empty Enemy slot
        UpdateUI();
        ShowHint("Find all the pages.", 4f);
    }

    public void CollectPage()
    {
        pages++;
        if (enemy != null) enemy.Enrage(pages);

        // The House Changes: block one route per page
        int i = pages - 1;
        if (i < barricades.Length && barricades[i] != null)
            barricades[i].SetActive(true);

        UpdateUI();

        if (pages >= totalPages)
        {
            SpawnKey();
            ShowHint("That's all of them... now find the KEY!", 5f);
        }
        else
        {
            ShowHint($"Page {pages}/{totalPages}. Something moved in the house...", 4f);
        }
    }

    // called by the save system after "Continue"
    public void RestoreState(int savedPages, bool hasKey)
    {
        pages = savedPages;
        HasKey = hasKey;
        if (enemy != null) enemy.Enrage(pages);

        for (int i = 0; i < pages && i < barricades.Length; i++)
            if (barricades[i] != null) barricades[i].SetActive(true);

        UpdateUI();
        ShowHint("Game loaded.", 3f);
    }

    void SpawnKey()
    {
        if (keySpawnPoints == null || keySpawnPoints.Length == 0 || keyPrefab == null)
        { ShowHint("No key spawn points set!", 3f); return; }
        Transform p = keySpawnPoints[Random.Range(0, keySpawnPoints.Length)];
        Instantiate(keyPrefab, p.position, p.rotation);
    }

    public void CollectKey()
    {
        HasKey = true;
        ShowHint("You got the key! Run to the front door!", 5f);
    }

    public void ShowHint(string msg, float seconds = 3f)
    {
        hintText.text = msg;
        CancelInvoke(nameof(ClearHint));
        Invoke(nameof(ClearHint), seconds);
    }

    void ClearHint() { hintText.text = ""; }

    void UpdateUI() { pageText.text = $"Pages: {pages}/{totalPages}"; }

    public void Win()  { End(winPanel); }
    public void Lose() { End(losePanel); }

    void End(GameObject panel)
    {
        if (ended) return;
        ended = true;
        if (panel == winPanel) SaveSystem.DeleteSave();   // you escaped: the old save is no longer needed
        panel.SetActive(true);
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