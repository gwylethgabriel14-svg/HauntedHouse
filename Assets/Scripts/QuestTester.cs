
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// TEMPORARY TEST ONLY. Attach to an empty GameObject in the scene.
// Remove/disable before release. Can affect autosaved quest progress.
public class QuestTester : MonoBehaviour
{
    public PlayerController player;
    public MusicBoxManager musicBoxManager;
    public float distanceAhead = 2.0f;

    void Update()
    {
        if (Pressed(KeyCode.F5, 5))
            BringNotes();

        if (Pressed(KeyCode.F6, 6))
            BringMusicBox();

        if (Pressed(KeyCode.F7, 7))
            GiveRemainingNotes();

        if (Pressed(KeyCode.F8, 8))
            ActivateMusicBox();
    }

    bool Pressed(KeyCode legacyKey, int number)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (number == 5 && Keyboard.current.f5Key.wasPressedThisFrame) return true;
            if (number == 6 && Keyboard.current.f6Key.wasPressedThisFrame) return true;
            if (number == 7 && Keyboard.current.f7Key.wasPressedThisFrame) return true;
            if (number == 8 && Keyboard.current.f8Key.wasPressedThisFrame) return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(legacyKey))
            return true;
#endif
        return false;
    }

    void Resolve()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (musicBoxManager == null)
            musicBoxManager = FindFirstObjectByType<MusicBoxManager>();
    }

    Vector3 Front(float distance)
    {
        Vector3 forward = player.transform.forward;
        forward.y = 0;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        return player.transform.position +
               forward.normalized * distance;
    }

    void BringNotes()
    {
        Resolve();

        if (player == null)
            return;

        Page[] notes = FindObjectsByType<Page>(
            FindObjectsSortMode.None);

        Vector3 right = player.transform.right;

        for (int i = 0; i < notes.Length; i++)
        {
            Vector3 pos =
                Front(distanceAhead) +
                right *
                ((i - (notes.Length - 1) * 0.5f) * 0.65f);

            pos.y = player.transform.position.y + 1.35f;

            notes[i].transform.position = pos;

            notes[i].transform.rotation =
                Quaternion.LookRotation(
                    player.transform.position - pos);
        }

        Debug.Log(
            "[QuestTester] F5: brought " +
            notes.Length +
            " remaining notes to player.");
    }

    void BringMusicBox()
    {
        Resolve();

        if (player == null || musicBoxManager == null)
            return;

        Transform box =
            musicBoxManager.GetActiveMusicBox();

        if (box == null)
        {
            Debug.LogWarning(
                "Music Box not spawned or restored.");
            return;
        }

        Renderer[] rr =
            box.GetComponentsInChildren<Renderer>();

        float lowest = float.PositiveInfinity;

        foreach (Renderer r in rr)
        {
            if (r.enabled &&
                !(r is ParticleSystemRenderer))
            {
                lowest = Mathf.Min(
                    lowest,
                    r.bounds.min.y);
            }
        }

        Vector3 desired = Front(distanceAhead);
        desired.y = player.transform.position.y + 0.05f;

        if (float.IsInfinity(lowest))
        {
            box.position = desired;
        }
        else
        {
            box.position += desired -
                new Vector3(
                    box.position.x,
                    lowest,
                    box.position.z);
        }

        Debug.Log(
            "[QuestTester] F6: music box moved close to player (debug only).");
    }

    void GiveRemainingNotes()
    {
        Resolve();

        if (player == null ||
            GameManager.Instance == null)
        {
            return;
        }

        Page[] notes =
            FindObjectsByType<Page>(
                FindObjectsSortMode.None);

        foreach (Page note in notes)
        {
            if (GameManager.Instance.Pages >=
                GameManager.Instance.totalPages)
            {
                break;
            }

            // Uses the real pickup logic, including inventory.
            note.Interact(player);
        }

        Debug.Log(
            "[QuestTester] F7: progress " +
            GameManager.Instance.Pages +
            "/" +
            GameManager.Instance.totalPages);
    }

    void ActivateMusicBox()
    {
        Resolve();

        if (player == null || musicBoxManager == null)
            return;

        Transform box =
            musicBoxManager.GetActiveMusicBox();

        if (box == null)
        {
            Debug.LogWarning("Music box not found.");
            return;
        }

        MusicBoxItem item =
            box.GetComponent<MusicBoxItem>();

        if (item == null)
        {
            item =
                box.GetComponentInChildren<MusicBoxItem>();
        }

        if (item == null)
        {
            Debug.LogWarning(
                "Music box prefab needs MusicBoxItem.");
            return;
        }

        item.Interact(player);

        Debug.Log(
            "[QuestTester] F8: attempted Music Box interaction.");
    }
}
