
using System.Collections;
using UnityEngine;
using HauntedItems;

// ==========================================================
// THE LAST LULLABY - COMPLETE STARTER ITEMS TEST
//
// ONE SCRIPT FOR ALL TEST ITEMS:
//
// - 3 separate Music Notes
// - 1 Tranquilizer Gun
// - 3 loaded darts
// - 6 spare darts
// - 3 Bear Traps
//
// Works with existing:
// - GameManager
// - SaveSystem
// - Inventory
// - PlayerItemInteractor
// - TranquilizerGun
// - TrapHotbarController
// - TrapItem
//
// NEW GAME ONLY.
// Does not modify an existing saved game.
//
// Attach this to the actual Player.
// Disable before final release.
// ==========================================================

[DisallowMultipleComponent]
public class StartWith3MusicNotes : MonoBehaviour
{
    [Header("MAIN TEST SETTINGS")]
    public bool giveOnStart = true;

    [Header("TEST ITEMS")]
    public bool giveMusicNotes = true;
    public bool giveTranquilizerGun = true;
    public bool giveDarts = true;
    public bool giveBearTraps = true;

    [Header("MUSIC NOTES")]
    public Sprite note1Icon;
    public Sprite note2Icon;
    public Sprite note3Icon;

    [Header("TRANQUILIZER GUN")]
    [Tooltip("Drag the TranquilizerGun PREFAB from Project.")]
    public GameObject tranquilizerGunPrefab;

    public Sprite tranquilizerGunIcon;

    public string gunItemName = "Tranquilizer Gun";

    [Min(0)]
    public int startingLoadedAmmo = 3;

    [Header("TRANQUILIZER DARTS")]
    public Sprite dartIcon;

    public string dartItemName = "Tranquilizer Darts";

    [Min(0)]
    public int startingSpareDarts = 6;

    [Header("BEAR TRAPS")]
    [Tooltip("Drag the Bear Trap PREFAB from Project.")]
    public GameObject bearTrapPrefab;

    public Sprite trapIcon;

    public string trapItemName = "Bear Trap";

    [Min(0)]
    public int startingTraps = 3;

    [Header("HELD TRAP APPEARANCE")]
    public Vector3 heldTrapPosition =
        new Vector3(0.25f, -0.26f, 0.55f);

    public Vector3 heldTrapRotation =
        new Vector3(12f, -25f, 5f);

    public float heldTrapScale = 0.55f;

    [Header("AUTO FIND REFERENCES")]
    public Camera playerCamera;
    public PlayerItemInteractor playerInteractor;
    public TrapHotbarController trapController;

    [Header("SAVE PROTECTION")]
    public bool disableAutosaveDuringTest = true;

    [Header("DEBUG")]
    public bool showDebugLogs = true;

    // ==========================================================
    // PRIVATE VARIABLES
    // ==========================================================

    private bool itemsGiven;
    private bool testingInitialized;
    private bool syncDartsWithInventory;

    private Inventory inventory;

    private GameObject testGun;
    private GameObject trapHeldPreview;

    // ==========================================================
    // START
    // ==========================================================

    private IEnumerator Start()
    {
        if (!giveOnStart)
            yield break;

        if (SaveSystem.LoadingFromSave)
        {
            Debug.LogWarning(
                "[Starter Test] This system only works " +
                "with NEW GAME, not Continue.",
                this);

            yield break;
        }

        // Wait for required systems to initialize.
        for (int i = 0; i < 20; i++)
        {
            FindReferences();

            if (Inventory.Instance != null &&
                GameManager.Instance != null)
            {
                break;
            }

            yield return null;
        }

        yield return null;

        GiveAllTestItems();
    }

    // ==========================================================
    // FIND REFERENCES
    // ==========================================================

    private void FindReferences()
    {
        inventory = Inventory.Instance;

        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerInteractor == null)
        {
            playerInteractor =
                GetComponent<PlayerItemInteractor>();
        }

        if (playerInteractor == null)
        {
            playerInteractor =
                GetComponentInChildren<PlayerItemInteractor>(true);
        }

        if (playerInteractor == null)
        {
            playerInteractor =
                GetComponentInParent<PlayerItemInteractor>();
        }

        if (trapController == null)
        {
            trapController =
                GetComponent<TrapHotbarController>();
        }

        if (trapController == null)
        {
            trapController =
                GetComponentInChildren<TrapHotbarController>(true);
        }

        if (bearTrapPrefab == null &&
            trapController != null)
        {
            bearTrapPrefab =
                trapController.trapPrefab;
        }
    }

    // ==========================================================
    // GIVE ALL ITEMS
    // ==========================================================

    [ContextMenu("Give All Test Items")]
    public void GiveAllTestItems()
    {
        if (itemsGiven)
        {
            Log("Test items already given.");
            return;
        }

        if (SaveSystem.LoadingFromSave)
        {
            Debug.LogWarning(
                "[Starter Test] Cannot grant items " +
                "while loading a saved game.",
                this);

            return;
        }

        FindReferences();

        GameManager gm = GameManager.Instance;
        Inventory inv = Inventory.Instance;

        if (gm == null || inv == null)
        {
            Debug.LogWarning(
                "[Starter Test] Missing GameManager " +
                "or Inventory.",
                this);

            return;
        }

        if (disableAutosaveDuringTest)
        {
            gm.autoSaveOnProgress = false;
        }

        int previousSlot = inv.SelectedIndex;

        // First: original three separate music notes.
        if (giveMusicNotes)
            GiveNotes();

        // Second: gun.
        if (giveTranquilizerGun)
            GiveGun();

        // Third: spare ammunition.
        if (giveDarts)
            GiveSpareDarts();

        // Fourth: bear traps.
        if (giveBearTraps)
            GiveTraps();

        inv.Select(previousSlot);

        itemsGiven = true;
        testingInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log(
                "[Starter Test] ALL TEST ITEMS READY!\n" +
                "Music Notes: " +
                inv.CountMusicNotes() + "/3\n" +
                "Tranquilizer Guns: " +
                inv.CountItem(gunItemName) + "\n" +
                "Spare Darts: " +
                (playerInteractor != null
                    ? playerInteractor.darts
                    : 0) + "\n" +
                "Bear Traps: " +
                inv.CountItem(trapItemName),
                this);
        }
    }

    // ==========================================================
    // ORIGINAL MUSIC NOTES FUNCTION
    // ==========================================================

    [ContextMenu("Give 3 Music Notes For Testing")]
    public void GiveNotes()
    {
        GameManager gm = GameManager.Instance;
        Inventory inv = Inventory.Instance;

        if (gm == null || inv == null)
        {
            Debug.LogWarning(
                "[Test Notes] Missing GameManager " +
                "or Inventory.",
                this);

            return;
        }

        if (SaveSystem.LoadingFromSave)
        {
            Debug.LogWarning(
                "[Test Notes] Use New Game, " +
                "not Continue, for testing.",
                this);

            return;
        }

        if (gm.totalPages != 3)
        {
            Debug.LogWarning(
                "[Test Notes] Set Total Pages to 3.",
                this);

            return;
        }

        if (gm.MusicBoxFound)
        {
            Debug.LogWarning(
                "[Test Notes] Music Box already activated.",
                this);

            return;
        }

        Sprite[] icons =
        {
            note1Icon,
            note2Icon,
            note3Icon
        };

        int added = 0;

        for (int piece = 1; piece <= 3; piece++)
        {
            string itemName =
                "Music Note " + piece;

            if (inv.HasItem(itemName))
                continue;

            bool success =
                inv.AddMusicNote(
                    piece,
                    icons[piece - 1]);

            if (!success)
            {
                Debug.LogWarning(
                    "[Test Notes] Hotbar full. " +
                    "Free up a slot.",
                    this);

                break;
            }

            added++;
        }

        if (disableAutosaveDuringTest)
        {
            gm.autoSaveOnProgress = false;
        }

        // Preserve the original music-box quest logic.
        while (gm.Pages < 3 &&
               inv.CountMusicNotes() >= 3)
        {
            gm.CollectPage();
        }

        Log(
            "Music Notes added: " +
            added +
            ". Total: " +
            inv.CountMusicNotes() +
            "/3.");
    }

    // ==========================================================
    // GIVE TRANQUILIZER GUN
    // ==========================================================

    private void GiveGun()
    {
        if (inventory == null)
            return;

        if (inventory.HasItem(gunItemName))
        {
            Log("Tranquilizer Gun already owned.");
            return;
        }

        if (tranquilizerGunPrefab == null)
        {
            Debug.LogWarning(
                "[Starter Test] Assign the " +
                "Tranquilizer Gun Prefab.",
                this);

            return;
        }

        if (playerCamera == null ||
            playerInteractor == null)
        {
            Debug.LogWarning(
                "[Starter Test] Gun needs a " +
                "Main Camera and PlayerItemInteractor.",
                this);

            return;
        }

        testGun = Instantiate(
            tranquilizerGunPrefab,
            playerCamera.transform.position,
            playerCamera.transform.rotation);

        testGun.name =
            "TEST_TranquilizerGun";

        TranquilizerGun gun =
            testGun.GetComponent<TranquilizerGun>();

        if (gun == null)
        {
            gun = testGun.GetComponentInChildren<
                TranquilizerGun>(true);
        }

        if (gun == null)
        {
            Debug.LogError(
                "[Starter Test] Tranquilizer Gun " +
                "Prefab has no TranquilizerGun component.",
                this);

            Destroy(testGun);
            testGun = null;

            return;
        }

        gun.enabled = true;

        gun.inventoryItemName =
            gunItemName;

        if (tranquilizerGunIcon != null)
        {
            gun.inventoryIcon =
                tranquilizerGunIcon;
        }

        gun.addToInventoryOnEquip = true;
        gun.doNotAutoSelect = true;

        gun.magazineSize =
            Mathf.Max(1, gun.magazineSize);

        gun.ammoInMagazine =
            Mathf.Clamp(
                startingLoadedAmmo,
                0,
                gun.magazineSize);

        // Existing gun system registers itself in Inventory.
        gun.Equip(
            playerInteractor,
            playerCamera);

        if (!inventory.HasItem(gunItemName))
        {
            Debug.LogWarning(
                "[Starter Test] Gun registration failed. " +
                "Check free slots and gun setup.",
                this);

            Destroy(testGun);
            testGun = null;

            return;
        }

        gun.RefreshVisuals();

        Log(
            "Tranquilizer Gun added. Loaded darts: " +
            gun.ammoInMagazine);
    }

    // ==========================================================
    // GIVE SPARE DARTS
    // ==========================================================

    private void GiveSpareDarts()
    {
        if (inventory == null)
            return;

        if (playerInteractor == null)
        {
            Debug.LogWarning(
                "[Starter Test] PlayerItemInteractor " +
                "missing. Cannot give darts.",
                this);

            return;
        }

        int existingDarts =
            Mathf.Max(0, playerInteractor.darts);

        int requested =
            Mathf.Max(0, startingSpareDarts);

        // Preserve already owned ammunition.
        playerInteractor.darts =
            Mathf.Max(existingDarts, requested);

        // Sync the current ammo count to the hotbar.
        SyncDarts();

        Log(
            "Spare Darts added. Total: " +
            playerInteractor.darts);
    }

    // ==========================================================
    // GIVE BEAR TRAPS
    // ==========================================================

    private void GiveTraps()
    {
        if (inventory == null)
            return;

        if (startingTraps <= 0)
            return;

        if (inventory.HasItem(trapItemName))
        {
            Log("Bear Traps already in Inventory.");
            return;
        }

        if (bearTrapPrefab == null)
        {
            Debug.LogWarning(
                "[Starter Test] Assign Bear Trap Prefab.",
                this);

            return;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning(
                "[Starter Test] Cannot give traps " +
                "without a Player Camera.",
                this);

            return;
        }

        trapHeldPreview =
            CreateHeldTrapPreview();

        if (trapHeldPreview == null)
        {
            Debug.LogError(
                "[Starter Test] Failed to create " +
                "trap preview.",
                this);

            return;
        }

        bool added = inventory.AddItem(
            trapItemName,
            trapIcon,
            trapHeldPreview,
            true,
            startingTraps);

        if (!added)
        {
            Debug.LogWarning(
                "[Starter Test] No free hotbar slot " +
                "for Bear Traps.",
                this);

            Destroy(trapHeldPreview);
            trapHeldPreview = null;

            return;
        }

        if (trapController != null)
        {
            trapController.trapItemName =
                trapItemName;

            trapController.trapPrefab =
                bearTrapPrefab;

            if (trapIcon != null)
            {
                trapController.trapIcon =
                    trapIcon;
            }
        }

        Log(
            startingTraps +
            " Bear Traps added to Inventory.");
    }

    // ==========================================================
    // HELD TRAP MODEL
    // ==========================================================

    private GameObject CreateHeldTrapPreview()
    {
        GameObject preview = Instantiate(
            bearTrapPrefab,
            playerCamera.transform);

        preview.name =
            "TEST_HeldBearTrap";

        preview.transform.localPosition =
            heldTrapPosition;

        preview.transform.localRotation =
            Quaternion.Euler(heldTrapRotation);

        preview.transform.localScale =
            Vector3.one * heldTrapScale;

        // Never allow a held trap to trigger.
        foreach (TrapItem trap in
                 preview.GetComponentsInChildren<
                     TrapItem>(true))
        {
            trap.armOnStart = false;
            trap.enabled = false;
        }

        foreach (Collider col in
                 preview.GetComponentsInChildren<
                     Collider>(true))
        {
            col.enabled = false;
        }

        foreach (Rigidbody rb in
                 preview.GetComponentsInChildren<
                     Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }

        foreach (PickupItem pickup in
                 preview.GetComponentsInChildren<
                     PickupItem>(true))
        {
            pickup.enabled = false;
        }

        preview.SetActive(false);

        return preview;
    }

    // ==========================================================
    // DART INVENTORY SYNCHRONIZATION
    // ==========================================================

    private void Update()
    {
        if (!testingInitialized ||
            inventory == null)
        {
            return;
        }

        if (syncDartsWithInventory &&
            playerInteractor != null)
        {
            SyncDarts();
        }

        // Clean up preview if traps run out.
        if (trapHeldPreview != null &&
            !inventory.HasItem(trapItemName))
        {
            Destroy(trapHeldPreview);
            trapHeldPreview = null;
        }
    }

    private void SyncDarts()
    {
        if (inventory == null ||
            playerInteractor == null)
        {
            return;
        }

        int actual =
            Mathf.Max(0, playerInteractor.darts);

        int displayed =
            inventory.CountItem(dartItemName);

        if (actual == displayed)
        {
            syncDartsWithInventory = true;
            return;
        }

        // Add missing displayed darts.
        if (actual > displayed)
        {
            int difference = actual - displayed;

            bool success = inventory.AddItem(
                dartItemName,
                dartIcon,
                null,
                true,
                difference);

            if (!success)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning(
                        "[Starter Test] No free slot " +
                        "for spare darts.",
                        this);
                }

                return;
            }
        }
        else
        {
            // Remove darts after gun reloads.
            int difference = displayed - actual;

            inventory.RemoveItem(
                dartItemName,
                difference);
        }

        syncDartsWithInventory = true;
    }

    // ==========================================================
    // DEBUG
    // ==========================================================

    private void Log(string message)
    {
        if (!showDebugLogs)
            return;

        Debug.Log(
            "[Starter Test] " + message,
            this);
    }
}
