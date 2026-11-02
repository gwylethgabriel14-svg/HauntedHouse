using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Left-side menu for "The Last Lullaby": NEW GAME, CONTINUE, SETTINGS, QUIT.
// No button images needed: everything is built by this script at Play.
// Add it to an empty GameObject in the MainMenu scene.
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

    [Header("Look")]
    public TMP_FontAsset font;              // optional: an old serif font looks best
    public float fontSize = 46f;
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
        public float t;
        public bool enabled = true;
        public System.Action onClick;
    }

    readonly List<Item> items = new List<Item>();
    int selected;
    bool starting;
    bool settingsOpen;

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

        BuildCanvas();
        BuildItems();
        BuildSettings();
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
        float h = fontSize + 24f;

        var row = new GameObject("Item_" + text, typeof(RectTransform), typeof(Image));
        row.transform.SetParent(canvasRoot, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(700f, h);
        rt.anchoredPosition = new Vector2(position.x, position.y - index * (h + itemSpacing));
        var bg = row.GetComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);   // invisible, but catches the mouse

        var label = MakeText(row.transform, "Label", text, fontSize, TextAlignmentOptions.Left, normalColor);
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

        var item = new Item { rt = rt, label = label, line = line, enabled = enabled, onClick = onClick };
        items.Add(item);

        AddEvent(row, EventTriggerType.PointerEnter, () =>
        {
            if (starting || settingsOpen || !item.enabled) return;
            if (selected != index) Play(hoverSound);
            selected = index;
        });
        AddEvent(row, EventTriggerType.PointerClick, () => Activate(index));
    }

    // ------------------------------------------------------------------ input / animation

    void Update()
    {
        if (settingsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseSettings();
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
        settingsPanel.SetActive(false);
    }

    void BuildSettings()
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        settingsPanel.transform.SetParent(canvasRoot, false);
        var pr = settingsPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(900f, 520f);
        pr.anchoredPosition = Vector2.zero;
        settingsPanel.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.93f);

        var title = MakeText(settingsPanel.transform, "Title", "SETTINGS", fontSize, TextAlignmentOptions.Center, hoverColor);
        Place(title.rectTransform, new Vector2(0f, 190f), new Vector2(700f, 70f));

        // volume
        var volLabel = MakeText(settingsPanel.transform, "VolumeLabel", "VOLUME", fontSize * 0.7f, TextAlignmentOptions.Left, normalColor);
        Place(volLabel.rectTransform, new Vector2(-250f, 40f), new Vector2(300f, 60f));
        var slider = MakeSlider(settingsPanel.transform, new Vector2(300f, 40f));
        slider.GetComponent<RectTransform>().anchoredPosition = new Vector2(190f, 40f);
        slider.value = PlayerPrefs.GetFloat(KeyVolume, 1f);
        slider.onValueChanged.AddListener(v =>
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(KeyVolume, v);
        });

        // fullscreen
        var fsLabel = MakeText(settingsPanel.transform, "FullscreenLabel", "FULLSCREEN", fontSize * 0.7f, TextAlignmentOptions.Left, normalColor);
        Place(fsLabel.rectTransform, new Vector2(-250f, -60f), new Vector2(300f, 60f));
        fullscreenValue = ClickableText(settingsPanel.transform, "FullscreenValue", Screen.fullScreen ? "ON" : "OFF",
            fontSize * 0.7f, new Vector2(190f, -60f), new Vector2(200f, 60f), () =>
            {
                bool fs = !Screen.fullScreen;
                Screen.fullScreen = fs;
                PlayerPrefs.SetInt(KeyFullscreen, fs ? 1 : 0);
                fullscreenValue.text = fs ? "ON" : "OFF";
            });

        // back
        ClickableText(settingsPanel.transform, "Back", "BACK", fontSize * 0.8f, new Vector2(0f, -190f), new Vector2(300f, 70f), CloseSettings);

        settingsPanel.SetActive(false);
        fadeImage.transform.SetAsLastSibling();
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