
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// =========================================================
// THE LAST LULLABY - OBJECTIVE GUIDE
//
// Creates an objectives HUD at the TOP-RIGHT.
//
// Tracks:
// 1. Collect 3 Music Notes - automatic via Inventory
// 2. Complete the Music Box - completion notification
// 3. Find the Exit Key - automatic via Inventory
// 4. Escape through the Front Door
//
// Press O to show/hide the objectives.
//
// Editor menu:
// The Last Lullaby > 16 - Setup Objectives Guide
//
// This script does NOT modify player inventory,
// enemy AI, music box, door or game progress.
// =========================================================

[DisallowMultipleComponent]
public class ObjectiveGuide : MonoBehaviour
{
    public static ObjectiveGuide Instance { get; private set; }

    [Header("Objective Settings")]
    [Min(1)]
    public int requiredMusicNotes = 3;

    public string exitKeyItemName = "Exit Key";

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.O;

    public bool showOnStart = true;
    public bool hideWhilePaused = true;

    [Header("HUD Position - Top Right")]
    public Vector2 position =
        new Vector2(-35f, -40f);

    public Vector2 panelSize =
        new Vector2(420f, 205f);

    [Header("Appearance")]
    public TMP_FontAsset font;

    public Color backgroundColor =
        new Color(0.055f, 0.045f, 0.045f, 0.91f);

    public Color headingColor =
        new Color(0.84f, 0.70f, 0.49f, 1f);

    public Color textColor =
        new Color(0.94f, 0.88f, 0.79f, 1f);

    public Color progressColor =
        new Color(0.72f, 0.54f, 0.34f, 1f);

    [Header("Current Progress - Read Only")]
    [SerializeField]
    private int currentStage;

    [SerializeField]
    private int musicNotesCollected;

    [SerializeField]
    private int musicNotesInserted;

    [SerializeField]
    private bool musicBoxCompleted;

    [SerializeField]
    private bool exitKeyFound;

    [SerializeField]
    private bool escaped;

    private Inventory inventory;

    private GameObject canvasObject;
    private GameObject panel;

    private TMP_Text headingText;
    private TMP_Text objectiveTitle;
    private TMP_Text descriptionText;
    private TMP_Text progressText;

    private Image progressFill;

    private bool playerWantsVisible;
    private float nextInventoryCheck;

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[OBJECTIVES] Duplicate ObjectiveGuide found.",
                this);

            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (Instance != this)
            return;

        inventory = Inventory.Instance;

        playerWantsVisible = showOnStart;

        BuildHUD();
        RefreshHUD();
    }

    private void Update()
    {
        if (Instance != this)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            playerWantsVisible = !playerWantsVisible;
        }

        if (Time.unscaledTime >= nextInventoryCheck)
        {
            nextInventoryCheck = Time.unscaledTime + 0.2f;

            CheckInventory();
            UpdateStage();
            RefreshHUD();
        }

        UpdateVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    // =====================================================
    // INVENTORY TRACKING
    // =====================================================

    private void CheckInventory()
    {
        if (inventory == null)
            inventory = Inventory.Instance;

        if (inventory == null)
            return;

        // Keep the highest count observed.
        // This prevents progress going backward
        // if notes leave the inventory later.

        int noteCount =
            Mathf.Max(0, inventory.CountMusicNotes());

        musicNotesCollected =
            Mathf.Max(
                musicNotesCollected,
                Mathf.Min(noteCount, requiredMusicNotes));

        // Automatically detect the key
        // when its name matches the inventory item.

        if (!string.IsNullOrEmpty(exitKeyItemName) &&
            inventory.HasItem(exitKeyItemName))
        {
            exitKeyFound = true;
        }
    }

    // =====================================================
    // OBJECTIVE PROGRESSION
    // =====================================================

    private void UpdateStage()
    {
        // Stage 0 - Collect notes
        if (currentStage == 0 &&
            musicNotesCollected >= requiredMusicNotes)
        {
            currentStage = 1;
        }

        // Stage 1 - Restore music box
        if (currentStage == 1 && musicBoxCompleted)
        {
            currentStage = 2;
        }

        // Stage 2 - Find exit key
        if (currentStage == 2 && exitKeyFound)
        {
            currentStage = 3;
        }

        // Stage 3 - Escape
        if (currentStage == 3 && escaped)
        {
            currentStage = 4;
        }
    }

    // =====================================================
    // PUBLIC GAMEPLAY NOTIFICATIONS
    //
    // Call these only when the actual gameplay
    // interaction succeeds.
    // =====================================================

    public void NotifyMusicNoteCollected()
    {
        musicNotesCollected =
            Mathf.Min(
                requiredMusicNotes,
                musicNotesCollected + 1);

        UpdateStage();
        RefreshHUD();
    }

    public void NotifyMusicNoteInserted()
    {
        musicNotesInserted =
            Mathf.Min(
                requiredMusicNotes,
                musicNotesInserted + 1);

        // This does NOT automatically complete the
        // music box unless all notes are inserted.

        if (musicNotesInserted >= requiredMusicNotes)
        {
            NotifyMusicBoxCompleted();
        }
        else
        {
            RefreshHUD();
        }
    }

    public void NotifyMusicBoxCompleted()
    {
        musicBoxCompleted = true;

        // A completed Music Box means that the
        // music notes requirement is satisfied.
        musicNotesCollected = requiredMusicNotes;

        UpdateStage();
        RefreshHUD();
    }

    public void NotifyExitKeyCollected()
    {
        exitKeyFound = true;

        UpdateStage();
        RefreshHUD();
    }

    public void NotifyEscaped()
    {
        escaped = true;

        UpdateStage();
        RefreshHUD();
    }

    // =====================================================
    // HUD CONTENT
    // =====================================================

    private void RefreshHUD()
    {
        if (panel == null)
            return;

        if (headingText != null)
            headingText.text = "CURRENT OBJECTIVE";

        string objective = "";
        string description = "";
        string progress = "";

        float fill = 0f;

        switch (currentStage)
        {
            case 0:
                objective = "The Broken Melody";

                description =
                    "Find the torn Music Notes " +
                    "hidden throughout the house.";

                progress =
                    "Music Notes  " +
                    musicNotesCollected +
                    " / " +
                    requiredMusicNotes;

                fill =
                    (float)musicNotesCollected /
                    requiredMusicNotes;

                break;

            case 1:
                objective = "Restore the Lullaby";

                description =
                    "Return to the Music Box " +
                    "and restore the melody.";

                progress =
                    "Notes Inserted  " +
                    musicNotesInserted +
                    " / " +
                    requiredMusicNotes;

                fill =
                    (float)musicNotesInserted /
                    requiredMusicNotes;

                break;

            case 2:
                objective = "The Way Out";

                description =
                    "The melody is restored. " +
                    "Search for the exit key.";

                progress = "Find the Exit Key";
                fill = 0f;

                break;

            case 3:
                objective = "Escape";

                description =
                    "Reach the front door " +
                    "and escape from Eleanor.";

                progress = "Get Out Alive";
                fill = 0f;

                break;

            default:
                objective = "You Escaped";

                description =
                    "The nightmare is finally over.";

                progress = "All Objectives Completed";
                fill = 1f;

                break;
        }

        objectiveTitle.text = objective;
        descriptionText.text = description;
        progressText.text = progress;

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(fill);

        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (panel == null)
            return;

        bool visible = playerWantsVisible;

        if (hideWhilePaused && Time.timeScale == 0f)
            visible = false;

        // Hide after escaping.
        if (escaped)
            visible = false;

        if (panel.activeSelf != visible)
            panel.SetActive(visible);
    }

    // =====================================================
    // BUILD UI
    // =====================================================

    private void BuildHUD()
    {
        canvasObject = new GameObject(
            "ObjectivesGuideCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(
            transform,
            false);

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.overrideSorting = true;
        canvas.sortingOrder = 140;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        // PANEL

        panel = new GameObject(
            "ObjectivesPanel",
            typeof(RectTransform),
            typeof(Image));

        panel.transform.SetParent(
            canvasObject.transform,
            false);

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);

        panelRect.anchoredPosition = position;
        panelRect.sizeDelta = panelSize;

        Image bg = panel.GetComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        // HEADER

        headingText = CreateText(
            panel.transform,
            "Header",
            "CURRENT OBJECTIVE",
            19f,
            headingColor,
            new Vector2(0f, -15f),
            new Vector2(-1f, -0.1f));

        headingText.fontStyle = FontStyles.Bold;
        headingText.characterSpacing = 2f;

        // TITLE

        objectiveTitle = CreateText(
            panel.transform,
            "ObjectiveTitle",
            "The Broken Melody",
            29f,
            textColor,
            new Vector2(0f, -46f),
            new Vector2(-1f, -0.1f));

        objectiveTitle.fontStyle = FontStyles.Bold;

        // DESCRIPTION

        descriptionText = CreateText(
            panel.transform,
            "Description",
            "",
            19f,
            textColor,
            new Vector2(0f, -92f),
            new Vector2(-1f, -0.1f));

        descriptionText.textWrappingMode =
            TextWrappingModes.Normal;

        descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin = 15f;
        descriptionText.fontSizeMax = 19f;

        // PROGRESS TEXT

        progressText = CreateText(
            panel.transform,
            "ProgressText",
            "Music Notes 0 / 3",
            18f,
            headingColor,
            new Vector2(0f, -151f),
            new Vector2(-1f, -0.1f));

        // PROGRESS BAR BACKGROUND

        GameObject progressBG = new GameObject(
            "ProgressBarBackground",
            typeof(RectTransform),
            typeof(Image));

        progressBG.transform.SetParent(
            panel.transform,
            false);

        RectTransform barRect =
            progressBG.GetComponent<RectTransform>();

        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);

        barRect.offsetMin = new Vector2(22f, -186f);
        barRect.offsetMax = new Vector2(-22f, -179f);

        Image barBG =
            progressBG.GetComponent<Image>();

        barBG.color =
            new Color(0.27f, 0.22f, 0.19f, 1f);

        barBG.raycastTarget = false;

        // PROGRESS BAR FILL

        GameObject fillGO = new GameObject(
            "ProgressFill",
            typeof(RectTransform),
            typeof(Image));

        fillGO.transform.SetParent(
            progressBG.transform,
            false);

        RectTransform fillRect =
            fillGO.GetComponent<RectTransform>();

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;

        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        progressFill =
            fillGO.GetComponent<Image>();

        progressFill.color = progressColor;
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod =
            Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        progressFill.raycastTarget = false;
    }

    private TMP_Text CreateText(
        Transform parent,
        string objectName,
        string content,
        float size,
        Color color,
        Vector2 positionOffset,
        Vector2 unused)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform));

        go.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            go.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        RectTransform rt = text.rectTransform;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        rt.anchoredPosition =
            new Vector2(0f, positionOffset.y);

        rt.sizeDelta =
            new Vector2(-44f, 55f);

        return text;
    }

    // =====================================================
    // EDITOR BUTTON / SETUP
    // =====================================================

#if UNITY_EDITOR

    [MenuItem(
        "The Last Lullaby/16 - Setup Objectives Guide")]
    private static void SetupObjectivesGuide()
    {
        ObjectiveGuide existing =
            FindFirstObjectByType<ObjectiveGuide>();

        if (existing != null)
        {
            Selection.activeGameObject =
                existing.gameObject;

            EditorGUIUtility.PingObject(existing);

            EditorUtility.DisplayDialog(
                "Objectives Guide",
                "An Objectives Guide already exists.\n\n" +
                "Selected the existing object.",
                "OK");

            return;
        }

        GameObject root = new GameObject(
            "ObjectivesGuide");

        Undo.RegisterCreatedObjectUndo(
            root,
            "Create Objectives Guide");

        ObjectiveGuide guide =
            root.AddComponent<ObjectiveGuide>();

        guide.requiredMusicNotes = 3;
        guide.exitKeyItemName = "Exit Key";

        guide.position =
            new Vector2(-35f, -40f);

        guide.panelSize =
            new Vector2(420f, 205f);

        EditorSceneManager.MarkSceneDirty(
            root.scene);

        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog(
            "The Last Lullaby",
            "Objectives Guide created!\n\n" +
            "The HUD will appear at the TOP-RIGHT " +
            "when you press Play.\n\n" +
            "Press O to hide/show objectives.\n\n" +
            "Save the scene.",
            "OK");
    }

#endif
}
