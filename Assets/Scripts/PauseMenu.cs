using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause menu for "The Last Lullaby". Press Esc (or P) while playing to pause, press again to resume.
// Everything is built by this script at Play. Add it to an empty GameObject in the House scene
// (not on the Player).
public class PauseMenu : MonoBehaviour
{
    [Header("Scenes")]
    public string mainMenuScene = "MainMenu";

    [Header("Look")]
    public TMP_FontAsset font;              // optional: same font as the main menu
    public string title = "PAUSED";
    public float titleSize = 84f;
    public Vector2 titlePosition = new Vector2(150f, 70f);   // from the top-left corner (x right, y down)
    public Color titleColor = new Color(0.86f, 0.80f, 0.65f, 1f);
    public float fontSize = 46f;
    public float itemSpacing = 14f;
    public Vector2 position = new Vector2(150f, -40f);       // x from the left, y from the vertical center
    public Color normalColor = new Color(0.78f, 0.74f, 0.64f, 0.85f);
    public Color hoverColor = new Color(0.97f, 0.94f, 0.84f, 1f);
    [Range(0f, 1f)] public float dim = 0.78f;                // how dark the game gets behind the menu

    [Header("Button plates (optional)")]
    public bool usePlates = true;
    public string plateNormalResource = "Menu/btn_normal";   // Assets/Resources/Menu/...
    public string plateHoverResource = "Menu/btn_highlight";
    public string platePressedResource = "Menu/btn_pressed";
    public Vector2 plateSize = new Vector2(420f, 78f);
    public float plateFontSize = 38f;

    [Header("Audio (optional)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    class Item
    {
        public TMP_Text label;
        public Image line;
        public Image plateBase, plateHover, platePress;
        public bool pressed;
        public float press;
        public float t;
        public System.Action onClick;
    }

    readonly List<Item> items = new List<Item>();
    readonly List<Behaviour> frozen = new List<Behaviour>();

    int selected;
    bool paused, settingsOpen, howToPlayOpen, loading, platesOn;

    GameObject root, settingsPanel, howToPlayPanel;
    CanvasGroup rootGroup;
    Transform canvasRoot;
    TMP_Text fullscreenValue;
    AudioSource sfx;
    Sprite plateNormal, plateHover, platePressed;

    const string KeyVolume = "LL_Volume";
    const string KeyFullscreen = "LL_Fullscreen";

    const string HowLeft =
        "<color=#F7F0D6>GOAL</color>\n" +
        "Find the four torn diary pages hidden in the house. When the last one is read, a music box key appears somewhere. Take it to the front door and escape.\n\n" +
        "<color=#F7F0D6>CONTROLS</color>\n" +
        "W A S D     Move\n" +
        "Mouse     Look\n" +
        "Shift     Run (loud)\n" +
        "Ctrl     Crouch (quiet)\n" +
        "F     Flashlight\n" +
        "E     Pick up / Hide / Open\n" +
        "G     Throw a bottle";

    const string HowRight =
        "<color=#F7F0D6>SURVIVE</color>\n" +
        "She hears you. Running is loud, crouching is quiet.\n\n" +
        "Hide in a locker with E when she is close. She can't see you inside.\n\n" +
        "Throw a bottle to lure her away from where you are.\n\n" +
        "Batteries keep your flashlight alive. It flickers when it's almost out.\n\n" +
        "Every page you find makes her faster, and the house closes in around you.\n\n" +
        "Your progress is saved each time you find a page.";

    // ------------------------------------------------------------------ setup

    void Start()
    {
        EnsureEventSystem();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true;     // menu sounds still play while the game is paused

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

    void OnDestroy()
    {
        if (!paused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void BuildUI()
    {
        var cgo = new GameObject("PauseCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(cgo.transform, false);
        var rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = rr.offsetMax = Vector2.zero;
        rootGroup = root.GetComponent<CanvasGroup>();
        canvasRoot = root.transform;

        // dark overlay; also blocks clicks from reaching anything behind the menu
        var shade = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(canvasRoot, false);
        var sr = shade.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, dim);

        var head = MakeText(canvasRoot, "Title", title, titleSize, TextAlignmentOptions.TopLeft, titleColor);
        head.characterSpacing = 4f;
        var hr = head.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
        hr.pivot = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(1400f, titleSize * 1.4f);
        hr.anchoredPosition = new Vector2(titlePosition.x, -titlePosition.y);

        AddItem("RESUME", Resume);
        AddItem("SETTINGS", OpenSettings);
        AddItem("MAIN MENU", GoToMainMenu);
        AddItem("QUIT", QuitGame);

        BuildSettings();
        BuildHowToPlay();
    }

    void AddItem(string text, System.Action onClick)
    {
        int index = items.Count;
        float h = platesOn ? plateSize.y : fontSize + 24f;

        var row = new GameObject("Item_" + text, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(canvasRoot, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = platesOn ? plateSize : new Vector2(700f, h);
        rt.anchoredPosition = new Vector2(position.x, position.y - index * (h + itemSpacing));
        var bg = row.GetComponent<Image>();

        Image hover = null, press = null;
        if (platesOn)
        {
            bg.sprite = plateNormal;
            bg.color = Color.white;
            hover = MakeOverlay(row.transform, "PlateHover", plateHover);
            press = MakeOverlay(row.transform, "PlatePressed", platePressed);
        }
        else
        {
            bg.color = new Color(0, 0, 0, 0);   // invisible, but catches the mouse
        }

        float size = platesOn ? plateFontSize : fontSize;
        var align = platesOn ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        var label = MakeText(row.transform, "Label", text, size, align, normalColor);
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        Image line = null;
        if (!platesOn)
        {
            var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(row.transform, false);
            var lnr = lineGo.GetComponent<RectTransform>();
            lnr.anchorMin = lnr.anchorMax = new Vector2(0f, 0f);
            lnr.pivot = new Vector2(0f, 0f);
            lnr.anchoredPosition = new Vector2(0f, 4f);
            lnr.sizeDelta = new Vector2(0f, 2f);
            line = lineGo.GetComponent<Image>();
            line.color = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0.7f);
            line.raycastTarget = false;
        }

        var item = new Item
        {
            label = label, line = line, onClick = onClick,
            plateBase = platesOn ? bg : null, plateHover = hover, platePress = press
        };
        items.Add(item);

        AddEvent(row, EventTriggerType.PointerEnter, () =>
        {
            if (settingsOpen) return;
            if (selected != index) Play(hoverSound);
            selected = index;
        });
        AddEvent(row, EventTriggerType.PointerDown, () => item.pressed = !settingsOpen);
        AddEvent(row, EventTriggerType.PointerUp, () => item.pressed = false);
        AddEvent(row, EventTriggerType.PointerExit, () => item.pressed = false);
        AddEvent(row, EventTriggerType.PointerClick, () => Activate(index));
    }

    // ------------------------------------------------------------------ pause / resume

    bool CanPause()
    {
        if (loading || Time.timeScale <= 0f) return false;

        GameManager gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.IsEnded) return false;   // <-- idagdag ito
            if (gm.winPanel != null && gm.winPanel.activeInHierarchy) return false;
            if (gm.losePanel != null && gm.losePanel.activeInHierarchy) return false;
        }
        return true;
    }

    void Pause()
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

    void Resume()
    {
        paused = false;
        root.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        FreezePlayer(false);
    }

    // turns off the player's scripts while paused, so the mouse can't still turn the camera or pick things up
    void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            frozen.Clear();
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;

            foreach (MonoBehaviour b in player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (b == this || !b.enabled) continue;
                b.enabled = false;
                frozen.Add(b);
            }
        }
        else
        {
            foreach (Behaviour b in frozen)
                if (b != null) b.enabled = true;
            frozen.Clear();
        }
    }

    void GoToMainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError("Scene '" + mainMenuScene + "' is not in Build Settings (File > Build Profiles > Scene List) or the name is wrong.");
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        paused = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ------------------------------------------------------------------ input / animation

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (!paused)
            {
                if (CanPause()) Pause();
            }
            else Back();
            return;
        }

        if (!paused) return;

        rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, 1f, Time.unscaledDeltaTime * 8f);
        if (settingsOpen) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            Activate(selected);

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            float target = i == selected ? 1f : 0f;
            it.t = Mathf.MoveTowards(it.t, target, Time.unscaledDeltaTime * 6f);
            it.label.color = Color.Lerp(normalColor, hoverColor, it.t);

            if (it.plateBase != null)
            {
                if (it.plateHover != null) it.plateHover.color = new Color(1f, 1f, 1f, it.t);

                it.press = Mathf.MoveTowards(it.press, it.pressed ? 1f : 0f, Time.unscaledDeltaTime * 12f);
                if (it.platePress != null) it.platePress.color = new Color(1f, 1f, 1f, it.press);
            }
            else
            {
                var p = it.label.rectTransform.anchoredPosition;
                p.x = Mathf.Lerp(0f, 26f, it.t);
                it.label.rectTransform.anchoredPosition = p;

                it.line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, it.label.preferredWidth + 26f, it.t), 2f);
            }
        }
    }

    // Esc / P: one step back, and resume from the main pause screen
    void Back()
    {
        if (howToPlayOpen) CloseHowToPlay();
        else if (settingsOpen) CloseSettings();
        else Resume();
    }

    void Move(int dir)
    {
        int start = selected;
        selected = (selected + dir + items.Count) % items.Count;
        if (selected != start) Play(hoverSound);
    }

    void Activate(int index)
    {
        if (!paused || settingsOpen) return;
        Play(clickSound);
        items[index].onClick?.Invoke();
    }

    // ------------------------------------------------------------------ settings and how to play

    void OpenSettings()
    {
        settingsOpen = true;
        settingsPanel.SetActive(true);
    }

    void CloseSettings()
    {
        PlayerPrefs.Save();
        settingsOpen = false;
        howToPlayOpen = false;
        howToPlayPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    void OpenHowToPlay()
    {
        howToPlayOpen = true;
        howToPlayPanel.SetActive(true);
    }

    void CloseHowToPlay()
    {
        howToPlayOpen = false;
        howToPlayPanel.SetActive(false);
    }

    void BuildSettings()
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(canvasRoot, false);
        var pr = settingsPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(900f, 560f);
        pr.anchoredPosition = Vector2.zero;
        settingsPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.96f);

        var head = MakeText(settingsPanel.transform, "Title", "SETTINGS", fontSize, TextAlignmentOptions.Center, hoverColor);
        Place(head.rectTransform, new Vector2(0f, 210f), new Vector2(700f, 70f));

        // volume
        var volLabel = MakeText(settingsPanel.transform, "VolumeLabel", "VOLUME", fontSize * 0.7f, TextAlignmentOptions.Left, normalColor);
        Place(volLabel.rectTransform, new Vector2(-250f, 70f), new Vector2(300f, 60f));
        var slider = MakeSlider(settingsPanel.transform, new Vector2(300f, 40f));
        slider.GetComponent<RectTransform>().anchoredPosition = new Vector2(190f, 70f);
        slider.value = PlayerPrefs.GetFloat(KeyVolume, 1f);
        slider.onValueChanged.AddListener(v =>
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(KeyVolume, v);
        });

        // fullscreen
        var fsLabel = MakeText(settingsPanel.transform, "FullscreenLabel", "FULLSCREEN", fontSize * 0.7f, TextAlignmentOptions.Left, normalColor);
        Place(fsLabel.rectTransform, new Vector2(-250f, -10f), new Vector2(300f, 60f));
        fullscreenValue = ClickableText(settingsPanel.transform, "FullscreenValue", Screen.fullScreen ? "ON" : "OFF",
            fontSize * 0.7f, new Vector2(190f, -10f), new Vector2(200f, 60f), () =>
            {
                bool fs = !Screen.fullScreen;
                Screen.fullScreen = fs;
                PlayerPrefs.SetInt(KeyFullscreen, fs ? 1 : 0);
                fullscreenValue.text = fs ? "ON" : "OFF";
            });

        ClickableText(settingsPanel.transform, "HowToPlay", "HOW TO PLAY", fontSize * 0.7f, new Vector2(0f, -90f), new Vector2(420f, 60f), OpenHowToPlay);
        ClickableText(settingsPanel.transform, "Back", "BACK", fontSize * 0.8f, new Vector2(0f, -205f), new Vector2(300f, 70f), CloseSettings);

        settingsPanel.SetActive(false);
    }

    void BuildHowToPlay()
    {
        howToPlayPanel = new GameObject("HowToPlayPanel", typeof(RectTransform), typeof(Image));
        howToPlayPanel.transform.SetParent(canvasRoot, false);
        var pr = howToPlayPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(1400f, 780f);
        pr.anchoredPosition = Vector2.zero;
        howToPlayPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.97f);

        var head = MakeText(howToPlayPanel.transform, "Title", "HOW TO PLAY", fontSize, TextAlignmentOptions.Center, hoverColor);
        Place(head.rectTransform, new Vector2(0f, 320f), new Vector2(900f, 70f));

        MakeBody(howToPlayPanel.transform, "Left", HowLeft, new Vector2(-350f, 0f), new Vector2(620f, 560f));
        MakeBody(howToPlayPanel.transform, "Right", HowRight, new Vector2(350f, 0f), new Vector2(620f, 560f));

        ClickableText(howToPlayPanel.transform, "Back", "BACK", fontSize * 0.8f, new Vector2(0f, -330f), new Vector2(300f, 70f), CloseHowToPlay);

        howToPlayPanel.SetActive(false);
    }

    Slider MakeSlider(Transform parent, Vector2 size)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;

        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = new Vector2(0f, 0.4f); bgr.anchorMax = new Vector2(1f, 0.6f);
        bgr.offsetMin = bgr.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.23f, 1f);

        var fa = new GameObject("Fill Area", typeof(RectTransform));
        fa.transform.SetParent(go.transform, false);
        var far = fa.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.4f); far.anchorMax = new Vector2(1f, 0.6f);
        far.offsetMin = new Vector2(5f, 0f); far.offsetMax = new Vector2(-15f, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fa.transform, false);
        var fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0f); fr.anchorMax = new Vector2(0f, 1f);
        fr.sizeDelta = new Vector2(10f, 0f);
        fill.GetComponent<Image>().color = normalColor;

        var ha = new GameObject("Handle Slide Area", typeof(RectTransform));
        ha.transform.SetParent(go.transform, false);
        var har = ha.GetComponent<RectTransform>();
        har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one;
        har.offsetMin = new Vector2(10f, 0f); har.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(ha.transform, false);
        var hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(20f, 0f);
        var hi = handle.GetComponent<Image>();
        hi.color = hoverColor;

        var s = go.AddComponent<Slider>();
        s.fillRect = fr;
        s.handleRect = hr;
        s.targetGraphic = hi;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f;
        s.maxValue = 1f;
        return s;
    }

    // ------------------------------------------------------------------ small helpers

    TMP_Text MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.characterSpacing = 6f;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // wrapped body text that shrinks to fit its box
    TMP_Text MakeBody(Transform parent, string name, string text, Vector2 pos, Vector2 size)
    {
        float body = fontSize * 0.62f;
        var t = MakeText(parent, name, text, body, TextAlignmentOptions.TopLeft, normalColor);
        t.characterSpacing = 1f;
        t.lineSpacing = 8f;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 18f;
        t.fontSizeMax = body;
        Place(t.rectTransform, pos, size);
        return t;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    TMP_Text ClickableText(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), pos, box);
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        var label = MakeText(go.transform, "Label", text, size, TextAlignmentOptions.Center, normalColor);
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        AddEvent(go, EventTriggerType.PointerEnter, () => { label.color = hoverColor; Play(hoverSound); });
        AddEvent(go, EventTriggerType.PointerExit, () => label.color = normalColor);
        AddEvent(go, EventTriggerType.PointerClick, () => { Play(clickSound); onClick?.Invoke(); });
        return label;
    }

    Image MakeOverlay(Transform parent, string name, Sprite sprite)
    {
        if (sprite == null) return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = false;
        return img;
    }

    Sprite LoadSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        var tex = Resources.Load<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        return null;
    }

    static void AddEvent(GameObject go, EventTriggerType type, System.Action action)
    {
        var trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    void Play(AudioClip clip)
    {
        if (clip != null && sfx != null) sfx.PlayOneShot(clip, sfxVolume);
    }
}