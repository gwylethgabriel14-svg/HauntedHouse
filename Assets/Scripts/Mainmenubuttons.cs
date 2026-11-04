using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Left-side menu for "The Last Lullaby": NEW GAME, CONTINUE, SETTINGS, QUIT.
// Everything is built by this script at Play.
// Add it to an empty GameObject in the MainMenu scene.
//
// BUTTON STYLE
//   Use Box Buttons = ON  -> textured, rough-edged boxes (Resources/Menu/btn_normal, btn_highlight, btn_pressed)
//   Use Box Buttons = OFF -> plain text with an underline (the old style)
//   If the box sprites cannot be found, the menu falls back to the underline style and prints a warning.
public class MainMenuButtons : MonoBehaviour
{
    [Header("Scenes")]
    public string newGameScene = "Intro";   // the scene that plays the storyboard intro
    public string continueScene = "House";  // Continue skips the intro and goes straight here

    [Header("Background & Title (the script builds these too)")]
    public Sprite background;                       // drag your menu art here (optional)
    public string backgroundResource = "Intro/07";  // used when Background is empty (Assets/Resources/Intro/07)
    public string title = "THE LAST LULLABY";
    public float titleSize = 84f;
    public Vector2 titlePosition = new Vector2(150f, 70f);   // from the top-left corner (x right, y down)
    public Color titleColor = new Color(0.86f, 0.80f, 0.65f, 1f);

    [Header("Button Style")]
    public bool useBoxButtons = true;                       // textured boxes instead of underlined text
    public string normalSkin = "Menu/btn_normal";           // Assets/Resources/Menu/btn_normal
    public string highlightSkin = "Menu/btn_highlight";
    public string pressedSkin = "Menu/btn_pressed";
    public Vector2 buttonSize = new Vector2(420f, 78f);     // size of each box (reference 1920x1080)
    public float boxedFontSize = 38f;                       // text size inside the boxes

    [Header("Look")]
    public TMP_FontAsset font;              // optional: an old serif font looks best
    public float fontSize = 46f;            // used by the underline style, title row of settings, etc.
    public float itemSpacing = 14f;
    public Vector2 position = new Vector2(150f, -70f);  // x from the left, y from the vertical center
    public Color normalColor = new Color(0.78f, 0.74f, 0.64f, 0.85f);
    public Color hoverColor = new Color(0.97f, 0.94f, 0.84f, 1f);
    [Range(0f, 1f)] public float leftShade = 0.85f;     // dark gradient behind the buttons

    [Header("Audio (optional)")]
    public AudioClip menuMusic;             // e.g. sad_lullaby
    [Range(0f, 1f)] public float musicVolume = 0.4f;
    public AudioClip hoverSound;            // e.g. type_soft
    public AudioClip clickSound;            // e.g. type_key
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    [Header("Old menu")]
    public bool hideOldButtons = true;      // hides PlayButton, QuitButton, Controls, Subtitle if they exist

    class Item
    {
        public RectTransform rt;
        public TMP_Text label;
        public Image line;
        public Image bg;
        public float t;
        public bool enabled = true;
        public System.Action onClick;
    }

    readonly List<Item> items = new List<Item>();
    int selected;
    int hovered = -1;
    bool starting;
    bool settingsOpen;
    bool howToPlayOpen;
    GameObject howToPlayPanel;

    // box button skin
    bool boxed;
    Sprite skinNormal, skinHighlight, skinPressed;

    Transform canvasRoot;
    Image fadeImage;
    GameObject settingsPanel;
    TMP_Text fullscreenValue;
    AudioSource sfx, music;

    const string KeyStarted = "LL_Started";
    const string KeyVolume = "LL_Volume";
    const string KeyFullscreen = "LL_Fullscreen";

    // apply saved volume / fullscreen in every scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(KeyVolume, 1f);
        int fs = PlayerPrefs.GetInt(KeyFullscreen, -1);
        if (fs >= 0) Screen.fullScreen = fs == 1;
    }

    // ------------------------------------------------------------------ setup

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hideOldButtons) HideOldUI();
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

    // loads the three box sprites; returns false if the normal one is missing
    bool LoadSkin()
    {
        skinNormal = LoadSkinSprite(normalSkin);
        if (skinNormal == null)
        {
            Debug.LogWarning("MainMenuButtons: box button sprite not found at Resources/" + normalSkin +
                             ". Using the underline style instead.");
            return false;
        }

        skinHighlight = LoadSkinSprite(highlightSkin);
        skinPressed = LoadSkinSprite(pressedSkin);
        if (skinHighlight == null) skinHighlight = skinNormal;
        if (skinPressed == null) skinPressed = skinNormal;
        return true;
    }

    // works even if the file was not imported as a Sprite
    Sprite LoadSkinSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        return null;
    }

    void HideOldUI()
    {
        string[] names = { "PlayButton", "QuitButton", "Controls", "Subtitle" };
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var n in names)
                if (t.name == n) t.gameObject.SetActive(false);
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.SetParent(null);
    }

    void BuildCanvas()
    {
        var cgo = new GameObject("MenuButtonsCanvas");
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();
        canvasRoot = cgo.transform;

        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }

        BuildBackground();

        // dark gradient on the left so the text is readable
        var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < 64; i++)
        {
            float a = leftShade * Mathf.Pow(1f - i / 63f, 1.6f);
            tex.SetPixel(i, 0, new Color(0f, 0f, 0f, a));
        }
        tex.Apply();
        var shade = new GameObject("LeftShade", typeof(RectTransform), typeof(RawImage));
        shade.transform.SetParent(canvasRoot, false);
        var sr = shade.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = new Vector2(0.6f, 1f);
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        var raw = shade.GetComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;

        BuildTitle();

        // black overlay used for fading out
        var fade = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fade.transform.SetParent(canvasRoot, false);
        var fr = fade.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fadeImage = fade.GetComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
        fade.transform.SetAsLastSibling();
    }

    void BuildBackground()
    {
        Sprite bg = background;
        if (bg == null && !string.IsNullOrEmpty(backgroundResource))
        {
            bg = Resources.Load<Sprite>(backgroundResource);
            if (bg == null)
            {
                var tex = Resources.Load<Texture2D>(backgroundResource);
                if (tex != null) bg = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        if (bg == null)
        {
            Debug.LogWarning("MainMenuButtons: no background image. Drag your menu art into the Background field.");
            return;
        }

        var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot, false);
        go.transform.SetAsFirstSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = bg;
        img.raycastTarget = false;
        var fit = go.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // fills the screen, no stretching
        fit.aspectRatio = bg.rect.width / bg.rect.height;
    }

    void BuildTitle()
    {
        if (string.IsNullOrEmpty(title)) return;
        var t = MakeText(canvasRoot, "Title", title, titleSize, TextAlignmentOptions.TopLeft, titleColor);
        t.characterSpacing = 4f;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(1400f, titleSize * 1.4f);
        rt.anchoredPosition = new Vector2(titlePosition.x, -titlePosition.y);
    }

    void BuildItems()
    {
        bool hasSave = SaveSystem.HasSave();
        AddItem("NEW GAME", true, NewGame);
        AddItem("CONTINUE", hasSave, ContinueGame);
        AddItem("SETTINGS", true, OpenSettings);
        AddItem("QUIT", true, QuitGame);

        selected = 0;
        fadeImage.transform.SetAsLastSibling();
    }

    void AddItem(string text, bool enabled, System.Action onClick)
    {
        int index = items.Count;
        float h = boxed ? buttonSize.y : fontSize + 24f;
        float w = boxed ? buttonSize.x : 700f;

        var row = new GameObject("Item_" + text, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(canvasRoot, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(position.x, position.y - index * (h + itemSpacing));

        var bg = row.GetComponent<Image>();
        if (boxed)
        {
            bg.sprite = skinNormal;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
        else
        {
            bg.color = new Color(0, 0, 0, 0);   // invisible, but catches the mouse
        }

        var label = MakeText(row.transform, "Label", text,
                             boxed ? boxedFontSize : fontSize,
                             boxed ? TextAlignmentOptions.Center : TextAlignmentOptions.Left,
                             normalColor);
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(row.transform, false);
        var lnr = lineGo.GetComponent<RectTransform>();
        lnr.anchorMin = lnr.anchorMax = new Vector2(0f, 0f);
        lnr.pivot = new Vector2(0f, 0f);
        lnr.anchoredPosition = new Vector2(0f, 4f);
        lnr.sizeDelta = new Vector2(0f, 2f);
        var line = lineGo.GetComponent<Image>();
        line.color = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0.7f);
        line.raycastTarget = false;
        if (boxed) lineGo.SetActive(false);   // the box replaces the underline

        var item = new Item { rt = rt, label = label, line = line, bg = bg, enabled = enabled, onClick = onClick };
        items.Add(item);

        AddEvent(row, EventTriggerType.PointerEnter, () =>
        {
            if (starting || settingsOpen || !item.enabled) return;
            if (selected != index) Play(hoverSound);
            selected = index;
            hovered = index;
        });
        AddEvent(row, EventTriggerType.PointerExit, () =>
        {
            if (hovered == index) hovered = -1;
        });
        AddEvent(row, EventTriggerType.PointerClick, () => Activate(index));
    }

    // ------------------------------------------------------------------ input / animation

    void Update()
    {
        if (settingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (howToPlayOpen) CloseHowToPlay();
                else CloseSettings();
            }
            return;
        }

        if (!starting)
        {
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(+1);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Activate(selected);
        }

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            float target = (i == selected && it.enabled && !starting) ? 1f : 0f;
            it.t = Mathf.MoveTowards(it.t, target, Time.unscaledDeltaTime * 6f);

            Color c = Color.Lerp(normalColor, hoverColor, it.t);
            if (!it.enabled) c.a = 0.25f;
            it.label.color = c;

            if (boxed)
            {
                // box style: swap the sprite (normal / highlight / pressed), the text stays centred
                bool isSelected = i == selected && it.enabled && !starting;
                bool isPressed = it.enabled && !starting &&
                    ((hovered == i && Input.GetMouseButton(0)) ||
                     (isSelected && (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space))));

                it.bg.sprite = isPressed ? skinPressed : (isSelected ? skinHighlight : skinNormal);
                it.bg.color = it.enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                continue;
            }

            // underline style
            var p = it.label.rectTransform.anchoredPosition;
            p.x = Mathf.Lerp(0f, 26f, it.t);
            it.label.rectTransform.anchoredPosition = p;

            it.line.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, it.label.preferredWidth + 26f, it.t), 2f);
        }
    }

    void Move(int dir)
    {
        int start = selected;
        for (int k = 0; k < items.Count; k++)
        {
            selected = (selected + dir + items.Count) % items.Count;
            if (items[selected].enabled) break;
        }
        if (selected != start) Play(hoverSound);
    }

    void Activate(int index)
    {
        if (starting || settingsOpen) return;
        var it = items[index];
        if (!it.enabled) return;
        Play(clickSound);
        it.onClick?.Invoke();
    }

    // ------------------------------------------------------------------ actions

    void NewGame()
    {
        SaveSystem.DeleteSave();   // a new game erases the old save
        PlayerHealth.ResetPersisted();   // <-- idagdag ito
        PlayerPrefs.SetInt(KeyStarted, 1);
        PlayerPrefs.Save();
        StartCoroutine(LoadScene(newGameScene));
    }

    void ContinueGame()
    {
        StartCoroutine(LoadScene(SaveSystem.PrepareContinue(continueScene)));
    }

    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    IEnumerator LoadScene(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("Scene '" + scene + "' is not in Build Settings (File > Build Profiles > Scene List) or the name is wrong.");
            yield break;
        }

        starting = true;
        float t = 0f, dur = 0.9f;
        float m0 = music != null ? music.volume : 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            fadeImage.color = new Color(0, 0, 0, k);
            if (music != null) music.volume = Mathf.Lerp(m0, 0f, k);
            yield return null;
        }
        SceneManager.LoadScene(scene);
    }

    // ------------------------------------------------------------------ settings panel

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
        settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
    }

    void OpenHowToPlay()
    {
        howToPlayOpen = true;
        settingsPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    void CloseHowToPlay()
    {
        howToPlayOpen = false;
        howToPlayPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // same text as the pause menu's HOW TO PLAY
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

    void BuildSettings()
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(canvasRoot, false);
        var pr = settingsPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(900f, 560f);
        pr.anchoredPosition = Vector2.zero;
        settingsPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.96f);

        var title = MakeText(settingsPanel.transform, "Title", "SETTINGS", fontSize, TextAlignmentOptions.Center, hoverColor);
        Place(title.rectTransform, new Vector2(0f, 210f), new Vector2(700f, 70f));

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

        // how to play and back
        ClickableText(settingsPanel.transform, "HowToPlay", "HOW TO PLAY", fontSize * 0.7f,
            new Vector2(0f, -90f), new Vector2(420f, 60f), OpenHowToPlay);
        ClickableText(settingsPanel.transform, "Back", "BACK", fontSize * 0.8f,
            new Vector2(0f, -205f), new Vector2(300f, 70f), CloseSettings);

        settingsPanel.SetActive(false);

        BuildHowToPlay();
        fadeImage.transform.SetAsLastSibling();
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

        var title = MakeText(howToPlayPanel.transform, "Title", "HOW TO PLAY", fontSize, TextAlignmentOptions.Center, hoverColor);
        Place(title.rectTransform, new Vector2(0f, 320f), new Vector2(900f, 70f));

        MakeBody(howToPlayPanel.transform, "Left", HowLeft, new Vector2(-350f, 0f), new Vector2(620f, 560f));
        MakeBody(howToPlayPanel.transform, "Right", HowRight, new Vector2(350f, 0f), new Vector2(620f, 560f));

        ClickableText(howToPlayPanel.transform, "Back", "BACK", fontSize * 0.8f,
            new Vector2(0f, -330f), new Vector2(300f, 70f), CloseHowToPlay);

        howToPlayPanel.SetActive(false);
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