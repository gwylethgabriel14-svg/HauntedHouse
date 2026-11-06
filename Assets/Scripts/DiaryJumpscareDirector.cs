
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// THE LAST LULLABY
// PARCHMENT DIARY + FACE CLOSE-UP JUMPSCARE
//
// Replace your entire DiaryJumpscareDirector.cs.
//
// Compatible with the existing:
// - DiaryPageInteractable
// - DiaryJumpscareSetupWindow
// - EnemyAI
// - PlayerController

[DisallowMultipleComponent]
public class DiaryJumpscareDirector : MonoBehaviour
{
    [Header("Scene References")]
    public Camera playerCamera;
    public Transform player;
    public GameObject eleanorEnemy;

    public DiaryPageInteractable[] pages =
        new DiaryPageInteractable[4];

    [Header("Optional")]
    public TMP_FontAsset handwritingFont;
    public AudioClip jumpscareSound;

    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Header("Random Placement")]
    public bool randomizeOnNewGame = true;
    public float minimumDistanceFromPlayer = 5f;
    public float minimumDistanceBetweenPages = 4f;
    public LayerMask surfaceMask = ~0;

    public float placementClearRadius = 0.38f;
    public float pageSurfaceLift = 0.022f;

    [Header("Interaction")]
    public KeyCode readKey = KeyCode.E;
    public float readDistance = 3.5f;

    [Header("Reading UI")]
    public float closingSeconds = 0.3f;

    [Range(16f, 36f)]
    public float diaryFontSize = 25f;

    [Header("Jumpscare - Face Close-up")]
    public float scareDistance = 1.4f;
    public float scareSeconds = 1.4f;
    public float screenShake = 0.055f;

    public LayerMask obstacleMask = ~0;

    [Tooltip("Set to 180 if Eleanor faces backwards.")]
    public float jumpscareYawOffset = 0f;

    [Tooltip("Positive value moves face down.")]
    public float faceVerticalOffset = 0.08f;

    public float scareLightIntensity = 3.5f;

    [Header("Debug")]
    public bool debugLogs = true;

    // =====================================================
    // INTERNAL VARIABLES
    // =====================================================

    private Sprite worldParchmentSprite;
    private Sprite readingParchmentSprite;
    private Material parchmentMaterial;

    private GameObject readingPanel;
    private GameObject promptObject;

    private CanvasGroup readingGroup;

    private TMP_Text heading;
    private TMP_Text body;
    private TMP_Text prompt;

    private RectTransform bodyRect;
    private RectTransform scrollViewport;

    private ScrollRect readingScroll;

    private Image flash;
    private AudioSource audioSource;

    private DiaryPageInteractable openedPage;

    private bool reading;
    private bool busy;

    private readonly bool[] hasScared = new bool[4];

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (player == null && playerCamera != null)
        {
            PlayerController controller =
                playerCamera.GetComponentInParent<PlayerController>();

            player = controller != null
                ? controller.transform
                : playerCamera.transform;
        }

        EnsureResources();
        BuildUI();

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private IEnumerator Start()
    {
        yield return null;

        StyleAllPages();

        if (randomizeOnNewGame)
            RandomizeAndSnapPages();
        else
            SnapAllPages();
    }

    private void Update()
    {
        if (busy ||
            playerCamera == null ||
            Time.timeScale == 0f)
        {
            SetPrompt(false);
            return;
        }

        if (reading)
        {
            SetPrompt(false);

            if (Input.GetKeyDown(readKey))
            {
                StartCoroutine(CloseAndScare());
            }

            return;
        }

        DiaryPageInteractable target = LookAtPage();

        SetPrompt(target != null);

        if (target != null &&
            Input.GetKeyDown(readKey))
        {
            Open(target);
        }
    }

    // =====================================================
    // PARCHMENT RESOURCES
    // =====================================================

    private void EnsureResources()
    {
        if (worldParchmentSprite == null)
        {
            worldParchmentSprite =
                BuildParchmentSprite(384, 512, true);
        }

        if (readingParchmentSprite == null)
        {
            readingParchmentSprite =
                BuildParchmentSprite(768, 1024, false);
        }

        if (parchmentMaterial == null)
        {
            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader != null)
            {
                parchmentMaterial = new Material(shader);
            }
        }
    }

    // =====================================================
    // WORLD DIARY VISUAL
    // =====================================================

    public void StyleAllPages()
    {
        EnsureResources();

        if (pages == null)
            return;

        foreach (DiaryPageInteractable page in pages)
        {
            if (page != null)
                StylePage(page);
        }
    }

    private void StylePage(DiaryPageInteractable page)
    {
        Transform oldBook =
            page.transform.Find("VintageDiaryModel");

        if (oldBook != null)
            RemoveOld(oldBook.gameObject);

        Transform oldPaper =
            page.transform.Find("ParchmentVisual");

        if (oldPaper != null)
            RemoveOld(oldPaper.gameObject);

        Renderer original =
            page.GetComponent<Renderer>();

        if (original != null)
            original.enabled = false;

        page.transform.localScale = Vector3.one;

        GameObject paper =
            new GameObject("ParchmentVisual");

        paper.transform.SetParent(
            page.transform,
            false);

        paper.transform.localPosition =
            new Vector3(0f, 0.012f, 0f);

        paper.transform.localRotation =
            Quaternion.Euler(-90f, 0f, 0f);

        paper.transform.localScale =
            new Vector3(0.14f, 0.14f, 1f);

        SpriteRenderer sprite =
            paper.AddComponent<SpriteRenderer>();

        sprite.sprite = worldParchmentSprite;

        if (parchmentMaterial != null)
        {
            sprite.sharedMaterial =
                parchmentMaterial;
        }

        sprite.sortingOrder = 1;

        BoxCollider box =
            page.GetComponent<BoxCollider>();

        if (box == null)
            box = page.gameObject.AddComponent<BoxCollider>();

        box.enabled = true;
        box.isTrigger = false;

        box.center =
            new Vector3(0f, 0.012f, 0f);

        box.size =
            new Vector3(0.54f, 0.04f, 0.72f);
    }

    private static void RemoveOld(GameObject old)
    {
        old.SetActive(false);

        if (Application.isPlaying)
            Destroy(old);
        else
            DestroyImmediate(old);
    }

    // =====================================================
    // RANDOM DIARY PLACEMENT
    // =====================================================

    public void RandomizeAndSnapPages()
    {
        if (pages == null)
            return;

        List<Transform> candidates =
            new List<Transform>();

        Transform[] all =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform t in all)
        {
            if (t == null)
                continue;

            if (t.name != "BigPageSpots" &&
                t.name != "BigUpPageSpots")
                continue;

            if (!t.gameObject.scene.IsValid())
                continue;

            foreach (Transform child in t)
            {
                if (child != null &&
                    child.gameObject.activeInHierarchy)
                {
                    candidates.Add(child);
                }
            }
        }

        if (candidates.Count < 4)
        {
            Debug.LogWarning(
                "[Diary] Fewer than four diary markers. " +
                "Using existing positions.",
                this);

            SnapAllPages();
            return;
        }

        // Shuffle markers.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            Transform temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        List<Vector3> chosen =
            new List<Vector3>();

        foreach (DiaryPageInteractable page in pages)
        {
            if (page == null)
                continue;

            bool placed = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform marker = candidates[i];

                if (marker == null)
                    continue;

                if (player != null &&
                    Vector3.Distance(
                        player.position,
                        marker.position) <
                    minimumDistanceFromPlayer)
                {
                    continue;
                }

                bool farEnough = true;

                foreach (Vector3 previous in chosen)
                {
                    if (Vector3.Distance(
                        previous,
                        marker.position) <
                        minimumDistanceBetweenPages)
                    {
                        farEnough = false;
                        break;
                    }
                }

                if (!farEnough)
                    continue;

                Vector3 previousPosition =
                    page.transform.position;

                Quaternion previousRotation =
                    page.transform.rotation;

                page.transform.position =
                    marker.position;

                page.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        UnityEngine.Random.Range(0f, 360f),
                        0f);

                if (!SnapPage(page))
                {
                    page.transform.SetPositionAndRotation(
                        previousPosition,
                        previousRotation);

                    continue;
                }

                chosen.Add(page.transform.position);

                candidates.RemoveAt(i);

                page.readOnce = false;
                page.gameObject.SetActive(true);

                placed = true;
                break;
            }

            if (!placed)
            {
                if (debugLogs)
                {
                    Debug.LogWarning(
                        "[Diary] Could not find a free " +
                        "marker for " + page.name,
                        page);
                }

                SnapPage(page);
            }
        }

        Array.Clear(
            hasScared,
            0,
            hasScared.Length);
    }

    public void SnapAllPages()
    {
        if (pages == null)
            return;

        foreach (DiaryPageInteractable page in pages)
        {
            if (page != null)
                SnapPage(page);
        }
    }

    private bool SnapPage(DiaryPageInteractable page)
    {
        Physics.SyncTransforms();

        Vector3 original =
            page.transform.position;

        RaycastHit[] hits =
            Physics.RaycastAll(
                original + Vector3.up * 1.5f,
                Vector3.down,
                6f,
                surfaceMask,
                QueryTriggerInteraction.Ignore);

        Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.normal.y < 0.85f)
                continue;

            if (hit.point.y > original.y + 0.4f)
                continue;

            if (hit.collider.GetComponentInParent<
                    DiaryPageInteractable>() != null)
                continue;

            if (hit.collider.GetComponentInParent<
                    PlayerController>() != null)
                continue;

            if (hit.collider.GetComponentInParent<
                    EnemyAI>() != null)
                continue;

            Vector3 proposed = new Vector3(
                original.x,
                hit.point.y + pageSurfaceLift,
                original.z);

            if (AreaOccupied(
                proposed,
                hit.collider,
                page))
            {
                continue;
            }

            page.transform.position = proposed;

            if (debugLogs)
            {
                Debug.Log(
                    "[Diary] " + page.name +
                    " placed on " +
                    hit.collider.name,
                    page);
            }

            return true;
        }

        return false;
    }

    private bool AreaOccupied(
        Vector3 position,
        Collider support,
        DiaryPageInteractable own)
    {
        Collider[] nearby =
            Physics.OverlapBox(
                position + Vector3.up * 0.11f,
                new Vector3(
                    placementClearRadius,
                    0.09f,
                    placementClearRadius),
                Quaternion.identity,
                surfaceMask,
                QueryTriggerInteraction.Ignore);

        foreach (Collider col in nearby)
        {
            if (col == null || col == support)
                continue;

            if (col.GetComponentInParent<
                    DiaryPageInteractable>() == own)
                continue;

            if (player != null &&
                col.transform.IsChildOf(player))
                continue;

            if (eleanorEnemy != null &&
                col.transform.IsChildOf(
                    eleanorEnemy.transform))
                continue;

            return true;
        }

        return false;
    }

    // =====================================================
    // PRESS E TO READ
    // =====================================================

    private DiaryPageInteractable LookAtPage()
    {
        RaycastHit[] hits =
            Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                readDistance,
                ~0,
                QueryTriggerInteraction.Collide);

        Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (player != null &&
                hit.collider.transform.IsChildOf(player))
            {
                continue;
            }

            DiaryPageInteractable page =
                hit.collider.GetComponentInParent<
                    DiaryPageInteractable>();

            if (page != null &&
                page.pageNumber >= 1 &&
                page.pageNumber <= 4 &&
                pages != null &&
                pages.Length >= page.pageNumber &&
                pages[page.pageNumber - 1] == page)
            {
                return page;
            }

            if (!hit.collider.isTrigger)
                break;
        }

        return null;
    }

    private void Open(DiaryPageInteractable page)
    {
        openedPage = page;

        heading.text = page.pageTitle;
        body.text = page.diaryEntry;

        body.fontSize = diaryFontSize;

        if (handwritingFont != null)
        {
            heading.font = handwritingFont;
            body.font = handwritingFont;
        }

        readingPanel.SetActive(true);
        readingGroup.alpha = 1f;

        reading = true;

        // Ensure the text has a real, visible height.
        Canvas.ForceUpdateCanvases();

        float width = Mathf.Max(
            100f,
            scrollViewport.rect.width - 14f);

        string text =
            page.diaryEntry ?? string.Empty;

        float preferred =
            body.GetPreferredValues(
                text,
                width,
                0f).y;

        float contentHeight = Mathf.Max(
            scrollViewport.rect.height,
            preferred + 28f);

        bodyRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width);

        bodyRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            contentHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            bodyRect);

        Canvas.ForceUpdateCanvases();

        readingScroll.verticalNormalizedPosition = 1f;

        if (debugLogs)
        {
            Debug.Log(
                "[Diary] Opened " +
                page.pageTitle +
                ". Text length: " +
                text.Length +
                ", height: " +
                contentHeight,
                page);
        }

        SetPrompt(false);
    }

    // =====================================================
    // CLOSE DIARY
    // =====================================================

    private IEnumerator CloseAndScare()
    {
        busy = true;

        DiaryPageInteractable closing =
            openedPage;

        float duration =
            Mathf.Max(0.02f, closingSeconds);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            readingGroup.alpha =
                1f - Mathf.Clamp01(
                    elapsed / duration);

            yield return null;
        }

        readingPanel.SetActive(false);
        readingGroup.alpha = 0f;

        reading = false;
        openedPage = null;

        if (closing != null)
            closing.readOnce = true;

        yield return new WaitForSecondsRealtime(0.12f);

        if (closing != null &&
            closing.pageNumber >= 1 &&
            closing.pageNumber <= 4)
        {
            int index = closing.pageNumber - 1;

            if (!hasScared[index])
            {
                yield return StartCoroutine(
                    Scare(index));
            }
        }

        busy = false;
    }

    // =====================================================
    // FACE-TO-CAMERA JUMPSCARE
    // =====================================================

    private IEnumerator Scare(int index)
    {
        if (playerCamera == null ||
            eleanorEnemy == null)
        {
            Debug.LogError(
                "[Diary] Assign Main Camera and " +
                "Woman enemy root for the jumpscare.",
                this);

            yield break;
        }

        Transform cameraTransform =
            playerCamera.transform;

        // Clone the full Eleanor model.
        GameObject clone =
            Instantiate(eleanorEnemy);

        clone.name =
            "Eleanor_Diary_Closeup_Only";

        clone.SetActive(true);

        // Disable copied gameplay logic.
        foreach (MonoBehaviour script in
            clone.GetComponentsInChildren<
                MonoBehaviour>(true))
        {
            if (script != null)
                script.enabled = false;
        }

        foreach (NavMeshAgent nav in
            clone.GetComponentsInChildren<
                NavMeshAgent>(true))
        {
            if (nav != null)
                nav.enabled = false;
        }

        foreach (Collider col in
            clone.GetComponentsInChildren<
                Collider>(true))
        {
            if (col != null)
                col.enabled = false;
        }

        foreach (Rigidbody rb in
            clone.GetComponentsInChildren<
                Rigidbody>(true))
        {
            if (rb == null)
                continue;

            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (AudioSource source in
            clone.GetComponentsInChildren<
                AudioSource>(true))
        {
            if (source != null)
                source.enabled = false;
        }

        foreach (Camera cam in
            clone.GetComponentsInChildren<
                Camera>(true))
        {
            if (cam != null)
                cam.enabled = false;
        }

        foreach (Light light in
            clone.GetComponentsInChildren<
                Light>(true))
        {
            if (light != null)
                light.enabled = false;
        }

        // Keep the model rig and pose.
        foreach (Animator animator in
            clone.GetComponentsInChildren<
                Animator>(true))
        {
            if (animator == null)
                continue;

            animator.enabled = true;
            animator.speed = 0f;
        }

        // Face Eleanor toward the camera.
        Vector3 flatForward =
            Vector3.ProjectOnPlane(
                cameraTransform.forward,
                Vector3.up);

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward =
                cameraTransform.forward;
        }

        flatForward.Normalize();

        clone.transform.rotation =
            Quaternion.LookRotation(
                -flatForward,
                Vector3.up) *
            Quaternion.Euler(
                0f,
                jumpscareYawOffset,
                0f);

        clone.transform.position =
            cameraTransform.position +
            cameraTransform.forward *
            Mathf.Max(0.8f, scareDistance);

        // Locate Eleanor's head.
        Transform head =
            FindHead(clone.transform);

        Vector3 headCenter =
            head != null
                ? head.position
                : EstimateHead(clone);

        // Move the head directly in front
        // of the player's crosshair.
        Vector3 desiredFace =
            cameraTransform.position +
            cameraTransform.forward *
            Mathf.Max(0.8f, scareDistance) -
            cameraTransform.up *
            faceVerticalOffset;

        clone.transform.position +=
            desiredFace - headCenter;

        // Follow the camera during the scare.
        clone.transform.SetParent(
            cameraTransform,
            true);

        // Light Eleanor's face.
        GameObject lamp =
            new GameObject(
                "DiaryJumpscareFaceLight");

        lamp.transform.SetParent(
            cameraTransform,
            false);

        lamp.transform.localPosition =
            new Vector3(0f, 0.12f, 0.18f);

        Light faceLight =
            lamp.AddComponent<Light>();

        faceLight.type = LightType.Point;

        faceLight.color =
            new Color(1f, 0.60f, 0.51f);

        faceLight.range = 4f;
        faceLight.intensity = scareLightIntensity;
        faceLight.shadows = LightShadows.None;

        // Sound effect.
        if (jumpscareSound != null)
        {
            audioSource.PlayOneShot(
                jumpscareSound,
                soundVolume);
        }

        hasScared[index] = true;

        if (debugLogs)
        {
            Debug.Log(
                "[Diary] FACE JUMPSCARE started. " +
                "Head: " +
                (head != null
                    ? head.name
                    : "Renderer estimate"),
                this);
        }

        float duration =
            Mathf.Max(
                0.25f,
                scareSeconds +
                (index == 3 ? 0.55f : 0f));

        Vector3 lastShake =
            Vector3.zero;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (playerCamera == null)
                break;

            cameraTransform.localPosition -=
                lastShake;

            lastShake =
                UnityEngine.Random.insideUnitSphere *
                screenShake *
                (1f - Mathf.Clamp01(
                    elapsed / duration));

            cameraTransform.localPosition +=
                lastShake;

            // Short transparent red flash.
            // Avoid covering Eleanor's face.
            float alpha =
                0.25f *
                (1f - Mathf.Clamp01(
                    elapsed / 0.16f));

            flash.color =
                new Color(
                    0.9f,
                    0.06f,
                    0.06f,
                    alpha);

            yield return null;
        }

        if (playerCamera != null)
        {
            cameraTransform.localPosition -=
                lastShake;
        }

        flash.color = Color.clear;

        if (lamp != null)
            Destroy(lamp);

        if (clone != null)
            Destroy(clone);

        if (debugLogs)
        {
            Debug.Log(
                "[Diary] FACE JUMPSCARE complete " +
                "for page " +
                (index + 1),
                this);
        }
    }

    // =====================================================
    // FIND ELEANOR'S HEAD BONE
    // =====================================================

    private static Transform FindHead(
        Transform root)
    {
        Transform[] all =
            root.GetComponentsInChildren<
                Transform>(true);

        foreach (Transform t in all)
        {
            string name =
                t.name.ToLowerInvariant();

            if (name == "mixamorig:head" ||
                name == "head" ||
                name.EndsWith(":head"))
            {
                return t;
            }
        }

        foreach (Transform t in all)
        {
            string name =
                t.name.ToLowerInvariant();

            if (name.Contains("head") &&
                !name.Contains("hair") &&
                !name.Contains("headwear"))
            {
                return t;
            }
        }

        return null;
    }

    private static Vector3 EstimateHead(
        GameObject clone)
    {
        Renderer[] renderers =
            clone.GetComponentsInChildren<
                Renderer>(true);

        Bounds bounds = new Bounds(
            clone.transform.position,
            Vector3.zero);

        bool found = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.enabled ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (found)
        {
            return new Vector3(
                bounds.center.x,
                bounds.min.y +
                bounds.size.y * 0.88f,
                bounds.center.z);
        }

        return clone.transform.position +
               Vector3.up * 1.6f;
    }

    // =====================================================
    // BUILD DIARY READING UI
    // =====================================================

    private void BuildUI()
    {
        GameObject root = new GameObject(
            "DiaryCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        root.transform.SetParent(
            transform,
            false);

        Canvas canvas =
            root.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 450;

        CanvasScaler scaler =
            root.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.matchWidthOrHeight = 0.5f;

        // -------------------------------------
        // PRESS E PROMPT
        // -------------------------------------

        promptObject = new GameObject(
            "DiaryReadPrompt",
            typeof(RectTransform),
            typeof(Image));

        promptObject.transform.SetParent(
            root.transform,
            false);

        RectTransform promptRect =
            promptObject.GetComponent<RectTransform>();

        promptRect.anchorMin =
            promptRect.anchorMax =
            new Vector2(0.5f, 0.13f);

        promptRect.sizeDelta =
            new Vector2(370f, 54f);

        promptRect.anchoredPosition =
            Vector2.zero;

        Image promptBackground =
            promptObject.GetComponent<Image>();

        promptBackground.color =
            new Color(0f, 0f, 0f, 0.66f);

        promptBackground.raycastTarget = false;

        prompt = AddText(
            promptObject.transform,
            "Prompt",
            29f,
            TextAlignmentOptions.Center);

        Stretch(prompt.rectTransform);

        prompt.text = "PRESS E TO READ";

        prompt.color =
            new Color(1f, 0.91f, 0.75f);

        // -------------------------------------
        // PARCHMENT PANEL
        // -------------------------------------

        readingPanel = new GameObject(
            "ParchmentReadingPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup));

        readingPanel.transform.SetParent(
            root.transform,
            false);

        RectTransform panel =
            readingPanel.GetComponent<RectTransform>();

        panel.anchorMin =
            panel.anchorMax =
            new Vector2(0.5f, 0.5f);

        panel.pivot =
            new Vector2(0.5f, 0.5f);

        panel.sizeDelta =
            new Vector2(830f, 905f);

        panel.anchoredPosition =
            Vector2.zero;

        Image parchment =
            readingPanel.GetComponent<Image>();

        parchment.sprite =
            readingParchmentSprite;

        parchment.color = Color.white;
        parchment.raycastTarget = false;

        readingGroup =
            readingPanel.GetComponent<CanvasGroup>();

        readingGroup.blocksRaycasts = false;
        readingGroup.interactable = false;

        // -------------------------------------
        // DIARY DATE
        // -------------------------------------

        heading = AddText(
            readingPanel.transform,
            "DiaryDate",
            37f,
            TextAlignmentOptions.Center);

        Anchor(
            heading.rectTransform,
            new Vector2(0.12f, 0.85f),
            new Vector2(0.88f, 0.94f));

        heading.color =
            new Color(0.23f, 0.11f, 0.065f);

        // -------------------------------------
        // SCROLLABLE BODY
        // -------------------------------------

        GameObject viewportObject =
            new GameObject(
                "DiaryTextViewport",
                typeof(RectTransform),
                typeof(RectMask2D),
                typeof(ScrollRect));

        viewportObject.transform.SetParent(
            readingPanel.transform,
            false);

        scrollViewport =
            viewportObject.GetComponent<RectTransform>();

        Anchor(
            scrollViewport,
            new Vector2(0.15f, 0.17f),
            new Vector2(0.85f, 0.83f));

        readingScroll =
            viewportObject.GetComponent<ScrollRect>();

        readingScroll.viewport = scrollViewport;
        readingScroll.horizontal = false;
        readingScroll.vertical = true;

        readingScroll.movementType =
            ScrollRect.MovementType.Clamped;

        readingScroll.scrollSensitivity = 38f;
        readingScroll.inertia = false;

        // Text is directly inside the viewport.
        // This avoids a zero-height Content object.

        body = AddText(
            viewportObject.transform,
            "ActualDiaryText",
            diaryFontSize,
            TextAlignmentOptions.TopLeft);

        bodyRect = body.rectTransform;

        bodyRect.anchorMin =
            new Vector2(0.5f, 1f);

        bodyRect.anchorMax =
            new Vector2(0.5f, 1f);

        bodyRect.pivot =
            new Vector2(0.5f, 1f);

        bodyRect.sizeDelta =
            new Vector2(540f, 700f);

        bodyRect.anchoredPosition =
            Vector2.zero;

        body.textWrappingMode =
            TextWrappingModes.Normal;

        body.overflowMode =
            TextOverflowModes.Overflow;

        body.enableAutoSizing = false;
        body.lineSpacing = 5f;

        body.color =
            new Color(0.22f, 0.115f, 0.06f);

        readingScroll.content = bodyRect;

        if (handwritingFont != null)
        {
            heading.font = handwritingFont;
            body.font = handwritingFont;
        }

        // -------------------------------------
        // CLOSE HINT
        // -------------------------------------

        TMP_Text closeHint = AddText(
            readingPanel.transform,
            "CloseHint",
            23f,
            TextAlignmentOptions.Center);

        Anchor(
            closeHint.rectTransform,
            new Vector2(0.16f, 0.06f),
            new Vector2(0.84f, 0.115f));

        closeHint.text =
            "PRESS E TO CLOSE  |  MOUSE WHEEL TO SCROLL";

        closeHint.color =
            new Color(0.26f, 0.14f, 0.075f);

        readingPanel.SetActive(false);

        // -------------------------------------
        // JUMPSCARE SCREEN FLASH
        // -------------------------------------

        GameObject flashObject =
            new GameObject(
                "JumpscareFlash",
                typeof(RectTransform),
                typeof(Image));

        flashObject.transform.SetParent(
            root.transform,
            false);

        Stretch(
            flashObject.GetComponent<RectTransform>());

        flash =
            flashObject.GetComponent<Image>();

        flash.color = Color.clear;
        flash.raycastTarget = false;

        SetPrompt(false);
    }

    // =====================================================
    // UI HELPERS
    // =====================================================

    private static TMP_Text AddText(
        Transform parent,
        string objectName,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject go =
            new GameObject(
                objectName,
                typeof(RectTransform));

        go.transform.SetParent(
            parent,
            false);

        TextMeshProUGUI label =
            go.AddComponent<TextMeshProUGUI>();

        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;

        return label;
    }

    private static void Anchor(
        RectTransform rect,
        Vector2 minimum,
        Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(
        RectTransform rect)
    {
        Anchor(
            rect,
            Vector2.zero,
            Vector2.one);
    }

    private void SetPrompt(bool visible)
    {
        if (promptObject != null)
        {
            promptObject.SetActive(visible);
        }
    }

    // =====================================================
    // GENERATE OLD PARCHMENT TEXTURE
    // =====================================================

    private static Sprite BuildParchmentSprite(
        int width,
        int height,
        bool scribbles)
    {
        Texture2D texture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);

        texture.wrapMode =
            TextureWrapMode.Clamp;

        texture.filterMode =
            FilterMode.Bilinear;

        Color[] pixels =
            new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u =
                    x / (float)(width - 1);

                float v =
                    y / (float)(height - 1);

                float stain =
                    Mathf.PerlinNoise(
                        u * 7.4f + 2.1f,
                        v * 8.9f + 3.1f);

                float grain =
                    Mathf.PerlinNoise(
                        u * 45f,
                        v * 54f);

                float edge =
                    Mathf.Min(
                        Mathf.Min(u, 1f - u),
                        Mathf.Min(v, 1f - v));

                float tears =
                    Mathf.PerlinNoise(
                        u * 28f,
                        v * 21f) * 0.021f;

                float alpha =
                    Mathf.Clamp01(
                        (edge - 0.018f - tears)
                        * 155f);

                float darkness =
                    Mathf.Clamp01(
                        (0.095f - edge) * 6.5f);

                float tone =
                    0.96f -
                    stain * 0.19f -
                    grain * 0.08f -
                    darkness * 0.40f;

                pixels[y * width + x] =
                    new Color(
                        tone,
                        tone * 0.78f,
                        tone * 0.47f,
                        alpha);
            }
        }

        // Faint handwritten-looking scribbles
        // for the parchment visible in the house.
        if (scribbles)
        {
            for (int line = 0; line < 18; line++)
            {
                float v =
                    0.80f - line * 0.034f;

                float lineLength =
                    line % 5 == 4
                        ? 0.30f
                        : 0.62f;

                for (int s = 0; s <= 200; s++)
                {
                    float t = s / 200f;

                    float u =
                        0.18f + lineLength * t;

                    float vy =
                        v +
                        Mathf.Sin(
                            t * 75f + line) * 0.003f +
                        Mathf.Sin(
                            t * 22f + line * 3f) *
                        0.002f;

                    int px =
                        Mathf.RoundToInt(
                            u * (width - 1));

                    int py =
                        Mathf.RoundToInt(
                            vy * (height - 1));

                    if (px < 0 ||
                        px >= width ||
                        py < 0 ||
                        py >= height)
                    {
                        continue;
                    }

                    int index =
                        py * width + px;

                    Color ink = new Color(
                        0.17f,
                        0.09f,
                        0.05f,
                        pixels[index].a);

                    pixels[index] =
                        Color.Lerp(
                            pixels[index],
                            ink,
                            0.40f);
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(
                0,
                0,
                width,
                height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}
