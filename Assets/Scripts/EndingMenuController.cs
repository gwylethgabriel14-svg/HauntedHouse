
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// ======================================================
// THE LAST LULLABY - ENDING MENU CONTROLLER
//
// Automatically replaces GameManager's OLD WinPanel
// with the NEW fullscreen EndingMenuCanvas.
//
// New ending:
// - Fullscreen picture 09
// - Centered YOU ESCAPED
// - Centered MAIN MENU and QUIT GAME
// - Existing Pause Menu button textures
//
// Does not change key/music box quest logic.
// ======================================================

[DisallowMultipleComponent]
public class EndingMenuController : MonoBehaviour
{
    [Header("Generated UI")]
    public GameObject endingPanel;
    public Image backgroundImage;
    public Sprite endingPicture;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    [Header("Buttons")]
    public Button mainMenuButton;
    public Button quitGameButton;

    [Header("Scene Settings")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Ending Behavior")]
    public bool pauseGameOnEnding = true;
    public bool unlockCursor = true;

    [Header("Replace Old Win Screen")]
    public bool automaticallyReplaceOldWinPanel = true;
    public bool hideOldWinPanel = true;

    [Tooltip("Optional manual reference. Leave empty to use GameManager.winPanel.")]
    public GameObject oldWinPanelOverride;

    [Header("Optional Gameplay Scripts")]
    public Behaviour[] scriptsToDisable;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private GameManager gameManager;
    private Canvas endingCanvas;
    private bool endingShown;

    public bool EndingShown
    {
        get { return endingShown; }
    }

    // ======================================================
    // INITIALIZATION
    // ======================================================

    private void Awake()
    {
        endingShown = false;

        FindReferences();

        if (endingPicture == null)
        {
            endingPicture =
                Resources.Load<Sprite>("Intro/09");
        }

        RefreshContent();

        SetupButtons();

        // Keep ending hidden at the beginning.
        if (endingPanel != null)
        {
            endingPanel.SetActive(false);
        }
    }

    private void Start()
    {
        FindReferences();

        if (showDebugLogs)
        {
            Debug.Log(
                "[ENDING] New ending system ready. " +
                "Waiting for the old WinPanel.",
                this);
        }
    }

    // ======================================================
    // FIND REFERENCES
    // ======================================================

    private void FindReferences()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;

            if (gameManager == null)
            {
                gameManager =
                    FindFirstObjectByType<GameManager>();
            }
        }

        if (endingCanvas == null)
        {
            endingCanvas =
                GetComponent<Canvas>();

            if (endingCanvas == null)
            {
                endingCanvas =
                    GetComponentInParent<Canvas>();
            }
        }
    }

    private GameObject GetOldWinPanel()
    {
        if (oldWinPanelOverride != null)
            return oldWinPanelOverride;

        FindReferences();

        if (gameManager != null)
            return gameManager.winPanel;

        return null;
    }

    // ======================================================
    // AUTOMATIC WIN DETECTION
    // ======================================================

    private void LateUpdate()
    {
        if (!automaticallyReplaceOldWinPanel)
            return;

        if (endingShown)
        {
            HideOldWinScreen();
            return;
        }

        GameObject oldPanel = GetOldWinPanel();

        if (oldPanel == null)
            return;

        // GameManager activates its old WinPanel
        // when the player successfully escapes.
        if (oldPanel.activeInHierarchy)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "[ENDING] OLD WIN PANEL DETECTED! " +
                    "Opening the new Ending Menu.",
                    this);
            }

            ShowEnding();
        }
    }

    // ======================================================
    // REFRESH GENERATED CONTENT
    // ======================================================

    public void RefreshContent()
    {
        if (endingPicture == null)
        {
            endingPicture =
                Resources.Load<Sprite>("Intro/09");
        }

        if (backgroundImage != null)
        {
            if (endingPicture != null)
            {
                backgroundImage.sprite =
                    endingPicture;
            }

            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;
        }

        if (titleText != null)
        {
            titleText.text = "YOU ESCAPED";

            titleText.alignment =
                TextAlignmentOptions.Center;

            titleText.raycastTarget = false;
        }

        if (subtitleText != null)
        {
            subtitleText.text = "THE LAST LULLABY";

            subtitleText.alignment =
                TextAlignmentOptions.Center;

            subtitleText.raycastTarget = false;
        }
    }

    // ======================================================
    // BUTTON FUNCTIONS
    // ======================================================

    private void SetupButtons()
    {
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(
                ReturnToMainMenu);

            mainMenuButton.onClick.AddListener(
                ReturnToMainMenu);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveListener(
                QuitGame);

            quitGameButton.onClick.AddListener(
                QuitGame);
        }
    }

    // ======================================================
    // SHOW ENDING
    // ======================================================

    [ContextMenu("TEST - Show New Ending")]
    public void ShowEnding()
    {
        if (endingShown)
            return;

        if (endingPanel == null)
        {
            Debug.LogError(
                "[ENDING] EndingPanel is missing! " +
                "Assign it in the Inspector.",
                this);

            return;
        }

        endingShown = true;

        FindReferences();

        RefreshContent();

        // Show NEW ending first.
        endingPanel.SetActive(true);

        // Hide the old white-button ending.
        HideOldWinScreen();

        // Ensure new UI draws over everything.
        if (endingCanvas != null)
        {
            endingCanvas.enabled = true;

            endingCanvas.overrideSorting = true;

            endingCanvas.sortingOrder = 5000;
        }

        // Optional gameplay scripts to stop.
        if (scriptsToDisable != null)
        {
            foreach (Behaviour behaviour in
                scriptsToDisable)
            {
                if (behaviour == null)
                    continue;

                if (behaviour == this)
                    continue;

                behaviour.enabled = false;
            }
        }

        if (unlockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (pauseGameOnEnding)
        {
            Time.timeScale = 0f;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[ENDING] SUCCESS! Fullscreen picture 09 " +
                "and new menu are now displayed.",
                this);
        }
    }

    // ======================================================
    // HIDE OLD WIN PANEL
    // ======================================================

    private void HideOldWinScreen()
    {
        if (!hideOldWinPanel)
            return;

        GameObject oldPanel = GetOldWinPanel();

        if (oldPanel == null)
            return;

        // Never disable the new ending panel.
        if (oldPanel == endingPanel)
            return;

        // Never disable a parent that contains
        // our new EndingMenuCanvas.
        if (transform == oldPanel.transform ||
            transform.IsChildOf(oldPanel.transform))
        {
            Debug.LogWarning(
                "[ENDING] Old WinPanel is a parent " +
                "of the new ending. Cannot disable it.",
                this);

            return;
        }

        if (oldPanel.activeSelf)
        {
            oldPanel.SetActive(false);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[ENDING] Old white-button WinPanel hidden.",
                    this);
            }
        }
    }

    // ======================================================
    // RETURN TO MAIN MENU
    // ======================================================

    public void ReturnToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(
            mainMenuSceneName))
        {
            Debug.LogError(
                "[ENDING] Main Menu scene name is empty.",
                this);

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            mainMenuSceneName))
        {
            Debug.LogError(
                "[ENDING] Main Menu scene not found: " +
                mainMenuSceneName,
                this);

            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(
            mainMenuSceneName);
    }

    // ======================================================
    // QUIT GAME
    // ======================================================

    public void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (showDebugLogs)
        {
            Debug.Log(
                "[ENDING] Quit Game clicked.",
                this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ======================================================
    // CLEANUP
    // ======================================================

    private void OnDestroy()
    {
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(
                ReturnToMainMenu);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.RemoveListener(
                QuitGame);
        }
    }
}
