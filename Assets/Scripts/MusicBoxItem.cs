
using UnityEngine;

// MUSIC BOX QUEST
//
// Press E = Insert ONE music note.
// After inserting all 3 notes, the music box
// releases the Exit Key.
//
// Compatible with:
// GameManager
// Inventory
// PlayerController
// IInteractable
// BuildMusicBox.cs

public class MusicBoxItem : MonoBehaviour, IInteractable
{
    [Header("Music Note Insertion")]
    public int requiredNotes = 3;

    [Header("Optional Inserted Note Visuals")]
    public GameObject note1Visual;
    public GameObject note2Visual;
    public GameObject note3Visual;

    [Header("Key Reward")]
    public Transform keySpawnPoint;
    public float fallbackKeyHeight = 0.45f;

    [Header("Music Box Look")]
    public Transform platter;
    public float spinSpeed = 25f;
    public Light glow;

    [Range(0f, 1f)]
    public float glowPulse = 0.25f;

    [Header("Audio")]
    public AudioClip insertNoteSound;
    public AudioClip completionMelody;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    [Header("Legacy Builder Compatibility")]
    // Used by BuildMusicBox.cs.
    public AudioClip pickupSound;

    [Range(0f, 1f)]
    public float pickupVolume = 0.8f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private int insertedNotes = 0;
    private bool activated = false;
    private float baseIntensity;

    public int InsertedNotes => insertedNotes;

    public string Prompt
    {
        get
        {
            GameManager gm = GameManager.Instance;
            Inventory inv = Inventory.Instance;

            if (activated ||
                (gm != null && gm.MusicBoxFound))
            {
                return "Melody completed";
            }

            if (insertedNotes >= requiredNotes)
            {
                return "Music Box completed";
            }

            if (inv == null)
            {
                return "Music Box";
            }

            if (inv.CountMusicNotes() > 0)
            {
                return "Insert music note (" +
                       insertedNotes + "/" +
                       requiredNotes + ")";
            }

            return "Find music notes (" +
                   insertedNotes + "/" +
                   requiredNotes + ")";
        }
    }

    void Start()
    {
        requiredNotes = 3;

        if (glow != null)
        {
            baseIntensity = glow.intensity;
        }

        GameManager gm = GameManager.Instance;

        if (gm != null && gm.MusicBoxFound)
        {
            activated = true;
            insertedNotes = requiredNotes;
        }

        UpdateNoteVisuals();
    }

    void Update()
    {
        if (platter != null)
        {
            float speed = activated
                ? spinSpeed * 1.5f
                : spinSpeed;

            platter.Rotate(
                0f,
                speed * Time.deltaTime,
                0f,
                Space.Self);
        }

        if (glow != null)
        {
            glow.intensity =
                baseIntensity *
                (1f + glowPulse *
                Mathf.Sin(Time.time * 2.2f));
        }
    }

    public void Interact(PlayerController player)
    {
        GameManager gm = GameManager.Instance;
        Inventory inv = Inventory.Instance;

        if (player == null ||
            gm == null ||
            inv == null ||
            gm.IsEnded ||
            activated ||
            gm.MusicBoxFound)
        {
            return;
        }

        if (insertedNotes >= requiredNotes)
            return;

        // Find ONE available music note.
        string noteToInsert = FindAvailableNote(inv);

        if (string.IsNullOrEmpty(noteToInsert))
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "[MusicBox] No music notes " +
                    "available in inventory.");
            }

            return;
        }

        bool isFinalNote =
            insertedNotes + 1 >= requiredNotes;

        // Final insertion:
        // Make sure the quest counter is complete.
        if (isFinalNote)
        {
            if (!gm.HasAllMusicNotes)
            {
                Debug.LogWarning(
                    "[MusicBox] Quest counter is " +
                    gm.Pages + "/" + gm.totalPages +
                    ". Cannot release key yet.");

                return;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (keySpawnPoint != null)
            {
                spawnPosition = keySpawnPoint.position;
                spawnRotation = keySpawnPoint.rotation;
            }
            else
            {
                // Spawn in front of the music box.
                spawnPosition =
                    transform.position +
                    Vector3.up * fallbackKeyHeight -
                    transform.forward * 0.35f;

                spawnRotation = Quaternion.identity;
            }

            // Release the key on the third insertion.
            bool success = gm.ActivateMusicBox(
                spawnPosition,
                spawnRotation);

            if (!success)
            {
                Debug.LogWarning(
                    "[MusicBox] Key could not spawn. " +
                    "Check GameManager Key Prefab.");

                return;
            }
        }

        // Remove only ONE music note.
        bool removed = inv.RemoveItem(
            noteToInsert,
            1);

        if (!removed)
        {
            Debug.LogWarning(
                "[MusicBox] Failed to remove " +
                noteToInsert);

            return;
        }

        insertedNotes++;

        // Display inserted note pieces.
        UpdateNoteVisuals();

        // Play insertion sound.
        AudioClip insertClip =
            insertNoteSound != null
            ? insertNoteSound
            : pickupSound;

        if (insertClip != null)
        {
            float volume =
                insertNoteSound != null
                ? soundVolume
                : pickupVolume;

            AudioSource.PlayClipAtPoint(
                insertClip,
                transform.position,
                volume);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[MusicBox] Inserted " +
                noteToInsert +
                " | Progress: " +
                insertedNotes + "/" +
                requiredNotes);
        }

        // Complete after the third note.
        if (isFinalNote)
        {
            CompleteMusicBox();
        }
    }

    string FindAvailableNote(Inventory inv)
    {
        // Numbered music note pieces.
        if (inv.HasItem("Music Note 1"))
        {
            return "Music Note 1";
        }

        if (inv.HasItem("Music Note 2"))
        {
            return "Music Note 2";
        }

        if (inv.HasItem("Music Note 3"))
        {
            return "Music Note 3";
        }

        // Compatibility with older collectibles.
        if (inv.HasItem("Music Note"))
        {
            return "Music Note";
        }

        if (inv.HasItem("Page"))
        {
            return "Page";
        }

        return null;
    }

    void UpdateNoteVisuals()
    {
        if (note1Visual != null)
        {
            note1Visual.SetActive(
                insertedNotes >= 1);
        }

        if (note2Visual != null)
        {
            note2Visual.SetActive(
                insertedNotes >= 2);
        }

        if (note3Visual != null)
        {
            note3Visual.SetActive(
                insertedNotes >= 3);
        }
    }

    void CompleteMusicBox()
    {
        activated = true;

        // Play completion melody.
        if (completionMelody != null)
        {
            AudioSource.PlayClipAtPoint(
                completionMelody,
                transform.position,
                soundVolume);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "[MusicBox] COMPLETE! " +
                "All three music notes inserted. " +
                "The Exit Key has appeared!");
        }
    }
}
