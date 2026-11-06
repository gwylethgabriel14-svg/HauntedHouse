
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Horror game waking-up animation.
// Eyes slowly open, blink, then fully wake up.
// Includes safety checks to prevent permanent black screen.

public class WakeUp : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Range(0f, 1f)]
        public float blackness = 1f;

        [Min(0f)]
        public float time = 0.5f;

        [Min(0f)]
        public float hold = 0.2f;
    }

    [Header("UI")]
    public Image overlay;

    [Header("Player")]
    [Tooltip("Movement and look scripts disabled during waking.")]
    public MonoBehaviour[] disableWhileWaking;

    [Header("Timing")]
    public float startDelay = 1f;

    [Header("Blink Pattern")]
    public Step[] steps = new Step[]
    {
        new Step
        {
            blackness = 0.55f,
            time = 1.2f,
            hold = 0.4f
        },

        new Step
        {
            blackness = 1f,
            time = 0.4f,
            hold = 0.6f
        },

        new Step
        {
            blackness = 0.25f,
            time = 0.9f,
            hold = 0.5f
        },

        new Step
        {
            blackness = 1f,
            time = 0.3f,
            hold = 0.4f
        },

        new Step
        {
            blackness = 0f,
            time = 1.5f,
            hold = 0f
        }
    };

    [Header("Audio")]
    public AudioClip wakeSound;

    [Range(0f, 1f)]
    public float wakeVolume = 0.6f;

    [Header("Safety")]
    [Tooltip("Automatically removes black overlay if animation takes too long.")]
    public float maxWakeDuration = 15f;

    [Tooltip("Disable BlackOverlay after waking.")]
    public bool disableOverlayWhenFinished = true;

    Coroutine wakeRoutine;
    bool waking;
    bool finished;

    float wakeStartTime;

    void Start()
    {
        if (overlay == null)
        {
            Debug.LogWarning(
                "[WakeUp] BlackOverlay is not assigned!",
                this);

            SetPlayer(true);
            enabled = false;
            return;
        }

        // Ensure overlay is available at start.
        overlay.gameObject.SetActive(true);
        overlay.raycastTarget = false;

        SetAlpha(1f);
        SetPlayer(false);

        waking = true;
        finished = false;
        wakeStartTime = Time.unscaledTime;

        wakeRoutine = StartCoroutine(RunWakeUp());
    }

    void Update()
    {
        if (!waking || finished)
            return;

        // Safety timeout.
        if (maxWakeDuration > 0f &&
            Time.unscaledTime - wakeStartTime >=
            maxWakeDuration)
        {
            Debug.LogWarning(
                "[WakeUp] Animation timeout. " +
                "Removing black overlay.",
                this);

            if (wakeRoutine != null)
            {
                StopCoroutine(wakeRoutine);
                wakeRoutine = null;
            }

            FinishWakeUp();
        }
    }

    IEnumerator RunWakeUp()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                startDelay);
        }

        if (wakeSound != null)
        {
            Camera cam = Camera.main;

            Vector3 soundPos = cam != null
                ? cam.transform.position
                : transform.position;

            AudioSource.PlayClipAtPoint(
                wakeSound,
                soundPos,
                wakeVolume);
        }

        if (steps != null)
        {
            foreach (Step step in steps)
            {
                if (step == null)
                    continue;

                if (overlay == null)
                    break;

                float from = overlay.color.a;
                float target = Mathf.Clamp01(
                    step.blackness);

                float duration = Mathf.Max(
                    0.01f, step.time);

                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (overlay == null)
                        break;

                    elapsed += Time.unscaledDeltaTime;

                    float progress = Mathf.Clamp01(
                        elapsed / duration);

                    float smooth = Mathf.SmoothStep(
                        0f, 1f, progress);

                    SetAlpha(Mathf.Lerp(
                        from, target, smooth));

                    yield return null;
                }

                if (overlay == null)
                    break;

                SetAlpha(target);

                if (step.hold > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        step.hold);
                }
            }
        }

        FinishWakeUp();
    }

    void FinishWakeUp()
    {
        if (finished)
            return;

        finished = true;
        waking = false;
        wakeRoutine = null;

        SetAlpha(0f);

        if (overlay != null &&
            disableOverlayWhenFinished)
        {
            overlay.gameObject.SetActive(false);
        }

        SetPlayer(true);

        Debug.Log(
            "[WakeUp] Animation completed. " +
            "Player is awake!",
            this);
    }

    void SetPlayer(bool active)
    {
        if (disableWhileWaking == null)
            return;

        foreach (MonoBehaviour behaviour
                 in disableWhileWaking)
        {
            if (behaviour != null &&
                behaviour != this)
            {
                behaviour.enabled = active;
            }
        }
    }

    void SetAlpha(float alpha)
    {
        if (overlay == null)
            return;

        Color color = overlay.color;
        color.a = Mathf.Clamp01(alpha);
        overlay.color = color;
    }

    void OnDisable()
    {
        if (!Application.isPlaying ||
            !waking ||
            finished)
        {
            return;
        }

        // Don't leave the screen black if this
        // component is disabled during gameplay.
        FinishWakeUp();
    }
}
