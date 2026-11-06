using System;
using System.Collections.Generic;
using HauntedItems;
using UnityEngine;

// THE LAST LULLABY - TRAP HOTBAR
//
// E          = Pick up unarmed trap (through the Interactor, TrapItem is IInteractable)
// Number 1-9 = Select item (handled by Inventory)
// Left Click = Place selected trap, instantly armed
// Q          = Drop selected trap, unarmed
//
// Add this ONLY to the actual Player object.

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class TrapHotbarController : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Inventory inventory;

    [Tooltip("Drag the Bear Trap prefab ASSET from Project.")]
    public GameObject trapPrefab;

    public Sprite trapIcon;

    public string trapItemName = "Bear Trap";

    [Header("Controls")]
    public KeyCode dropKey = KeyCode.Q;
    public int placeMouseButton = 0;

    public float placeRange = 3f;

    public LayerMask interactionMask = ~0;

    [Header("Held Trap Appearance")]
    public Vector3 heldPosition =
        new Vector3(0.25f, -0.26f, 0.55f);

    public Vector3 heldRotation =
        new Vector3(12f, -25f, 5f);

    public float heldScale = 0.55f;

    [Header("World Placement")]
    public float surfaceOffset = 0.035f;
    public float clearanceRadius = 0.30f;

    [Range(0.5f, 1f)]
    public float minimumSurfaceNormal = 0.82f;

    [Header("Drop Noise")]
    public AudioClip dropSound;

    [Range(0f, 1f)]
    public float dropSoundVolume = 0.9f;

    public float dropHearingRadius = 17f;
    public float maxEnemyFloorDifference = 3f;

    public bool dropMakesNoise = true;

    [Header("Optional Other Items")]
    public List<DropItemMapping> otherDroppableItems =
        new List<DropItemMapping>();

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Serializable]
    public class DropItemMapping
    {
        public string inventoryName;
        public GameObject worldPrefab;
    }

    private GameObject heldPreview;
    private AudioClip autoDropSound;

    private float nextAction;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (inventory == null)
            inventory = Inventory.Instance;
    }

    private void Start()
    {
        if (inventory == null)
            inventory = Inventory.Instance;

        if (dropSound == null)
            autoDropSound = GenerateMetalDropSound();

        DisableLegacyTrapPickups();

        if (trapPrefab == null)
        {
            Debug.LogError(
                "[TrapInventory] TRAP PREFAB IS MISSING! " +
                "Assign the original prefab asset in " +
                "Trap Hotbar Controller.",
                this);
        }

        if (inventory == null)
        {
            Debug.LogError(
                "[TrapInventory] INVENTORY IS MISSING!",
                this);
        }
    }

    private void DisableLegacyTrapPickups()
    {
        TrapItem[] all =
            FindObjectsByType<TrapItem>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (TrapItem trap in all)
        {
            if (trap == null)
                continue;

            if (trap.armOnStart)
                continue;

            PickupItem oldPickup =
                trap.GetComponent<PickupItem>();

            if (oldPickup != null)
                oldPickup.enabled = false;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        if (inventory == null)
            inventory = Inventory.Instance;

        if (playerCamera == null ||
            inventory == null)
            return;

        if (Time.time < nextAction)
            return;

        Inventory.Item selected =
            inventory.Selected;

        if (selected == null)
            return;

        if (Input.GetKeyDown(dropKey))
        {
            if (selected.name == trapItemName)
                DropTrap();
            else
                DropOtherItem(selected);

            return;
        }

        if (selected.name == trapItemName &&
            Input.GetMouseButtonDown(placeMouseButton))
        {
            PlaceTrap();
        }
    }

    // ========================================
    // PICKUP (called by TrapItem.Interact)
    // ========================================

    public void PickUp(TrapItem trap)
    {
        Debug.Log("[TrapInventory] PickUp called", this);

        if (trap == null || !trap.CanBePickedUp)
        {
            Debug.Log("[TrapInventory] Trap not pickable", this);
            return;
        }

        if (Time.time < nextAction)
        {
            Debug.Log("[TrapInventory] Cooldown", this);
            return;
        }

        if (inventory == null)
            inventory = Inventory.Instance;

        if (inventory == null)
        {
            Debug.LogError(
                "[TrapInventory] Inventory is null",
                this);
            return;
        }

        if (trapPrefab == null)
        {
            Debug.LogError(
                "[TrapInventory] Trap Prefab is None",
                this);
            return;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "[TrapInventory] Player Camera is null",
                this);
            return;
        }

        bool alreadyHaveTrap =
            inventory.HasItem(trapItemName);

        // If this is the first trap, build its
        // visible held model before registration.
        if (!alreadyHaveTrap)
        {
            if (heldPreview == null)
                heldPreview = CreateHeldPreview();

            if (heldPreview == null)
            {
                Debug.LogError(
                    "[TrapInventory] Failed to build " +
                    "held trap preview.",
                    this);

                return;
            }
        }

        bool added = inventory.AddItem(
            trapItemName,
            trapIcon,
            alreadyHaveTrap ? null : heldPreview,
            true,
            1);

        if (!added)
        {
            if (!alreadyHaveTrap &&
                heldPreview != null)
            {
                Destroy(heldPreview);
                heldPreview = null;
            }

            Debug.LogWarning(
                "[TrapInventory] Inventory FULL! " +
                "Cannot pick up trap.",
                this);

            return;
        }

        // Remove only after inventory accepted it.
        Destroy(trap.gameObject);

        // Refresh hotbar visuals.
        inventory.Select(inventory.SelectedIndex);

        nextAction = Time.time + 0.20f;

        if (showDebugLogs)
        {
            Debug.Log(
                "[TrapInventory] SUCCESS! Added " +
                trapItemName +
                " to inventory. Count: " +
                inventory.CountItem(trapItemName),
                this);
        }
    }

    // ========================================
    // HELD MODEL
    // ========================================

    private GameObject CreateHeldPreview()
    {
        if (trapPrefab == null ||
            playerCamera == null)
            return null;

        GameObject preview = Instantiate(
            trapPrefab,
            playerCamera.transform);

        preview.name =
            "Held_BearTrap_Inventory";

        preview.transform.localPosition =
            heldPosition;

        preview.transform.localRotation =
            Quaternion.Euler(heldRotation);

        preview.transform.localScale =
            Vector3.one * heldScale;

        foreach (TrapItem trap in
            preview.GetComponentsInChildren<TrapItem>(true))
        {
            trap.enabled = false;
        }

        foreach (Collider col in
            preview.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        foreach (Rigidbody rb in
            preview.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        foreach (PickupItem pickup in
            preview.GetComponentsInChildren<PickupItem>(true))
        {
            pickup.enabled = false;
        }

        preview.SetActive(false);

        return preview;
    }

    // ========================================
    // PLACE - LEFT CLICK ONLY
    // ========================================

    private void PlaceTrap()
    {
        if (!HasTrapSelected())
            return;

        if (trapPrefab == null)
            return;

        Vector3 position;
        Quaternion rotation;

        if (!FindSurface(
            true,
            out position,
            out rotation))
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    "[TrapInventory] Look at a clear " +
                    "floor to place the trap.",
                    this);
            }

            return;
        }

        GameObject placed = Instantiate(
            trapPrefab,
            position,
            rotation);

        TrapItem trap =
            placed.GetComponent<TrapItem>();

        if (trap == null)
            trap = placed.GetComponentInChildren<TrapItem>();

        if (trap == null)
        {
            Destroy(placed);

            Debug.LogError(
                "[TrapInventory] Prefab has no TrapItem!",
                this);

            return;
        }

        placed.name = "BearTrap_Placed";

        PickupItem oldPickup =
            placed.GetComponent<PickupItem>();

        if (oldPickup != null)
            oldPickup.enabled = false;

        // Instantly armed, no delay.
        trap.Place();

        inventory.RemoveSelected(1);

        CleanupPreviewIfEmpty();

        nextAction = Time.time + 0.25f;

        if (showDebugLogs)
        {
            Debug.Log(
                "[TrapInventory] LEFT CLICK: " +
                "Trap placed and ARMED immediately.",
                placed);
        }
    }

    // ========================================
    // DROP - Q
    // ========================================

    private void DropTrap()
    {
        if (!HasTrapSelected() ||
            trapPrefab == null)
            return;

        Vector3 position;
        Quaternion rotation;

        if (!FindSurface(
            false,
            out position,
            out rotation))
        {
            Debug.LogWarning(
                "[TrapInventory] No floor available " +
                "for dropping trap.",
                this);
            return;
        }

        GameObject dropped = Instantiate(
            trapPrefab,
            position,
            rotation);

        TrapItem trap =
            dropped.GetComponent<TrapItem>();

        if (trap == null)
            trap = dropped.GetComponentInChildren<TrapItem>();

        if (trap == null)
        {
            Destroy(dropped);
            return;
        }

        dropped.name =
            "BearTrap_Dropped_Unarmed";

        trap.SetAsPickup();

        PickupItem oldPickup =
            dropped.GetComponent<PickupItem>();

        if (oldPickup != null)
            oldPickup.enabled = false;

        inventory.RemoveSelected(1);

        CleanupPreviewIfEmpty();

        nextAction = Time.time + 0.25f;

        MakeDropNoise(position);

        if (showDebugLogs)
        {
            Debug.Log(
                "[TrapInventory] Q DROP: " +
                "Trap dropped. Eleanor may investigate.",
                dropped);
        }
    }

    // ========================================
    // OTHER DROPPABLE ITEMS
    // ========================================

    private void DropOtherItem(
        Inventory.Item item)
    {
        if (item == null)
            return;

        // Do not destroy equipped guns or other
        // held objects without a dedicated drop system.
        if (item.heldObject != null)
            return;

        foreach (DropItemMapping mapping
                 in otherDroppableItems)
        {
            if (mapping == null ||
                mapping.inventoryName != item.name ||
                mapping.worldPrefab == null)
                continue;

            Vector3 position;
            Quaternion rotation;

            if (!FindSurface(
                false,
                out position,
                out rotation))
                return;

            Instantiate(
                mapping.worldPrefab,
                position,
                rotation);

            inventory.RemoveSelected(1);

            nextAction = Time.time + 0.25f;

            MakeDropNoise(position);
            return;
        }
    }

    // ========================================
    // FIND FLOOR / VALID SURFACE
    // ========================================

    private bool FindSurface(
        bool placeAtCrosshair,
        out Vector3 position,
        out Quaternion rotation)
    {
        Vector3 origin;
        Vector3 direction;
        float distance;

        if (placeAtCrosshair)
        {
            origin = playerCamera.transform.position;
            direction = playerCamera.transform.forward;
            distance = placeRange;
        }
        else
        {
            Vector3 flatForward =
                Vector3.ProjectOnPlane(
                    playerCamera.transform.forward,
                    Vector3.up);

            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = transform.forward;

            origin =
                transform.position +
                flatForward.normalized * 1.35f +
                Vector3.up * 1.5f;

            direction = Vector3.down;
            distance = 4.5f;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            distance,
            interactionMask,
            QueryTriggerInteraction.Ignore);

        Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (IsPlayerCollider(hit.collider))
                continue;

            if (hit.normal.y < minimumSurfaceNormal)
            {
                if (placeAtCrosshair)
                    break;

                continue;
            }

            if (!ClearForTrap(hit))
            {
                if (placeAtCrosshair)
                    break;

                continue;
            }

            position =
                hit.point +
                Vector3.up * surfaceOffset;

            rotation = Quaternion.Euler(
                0f,
                playerCamera.transform.eulerAngles.y,
                0f);

            return true;
        }

        // If crosshair didn't find a floor,
        // attempt placement in front of player.
        if (placeAtCrosshair)
        {
            return FindSurface(
                false,
                out position,
                out rotation);
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;

        return false;
    }

    private bool ClearForTrap(RaycastHit hit)
    {
        Vector3 center =
            hit.point + Vector3.up * 0.15f;

        Collider[] overlaps = Physics.OverlapBox(
            center,
            new Vector3(
                clearanceRadius,
                0.12f,
                clearanceRadius),
            Quaternion.identity,
            interactionMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider col in overlaps)
        {
            if (col == null)
                continue;

            if (col == hit.collider)
                continue;

            if (IsPlayerCollider(col))
                continue;

            return false;
        }

        return true;
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null)
            return false;

        if (col.transform.IsChildOf(transform))
            return true;

        return
            col.GetComponentInParent<PlayerController>()
            != null;
    }

    private bool HasTrapSelected()
    {
        return inventory != null &&
               inventory.Selected != null &&
               inventory.Selected.name == trapItemName;
    }

    private void CleanupPreviewIfEmpty()
    {
        if (inventory.CountItem(trapItemName) > 0)
            return;

        if (heldPreview != null)
        {
            Destroy(heldPreview);
            heldPreview = null;
        }
    }

    // ========================================
    // DROP NOISE / ENEMY INVESTIGATION
    // ========================================

    private void MakeDropNoise(Vector3 position)
    {
        if (!dropMakesNoise)
            return;

        AudioClip sound =
            dropSound != null
                ? dropSound
                : autoDropSound;

        if (sound != null)
        {
            AudioSource.PlayClipAtPoint(
                sound,
                position,
                dropSoundVolume);
        }

        EnemyAI[] enemies =
            FindObjectsByType<EnemyAI>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.enabled)
                continue;

            Vector3 difference =
                enemy.transform.position - position;

            if (difference.magnitude >
                dropHearingRadius)
                continue;

            if (Mathf.Abs(difference.y) >
                maxEnemyFloorDifference)
                continue;

            enemy.HearNoise(position);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[TrapInventory] Enemy heard drop: " +
                    enemy.name,
                    enemy);
            }
        }
    }

    private static AudioClip GenerateMetalDropSound()
    {
        const int sampleRate = 22050;
        const float seconds = 0.38f;

        int length =
            Mathf.CeilToInt(sampleRate * seconds);

        float[] samples = new float[length];

        System.Random random =
            new System.Random(1529);

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;

            float envelope =
                Mathf.Exp(-13f * t);

            float ring =
                Mathf.Sin(2f * Mathf.PI * 760f * t) *
                0.38f +
                Mathf.Sin(2f * Mathf.PI * 1180f * t) *
                0.21f;

            float noise =
                ((float)random.NextDouble() * 2f - 1f) *
                0.18f;

            samples[i] =
                Mathf.Clamp(
                    (ring + noise) * envelope,
                    -1f,
                    1f);
        }

        AudioClip clip = AudioClip.Create(
            "TrapMetalDrop",
            length,
            1,
            sampleRate,
            false);

        clip.SetData(samples, 0);

        return clip;
    }
}