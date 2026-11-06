
#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ======================================================
// THE LAST LULLABY
// ENDING MENU GENERATOR - TEXTURED BUTTON FIX
//
// Uses the ACTUAL Pause Menu button sprites:
//  Assets/Resources/Menu/btn_normal
//  Assets/Resources/Menu/btn_highlight
//  Assets/Resources/Menu/btn_pressed
//
// Automatically removes the previous generated canvas.
// Creates full-screen image 09, centered title,
// and textured MAIN MENU / QUIT buttons.
//
// Existing EndingMenuController.cs stays unchanged.
// ======================================================

public class EndingMenuSetupWindow : EditorWindow
{
    private const string CanvasName = "EndingMenuCanvas";

    private Sprite endingImage;
    private Sprite normalPlate;
    private Sprite hoverPlate;
    private Sprite pressedPlate;

    private TMP_FontAsset menuFont;

    private string menuScene = "MainMenu";

    private bool useDarkOverlay = true;
    private float darkness = 0.20f;

    private Vector2 plateSize = new Vector2(420f, 78f);

    private Color textColor =
        new Color(0.78f, 0.74f, 0.64f, 0.85f);

    private Color titleColor =
        new Color(0.86f, 0.80f, 0.65f, 1f);

    private Vector2 scroll;

    [MenuItem("The Last Lullaby/12 - Ending Menu Setup")]
    public static void Open()
    {
        EndingMenuSetupWindow window =
            GetWindow<EndingMenuSetupWindow>(
                "Ending Menu Setup");

        window.minSize = new Vector2(420f, 500f);
        window.Show();
    }

    private void OnEnable()
    {
        FindAssets();
    }

    // ======================================================
    // EDITOR WINDOW
    // ======================================================

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Space(12);

        GUIStyle heading =
            new GUIStyle(EditorStyles.boldLabel);

        heading.fontSize = 19;
        heading.alignment = TextAnchor.MiddleCenter;

        GUILayout.Label("THE LAST LULLABY", heading);

        GUILayout.Space(5);

        GUILayout.Label(
            "ENDING MENU - TEXTURED BUTTON FIX",
            EditorStyles.centeredGreyMiniLabel);

        GUILayout.Space(15);

        EditorGUILayout.HelpBox(
            "This version builds the buttons using " +
            "the actual textured Pause Menu sprites, " +
            "not solid-color rectangles.",
            MessageType.Info);

        GUILayout.Space(10);

        EditorGUILayout.LabelField(
            "ENDING BACKGROUND",
            EditorStyles.boldLabel);

        endingImage =
            (Sprite)EditorGUILayout.ObjectField(
                "Picture 09",
                endingImage,
                typeof(Sprite),
                false);

        GUILayout.Space(12);

        EditorGUILayout.LabelField(
            "ACTUAL BUTTON IMAGES",
            EditorStyles.boldLabel);

        normalPlate =
            (Sprite)EditorGUILayout.ObjectField(
                "Normal Button",
                normalPlate,
                typeof(Sprite),
                false);

        hoverPlate =
            (Sprite)EditorGUILayout.ObjectField(
                "Hover Button",
                hoverPlate,
                typeof(Sprite),
                false);

        pressedPlate =
            (Sprite)EditorGUILayout.ObjectField(
                "Pressed Button",
                pressedPlate,
                typeof(Sprite),
                false);

        menuFont =
            (TMP_FontAsset)EditorGUILayout.ObjectField(
                "Menu Font",
                menuFont,
                typeof(TMP_FontAsset),
                false);

        if (GUILayout.Button("REFRESH BUTTON ASSETS"))
        {
            FindAssets();
        }

        GUILayout.Space(10);

        if (normalPlate != null &&
            hoverPlate != null &&
            pressedPlate != null)
        {
            EditorGUILayout.HelpBox(
                "All three button sprites are loaded.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Missing button sprites! Check the " +
                "Resources/Menu folder.",
                MessageType.Error);
        }

        GUILayout.Space(12);

        plateSize = EditorGUILayout.Vector2Field(
            "Button Size",
            plateSize);

        menuScene = EditorGUILayout.TextField(
            "Main Menu Scene",
            menuScene);

        useDarkOverlay = EditorGUILayout.Toggle(
            "Darken Background",
            useDarkOverlay);

        if (useDarkOverlay)
        {
            darkness = EditorGUILayout.Slider(
                "Darkness",
                darkness,
                0f,
                0.6f);
        }

        GUILayout.Space(18);

        GUI.backgroundColor =
            new Color(0.65f, 0.72f, 0.63f);

        if (GUILayout.Button(
            "REBUILD ENDING MENU - FIX BUTTONS",
            GUILayout.Height(50f)))
        {
            Generate();
        }

        GUI.backgroundColor = Color.white;

        GUILayout.Space(12);

        EditorGUILayout.HelpBox(
            "Stop Play Mode first. This rebuilds " +
            "EndingMenuCanvas in the CURRENT SCENE.",
            MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    // ======================================================
    // FIND RESOURCES
    // ======================================================

    private void FindAssets()
    {
        endingImage = LoadSprite("Intro/09");

        normalPlate = LoadSprite("Menu/btn_normal");

        hoverPlate = LoadSprite("Menu/btn_highlight");

        pressedPlate = LoadSprite("Menu/btn_pressed");

        PauseMenu pause =
            Object.FindFirstObjectByType<PauseMenu>();

        if (pause != null)
        {
            if (pause.font != null)
                menuFont = pause.font;

            textColor = pause.normalColor;
            titleColor = pause.titleColor;

            if (pause.plateSize.x > 0 &&
                pause.plateSize.y > 0)
            {
                plateSize = pause.plateSize;
            }

            Sprite normal =
                LoadSprite(pause.plateNormalResource);

            Sprite hover =
                LoadSprite(pause.plateHoverResource);

            Sprite pressed =
                LoadSprite(pause.platePressedResource);

            if (normal != null)
                normalPlate = normal;

            if (hover != null)
                hoverPlate = hover;

            if (pressed != null)
                pressedPlate = pressed;
        }

        Repaint();
    }

    private Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        Sprite sprite =
            Resources.Load<Sprite>(resourcePath);

        if (sprite != null)
            return sprite;

        string expected =
            "Assets/Resources/" + resourcePath;

        string assetName =
            Path.GetFileName(resourcePath);

        string[] guids =
            AssetDatabase.FindAssets(
                assetName,
                new[] { "Assets/Resources" });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            string noExtension =
                Path.ChangeExtension(path, null)
                    .Replace("\\", "/");

            if (noExtension != expected)
                continue;

            sprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    path);

            if (sprite != null)
                return sprite;

            TextureImporter importer =
                AssetImporter.GetAtPath(path)
                as TextureImporter;

            if (importer != null)
            {
                importer.textureType =
                    TextureImporterType.Sprite;

                importer.spriteImportMode =
                    SpriteImportMode.Single;

                importer.SaveAndReimport();

                sprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        path);

                if (sprite != null)
                    return sprite;
            }
        }

        return null;
    }

    // ======================================================
    // REBUILD ENDING MENU
    // ======================================================

    private void Generate()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Exit Play Mode before rebuilding.",
                "OK");

            return;
        }

        if (endingImage == null ||
            normalPlate == null ||
            hoverPlate == null ||
            pressedPlate == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Assets",
                "Assign picture 09 and the three " +
                "Pause Menu button sprites.",
                "OK");

            return;
        }

        GameObject oldCanvas =
            GameObject.Find(CanvasName);

        if (oldCanvas != null)
        {
            if (!EditorUtility.DisplayDialog(
                "Rebuild Ending Menu",
                "Replace the existing generated " +
                "EndingMenuCanvas?",
                "REPLACE",
                "CANCEL"))
            {
                return;
            }

            Undo.DestroyObjectImmediate(oldCanvas);
        }

        // ----------------------------------------------
        // CANVAS
        // ----------------------------------------------

        GameObject canvasObject =
            new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Rebuild Ending Menu");

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.overrideSorting = true;
        canvas.sortingOrder = 3000;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode
                .MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        // ----------------------------------------------
        // ENDING PANEL
        // ----------------------------------------------

        GameObject panel =
            CreateUI("EndingPanel", canvasObject.transform);

        Stretch(panel.GetComponent<RectTransform>());

        panel.AddComponent<RectMask2D>();

        // ----------------------------------------------
        // FULLSCREEN BACKGROUND 09
        // ----------------------------------------------

        GameObject background =
            CreateUI("Background09", panel.transform);

        Image backgroundImage =
            background.AddComponent<Image>();

        backgroundImage.sprite = endingImage;
        backgroundImage.color = Color.white;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = false;

        RectTransform bgRect =
            background.GetComponent<RectTransform>();

        Center(
            bgRect,
            new Vector2(1920f, 1080f),
            0f);

        AspectRatioFitter bgFitter =
            background.AddComponent<AspectRatioFitter>();

        bgFitter.aspectMode =
            AspectRatioFitter.AspectMode.EnvelopeParent;

        bgFitter.aspectRatio =
            endingImage.rect.width /
            Mathf.Max(1f, endingImage.rect.height);

        // ----------------------------------------------
        // DARK OVERLAY
        // ----------------------------------------------

        if (useDarkOverlay)
        {
            GameObject overlay =
                CreateUI("DarkOverlay", panel.transform);

            Image overlayImage =
                overlay.AddComponent<Image>();

            overlayImage.color = new Color(
                0f, 0f, 0f, darkness);

            overlayImage.raycastTarget = false;

            Stretch(
                overlay.GetComponent<RectTransform>());
        }

        // ----------------------------------------------
        // CENTERED TEXT
        // ----------------------------------------------

        TextMeshProUGUI title =
            CreateText(
                "YouEscapedText",
                panel.transform,
                "YOU ESCAPED",
                78f,
                titleColor,
                175f,
                new Vector2(1250f, 125f));

        title.characterSpacing = 4f;

        TextMeshProUGUI subtitle =
            CreateText(
                "SubtitleText",
                panel.transform,
                "THE LAST LULLABY",
                25f,
                textColor,
                94f,
                new Vector2(900f, 60f));

        // ----------------------------------------------
        // TEXTURED BUTTONS
        // ----------------------------------------------

        Button mainButton =
            CreateTexturedButton(
                "MainMenuButton",
                panel.transform,
                "MAIN MENU",
                -35f);

        Button quitButton =
            CreateTexturedButton(
                "QuitGameButton",
                panel.transform,
                "QUIT GAME",
                -135f);

        // ----------------------------------------------
        // CONNECT EXISTING CONTROLLER
        // ----------------------------------------------

        EndingMenuController controller =
            canvasObject.AddComponent<EndingMenuController>();

        controller.endingPanel = panel;

        controller.backgroundImage =
            backgroundImage;

        controller.endingPicture =
            endingImage;

        controller.titleText = title;
        controller.subtitleText = subtitle;

        controller.mainMenuButton = mainButton;
        controller.quitGameButton = quitButton;

        controller.mainMenuSceneName = menuScene;

        controller.pauseGameOnEnding = true;
        controller.unlockCursor = true;

        EnsureEventSystem();

        // Show layout for preview in Edit Mode.
        // Controller hides it when Play starts.
        panel.SetActive(true);

        EditorUtility.SetDirty(canvasObject);

        EditorSceneManager.MarkSceneDirty(
            canvasObject.scene);

        Selection.activeGameObject =
            canvasObject;

        // Verify the ACTUAL generated image assignments.
        Image mainImage =
            mainButton.GetComponent<Image>();

        Image quitImage =
            quitButton.GetComponent<Image>();

        Debug.Log(
            "[ENDING FIX] Normal sprite: " +
            normalPlate.name +
            "\n[ENDING FIX] Main button image: " +
            (mainImage.sprite != null
                ? mainImage.sprite.name
                : "NONE") +
            "\n[ENDING FIX] Quit button image: " +
            (quitImage.sprite != null
                ? quitImage.sprite.name
                : "NONE") +
            "\n[ENDING FIX] Button texture generated.");

        EditorUtility.DisplayDialog(
            "ENDING MENU REBUILT",
            "Finished!\n\n" +
            "Both buttons now use: " +
            normalPlate.name +
            "\n\nCheck EndingMenuCanvas " +
            "in the Game view.",
            "OK");
    }

    // ======================================================
    // CREATE EXACT TEXTURED BUTTON
    // ======================================================

    private Button CreateTexturedButton(
        string objectName,
        Transform parent,
        string label,
        float y)
    {
        GameObject obj =
            CreateUI(objectName, parent);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        Center(rect, plateSize, y);

        // IMPORTANT:
        // The actual plate IMAGE is the button background.
        // No brown color overlay.
        Image image =
            obj.AddComponent<Image>();

        image.sprite = normalPlate;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = true;

        Button button =
            obj.AddComponent<Button>();

        button.targetGraphic = image;

        // Exact Pause Menu state images.
        button.transition =
            Selectable.Transition.SpriteSwap;

        SpriteState states = new SpriteState();

        states.highlightedSprite = hoverPlate;
        states.pressedSprite = pressedPlate;
        states.selectedSprite = hoverPlate;
        states.disabledSprite = normalPlate;

        button.spriteState = states;

        // Keep the normal image untinted.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;

        button.colors = colors;
        button.interactable = true;

        // Text appears ON TOP of the textured plate.
        TextMeshProUGUI buttonText =
            CreateText(
                "Label",
                obj.transform,
                label,
                38f,
                textColor,
                0f,
                plateSize);

        Stretch(buttonText.rectTransform);

        buttonText.characterSpacing = 6f;
        buttonText.alignment =
            TextAlignmentOptions.Center;

        buttonText.raycastTarget = false;

        return button;
    }

    // ======================================================
    // TEXT
    // ======================================================

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string content,
        float size,
        Color color,
        float y,
        Vector2 dimensions)
    {
        GameObject obj =
            CreateUI(objectName, parent);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        Center(rect, dimensions, y);

        TextMeshProUGUI text =
            obj.AddComponent<TextMeshProUGUI>();

        if (menuFont != null)
            text.font = menuFont;

        text.text = content;
        text.fontSize = size;
        text.color = color;

        text.alignment =
            TextAlignmentOptions.Center;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        text.raycastTarget = false;

        return text;
    }

    // ======================================================
    // UI HELPERS
    // ======================================================

    private GameObject CreateUI(
        string name,
        Transform parent)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform));

        obj.transform.SetParent(parent, false);

        return obj;
    }

    private void Center(
        RectTransform rect,
        Vector2 size,
        float y)
    {
        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.sizeDelta = size;

        rect.anchoredPosition =
            new Vector2(0f, y);

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.anchoredPosition = Vector2.zero;

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    // ======================================================
    // EVENT SYSTEM
    // ======================================================

    private void EnsureEventSystem()
    {
        EventSystem existing =
            Object.FindFirstObjectByType<EventSystem>();

        if (existing != null)
            return;

        GameObject obj =
            new GameObject(
                "EndingMenuEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

        Undo.RegisterCreatedObjectUndo(
            obj,
            "Create Ending EventSystem");
    }
}

#endif
