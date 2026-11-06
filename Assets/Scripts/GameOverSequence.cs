using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// THE LAST LULLABY - GAME OVER SEQUENCE
//
// MOMMY CAPTURE:
// - Lock player movement and mouse look
// - Camera focuses on Mommy
// - Dialogue and jumpscare
// - Deduct one life
// - YOU GOT CAUGHT if lives remain
//
// DOLL / HP DEATH:
// - Falling camera animation
// - YOU DIED
// - Show remaining lives
//
// FINAL DEATH:
// - YOU DIED
// - The house keeps what it takes.
// - NO LIVES LEFT
//
// All ending buttons use the same torn-paper
// sprites as the Main Menu and Pause Menu.

public class GameOverSequence : MonoBehaviour
{
    [Header("Capture Dialogue")]
    [TextArea(1, 3)]
    public string[] captureLines =
    {
        "There you are, my child... Mommy's been looking for you."
    };

    public float charDelay = 0.05f;
    public float holdAfterLine = 1.2f;

    [Header("Capture Camera")]
    public float enemyHeadHeight = 1.65f;
    public float faceHeightOffset = 0.1f;
    public float approachDistance = 1.4f;
    public float approachTime = 1.6f;
    public float jumpscareDistance = 0.8f;

    [Header("Player Controls")]
    public bool lockPlayerOnCapture = true;

    [Header("Audio")]
    public AudioClip captureVoice;
    public AudioClip stingClip;
    public AudioClip deathClip;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Header("Scene")]
    public string menuScene = "MainMenu";

    [Header("Ending Text")]
    public string caughtTitle = "YOU GOT CAUGHT";
    public string captureTitle = "YOU DIED";
    public string deathTitle = "YOU DIED";

    public string captureLine = "She never let go.";

    public string dollDeathLine =
        "The doll took another life.";

    public string deathLine =
        "The house keeps what it takes.";

    [Header("Font")]
    public TMP_FontAsset font;

    [Header("Layout")]
    public float titleSize = 90f;
    public float subtitleSize = 39f;
    public float livesSize = 32f;
    public float buttonFontSize = 38f;

    public float titleY = 205f;
    public float subtitleY = 95f;
    public float livesY = 20f;
    public float firstButtonY = -90f;
    public float buttonSpacing = 95f;

    public Vector2 buttonSize =
        new Vector2(420f, 78f);

    [Header("Colors")]
    public Color caughtTitleColor =
        new Color(0.72f, 0.07f, 0.07f, 1f);

    public Color deathTitleColor =
        new Color(0.72f, 0.07f, 0.07f, 1f);

    public Color textColor =
        new Color(0.80f, 0.75f, 0.65f, 1f);

    public Color buttonColor =
        new Color(0.29f, 0.27f, 0.25f, 1f);

    public Color buttonHoverColor =
        new Color(0.40f, 0.36f, 0.32f, 1f);

    public Color hoverColor =
        new Color(0.98f, 0.94f, 0.84f, 1f);

    [Header("Torn Paper Buttons")]
    public bool usePlates = true;

    public string plateNormalResource =
        "Menu/btn_normal";

    public string plateHoverResource =
        "Menu/btn_highlight";

    public string platePressedResource =
        "Menu/btn_pressed";

    [Header("Appearance")]
    public bool showLivesLeft = true;

    [Range(0f, 1f)]
    public float backgroundDarkness = 1f;

    private bool running;

    private Canvas canvas;
    private Image fade;
    private Image flash;

    private TMP_Text subtitle;
    private GameObject panel;

    private Sprite plateNormal;
    private Sprite plateHover;
    private Sprite platePressed;

    private bool platesAvailable;

    private static readonly string[] FreezeScripts =
    {
        "Flashlight",
        "ThrowItem",
        "Interactor",
        "MouseLook",
        "CameraLook",
        "PlayerLook",
        "FirstPersonLook",
        "FirstPersonController"
    };

    // ==================================================
    // PUBLIC METHODS
    // ==================================================

    public void PlayCapture(EnemyAI enemy)
    {
        if (running)
            return;

        running = true;
        StartCoroutine(Capture(enemy));
    }

    public void PlayDeath()
    {
        if (running)
            return;

        running = true;
        StartCoroutine(Death());
    }

    // ==================================================
    // MOMMY / ENEMY CAPTURE
    // ==================================================

    private IEnumerator Capture(EnemyAI enemy)
    {
        BuildUI();

        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        if (player == null || enemy == null)
        {
            ShowGameOver(
                true,
                PlayerHealth.PersistedLives);

            yield break;
        }

        Camera captureCamera = null;

        if (player.cam != null)
        {
            captureCamera =
                player.cam.GetComponent<Camera>();
        }

        if (captureCamera == null)
            captureCamera = Camera.main;

        if (captureCamera == null)
        {
            ShowGameOver(
                true,
                PlayerHealth.PersistedLives);

            yield break;
        }

        Transform cam = captureCamera.transform;

        if (lockPlayerOnCapture)
        {
            FreezePlayer(player);
        }

        NavMeshAgent agent =
            enemy.GetComponent<NavMeshAgent>();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        Vector3 flat =
            player.transform.position -
            enemy.transform.position;

        flat.y = 0f;

        if (flat.sqrMagnitude < 0.01f)
            flat = enemy.transform.forward;

        flat.Normalize();

        Quaternion enemyFaceRotation =
            Quaternion.LookRotation(flat);

        Vector3 startPosition = cam.position;
        Quaternion startRotation = cam.rotation;

        float originalFov =
            captureCamera.fieldOfView;

        float originalNear =
            captureCamera.nearClipPlane;

        captureCamera.nearClipPlane = 0.03f;

        Transform headBone =
            FindHeadBone(enemy);

        Vector3 head =
            FacePos(headBone, enemy);

        GameObject lightObject =
            new GameObject("CaptureFaceLight");

        Light faceLight =
            lightObject.AddComponent<Light>();

        faceLight.type = LightType.Point;
        faceLight.color =
            new Color(0.75f, 0.82f, 1f);
        faceLight.intensity = 3f;
        faceLight.range = 4f;
        faceLight.shadows = LightShadows.None;

        float timer = 0f;
        float duration =
            Mathf.Max(0.01f, approachTime);

        // Camera approaches Mommy's face.
        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(timer / duration));

            head = FacePos(headBone, enemy);

            Vector3 targetPosition =
                head + flat * approachDistance;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    head - targetPosition);

            enemy.transform.rotation =
                Quaternion.Slerp(
                    enemy.transform.rotation,
                    enemyFaceRotation,
                    8f * Time.deltaTime);

            cam.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                t);

            cam.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t);

            captureCamera.fieldOfView =
                Mathf.Lerp(
                    originalFov,
                    originalFov - 15f,
                    t);

            lightObject.transform.position =
                targetPosition + Vector3.up * 0.1f;

            yield return null;
        }

        // Keep camera focused during dialogue.
        head = FacePos(headBone, enemy);

        cam.position =
            head + flat * approachDistance;

        cam.rotation =
            Quaternion.LookRotation(
                head - cam.position);

        if (captureVoice != null)
        {
            PlayAt(cam.position, captureVoice);
        }

        if (captureLines != null)
        {
            foreach (string line in captureLines)
            {
                yield return TypeSubtitle(line);

                head = FacePos(headBone, enemy);

                cam.position =
                    head + flat * approachDistance;

                cam.rotation =
                    Quaternion.LookRotation(
                        head - cam.position);

                yield return new WaitForSeconds(
                    holdAfterLine);
            }
        }

        subtitle.text = "";

        // Jumpscare.
        PlayAt(cam.position, stingClip);

        CameraShake.Shake(0.7f, 0.3f);

        StartCoroutine(
            Flash(0.9f, 0.3f));

        captureCamera.fieldOfView =
            originalFov - 20f;

        float hold = 0f;

        while (hold < 0.9f)
        {
            hold += Time.deltaTime;

            head = FacePos(headBone, enemy);

            cam.position =
                head + flat * jumpscareDistance;

            cam.rotation =
                Quaternion.LookRotation(
                    head - cam.position);

            yield return null;
        }

        yield return FadeTo(1f, 0.35f);

        if (lightObject != null)
            Destroy(lightObject);

        captureCamera.fieldOfView = originalFov;
        captureCamera.nearClipPlane = originalNear;

        // Mommy capture deducts ONE life.
        PlayerHealth health =
            FindFirstObjectByType<PlayerHealth>();

        int livesLeft =
            health != null
                ? health.LoseLifeForCapture()
                : PlayerHealth.PersistedLives;

        ShowGameOver(
            livesLeft > 0,
            livesLeft);
    }

    // ==================================================
    // DOLL / HP DEATH
    // ==================================================

    private IEnumerator Death()
    {
        BuildUI();

        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            ShowGameOver(
                false,
                PlayerHealth.PersistedLives);

            yield break;
        }

        Transform cam = null;

        if (player.cam != null)
        {
            cam = player.cam;
        }
        else if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        if (cam == null)
        {
            ShowGameOver(
                false,
                PlayerHealth.PersistedLives);

            yield break;
        }

        CharacterController controller =
            player.GetComponent<CharacterController>();

        FreezePlayer(player);

        PlayAt(cam.position, deathClip);
        CameraShake.Shake(0.4f, 0.12f);

        Vector3 startPosition =
            cam.localPosition;

        float floorY =
            controller != null
                ? controller.center.y -
                  controller.height / 2f + 0.25f
                : startPosition.y - 1.2f;

        Vector3 endPosition = new Vector3(
            startPosition.x,
            floorY,
            startPosition.z);

        Quaternion startRotation =
            cam.localRotation;

        Quaternion endRotation =
            Quaternion.Euler(-8f, 0f, 75f);

        float timer = 0f;
        float duration = 1.7f;

        // Falling camera animation.
        while (timer < duration)
        {
            timer += Time.deltaTime;

            float progress =
                Mathf.Clamp01(timer / duration);

            float ease = progress * progress;

            cam.localPosition =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    ease);

            cam.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    endRotation,
                    ease);

            fade.color = new Color(
                0.35f,
                0f,
                0f,
                0.7f * progress);

            yield return null;
        }

        yield return new WaitForSeconds(0.6f);

        yield return FadeTo(1f, 0.8f);

        // PlayerHealth already deducted the life
        // before starting this sequence.
        int livesLeft = Mathf.Max(
            0,
            PlayerHealth.PersistedLives);

        ShowGameOver(false, livesLeft);
    }

    // ==================================================
    // FREEZE PLAYER CONTROLS
    // ==================================================

    private void FreezePlayer(PlayerController player)
    {
        if (player == null)
            return;

        player.CanMove = false;
        player.enabled = false;

        MonoBehaviour[] playerScripts =
            player.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour script in playerScripts)
        {
            if (script == null || !script.enabled)
                continue;

            string scriptName =
                script.GetType().Name;

            foreach (string freezeName in FreezeScripts)
            {
                if (scriptName == freezeName)
                {
                    script.enabled = false;
                    break;
                }
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ==================================================
    // CINEMATIC HELPERS
    // ==================================================

    private Transform FindHeadBone(EnemyAI enemy)
    {
        Animator animator =
            enemy.GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman)
        {
            return animator.GetBoneTransform(
                HumanBodyBones.Head);
        }

        return null;
    }

    private Vector3 FacePos(
        Transform headBone,
        EnemyAI enemy)
    {
        if (headBone != null)
        {
            return headBone.position +
                   Vector3.up * faceHeightOffset;
        }

        return enemy.transform.position +
               Vector3.up * enemyHeadHeight;
    }

    private void PlayAt(
        Vector3 position,
        AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(
                clip,
                position,
                sfxVolume);
        }
    }

    private IEnumerator TypeSubtitle(string line)
    {
        if (subtitle == null)
            yield break;

        subtitle.text = line ?? "";
        subtitle.maxVisibleCharacters = 0;
        subtitle.ForceMeshUpdate();

        int total =
            subtitle.textInfo.characterCount;

        for (int i = 1; i <= total; i++)
        {
            subtitle.maxVisibleCharacters = i;

            yield return new WaitForSeconds(
                Mathf.Max(0f, charDelay));
        }

        subtitle.maxVisibleCharacters = total;
    }

    private IEnumerator FadeTo(
        float target,
        float duration)
    {
        Color color = fade.color;
        float start = color.a;
        float timer = 0f;

        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            color.a = Mathf.Lerp(
                start,
                target,
                Mathf.Clamp01(timer / duration));

            fade.color = color;

            yield return null;
        }

        color.a = target;
        fade.color = color;
    }

    private IEnumerator Flash(
        float alpha,
        float duration)
    {
        float timer = 0f;

        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            flash.color = new Color(
                1f,
                1f,
                1f,
                Mathf.Lerp(
                    alpha,
                    0f,
                    Mathf.Clamp01(timer / duration)));

            yield return null;
        }

        flash.color =
            new Color(1f, 1f, 1f, 0f);
    }

    // ==================================================
    // BUILD ENDING CANVAS
    // ==================================================

    private void BuildUI()
    {
        if (canvas != null)
            return;

        // Load the SAME button sprites
        // as the Main Menu and Pause Menu.
        if (usePlates)
        {
            plateNormal =
                LoadButtonSprite(plateNormalResource);

            plateHover =
                LoadButtonSprite(plateHoverResource);

            platePressed =
                LoadButtonSprite(platePressedResource);

            platesAvailable = plateNormal != null;

            if (!platesAvailable)
            {
                Debug.LogWarning(
                    "[GameOverSequence] Torn-paper " +
                    "button sprites not found. " +
                    "Using fallback buttons.");
            }
        }

        GameObject cgo =
            new GameObject("GameOverCanvas");

        cgo.transform.SetParent(transform, false);

        canvas = cgo.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 500;

        CanvasScaler scaler =
            cgo.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();

        // Black fade.
        fade = MakeFullImage(
            cgo.transform,
            "Fade",
            new Color(0f, 0f, 0f, 0f));

        // Jumpscare flash.
        flash = MakeFullImage(
            cgo.transform,
            "Flash",
            new Color(1f, 1f, 1f, 0f));

        // Dialogue subtitle.
        subtitle = MakeText(
            cgo.transform,
            "DialogueSubtitle",
            "",
            46f,
            TextAlignmentOptions.Center,
            textColor);

        RectTransform sr = subtitle.rectTransform;

        sr.anchorMin = new Vector2(0.1f, 0f);
        sr.anchorMax = new Vector2(0.9f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);

        sr.anchoredPosition =
            new Vector2(0f, 110f);

        sr.sizeDelta =
            new Vector2(0f, 120f);

        subtitle.textWrappingMode =
            TextWrappingModes.Normal;

        // Ending panel.
        panel = new GameObject(
            "EndingPanel",
            typeof(RectTransform),
            typeof(Image));

        panel.transform.SetParent(cgo.transform, false);

        RectTransform pr =
            panel.GetComponent<RectTransform>();

        Stretch(pr);

        Image background =
            panel.GetComponent<Image>();

        background.color = new Color(
            0f,
            0f,
            0f,
            backgroundDarkness);

        panel.SetActive(false);
    }

    // ==================================================
    // SHOW ENDING SCREEN
    // ==================================================

    private void ShowGameOver(
        bool isCaught,
        int livesLeft)
    {
        BuildUI();

        livesLeft = Mathf.Max(0, livesLeft);

        fade.color =
            new Color(0f, 0f, 0f, 1f);

        subtitle.text = "";

        foreach (Transform child in panel.transform)
        {
            Destroy(child.gameObject);
        }

        bool actualCaught =
            isCaught && livesLeft > 0;

        // Exact titles regardless of old
        // Inspector text values.
        string heading =
            actualCaught
                ? "YOU GOT CAUGHT"
                : "YOU DIED";

        // Correct subtitle for each situation.
        string description;

        if (livesLeft <= 0)
        {
            description = deathLine;
        }
        else if (actualCaught)
        {
            description = captureLine;
        }
        else
        {
            description = dollDeathLine;
        }

        Color headingColor =
            actualCaught
                ? caughtTitleColor
                : deathTitleColor;

        // TITLE

        TMP_Text titleText = MakeText(
            panel.transform,
            "EndingTitle",
            heading,
            titleSize,
            TextAlignmentOptions.Center,
            headingColor);

        titleText.characterSpacing = 6f;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 40f;
        titleText.fontSizeMax = titleSize;

        Place(
            titleText.rectTransform,
            new Vector2(0f, titleY),
            new Vector2(1500f, 135f));

        // SUBTITLE

        TMP_Text lineText = MakeText(
            panel.transform,
            "EndingDescription",
            description,
            subtitleSize,
            TextAlignmentOptions.Center,
            textColor);

        Place(
            lineText.rectTransform,
            new Vector2(0f, subtitleY),
            new Vector2(1400f, 75f));

        // REMAINING LIVES

        if (showLivesLeft)
        {
            string livesMessage =
                livesLeft > 0
                    ? "LIVES LEFT: " + livesLeft
                    : "NO LIVES LEFT";

            TMP_Text livesText = MakeText(
                panel.transform,
                "LivesText",
                livesMessage,
                livesSize,
                TextAlignmentOptions.Center,
                livesLeft > 0
                    ? textColor
                    : caughtTitleColor);

            Place(
                livesText.rectTransform,
                new Vector2(0f, livesY),
                new Vector2(1000f, 65f));
        }

        // BUTTONS

        int buttonIndex = 0;

        MakeButton(
            livesLeft > 0
                ? "TRY AGAIN"
                : "NEW GAME",
            buttonIndex++,
            Restart);

        MakeButton(
            "MAIN MENU",
            buttonIndex,
            GoToMenu);

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();

        running = false;

        Debug.Log(
            "[GameOverSequence] " +
            heading +
            " | Lives Left: " +
            livesLeft);
    }

    // ==================================================
    // MATCHING TORN-PAPER BUTTONS
    // ==================================================

    private void MakeButton(
        string labelText,
        int index,
        Action onClick)
    {
        GameObject go = new GameObject(
            "Button_" + labelText,
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(
            panel.transform,
            false);

        float buttonY =
            firstButtonY - index * buttonSpacing;

        Place(
            go.GetComponent<RectTransform>(),
            new Vector2(0f, buttonY),
            buttonSize);

        Image image = go.GetComponent<Image>();

        if (platesAvailable)
        {
            image.sprite = plateNormal;
            image.color = Color.white;
            image.type = Image.Type.Simple;
        }
        else
        {
            // Fallback if paper sprites are missing.
            image.color = buttonColor;
        }

        TMP_Text label = MakeText(
            go.transform,
            "Label",
            labelText,
            buttonFontSize,
            TextAlignmentOptions.Center,
            textColor);

        label.characterSpacing = 6f;

        Stretch(label.rectTransform);

        EventTrigger trigger =
            go.AddComponent<EventTrigger>();

        // Mouse hover.
        AddEvent(
            trigger,
            EventTriggerType.PointerEnter,
            () =>
            {
                label.color = hoverColor;

                if (platesAvailable)
                {
                    if (plateHover != null)
                        image.sprite = plateHover;
                    else
                        image.sprite = plateNormal;
                }
                else
                {
                    image.color = buttonHoverColor;
                }
            });

        // Mouse leaves button.
        AddEvent(
            trigger,
            EventTriggerType.PointerExit,
            () =>
            {
                label.color = textColor;

                if (platesAvailable)
                {
                    image.sprite = plateNormal;
                    image.color = Color.white;
                }
                else
                {
                    image.color = buttonColor;
                }
            });

        // Mouse button pressed.
        AddEvent(
            trigger,
            EventTriggerType.PointerDown,
            () =>
            {
                if (platesAvailable &&
                    platePressed != null)
                {
                    image.sprite = platePressed;
                }
            });

        // Mouse button released.
        AddEvent(
            trigger,
            EventTriggerType.PointerUp,
            () =>
            {
                if (platesAvailable)
                {
                    image.sprite =
                        plateHover != null
                            ? plateHover
                            : plateNormal;
                }
            });

        // Button action.
        AddEvent(
            trigger,
            EventTriggerType.PointerClick,
            () => onClick?.Invoke());
    }

    // ==================================================
    // BUTTON SPRITE LOADER
    // ==================================================

    private Sprite LoadButtonSprite(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Load sprite directly.
        Sprite sprite =
            Resources.Load<Sprite>(path);

        if (sprite != null)
            return sprite;

        // Same fallback behavior as PauseMenu:
        // load Texture2D and turn it into a sprite.
        Texture2D texture =
            Resources.Load<Texture2D>(path);

        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(
                0f,
                0f,
                texture.width,
                texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    // ==================================================
    // UI HELPERS
    // ==================================================

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
    }

    private TMP_Text MakeText(
        Transform parent,
        string objectName,
        string content,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform));

        go.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            go.AddComponent<TextMeshProUGUI>();

        if (font != null)
        {
            text.font = font;
        }

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        return text;
    }

    private static Image MakeFullImage(
        Transform parent,
        string objectName,
        Color color)
    {
        GameObject go = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image));

        go.transform.SetParent(parent, false);

        RectTransform rt =
            go.GetComponent<RectTransform>();

        Stretch(rt);

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static void Place(
        RectTransform rt,
        Vector2 position,
        Vector2 size)
    {
        rt.anchorMin =
            new Vector2(0.5f, 0.5f);

        rt.anchorMax =
            new Vector2(0.5f, 0.5f);

        rt.pivot =
            new Vector2(0.5f, 0.5f);

        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(
        RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddEvent(
        EventTrigger trigger,
        EventTriggerType type,
        Action action)
    {
        EventTrigger.Entry entry =
            new EventTrigger.Entry
            {
                eventID = type
            };

        entry.callback.AddListener(
            _ => action?.Invoke());

        trigger.triggers.Add(entry);
    }

    // ==================================================
    // BUTTON ACTIONS
    // ==================================================

    private void Restart()
    {
        // Reset lives only when none remain.
        if (PlayerHealth.PersistedLives <= 0)
        {
            PlayerHealth.ResetPersisted();
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name);
    }

    private void GoToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScene))
        {
            Debug.LogError(
                "[GameOverSequence] Scene '" +
                menuScene +
                "' is missing from Build Profiles.");

            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(menuScene);
    }
}