// Put this file in Assets/Editor/.
// Menu items:
//   Haunted House > BUILD MAIN MENU (styled)  - rebuilds Assets/Scenes/MainMenu.unity
//   Haunted House > ADD APPLY SETTINGS        - adds ApplySettings to the Player in House.unity
//
// The menu scene it builds:
//   Background (RawImage: Assets/UI/menu_bg.png if present, else near-black)
//   Vignette + LeftGradient        (textures generated once into Assets/UI/Generated)
//   Title "HAUNTED HOUSE ESCAPE"   (Anton SDF if available, fades in over 2 s)
//   PressPrompt                    (CanvasGroup, visible only in the PressKey state)
//   Version "v0.1.0"
//   MainPanel    PLAY / SETTINGS / QUIT
//   SettingsPanel  volume, mouse sensitivity, brightness, fullscreen, reset, back
//   BrightnessOverlay (dimming for the menu)
// It keeps the EventSystem and the SceneLoader object and saves the scene.
// Safe to run again: it clears the Canvas children and rebuilds them.

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class BuildMainMenu
{
    const string MenuScenePath   = "Assets/Scenes/MainMenu.unity";
    const string HouseScenePath  = "Assets/Scenes/House.unity";
    const string GeneratedFolder = "Assets/UI/Generated";
    const string BackgroundPath  = "Assets/UI/menu_bg.png";

    static readonly Color OffWhite        = new Color(0.85f, 0.82f, 0.76f);
    static readonly Color ButtonNormal    = new Color(0.20f, 0.18f, 0.16f, 0.85f);
    static readonly Color ButtonHighlight = new Color(0.35f, 0.31f, 0.27f, 0.95f);
    static readonly Color ButtonPressed   = new Color(0.50f, 0.44f, 0.38f, 1f);
    static readonly Color PanelBackground = new Color(0.12f, 0.11f, 0.10f, 0.92f);
    static readonly Color TrackColor      = new Color(0.06f, 0.06f, 0.06f, 0.90f);
    static readonly Color FillColor       = new Color(0.45f, 0.12f, 0.10f, 0.95f);

    static TMP_FontAsset font;
    static MainMenuController controller;

    // ======================================================================
    //  BUILD MAIN MENU
    // ======================================================================

    // [MenuItem("Haunted House/BUILD MAIN MENU (styled)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode first, then run Haunted House > BUILD MAIN MENU (styled).");
            return;
        }
        if (!File.Exists(MenuScenePath))
        {
            Debug.LogError("Assets/Scenes/MainMenu.unity not found. Run Haunted House > FINISH GAME (run all) once, then run this.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        // ---- Canvas (kept, emptied) ----
        var canvasGo = FindRoot("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                                      typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build main menu");
        }

        int removed = canvasGo.transform.childCount;
        for (int i = canvasGo.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(canvasGo.transform.GetChild(i).gameObject);

        // ---- EventSystem is kept, created only if it is missing ----
        if (FindRoot("EventSystem") == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build main menu");
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasGo);

        font = FindAntonFont();
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
            Debug.LogWarning("Anton SDF font asset not found in the project - using the default TMP font.");
        }

        var root = canvasGo.transform;

        // ---- controller first, so the buttons can point at it ----
        controller = canvasGo.GetComponent<MainMenuController>();
        if (controller == null) controller = Undo.AddComponent<MainMenuController>(canvasGo);

        var audio = canvasGo.GetComponent<AudioSource>();
        if (audio == null) audio = Undo.AddComponent<AudioSource>(canvasGo);
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.loop = false;

        var sceneLoader = FindRoot("SceneLoader") != null
            ? FindRoot("SceneLoader").GetComponent<SceneLoader>()
            : null;
        if (sceneLoader == null)
            Debug.LogWarning("No 'SceneLoader' object in the menu scene: PLAY and QUIT were not wired.");

        // --------------------------------------------------------------
        //  Background
        // --------------------------------------------------------------
        var bg = NewRect(root, "Background");
        Stretch(bg);
        var bgImage = bg.gameObject.AddComponent<RawImage>();
        bgImage.raycastTarget = false;
        var bgTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        if (bgTexture != null)
        {
            bgImage.texture = bgTexture;
            bgImage.color = Color.white;
        }
        else
        {
            bgImage.texture = null;
            bgImage.color = new Color(0.035f, 0.035f, 0.045f, 1f);   // near-black fallback
        }

        // ---- vignette (radial, transparent centre) ----
        var vignette = NewRect(root, "Vignette");
        Stretch(vignette);
        var vigImage = vignette.gameObject.AddComponent<Image>();
        vigImage.sprite = EnsureSprite("vignette", MakeVignetteTexture);
        vigImage.color = Color.white;
        vigImage.raycastTarget = false;

        // ---- dark gradient on the left, behind the buttons ----
        var gradient = NewRect(root, "LeftGradient");
        Stretch(gradient);
        var gradImage = gradient.gameObject.AddComponent<Image>();
        gradImage.sprite = EnsureSprite("left_gradient", MakeLeftGradientTexture);
        gradImage.color = Color.white;
        gradImage.raycastTarget = false;

        // --------------------------------------------------------------
        //  Title
        // --------------------------------------------------------------
        var titleRt = NewRect(root, "Title");
        Anchor(titleRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        titleRt.anchoredPosition = new Vector2(0f, 330f);
        titleRt.sizeDelta = new Vector2(1700f, 180f);
        var titleGroup = titleRt.gameObject.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;

        var titleText = titleRt.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(titleText, "HAUNTED HOUSE ESCAPE", 110f, TextAlignmentOptions.Center, OffWhite);
        titleText.characterSpacing = 3f;
        titleText.outlineWidth = 0.2f;
        titleText.outlineColor = new Color32(0, 0, 0, 200);

        // --------------------------------------------------------------
        //  Press any key prompt + version
        // --------------------------------------------------------------
        var promptRt = NewRect(root, "PressPrompt");
        Anchor(promptRt, new Vector2(1f, 0f), new Vector2(1f, 0f));
        promptRt.anchoredPosition = new Vector2(-80f, 110f);
        promptRt.sizeDelta = new Vector2(720f, 60f);
        var promptGroup = promptRt.gameObject.AddComponent<CanvasGroup>();
        promptGroup.blocksRaycasts = false;

        var promptLabel = NewRect(promptRt, "PromptLabel");
        Stretch(promptLabel);
        var promptText = promptLabel.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(promptText, "PRESS ANY KEY TO START", 26f, TextAlignmentOptions.Right, new Color(1f, 1f, 1f, 0.85f));
        promptText.characterSpacing = 6f;

        var versionRt = NewRect(root, "Version");
        Anchor(versionRt, new Vector2(1f, 0f), new Vector2(1f, 0f));
        versionRt.anchoredPosition = new Vector2(-40f, 28f);
        versionRt.sizeDelta = new Vector2(240f, 40f);
        var versionText = versionRt.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(versionText, "v0.1.0", 20f, TextAlignmentOptions.Right, new Color(1f, 1f, 1f, 0.28f));

        // --------------------------------------------------------------
        //  Main buttons
        // --------------------------------------------------------------
        var mainRt = NewRect(root, "MainPanel");
        Anchor(mainRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        mainRt.anchoredPosition = new Vector2(120f, 0f);
        mainRt.sizeDelta = new Vector2(380f, 260f);
        var mainGroup = mainRt.gameObject.AddComponent<CanvasGroup>();

        var play = MakeButton(mainRt, "Button_PLAY", "PLAY", new Vector2(0f, 90f),
                              new Vector2(380f, 70f), 34f, false);
        var settings = MakeButton(mainRt, "Button_SETTINGS", "SETTINGS", new Vector2(0f, 0f),
                                  new Vector2(380f, 70f), 34f, false);
        var quit = MakeButton(mainRt, "Button_QUIT", "QUIT", new Vector2(0f, -90f),
                              new Vector2(380f, 70f), 34f, false);

        if (sceneLoader != null)
        {
            UnityEventTools.AddPersistentListener(play.onClick, sceneLoader.PlayGame);
            UnityEventTools.AddPersistentListener(quit.onClick, sceneLoader.QuitGame);
        }
        UnityEventTools.AddPersistentListener(settings.onClick, controller.OpenSettings);

        // --------------------------------------------------------------
        //  Settings panel
        // --------------------------------------------------------------
        var setRt = NewRect(root, "SettingsPanel");
        Anchor(setRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        setRt.anchoredPosition = new Vector2(120f, 0f);
        setRt.sizeDelta = new Vector2(640f, 700f);

        var setBg = setRt.gameObject.AddComponent<Image>();
        setBg.color = PanelBackground;

        var setGroup = setRt.gameObject.AddComponent<CanvasGroup>();

        var setTitle = NewRect(setRt, "Title");
        Anchor(setTitle, new Vector2(0f, 1f), new Vector2(0f, 1f));
        setTitle.anchoredPosition = new Vector2(40f, -40f);
        setTitle.sizeDelta = new Vector2(560f, 60f);
        var setTitleText = setTitle.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(setTitleText, "SETTINGS", 44f, TextAlignmentOptions.Left, OffWhite);
        setTitleText.characterSpacing = 3f;
        setTitleText.outlineWidth = 0.15f;
        setTitleText.outlineColor = new Color32(0, 0, 0, 180);

        MakeRowLabel(setRt, "Master Volume", -112f);
        var volumeSlider = MakeSlider(setRt, "MasterVolumeSlider", new Vector2(40f, -156f),
                                      0f, 1f, GameSettings.DefaultMasterVolume);

        MakeRowLabel(setRt, "Mouse Sensitivity", -218f);
        var sensSlider = MakeSlider(setRt, "MouseSensSlider", new Vector2(40f, -262f),
                                    GameSettings.MinMouseSens, GameSettings.MaxMouseSens,
                                    GameSettings.DefaultMouseSens);

        MakeRowLabel(setRt, "Brightness", -324f);
        var brightSlider = MakeSlider(setRt, "BrightnessSlider", new Vector2(40f, -368f),
                                      -1f, 1f, GameSettings.DefaultBrightness);

        MakeRowLabel(setRt, "Fullscreen", -430f);
        var fullscreenToggle = MakeToggle(setRt, "FullscreenToggle", new Vector2(552f, -424f));

        var reset = MakeButton(setRt, "Button_RESET", "RESET TO DEFAULTS", new Vector2(40f, -500f),
                               new Vector2(560f, 60f), 30f, true);
        var back = MakeButton(setRt, "Button_BACK", "BACK", new Vector2(40f, -576f),
                              new Vector2(560f, 60f), 30f, true);

        UnityEventTools.AddPersistentListener(reset.onClick, controller.ResetSettings);
        UnityEventTools.AddPersistentListener(back.onClick, controller.BackToMain);

        // --------------------------------------------------------------
        //  Brightness dimming overlay (front-most)
        // --------------------------------------------------------------
        var overlay = NewRect(root, "BrightnessOverlay");
        Stretch(overlay);
        var overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);
        overlayImage.raycastTarget = false;
        overlayImage.enabled = false;

        // --------------------------------------------------------------
        //  Wire the controller fields
        // --------------------------------------------------------------
        var so = new SerializedObject(controller);
        SetRef(so, "pressKeyPrompt", promptGroup);
        SetRef(so, "mainPanel", mainGroup);
        SetRef(so, "settingsPanel", setGroup);
        SetRef(so, "titleGroup", titleGroup);
        SetRef(so, "promptText", promptText);
        SetRef(so, "brightnessOverlay", overlayImage);
        SetRef(so, "audioSource", audio);
        SetRef(so, "masterVolumeSlider", volumeSlider);
        SetRef(so, "mouseSensSlider", sensSlider);
        SetRef(so, "brightnessSlider", brightSlider);
        SetRef(so, "fullscreenToggle", fullscreenToggle);
        so.FindProperty("hoverClip").objectReferenceValue = null;
        so.FindProperty("clickClip").objectReferenceValue = null;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        // Saved starting state: prompt visible, both panels hidden, so nothing
        // flashes on the first frame before MainMenuController.Start runs.
        promptGroup.alpha = 1f;
        mainGroup.alpha = 0f;
        setGroup.alpha = 0f;
        mainGroup.interactable = false;
        mainGroup.blocksRaycasts = false;
        setGroup.interactable = false;
        setGroup.blocksRaycasts = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvasGo;

        Debug.Log("Main menu built: " + removed + " old Canvas children replaced. " +
                  "Font: " + (font != null ? font.name : "TMP default") + ". " +
                  "Background: " + (bgTexture != null ? BackgroundPath : "near-black fallback") + ".");
    }

    // ======================================================================
    //  ADD APPLY SETTINGS
    // ======================================================================

    [MenuItem("Haunted House/ADD APPLY SETTINGS")]
    public static void AddApplySettings()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode first, then run Haunted House > ADD APPLY SETTINGS.");
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != HouseScenePath)
        {
            if (!File.Exists(HouseScenePath))
            {
                Debug.LogError("Assets/Scenes/House.unity not found.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Single);
        }

        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("No 'Player' object in the House scene.");
            return;
        }

        var apply = player.GetComponent<ApplySettings>();
        bool added = false;
        if (apply == null)
        {
            apply = Undo.AddComponent<ApplySettings>(player);
            added = true;
        }

        var volume = FindGlobalVolume();
        if (volume != null)
        {
            Undo.RecordObject(apply, "Set Global Volume");
            apply.volume = volume;
        }
        else
        {
            Debug.LogWarning("No global Volume found in the scene: ApplySettings will look one up at runtime.");
        }

        EditorUtility.SetDirty(apply);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = player;

        Debug.Log("ApplySettings " + (added ? "added to" : "already on") + " Player; " +
                  "Volume = " + (volume != null ? volume.name : "not assigned") + ". Scene saved.");
    }

    // ======================================================================
    //  UI helpers
    // ======================================================================

    static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Build main menu");
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = min;
    }

    static void StyleText(TMP_Text text, string value, float size, TextAlignmentOptions align, Color color)
    {
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    static void MakeRowLabel(Transform parent, string label, float y)
    {
        var rt = NewRect(parent, "Label_" + label.Replace(" ", ""));
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f));
        rt.anchoredPosition = new Vector2(40f, y);
        rt.sizeDelta = new Vector2(560f, 34f);

        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(text, label, 30f, TextAlignmentOptions.Left, new Color(0.86f, 0.83f, 0.78f, 0.95f));
    }

    // fromTopLeft: anchored to the parent's top-left corner (settings panel rows)
    // instead of the parent's left-middle edge (main button column).
    static Button MakeButton(Transform parent, string name, string label, Vector2 pos,
                             Vector2 size, float fontSize, bool fromTopLeft)
    {
        var rt = NewRect(parent, name);
        Vector2 anchor = fromTopLeft ? new Vector2(0f, 1f) : new Vector2(0f, 0.5f);
        Anchor(rt, anchor, anchor);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var image = rt.gameObject.AddComponent<Image>();
        image.color = Color.white;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor      = ButtonNormal;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor     = ButtonPressed;
        colors.selectedColor    = ButtonNormal;
        colors.disabledColor    = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.08f;
        button.colors = colors;

        var labelRt = NewRect(rt, "Label");
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(20f, 0f);
        labelRt.offsetMax = new Vector2(-20f, 0f);

        var text = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        StyleText(text, label, fontSize, TextAlignmentOptions.Center, OffWhite);
        text.characterSpacing = 2f;
        text.outlineWidth = 0.12f;
        text.outlineColor = new Color32(235, 228, 216, 90);

        return button;
    }

    static Slider MakeSlider(Transform parent, string name, Vector2 pos, float min, float max,
                             float defaultValue)
    {
        var rt = NewRect(parent, name);
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f));
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(560f, 26f);

        var slider = rt.gameObject.AddComponent<Slider>();

        // track
        var track = NewRect(rt, "Background");
        track.anchorMin = new Vector2(0f, 0.35f);
        track.anchorMax = new Vector2(1f, 0.65f);
        track.offsetMin = Vector2.zero;
        track.offsetMax = Vector2.zero;
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = TrackColor;

        // fill
        var fillArea = NewRect(rt, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.35f);
        fillArea.anchorMax = new Vector2(1f, 0.65f);
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;

        var fill = NewRect(fillArea, "Fill");
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = FillColor;

        // handle
        var handleArea = NewRect(rt, "Handle Slide Area");
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        var handle = NewRect(handleArea, "Handle");
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(20f, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = OffWhite;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.value = Mathf.Clamp(defaultValue, min, max);

        return slider;
    }

    static Toggle MakeToggle(Transform parent, string name, Vector2 pos)
    {
        var rt = NewRect(parent, name);
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f));
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(48f, 48f);

        var toggle = rt.gameObject.AddComponent<Toggle>();

        var background = NewRect(rt, "Background");
        Stretch(background);
        var bgImage = background.gameObject.AddComponent<Image>();
        bgImage.color = TrackColor;

        var check = NewRect(background, "Checkmark");
        check.anchorMin = Vector2.zero;
        check.anchorMax = Vector2.one;
        check.offsetMin = new Vector2(8f, 8f);
        check.offsetMax = new Vector2(-8f, -8f);
        var checkImage = check.gameObject.AddComponent<Image>();
        checkImage.color = OffWhite;

        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;

        return toggle;
    }

    // ======================================================================
    //  Generated textures (saved as assets so the scene can reference them)
    // ======================================================================

    static Sprite EnsureSprite(string name, Func<Texture2D> make)
    {
        EnsureFolder("Assets/UI");
        EnsureFolder(GeneratedFolder);

        string path = GeneratedFolder + "/" + name + ".png";

        if (!File.Exists(path))
        {
            var texture = make();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Sprite ||
             importer.spriteImportMode != SpriteImportMode.Single ||
             !importer.alphaIsTransparency))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Transparent in the middle, black towards the edges.
    static Texture2D MakeVignetteTexture()
    {
        const int size = 256;
        const float inner = 0.40f;   // still clear up to this radius
        const float outerMax = 1.05f;
        const float strength = 0.95f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - centre) / centre;
                float dy = (y - centre) / centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.InverseLerp(inner, outerMax, distance);
                float alpha = Mathf.Pow(t, 1.6f) * strength;
                pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // Dark on the left, fading out to the right.
    static Texture2D MakeLeftGradientTexture()
    {
        const int width = 256;
        const int height = 4;
        const float strength = 0.88f;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float t = 1f - x / (float)(width - 1);
                float alpha = Mathf.Pow(t, 1.5f) * strength;
                pixels[y * width + x] = new Color(0f, 0f, 0f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    // ======================================================================
    //  Small helpers
    // ======================================================================

    static TMP_FontAsset FindAntonFont()
    {
        var direct = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Anton SDF.asset");
        if (direct != null) return direct;

        foreach (var guid in AssetDatabase.FindAssets("Anton SDF t:TMP_FontAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset != null) return asset;
        }
        return null;
    }

    static Volume FindGlobalVolume()
    {
        var volumes = UnityEngine.Object.FindObjectsByType<Volume>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Volume fallback = null;
        foreach (var volume in volumes)
        {
            if (!volume.isGlobal) continue;
            if (volume.name == "Global Volume") return volume;
            if (fallback == null) fallback = volume;
        }
        if (fallback == null && volumes.Length > 0) fallback = volumes[0];
        return fallback;
    }

    static GameObject FindRoot(string name)
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
    {
        var property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning("MainMenuController has no field '" + field + "'.");
            return;
        }
        property.objectReferenceValue = value;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
