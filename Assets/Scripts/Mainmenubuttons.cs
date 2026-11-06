using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// THE LAST LULLABY - MAIN MENU
// Updated HOW TO PLAY for the current gameplay.
// Existing menu controls, save system and button style preserved.

public class MainMenuButtons : MonoBehaviour
{
    [Header("Scenes")]
    public string newGameScene = "Intro";
    public string continueScene = "House";

    [Header("Background & Title")]
    public Sprite background;
    public string backgroundResource = "Intro/07";
    public string title = "THE LAST LULLABY";
    public float titleSize = 84f;
    public Vector2 titlePosition = new Vector2(150f, 70f);
    public Color titleColor = new Color(0.86f, 0.80f, 0.65f, 1f);

    [Header("Button Style")]
    public bool useBoxButtons = true;
    public string normalSkin = "Menu/btn_normal";
    public string highlightSkin = "Menu/btn_highlight";
    public string pressedSkin = "Menu/btn_pressed";
    public Vector2 buttonSize = new Vector2(420f, 78f);
    public float boxedFontSize = 38f;

    [Header("Look")]
    public TMP_FontAsset font;
    public float fontSize = 46f;
    public float itemSpacing = 14f;
    public Vector2 position = new Vector2(150f, -70f);
    public Color normalColor = new Color(0.78f, 0.74f, 0.64f, 0.85f);
    public Color hoverColor = new Color(0.97f, 0.94f, 0.84f, 1f);
    [Range(0f, 1f)] public float leftShade = 0.85f;

    [Header("Audio")]
    public AudioClip menuMusic;
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    [Header("Old Menu")]
    public bool hideOldButtons = true;

    private class Item
    {
        public RectTransform rt;
        public TMP_Text label;
        public Image line;
        public Image bg;
        public float t;
        public bool enabled = true;
        public System.Action onClick;
    }

    private readonly List<Item> items = new List<Item>();

    private int selected;
    private int hovered = -1;
    private bool starting;
    private bool settingsOpen;
    private bool howToPlayOpen;
    private bool boxed;

    private GameObject settingsPanel;
    private GameObject howToPlayPanel;
    private GameObject generatedCanvas;

    private Transform canvasRoot;
    private Image fadeImage;
    private TMP_Text fullscreenValue;

    private AudioSource sfx;
    private AudioSource music;

    private Sprite skinNormal;
    private Sprite skinHighlight;
    private Sprite skinPressed;

    private const string KeyStarted = "LL_Started";
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
    // SAVED SETTINGS
    // =====================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(KeyVolume, 1f);

        int fs = PlayerPrefs.GetInt(KeyFullscreen, -1);

        if (fs >= 0)
            Screen.fullScreen = fs == 1;
    }

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hideOldButtons)
            HideOldUI();

        EnsureEventSystem();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        if (menuMusic != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = menuMusic;
            music.loop = true;
            music.volume = musicVolume;
            music.Play();
        }

        boxed = useBoxButtons && LoadSkin();

        BuildCanvas();
        BuildItems();
        BuildSettings();
    }

    private void OnDestroy()
    {
        if (generatedCanvas != null)
            Destroy(generatedCanvas);
    }

    private bool LoadSkin()
    {
        skinNormal = LoadSkinSprite(normalSkin);

        if (skinNormal == null)
        {
            Debug.LogWarning(
                "MainMenuButtons: normal box sprite not found. " +
                "Using underline buttons instead.");

            return false;
        }

        skinHighlight = LoadSkinSprite(highlightSkin);
        skinPressed = LoadSkinSprite(pressedSkin);

        if (skinHighlight == null)
            skinHighlight = skinNormal;

        if (skinPressed == null)
            skinPressed = skinNormal;

        return true;
    }

    private Sprite LoadSkinSprite(string path)
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
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void HideOldUI()
    {
        string[] names =
        {
            "PlayButton",
            "QuitButton",
            "Controls",
            "Subtitle"
        };

        Transform[] transforms =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform t in transforms)
        {
            foreach (string n in names)
            {
                if (t.name == n)
                    t.gameObject.SetActive(false);
            }
        }
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
    // CANVAS
    // =====================================================

    private void BuildCanvas()
    {
        generatedCanvas = new GameObject("MenuButtonsCanvas");

        Canvas canvas = generatedCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler =
            generatedCanvas.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        generatedCanvas.AddComponent<GraphicRaycaster>();

        canvasRoot = generatedCanvas.transform;

        Camera cam = Camera.main;

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        BuildBackground();

        Texture2D tex = new Texture2D(
            64,
            1,
            TextureFormat.RGBA32,
            false);

        tex.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < 64; i++)
        {
            float alpha =
                leftShade * Mathf.Pow(1f - i / 63f, 1.6f);

            tex.SetPixel(
                i,
                0,
                new Color(0f, 0f, 0f, alpha));
        }

        tex.Apply();

        GameObject shade = new GameObject(
            "LeftShade",
            typeof(RectTransform),
            typeof(RawImage));

        shade.transform.SetParent(canvasRoot, false);

        RectTransform sr =
            shade.GetComponent<RectTransform>();

        sr.anchorMin = Vector2.zero;
        sr.anchorMax = new Vector2(0.6f, 1f);
        sr.offsetMin = Vector2.zero;
        sr.offsetMax = Vector2.zero;

        RawImage raw = shade.GetComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;

        BuildTitle();

        GameObject fade = new GameObject(
            "Fade",
            typeof(RectTransform),
            typeof(Image));

        fade.transform.SetParent(canvasRoot, false);

        RectTransform fr =
            fade.GetComponent<RectTransform>();

        Stretch(fr);

        fadeImage = fade.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;

        fade.transform.SetAsLastSibling();
    }

    private void BuildBackground()
    {
        Sprite bg = background;

        if (bg == null &&
            !string.IsNullOrEmpty(backgroundResource))
        {
            bg = LoadSkinSprite(backgroundResource);
        }

        if (bg == null)
        {
            Debug.LogWarning(
                "MainMenuButtons: background image missing.");

            return;
        }

        GameObject go = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(canvasRoot, false);
        go.transform.SetAsFirstSibling();

        RectTransform rt =
            go.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);

        Image img = go.GetComponent<Image>();
        img.sprite = bg;
        img.raycastTarget = false;

        AspectRatioFitter fit =
            go.AddComponent<AspectRatioFitter>();

        fit.aspectMode =
            AspectRatioFitter.AspectMode.EnvelopeParent;

        fit.aspectRatio = bg.rect.width / bg.rect.height;
    }

    private void BuildTitle()
    {
        if (string.IsNullOrEmpty(title))
            return;

        TMP_Text text = MakeText(
            canvasRoot,
            "Title",
            title,
            titleSize,
            TextAlignmentOptions.TopLeft,
            titleColor);

        text.characterSpacing = 4f;

        RectTransform rt = text.rectTransform;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        rt.sizeDelta =
            new Vector2(1400f, titleSize * 1.4f);

        rt.anchoredPosition =
            new Vector2(titlePosition.x, -titlePosition.y);
    }

    // =====================================================
    // MAIN MENU BUTTONS
    // =====================================================

    private void BuildItems()
    {
        AddItem("NEW GAME", true, NewGame);
        AddItem("SETTINGS", true, OpenSettings);
        AddItem("QUIT", true, QuitGame);

        selected = 0;

        fadeImage.transform.SetAsLastSibling();
    }

    private void AddItem(
        string text,
        bool enabled,
        System.Action onClick)
    {
        int index = items.Count;

        float h = boxed ? buttonSize.y : fontSize + 24f;
        float w = boxed ? buttonSize.x : 700f;

        GameObject row = new GameObject(
            "Item_" + text,
            typeof(RectTransform),
            typeof(Image));

        row.transform.SetParent(canvasRoot, false);

        RectTransform rt =
            row.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);

        rt.sizeDelta = new Vector2(w, h);

        rt.anchoredPosition = new Vector2(
            position.x,
            position.y - index * (h + itemSpacing));

        Image bg = row.GetComponent<Image>();

        if (boxed)
        {
            bg.sprite = skinNormal;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;

            bg.color = enabled
                ? Color.white
                : new Color(1f, 1f, 1f, 0.4f);
        }
        else
        {
            bg.color = Color.clear;
        }

        TMP_Text label = MakeText(
            row.transform,
            "Label",
            text,
            boxed ? boxedFontSize : fontSize,
            boxed
                ? TextAlignmentOptions.Center
                : TextAlignmentOptions.Left,
            normalColor);

        Stretch(label.rectTransform);

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

        Image line = lineGo.GetComponent<Image>();

        line.color = new Color(
            hoverColor.r,
            hoverColor.g,
            hoverColor.b,
            0.7f);

        line.raycastTarget = false;

        if (boxed)
            lineGo.SetActive(false);

        Item item = new Item
        {
            rt = rt,
            label = label,
            line = line,
            bg = bg,
            enabled = enabled,
            onClick = onClick
        };

        items.Add(item);

        AddEvent(
            row,
            EventTriggerType.PointerEnter,
            () =>
            {
                if (starting || settingsOpen ||
                    howToPlayOpen || !item.enabled)
                    return;

                if (selected != index)
                    Play(hoverSound);

                selected = index;
                hovered = index;
            });

        AddEvent(
            row,
            EventTriggerType.PointerExit,
            () =>
            {
                if (hovered == index)
                    hovered = -1;
            });

        AddEvent(
            row,
            EventTriggerType.PointerClick,
            () => Activate(index));
    }

    // =====================================================
    // INPUT AND ANIMATION
    // =====================================================

    private void Update()
    {
        if (settingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (howToPlayOpen)
                    CloseHowToPlay();
                else
                    CloseSettings();
            }

            return;
        }

        if (!starting)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.S))
                Move(1);

            if (Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W))
                Move(-1);

            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
                Activate(selected);
        }

        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];

            float target =
                i == selected && it.enabled && !starting
                    ? 1f
                    : 0f;

            it.t = Mathf.MoveTowards(
                it.t,
                target,
                Time.unscaledDeltaTime * 6f);

            Color c =
                Color.Lerp(normalColor, hoverColor, it.t);

            if (!it.enabled)
                c.a = 0.25f;

            it.label.color = c;

            if (boxed)
            {
                bool isSelected =
                    i == selected && it.enabled && !starting;

                bool isPressed =
                    it.enabled && !starting &&
                    ((hovered == i &&
                      Input.GetMouseButton(0)) ||
                     (isSelected &&
                      (Input.GetKey(KeyCode.Return) ||
                       Input.GetKey(KeyCode.Space))));

                it.bg.sprite = isPressed
                    ? skinPressed
                    : isSelected
                        ? skinHighlight
                        : skinNormal;

                it.bg.color = it.enabled
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.4f);

                continue;
            }

            Vector2 p =
                it.label.rectTransform.anchoredPosition;

            p.x = Mathf.Lerp(0f, 26f, it.t);

            it.label.rectTransform.anchoredPosition = p;

            it.line.rectTransform.sizeDelta =
                new Vector2(
                    Mathf.Lerp(
                        0f,
                        it.label.preferredWidth + 26f,
                        it.t),
                    2f);
        }
    }

    private void Move(int direction)
    {
        if (items.Count == 0)
            return;

        int old = selected;

        for (int i = 0; i < items.Count; i++)
        {
            selected =
                (selected + direction + items.Count)
                % items.Count;

            if (items[selected].enabled)
                break;
        }

        if (selected != old)
            Play(hoverSound);
    }

    private void Activate(int index)
    {
        if (starting || settingsOpen)
            return;

        if (index < 0 || index >= items.Count)
            return;

        Item it = items[index];

        if (!it.enabled)
            return;

        Play(clickSound);
        it.onClick?.Invoke();
    }

    // =====================================================
    // NEW GAME / CONTINUE / QUIT
    // =====================================================

    private void NewGame()
    {
        SaveSystem.DeleteSave();
        PlayerHealth.ResetPersisted();

        PlayerPrefs.SetInt(KeyStarted, 1);
        PlayerPrefs.Save();

        StartCoroutine(LoadScene(newGameScene));
    }

    private void ContinueGame()
    {
        StartCoroutine(
            LoadScene(
                SaveSystem.PrepareContinue(continueScene)));
    }

    private void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator LoadScene(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError(
                "Scene '" + scene +
                "' is not in the Build Profile Scene List.");

            yield break;
        }

        starting = true;

        float t = 0f;
        float duration = 0.9f;
        float initialMusic =
            music != null ? music.volume : 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / duration);

            fadeImage.color =
                new Color(0f, 0f, 0f, k);

            if (music != null)
            {
                music.volume =
                    Mathf.Lerp(initialMusic, 0f, k);
            }

            yield return null;
        }

        SceneManager.LoadScene(scene);
    }

    // =====================================================
    // SETTINGS NAVIGATION
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

        settingsPanel.SetActive(false);

        if (howToPlayPanel != null)
            howToPlayPanel.SetActive(false);
    }

    private void OpenHowToPlay()
    {
        howToPlayOpen = true;

        settingsPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    private void CloseHowToPlay()
    {
        howToPlayOpen = false;

        howToPlayPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // =====================================================
    // BUILD SETTINGS
    // =====================================================

    private void BuildSettings()
    {
        settingsPanel = new GameObject(
            "SettingsPanel",
            typeof(RectTransform),
            typeof(Image));

        settingsPanel.transform.SetParent(canvasRoot, false);

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

        TMP_Text volumeLabel = MakeText(
            settingsPanel.transform,
            "VolumeLabel",
            "VOLUME",
            fontSize * 0.7f,
            TextAlignmentOptions.Left,
            normalColor);

        Place(
            volumeLabel.rectTransform,
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
                PlayerPrefs.SetFloat(KeyVolume, value);
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
                bool fs = !Screen.fullScreen;

                Screen.fullScreen = fs;

                PlayerPrefs.SetInt(
                    KeyFullscreen,
                    fs ? 1 : 0);

                fullscreenValue.text =
                    fs ? "ON" : "OFF";
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

        BuildHowToPlay();

        fadeImage.transform.SetAsLastSibling();
    }

    // =====================================================
    // BUILD UPDATED HOW TO PLAY
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
    // UI HELPERS
    // =====================================================

    private TMP_Text MakeBody(
        Transform parent,
        string name,
        string content,
        Vector2 pos,
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

        Place(text.rectTransform, pos, size);

        return text;
    }

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

    private static void Place(
        RectTransform rt,
        Vector2 pos,
        Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
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
        Vector2 pos,
        Vector2 box,
        System.Action onClick)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(parent, false);

        Place(
            go.GetComponent<RectTransform>(),
            pos,
            box);

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

    private static void AddEvent(
        GameObject go,
        EventTriggerType type,
        System.Action action)
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