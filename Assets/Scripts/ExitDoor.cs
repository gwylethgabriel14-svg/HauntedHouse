
using System.Collections;
using UnityEngine;

// Final escape door for the Music Box quest.
// Player must collect the exit key before escaping.

public class ExitDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    public bool requireKey = true;

    [Header("Escape")]
    public float escapeDelay = 1.5f;

    [Header("Audio (Optional)")]
    public AudioClip lockedSound;
    public AudioClip unlockSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool escaping;

    public string Prompt
    {
        get
        {
            GameManager gm = GameManager.Instance;

            if (gm == null)
                return "Exit Door";

            if (escaping)
                return "Escaping...";

            if (!requireKey || gm.HasKey)
                return "Press E to unlock and escape";

            return "Locked - Exit key required";
        }
    }

    public void Interact(PlayerController player)
    {
        GameManager gm = GameManager.Instance;

        if (gm == null ||
            player == null ||
            escaping ||
            gm.IsEnded)
        {
            return;
        }

        // Cannot escape without the exit key.
        if (requireKey && !gm.HasKey)
        {
            if (lockedSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    lockedSound,
                    transform.position,
                    soundVolume);
            }

            gm.ShowHint(
                "It's locked. Find the exit key.",
                3f);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[ExitDoor] Door locked. " +
                    "Player has no exit key.");
            }

            return;
        }

        // Player has the key.
        escaping = true;

        if (unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(
                unlockSound,
                transform.position,
                soundVolume);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[ExitDoor] Exit key accepted! " +
                "Starting escape sequence.");
        }

        StartCoroutine(EscapeSequence());
    }

    IEnumerator EscapeSequence()
    {
        // Brief delay for the unlocking sound.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, escapeDelay));

        GameManager gm = GameManager.Instance;

        if (gm != null && !gm.IsEnded)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "[ExitDoor] SUCCESS! " +
                    "Player escaped the haunted house.");
            }

            gm.Win();
        }
    }
}
