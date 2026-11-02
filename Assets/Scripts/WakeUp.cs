using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Eyes opening effect: a black full-screen Image that "blinks" away.
// Put it on any object in the House scene (e.g. an empty "WakeUp" object).
public class WakeUp : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Range(0f, 1f)] public float blackness = 1f; // 1 = fully closed (black), 0 = fully open
        public float time = 0.5f;                    // how long to reach this blackness
        public float hold = 0.2f;                    // wait after reaching it
    }

    [Header("UI")]
    public Image overlay;                 // full-screen black Image (Raycast Target OFF)

    [Header("Player")]
    [Tooltip("Movement / look scripts to freeze until the eyes are open")]
    public MonoBehaviour[] disableWhileWaking;

    [Header("Timing")]
    public float startDelay = 1.0f;       // stays black before the first blink

    [Header("Blink pattern")]
    public Step[] steps = new Step[]
    {
        new Step { blackness = 0.55f, time = 1.2f, hold = 0.4f }, // eyes crack open
        new Step { blackness = 1.00f, time = 0.4f, hold = 0.6f }, // close again
        new Step { blackness = 0.25f, time = 0.9f, hold = 0.5f }, // open more
        new Step { blackness = 1.00f, time = 0.3f, hold = 0.4f }, // heavy blink
        new Step { blackness = 0.00f, time = 1.5f, hold = 0.0f }, // fully awake
    };

    [Header("Audio (optional)")]
    public AudioClip wakeSound;           // e.g. a breath or a heartbeat
    [Range(0f, 1f)] public float wakeVolume = 0.6f;

    void Start()
    {
        Time.timeScale = 1f;
        SetAlpha(1f);
        SetPlayer(false);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        if (wakeSound != null)
            AudioSource.PlayClipAtPoint(wakeSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, wakeVolume);

        foreach (Step s in steps)
        {
            float from = overlay.color.a;
            float t = 0f;
            float dur = Mathf.Max(0.01f, s.time);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                // smooth in/out so it feels like heavy eyelids
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                SetAlpha(Mathf.Lerp(from, s.blackness, k));
                yield return null;
            }
            SetAlpha(s.blackness);
            if (s.hold > 0f) yield return new WaitForSecondsRealtime(s.hold);
        }

        SetAlpha(0f);
        overlay.gameObject.SetActive(false);
        SetPlayer(true);
    }

    void SetPlayer(bool on)
    {
        if (disableWhileWaking == null) return;
        foreach (var m in disableWhileWaking)
            if (m != null) m.enabled = on;
    }

    void SetAlpha(float a)
    {
        if (overlay == null) return;
        Color c = overlay.color;
        c.a = a;
        overlay.color = c;
    }
}