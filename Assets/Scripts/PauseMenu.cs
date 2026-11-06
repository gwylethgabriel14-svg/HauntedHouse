
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// THE LAST LULLABY - PAUSE MENU
// Updated HOW TO PLAY to match the Main Menu.
// Existing pause functionality and button design preserved.

public class PauseMenu : MonoBehaviour
{
    [Header("Scenes")]
    public string mainMenuScene = "MainMenu";

    [Header("Main Pause Menu")]
    public TMP_FontAsset font;
    public string title = "GAME PAUSED";
    public float titleSize = 70f;
    public float fontSize = 46f;

    public Color titleColor =
        new Color(0.86f, 0.80f, 0.65f, 1f);

    public Color normalColor =
        new Color(0.78f, 0.74f, 0.64f, 0.85f);

    public Color hoverColor =
        new Color(0.97f, 0.94f, 0.84f, 1f);

    [Range(0f, 1f)]
    public float dim = 0.78f;

    [Header("Centered Layout")]
    public Vector2 menuOffset = Vector2.zero;
    public float titleY = 205f;
    public float firstButtonY = 75f;
    public float buttonSpacing = 85f;

    [Header("Button Plates")]
    public bool usePlates = true;
    public string plateNormalResource = "Menu/btn_normal";
    public string plateHoverResource = "Menu/btn_highlight";
    public string platePressedResource = "Menu/btn_pressed";

    public Vector2 plateSize = new Vector2(420f, 78f);
    public float plateFontSize = 38f;

    [Header("Audio")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Range(0f, 1f)]
    public float sfxVolume = 0.5f;

    private class Item
    {
        public TMP_Text label;
        public Image line;
        public Image plateBase;
        public Image plateHover;
        public Image platePress;

        public bool pressed;
        public float press;
        public float t;

        public Action onClick;
    }

    private readonly List<Item> items =
        new List<Item>();

    private readonly List<Behaviour> frozen =
        new List<Behaviour>();

    private int selected;

    private bool paused;
    private bool settingsOpen;
    private bool howToPlayOpen;
    private bool loading;
    private bool platesOn;

    private GameObject root;
    private GameObject settingsPanel;
    private GameObject howToPlayPanel;
    private GameObject pauseCanvas;

    private CanvasGroup rootGroup;
    private Transform canvasRoot;

    private TMP_Text fullscreenValue;
    private AudioSource sfx;

    private Sprite plateNormal;
    private Sprite plateHover;
    private Sprite platePressed;

    private const string KeyVolume = "LL_Volume";
    private const string KeyFullscreen = "LL_Fullscreen";

    // =====================================================
    // UPDATED HOW TO PLAY - LEFT COLUMN
    // =====================================================

    private const string HowLeft =
        "<color=#F7F0D6>YOUR OBJECTIVE</color>\n" +
        "Explore the house and find 3 torn Music Notes. " +
        "Insert them into the Music Box to complete " +
        "the melody. Find the exit key and escape " +
        "through the front door.\n\n" +

        "<color=#F7F0D6>CONTROLS</color>\n" +
        "W A S D       Move\n" +
        "Mouse         Look around\n" +
        "Shift         Run (loud)\n" +
        "Ctrl          Crouch (quiet)\n" +
        "E             Interact / Pick up / Hide\n" +
        "F             Flashlight\n" +
        "1-9 / Scroll  Select inventory slot\n" +
        "G             Throw bottle\n" +
        "Esc / P       Pause game";

    // =====================================================
    // UPDATED HOW TO PLAY - RIGHT COLUMN
    // =====================================================

    private const string HowRight =
        "<color=#F7F0D6>ITEMS & EQUIPMENT</color>\n" +
        "Tranquilizer Gun: Equip from the hotbar. " +
        "Left-click to shoot; press R to reload. " +
        "DARTS shows the remaining magazine shots.\n\n" +

        "Bear Traps: Equip from your hotbar and " +
        "left-click to place them on the floor.\n\n" +

        "Find spare darts, traps and flashlight " +
        "batteries while exploring.\n\n" +

        "<color=#F7F0D6>SURVIVAL TIPS</color>\n" +
        "Eleanor hears noise. Run only when needed, " +
        "crouch to stay quiet, and hide in lockers.\n\n" +

        "Use bottles to distract her. Search for " +
        "diary pages to uncover the house's story.\n\n" +

        "Protect your health and limited lives.";

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Start()
    {
        EnsureEventSystem();

        sfx = GetComponent<AudioSource>();

        if (sfx == null)
            sfx = gameObject.AddComponent<AudioSource>();

        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true;

        AudioListener.volume =
            PlayerPrefs.GetFloat(KeyVolume, 1f);

        if (usePlates)
        {
            plateNormal = LoadSprite(plateNormalResource);
            plateHover = LoadSprite(plateHoverResource);
            platePressed = LoadSprite(platePressedResource);

            platesOn = plateNormal != null;
        }

        BuildUI();

        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (paused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        if (pauseCanvas != null)
            Destroy(pauseCanvas);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
    }

    // =====================================================
    // BUILD MAIN PAUSE UI
    // =====================================================

    private void BuildUI()
    {
        items.Clear();

        pauseCanvas = new GameObject("PauseCanvas");

        Canvas canvas =
            pauseCanvas.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 100;

        CanvasScaler scaler =
            pauseCanvas.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        pauseCanvas.AddComponent<GraphicRaycaster>();

        root = new GameObject(
            "Root",
            typeof(RectTransform),
            typeof(CanvasGroup));

        root.transform.SetParent(
            pauseCanvas.transform,
            false);

        RectTransform rr =
            root.GetComponent<RectTransform>();

        Stretch(rr);

        rootGroup = root.GetComponent<CanvasGroup>();
        canvasRoot = root.transform;

        // Dark overlay

        GameObject shade = new GameObject(
            "Dim",
            typeof(RectTransform),
            typeof(Image));

        shade.transform.SetParent(canvasRoot, false);

        RectTransform sr =
            shade.GetComponent<RectTransform>();

        Stretch(sr);

        shade.GetComponent<Image>().color =
            new Color(0f, 0f, 0f, dim);

        // Pause title

        TMP_Text head = MakeText(
            canvasRoot,
            "Title",
            title,
            titleSize,
            TextAlignmentOptions.Center,
            titleColor);

        head.characterSpacing = 4f;

        Place(
            head.rectTransform,
            new Vector2(
                menuOffset.x,
                titleY + menuOffset.y),
            new Vector2(1100f, 120f));

        // Main menu buttons

        AddItem("RESUME", Resume);
        AddItem("SETTINGS", OpenSettings);
        AddItem("MAIN MENU", GoToMainMenu);
        AddItem("QUIT", QuitGame);

        BuildSettings();
        BuildHowToPlay();
    }

    // =====================================================
    // BUILD MENU BUTTON
    // =====================================================

    private void AddItem(
        string text,
        Action onClick)
    {
        int index = items.Count;

        float h = platesOn
            ? plateSize.y
            : fontSize + 24f;

        GameObject row = new GameObject(
            "Item_" + text,
            typeof(RectTransform),
            typeof(Image));

        row.transform.SetParent(canvasRoot, false);

        RectTransform rt =
            row.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.sizeDelta = platesOn
            ? plateSize
            : new Vector2(700f, h);

        rt.anchoredPosition = new Vector2(
            menuOffset.x,
            firstButtonY +
            menuOffset.y -
            index * buttonSpacing);

        Image bg = row.GetComponent<Image>();

        Image hover = null;
        Image press = null;

        if (platesOn)
        {
            bg.sprite = plateNormal;
            bg.color = Color.white;

            hover = MakeOverlay(
                row.transform,
                "PlateHover",
                plateHover);

            press = MakeOverlay(
                row.transform,
                "PlatePressed",
                platePressed);
        }
        else
        {
            bg.color = Color.clear;
        }

        float size = platesOn
            ? plateFontSize
            : fontSize;

        TMP_Text label = MakeText(
            row.transform,
            "Label",
            text,
            size,
            TextAlignmentOptions.Center,
            normalColor);

        Stretch(label.rectTransform);

        Image line = null;

        if (!platesOn)
        {
            GameObject lineGo = new GameObject(
                "Line",
                typeof(RectTransform),
                typeof(Image));

            lineGo.transform.SetParent(row.transform, false);

            RectTransform lnr =
                lineGo.GetComponent<RectTransform>();

            lnr.anchorMin = Vector2.zero;
            lnr.anchorMax = Vector2.zero;
            lnr.pivot = Vector2.zero;
            lnr.anchoredPosition = new Vector2(0f, 4f);
            lnr.sizeDelta = new Vector2(0f, 2f);

            line = lineGo.GetComponent<Image>();

            line.color = new Color(
                hoverColor.r,
                hoverColor.g,
                hoverColor.b,
                0.7f);

            line.raycastTarget = false;
        }

        Item item = new Item
        {
            label = label,
            line = line,
            onClick = onClick,
            plateBase = platesOn ? bg : null,
            plateHover = hover,
            platePress = press
        };

        items.Add(item);

        AddEvent(
            row,
            EventTriggerType.PointerEnter,
            () =>
            {
                if (settingsOpen || howToPlayOpen)
                    return;

                if (selected != index)
                    Play(hoverSound);

                selected = index;
            });

        AddEvent(
            row,
            EventTriggerType.PointerDown,
            () =>
            {
                item.pressed =
                    !settingsOpen && !howToPlayOpen;
            });

        AddEvent(
            row,
            EventTriggerType.PointerUp,
            () => item.pressed = false);

        AddEvent(
            row,
            EventTriggerType.PointerExit,
            () => item.pressed = false);

        AddEvent(
            row,
            EventTriggerType.PointerClick,
            () => Activate(index));
    }

    // =====================================================
    // PAUSE AND RESUME
    // =====================================================

    private bool CanPause()
    {
        if (loading || Time.timeScale <= 0f)
            return false;

        GameManager gm = GameManager.Instance;

        if (gm != null)
        {
            if (gm.IsEnded)
                return false;

            if (gm.winPanel != null &&
                gm.winPanel.activeInHierarchy)
                return false;

            if (gm.losePanel != null &&
                gm.losePanel.activeInHierarchy)
                return false;
        }

        return true;
    }

    private void Pause()
    {
        paused = true;
        settingsOpen = false;
        howToPlayOpen = false;
        selected = 0;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        FreezePlayer(true);

        settingsPanel.SetActive(false);
        howToPlayPanel.SetActive(false);

        rootGroup.alpha = 0f;
        root.SetActive(true);
    }

    private void Resume()
    {
        paused = false;
        root.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        FreezePlayer(false);
    }

    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            frozen.Clear();

            PlayerController player =
                FindFirstObjectByType<PlayerController>();

            if (player == null)
                return;

            foreach (
                MonoBehaviour behaviour
                in player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (behaviour == this ||
                    !behaviour.enabled)
                    continue;

                behaviour.enabled = false;
                frozen.Add(behaviour);
            }
        }
        else
        {
            foreach (Behaviour behaviour in frozen)
            {
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            frozen.Clear();
        }
    }

    // =====================================================
    // MAIN MENU AND QUIT
    // =====================================================

    private void GoToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(
            mainMenuScene))
        {
            Debug.LogError(
                "Scene '" + mainMenuScene +
                "' is missing from the Scene List.");

            return;
        }

        loading = true;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        paused = false;

        SceneManager.LoadScene(mainMenuScene);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // =====================================================
    // INPUT AND ANIMATIONS
    // =====================================================

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.P))
        {
            if (!paused)
            {
                if (CanPause())
                    Pause();
            }
            else
            {
                Back();
            }

            return;
        }

        if (!paused)
            return;

        rootGroup.alpha = Mathf.MoveTowards(
            rootGroup.alpha,
            1f,
            Time.unscaledDeltaTime * 8f);

        if (settingsOpen || howToPlayOpen)
            return;

        if (Input.GetKeyDown(KeyCode.DownArrow) ||
            Input.GetKeyDown(KeyCode.S))
        {
            Move(1);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) ||
            Input.GetKeyDown(KeyCode.W))
        {
            Move(-1);
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space))
        {
            Activate(selected);
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];

            float target = i == selected ? 1f : 0f;

            it.t = Mathf.MoveTowards(
                it.t,
                target,
                Time.unscaledDeltaTime * 6f);

            it.label.color = Color.Lerp(
                normalColor,
                hoverColor,
                it.t);

            if (it.plateBase != null)
            {
                if (it.plateHover != null)
                {
                    it.plateHover.color =
                        new Color(1f, 1f, 1f, it.t);
                }

                it.press = Mathf.MoveTowards(
                    it.press,
                    it.pressed ? 1f : 0f,
                    Time.unscaledDeltaTime * 12f);

                if (it.platePress != null)
                {
                    it.platePress.color =
                        new Color(1f, 1f, 1f, it.press);
                }
            }
            else
            {
                Vector2 p =
                    it.label.rectTransform.anchoredPosition;

                p.x = 0f;

                it.label.rectTransform.anchoredPosition = p;

                if (it.line != null)
                {
                    it.line.rectTransform.sizeDelta =
                        new Vector2(
                            Mathf.Lerp(
                                0f,
                                it.label.preferredWidth + 26f,
                                it.t),
                            2f);
                }
            }
        }
    }

    private void Back()
    {
        if (howToPlayOpen)
        {
            CloseHowToPlay();
        }
        else if (settingsOpen)
        {
            CloseSettings();
        }
        else
        {
            Resume();
        }
    }

    private void Move(int direction)
    {
        if (items.Count == 0)
            return;

        int old = selected;

        selected =
            (selected + direction + items.Count)
            % items.Count;

        if (selected != old)
            Play(hoverSound);
    }

    private void Activate(int index)
    {
        if (!paused ||
            settingsOpen ||
            howToPlayOpen)
            return;

        if (index < 0 || index >= items.Count)
            return;

        Play(clickSound);

        items[index].onClick?.Invoke();
    }

    // =====================================================
    // SETTINGS
    // =====================================================

    private void OpenSettings()
    {
        settingsOpen = true;
        settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        PlayerPrefs.Save();

        settingsOpen = false;
        howToPlayOpen = false;

        howToPlayPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private void OpenHowToPlay()
    {
        howToPlayOpen = true;
        howToPlayPanel.SetActive(true);
    }

    private void CloseHowToPlay()
    {
        howToPlayOpen = false;
        howToPlayPanel.SetActive(false);
    }

    private void BuildSettings()
    {
        settingsPanel = new GameObject(
            "SettingsPanel",
            typeof(RectTransform),
            typeof(Image));

        settingsPanel.transform.SetParent(
            canvasRoot,
            false);

        RectTransform pr =
            settingsPanel.GetComponent<RectTransform>();

        Place(
            pr,
            Vector2.zero,
            new Vector2(900f, 560f));

        settingsPanel.GetComponent<Image>().color =
            new Color(0.02f, 0.02f, 0.02f, 0.96f);

        TMP_Text head = MakeText(
            settingsPanel.transform,
            "Title",
            "SETTINGS",
            fontSize,
            TextAlignmentOptions.Center,
            hoverColor);

        Place(
            head.rectTransform,
            new Vector2(0f, 210f),
            new Vector2(700f, 70f));

        TMP_Text volLabel = MakeText(
            settingsPanel.transform,
            "VolumeLabel",
            "VOLUME",
            fontSize * 0.7f,
            TextAlignmentOptions.Left,
            normalColor);

        Place(
            volLabel.rectTransform,
            new Vector2(-250f, 70f),
            new Vector2(300f, 60f));

        Slider slider = MakeSlider(
            settingsPanel.transform,
            new Vector2(300f, 40f));

        slider.GetComponent<RectTransform>()
            .anchoredPosition = new Vector2(190f, 70f);

        slider.value =
            PlayerPrefs.GetFloat(KeyVolume, 1f);

        slider.onValueChanged.AddListener(
            value =>
            {
                AudioListener.volume = value;

                PlayerPrefs.SetFloat(
                    KeyVolume,
                    value);
            });

        TMP_Text fsLabel = MakeText(
            settingsPanel.transform,
            "FullscreenLabel",
            "FULLSCREEN",
            fontSize * 0.7f,
            TextAlignmentOptions.Left,
            normalColor);

        Place(
            fsLabel.rectTransform,
            new Vector2(-250f, -10f),
            new Vector2(300f, 60f));

        fullscreenValue = ClickableText(
            settingsPanel.transform,
            "FullscreenValue",
            Screen.fullScreen ? "ON" : "OFF",
            fontSize * 0.7f,
            new Vector2(190f, -10f),
            new Vector2(200f, 60f),
            () =>
            {
                bool fullscreen = !Screen.fullScreen;

                Screen.fullScreen = fullscreen;

                PlayerPrefs.SetInt(
                    KeyFullscreen,
                    fullscreen ? 1 : 0);

                fullscreenValue.text =
                    fullscreen ? "ON" : "OFF";
            });

        ClickableText(
            settingsPanel.transform,
            "HowToPlay",
            "HOW TO PLAY",
            fontSize * 0.7f,
            new Vector2(0f, -90f),
            new Vector2(420f, 60f),
            OpenHowToPlay);

        ClickableText(
            settingsPanel.transform,
            "Back",
            "BACK",
            fontSize * 0.8f,
            new Vector2(0f, -205f),
            new Vector2(300f, 70f),
            CloseSettings);

        settingsPanel.SetActive(false);
    }

    // =====================================================
    // UPDATED HOW TO PLAY PANEL
    // =====================================================

    private void BuildHowToPlay()
    {
        howToPlayPanel = new GameObject(
            "HowToPlayPanel",
            typeof(RectTransform),
            typeof(Image));

        howToPlayPanel.transform.SetParent(
            canvasRoot,
            false);

        RectTransform pr =
            howToPlayPanel.GetComponent<RectTransform>();

        Place(
            pr,
            Vector2.zero,
            new Vector2(1400f, 780f));

        howToPlayPanel.GetComponent<Image>().color =
            new Color(0.02f, 0.02f, 0.02f, 0.97f);

        TMP_Text head = MakeText(
            howToPlayPanel.transform,
            "Title",
            "HOW TO PLAY",
            fontSize,
            TextAlignmentOptions.Center,
            hoverColor);

        Place(
            head.rectTransform,
            new Vector2(0f, 320f),
            new Vector2(900f, 70f));

        MakeBody(
            howToPlayPanel.transform,
            "Left",
            HowLeft,
            new Vector2(-350f, 0f),
            new Vector2(620f, 560f));

        MakeBody(
            howToPlayPanel.transform,
            "Right",
            HowRight,
            new Vector2(350f, 0f),
            new Vector2(620f, 560f));

        ClickableText(
            howToPlayPanel.transform,
            "Back",
            "BACK",
            fontSize * 0.8f,
            new Vector2(0f, -330f),
            new Vector2(300f, 70f),
            CloseHowToPlay);

        howToPlayPanel.SetActive(false);
    }

    // =====================================================
    // SLIDER BUILDER
    // =====================================================

    private Slider MakeSlider(
        Transform parent,
        Vector2 size)
    {
        GameObject go = new GameObject(
            "Slider",
            typeof(RectTransform));

        go.transform.SetParent(parent, false);

        RectTransform rt =
            go.GetComponent<RectTransform>();

        Place(rt, Vector2.zero, size);

        GameObject bg = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(Image));

        bg.transform.SetParent(go.transform, false);

        RectTransform br =
            bg.GetComponent<RectTransform>();

        br.anchorMin = new Vector2(0f, 0.4f);
        br.anchorMax = new Vector2(1f, 0.6f);
        br.offsetMin = Vector2.zero;
        br.offsetMax = Vector2.zero;

        bg.GetComponent<Image>().color =
            new Color(0.25f, 0.25f, 0.23f, 1f);

        GameObject fillArea = new GameObject(
            "Fill Area",
            typeof(RectTransform));

        fillArea.transform.SetParent(go.transform, false);

        RectTransform far =
            fillArea.GetComponent<RectTransform>();

        far.anchorMin = new Vector2(0f, 0.4f);
        far.anchorMax = new Vector2(1f, 0.6f);
        far.offsetMin = new Vector2(5f, 0f);
        far.offsetMax = new Vector2(-15f, 0f);

        GameObject fill = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(Image));

        fill.transform.SetParent(fillArea.transform, false);

        RectTransform fr =
            fill.GetComponent<RectTransform>();

        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(0f, 1f);
        fr.sizeDelta = new Vector2(10f, 0f);

        fill.GetComponent<Image>().color = normalColor;

        GameObject handleArea = new GameObject(
            "Handle Slide Area",
            typeof(RectTransform));

        handleArea.transform.SetParent(go.transform, false);

        RectTransform har =
            handleArea.GetComponent<RectTransform>();

        Stretch(har);

        har.offsetMin = new Vector2(10f, 0f);
        har.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(Image));

        handle.transform.SetParent(handleArea.transform, false);

        RectTransform hr =
            handle.GetComponent<RectTransform>();

        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(20f, 0f);

        Image hi = handle.GetComponent<Image>();
        hi.color = hoverColor;

        Slider slider = go.AddComponent<Slider>();

        slider.fillRect = fr;
        slider.handleRect = hr;
        slider.targetGraphic = hi;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        return slider;
    }

    // =====================================================
    // UI HELPERS
    // =====================================================

    private TMP_Text MakeText(
        Transform parent,
        string name,
        string content,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform));

        go.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            go.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.characterSpacing = 6f;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        return text;
    }

    private TMP_Text MakeBody(
        Transform parent,
        string name,
        string content,
        Vector2 position,
        Vector2 size)
    {
        float bodySize = fontSize * 0.62f;

        TMP_Text text = MakeText(
            parent,
            name,
            content,
            bodySize,
            TextAlignmentOptions.TopLeft,
            normalColor);

        text.characterSpacing = 1f;
        text.lineSpacing = 5f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = bodySize;

        Place(
            text.rectTransform,
            position,
            size);

        return text;
    }

    private static void Place(
        RectTransform rt,
        Vector2 position,
        Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private TMP_Text ClickableText(
        Transform parent,
        string name,
        string content,
        float size,
        Vector2 position,
        Vector2 boxSize,
        Action onClick)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(parent, false);

        Place(
            go.GetComponent<RectTransform>(),
            position,
            boxSize);

        go.GetComponent<Image>().color = Color.clear;

        TMP_Text label = MakeText(
            go.transform,
            "Label",
            content,
            size,
            TextAlignmentOptions.Center,
            normalColor);

        Stretch(label.rectTransform);

        AddEvent(
            go,
            EventTriggerType.PointerEnter,
            () =>
            {
                label.color = hoverColor;
                Play(hoverSound);
            });

        AddEvent(
            go,
            EventTriggerType.PointerExit,
            () => label.color = normalColor);

        AddEvent(
            go,
            EventTriggerType.PointerClick,
            () =>
            {
                Play(clickSound);
                onClick?.Invoke();
            });

        return label;
    }

    private Image MakeOverlay(
        Transform parent,
        string name,
        Sprite sprite)
    {
        if (sprite == null)
            return null;

        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(parent, false);

        RectTransform rt =
            go.GetComponent<RectTransform>();

        Stretch(rt);

        Image image = go.GetComponent<Image>();

        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;

        return image;
    }

    private Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        Sprite sprite = Resources.Load<Sprite>(path);

        if (sprite != null)
            return sprite;

        Texture2D tex = Resources.Load<Texture2D>(path);

        if (tex == null)
            return null;

        return Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static void AddEvent(
        GameObject go,
        EventTriggerType type,
        Action action)
    {
        EventTrigger trigger =
            go.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = go.AddComponent<EventTrigger>();

        EventTrigger.Entry entry =
            new EventTrigger.Entry();

        entry.eventID = type;

        entry.callback.AddListener(_ => action());

        trigger.triggers.Add(entry);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && sfx != null)
            sfx.PlayOneShot(clip, sfxVolume);
    }
}
