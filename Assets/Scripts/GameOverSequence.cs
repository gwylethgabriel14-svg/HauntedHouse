using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The scary ending of a failed run:
//   PlayCapture(enemy): she catches you, the camera pushes into her face, she speaks, jumpscare, game over.
//   PlayDeath():        you lose your last life, you collapse, game over.
// GameManager calls these. The object is created automatically if the scene doesn't have one.
public class GameOverSequence : MonoBehaviour
{
    [Header("Capture: what she says")]
    [TextArea(1, 3)]
    public string[] captureLines =
    {
        "Where do you think you're going, my child?",
        "I won't lose you again."
    };
    public float charDelay = 0.05f;      // typing speed of the subtitles
    public float holdAfterLine = 1.2f;   // pause after each line

    [Header("Capture: camera")]
    public float enemyHeadHeight = 1.65f;   // fallback lang, kung walang head bone ang enemy
    public float faceHeightOffset = 0.1f;   // mula sa head bone papunta sa gitna ng mukha
    public float approachDistance = 1.4f;   // how close the camera gets to her face
    public float approachTime = 1.6f;
    public float jumpscareDistance = 0.8f;  // the final snap closer

    [Header("Audio (optional)")]
    public AudioClip captureVoice;       // a whispered voice line, if you have one
    public AudioClip stingClip;          // the jumpscare screech
    public AudioClip deathClip;          // you fall
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Game over screen")]
    public string menuScene = "MainMenu";
    public string caughtTitle = "CAUGHT";           // you still have lives left
    public string captureTitle = "GAME OVER";        // that was your last life
    public string captureLine = "She never let go.";
    public string deathTitle = "YOU DIED";
    public string deathLine = "The house keeps what it takes.";
    public TMP_FontAsset font;
    public Color titleColor = new Color(0.62f, 0.05f, 0.05f, 1f);
    public Color textColor = new Color(0.78f, 0.74f, 0.64f, 0.9f);
    public Color hoverColor = new Color(0.97f, 0.94f, 0.84f, 1f);

    static readonly string[] FreezeScripts = { "Flashlight", "ThrowItem", "Interactor" };

    bool running;
    Canvas canvas;
    Image fade, flash;
    TMP_Text subtitle;
    GameObject panel;

    // ------------------------------------------------------------------ public

    public void PlayCapture(EnemyAI enemy)
    {
        if (running) return;
        StartCoroutine(Capture(enemy));
    }

    public void PlayDeath()
    {
        if (running) return;
        StartCoroutine(Death());
    }

    // ------------------------------------------------------------------ capture

    IEnumerator Capture(EnemyAI enemy)
    {
        running = true;
        BuildUI();

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null || enemy == null) { ShowGameOver(captureTitle, captureLine, 0); yield break; }

        Camera camera = player.cam != null ? player.cam.GetComponent<Camera>() : Camera.main;
        if (camera == null) camera = Camera.main;
        Transform cam = camera.transform;

        FreezePlayer(player);

        // she stops and turns to you
        var agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }

        Vector3 flat = player.transform.position - enemy.transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) flat = enemy.transform.forward;
        flat.Normalize();
        Quaternion faceRot = Quaternion.LookRotation(flat);

        // camera: from your eyes to right in front of her face
        Vector3 p0 = cam.position;
        Quaternion r0 = cam.rotation;
        float fov0 = camera.fieldOfView;
        float near0 = camera.nearClipPlane;
        camera.nearClipPlane = 0.03f;   // para hindi maputol ang mukha kapag malapit

        Transform headBone = FindHeadBone(enemy);
        Vector3 head = FacePos(headBone, enemy);
        Vector3 p1 = head + flat * approachDistance;
        Quaternion r1 = Quaternion.LookRotation(head - p1);

        // a cold light on her face so you can see it
        var lightGo = new GameObject("CaptureFaceLight");
        lightGo.transform.position = p1 + Vector3.up * 0.1f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.75f, 0.82f, 1f);
        l.intensity = 3f;
        l.range = 4f;
        l.shadows = LightShadows.None;

        float t = 0f;
        while (t < approachTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / approachTime);

            // sumusunod ang camera sa ulo kahit gumagalaw ang animation
            head = FacePos(headBone, enemy);
            p1 = head + flat * approachDistance;
            r1 = Quaternion.LookRotation(head - p1);

            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, faceRot, 8f * Time.deltaTime);
            cam.position = Vector3.Lerp(p0, p1, k);
            cam.rotation = Quaternion.Slerp(r0, r1, k);
            camera.fieldOfView = Mathf.Lerp(fov0, fov0 - 15f, k);
            yield return null;
        }

        // what she says
        if (captureVoice != null) PlayAt(cam.position, captureVoice);
        foreach (string line in captureLines)
        {
            yield return TypeSubtitle(line);
            yield return new WaitForSeconds(holdAfterLine);
        }
        subtitle.text = "";

        // jumpscare: sudden, loud, too close
        PlayAt(cam.position, stingClip);
        CameraShake.Shake(0.7f, 0.3f);
        StartCoroutine(Flash(0.9f, 0.3f));
        camera.fieldOfView = fov0 - 20f;
        float hold = 0f;
        while (hold < 0.9f)
        {
            hold += Time.deltaTime;
            head = FacePos(headBone, enemy);
            cam.position = head + flat * jumpscareDistance;
            cam.rotation = Quaternion.LookRotation(head - cam.position);
            yield return null;
        }

        yield return FadeTo(1f, 0.35f);
        Destroy(lightGo);
        camera.fieldOfView = fov0;
        camera.nearClipPlane = near0;

        // being caught costs one life
        var health = FindFirstObjectByType<PlayerHealth>();
        int left = health != null ? health.LoseLifeForCapture() : 0;
        ShowGameOver(left > 0 ? caughtTitle : captureTitle, captureLine, left);
    }

    // ------------------------------------------------------------------ death (no lives left)

    IEnumerator Death()
    {
        running = true;
        BuildUI();

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) { ShowGameOver(deathTitle, deathLine, 0); yield break; }

        Transform cam = player.cam != null ? player.cam : Camera.main.transform;
        var cc = player.GetComponent<CharacterController>();

        FreezePlayer(player);
        PlayAt(cam.position, deathClip);
        CameraShake.Shake(0.4f, 0.12f);

        // you collapse: the camera drops to the floor and rolls to the side
        Vector3 lp0 = cam.localPosition;
        float floorLocalY = cc != null ? cc.center.y - cc.height / 2f + 0.25f : lp0.y - 1.2f;
        Vector3 lp1 = new Vector3(lp0.x, floorLocalY, lp0.z);
        Quaternion lr0 = cam.localRotation;
        Quaternion lr1 = Quaternion.Euler(-8f, 0f, 75f);

        float t = 0f, dur = 1.7f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            float ease = k * k;                       // slow start, hits the floor fast
            cam.localPosition = Vector3.Lerp(lp0, lp1, ease);
            cam.localRotation = Quaternion.Slerp(lr0, lr1, ease);
            fade.color = new Color(0.35f, 0f, 0f, 0.7f * k);
            yield return null;
        }

        yield return new WaitForSeconds(0.6f);
        fade.color = new Color(0.35f, 0f, 0f, 0.7f);
        yield return FadeTo(1f, 0.8f);
        ShowGameOver(deathTitle, deathLine, 0);
    }

    // ------------------------------------------------------------------ helpers

    void FreezePlayer(PlayerController player)
    {
        player.CanMove = false;
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (m == null || !m.enabled) continue;
            string n = m.GetType().Name;
            foreach (string f in FreezeScripts)
                if (n == f) m.enabled = false;
        }
    }

    void PlayAt(Vector3 pos, AudioClip clip)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, sfxVolume);
    }

    Transform FindHeadBone(EnemyAI enemy)
    {
        var a = enemy.GetComponentInChildren<Animator>();
        if (a != null && a.isHuman) return a.GetBoneTransform(HumanBodyBones.Head);
        return null;
    }

    Vector3 FacePos(Transform headBone, EnemyAI enemy)
    {
        if (headBone != null) return headBone.position + Vector3.up * faceHeightOffset;
        return enemy.transform.position + Vector3.up * enemyHeadHeight;
    }

    IEnumerator TypeSubtitle(string line)
    {
        subtitle.text = line;
        subtitle.maxVisibleCharacters = 0;
        subtitle.ForceMeshUpdate();
        int total = subtitle.textInfo.characterCount;
        for (int i = 1; i <= total; i++)
        {
            subtitle.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }
        subtitle.maxVisibleCharacters = total;
    }

    IEnumerator FadeTo(float alpha, float seconds)
    {
        Color c = fade.color;
        float a0 = c.a, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(a0, alpha, t / seconds);
            fade.color = c;
            yield return null;
        }
        c.a = alpha;
        fade.color = c;
    }

    IEnumerator Flash(float alpha, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(alpha, 0f, t / seconds));
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    // ------------------------------------------------------------------ UI

    void BuildUI()
    {
        if (canvas != null) return;

        var cgo = new GameObject("GameOverCanvas");
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        fade = MakeFullImage(cgo.transform, "Fade", new Color(0f, 0f, 0f, 0f));
        flash = MakeFullImage(cgo.transform, "Flash", new Color(1f, 1f, 1f, 0f));

        subtitle = MakeText(cgo.transform, "Subtitle", "", 46f, TextAlignmentOptions.Center, new Color(0.9f, 0.86f, 0.76f, 1f));
        var sr = subtitle.rectTransform;
        sr.anchorMin = new Vector2(0.1f, 0f); sr.anchorMax = new Vector2(0.9f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.anchoredPosition = new Vector2(0f, 110f);
        sr.sizeDelta = new Vector2(0f, 120f);
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.characterSpacing = 3f;

        // game over panel (hidden until needed)
        panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(cgo.transform, false);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = pr.offsetMax = Vector2.zero;
        panel.SetActive(false);
    }

    void ShowGameOver(string title, string line, int livesLeft = -1)
    {
        BuildUI();
        fade.color = new Color(0f, 0f, 0f, 1f);
        subtitle.text = "";

        foreach (Transform child in panel.transform) Destroy(child.gameObject);

        var t = MakeText(panel.transform, "Title", title, 130f, TextAlignmentOptions.Center, titleColor);
        t.characterSpacing = 12f;
        Place(t.rectTransform, new Vector2(0f, 190f), new Vector2(1400f, 170f));

        var s = MakeText(panel.transform, "Line", line, 44f, TextAlignmentOptions.Center, textColor);
        s.characterSpacing = 5f;
        Place(s.rectTransform, new Vector2(0f, 70f), new Vector2(1400f, 80f));

        string extra = livesLeft > 0 ? "LIVES LEFT: " + livesLeft : (livesLeft == 0 ? "NO LIVES LEFT" : "");
        if (extra != "")
        {
            var e = MakeText(panel.transform, "LivesLeft", extra, 36f, TextAlignmentOptions.Center,
                             livesLeft > 0 ? hoverColor : titleColor);
            e.characterSpacing = 8f;
            Place(e.rectTransform, new Vector2(0f, -10f), new Vector2(1000f, 60f));
        }

        float y = -100f;
        MakeButton(livesLeft == 0 ? "NEW GAME" : "TRY AGAIN", new Vector2(0f, y), Restart); y -= 90f;
        if (SaveSystem.HasSave()) { MakeButton("LOAD SAVE", new Vector2(0f, y), LoadSave); y -= 90f; }
        MakeButton("MAIN MENU", new Vector2(0f, y), GoToMenu);

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        running = false;
    }

    void MakeButton(string text, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel.transform, false);
        Place(go.GetComponent<RectTransform>(), pos, new Vector2(600f, 80f));
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var label = MakeText(go.transform, "Label", text, 50f, TextAlignmentOptions.Center, textColor);
        label.characterSpacing = 8f;
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        var trigger = go.AddComponent<EventTrigger>();
        AddEvent(trigger, EventTriggerType.PointerEnter, () => label.color = hoverColor);
        AddEvent(trigger, EventTriggerType.PointerExit, () => label.color = textColor);
        AddEvent(trigger, EventTriggerType.PointerClick, () => onClick());
    }

    static void AddEvent(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    static Image MakeFullImage(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

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

    // ------------------------------------------------------------------ buttons

    void Restart()
    {
        // no lives left: the next run starts with all of them again
        if (PlayerHealth.PersistedLives <= 0) PlayerHealth.ResetPersisted();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void LoadSave()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        string scene = SaveSystem.PrepareContinue(SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(scene);
    }

    void GoToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScene))
        {
            Debug.LogError("Scene '" + menuScene + "' is not in Build Settings.");
            return;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(menuScene);
    }
}