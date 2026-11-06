
using System;
using UnityEngine;

// =====================================================
// THE LAST LULLABY - KEY PICKUP SYSTEM
//
// Supports:
// 1. Existing IInteractable interaction
// 2. Direct E pickup fallback
// 3. Keys with no collider
// 4. Inventory integration
// 5. GameManager key collection
//
// Attach this to the actual key object.
// =====================================================

[DisallowMultipleComponent]
public class KeyItem : MonoBehaviour, IInteractable
{
    [Header("Inventory")]
    public string itemName = "Key";
    public Sprite icon;

    [Header("Interaction")]
    public KeyCode pickupKey = KeyCode.E;

    [Tooltip("Maximum distance to pick up the key.")]
    public float pickupRange = 3.5f;

    [Tooltip("How close the key must be to the screen center.")]
    [Range(0.02f, 0.3f)]
    public float aimTolerance = 0.12f;

    [Header("References")]
    public Camera playerCamera;

    [Tooltip(
        "Optional: assign the visible key mesh. " +
        "Leave empty if this GameObject is centered on the key."
    )]
    public Transform pickupAimTarget;

    [Header("Visibility")]
    [Tooltip(
        "When enabled, walls and solid objects " +
        "can block key pickup."
    )]
    public bool requireClearLineOfSight = true;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool collected;

    public string Prompt => "Take key";

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        FindCamera();

        if (pickupAimTarget == null)
            pickupAimTarget = transform;
    }

    private void Start()
    {
        FindCamera();
    }

    private void FindCamera()
    {
        if (playerCamera != null)
            return;

        playerCamera = Camera.main;

        if (playerCamera != null)
            return;

        PlayerController player =
            FindFirstObjectByType<PlayerController>();

        if (player != null)
        {
            playerCamera =
                player.GetComponentInChildren<Camera>(true);
        }
    }

    // =====================================================
    // E KEY FALLBACK
    // =====================================================

    private void Update()
    {
        if (collected)
            return;

        if (Time.timeScale <= 0f)
            return;

        if (!Input.GetKeyDown(pickupKey))
            return;

        FindCamera();

        if (playerCamera == null)
            return;

        // Only pick up when player is close
        // and aiming at this actual key.
        if (!CanPickUpFromCamera())
            return;

        PlayerController player =
            playerCamera.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            player =
                FindFirstObjectByType<PlayerController>();
        }

        if (player != null)
        {
            Interact(player);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(
                "[KEY] PlayerController not found.",
                this);
        }
    }

    // =====================================================
    // AIM DETECTION
    // =====================================================

    private bool CanPickUpFromCamera()
    {
        if (playerCamera == null)
            return false;

        Transform target =
            pickupAimTarget != null
                ? pickupAimTarget
                : transform;

        Vector3 keyPosition = target.position;

        Vector3 cameraPosition =
            playerCamera.transform.position;

        float distance =
            Vector3.Distance(
                cameraPosition,
                keyPosition);

        if (distance > pickupRange)
            return false;

        // Check whether the key is near
        // the player's screen crosshair.
        Vector3 viewport =
            playerCamera.WorldToViewportPoint(
                keyPosition);

        if (viewport.z <= 0f)
            return false;

        float horizontal =
            viewport.x - 0.5f;

        float vertical =
            viewport.y - 0.5f;

        float aimDistance =
            Mathf.Sqrt(
                horizontal * horizontal +
                vertical * vertical);

        if (aimDistance > aimTolerance)
            return false;

        if (requireClearLineOfSight)
        {
            if (!HasClearLineOfSight(
                cameraPosition,
                keyPosition))
            {
                return false;
            }
        }

        return true;
    }

    // =====================================================
    // WALL / OBSTACLE CHECK
    // =====================================================

    private bool HasClearLineOfSight(
        Vector3 start,
        Vector3 end)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return true;

        direction.Normalize();

        RaycastHit[] hits =
            Physics.RaycastAll(
                start,
                direction,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

        Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Collider col = hit.collider;

            if (col == null)
                continue;

            // Ignore the key and its children.
            if (col.transform == transform ||
                col.transform.IsChildOf(transform))
            {
                continue;
            }

            // Ignore the player's own body.
            if (col.GetComponentInParent<PlayerController>()
                != null)
            {
                continue;
            }

            // A solid object is in front of the key.
            if (showDebugLogs)
            {
                Debug.Log(
                    "[KEY] Pickup blocked by: " +
                    col.name,
                    col);
            }

            return false;
        }

        return true;
    }

    // =====================================================
    // ORIGINAL IINTERACTABLE METHOD
    // =====================================================

    public void Interact(PlayerController player)
    {
        if (collected)
            return;

        Inventory inv = Inventory.Instance;
        GameManager gm = GameManager.Instance;

        if (inv == null)
        {
            Debug.LogWarning(
                "[KEY] Inventory.Instance is missing.",
                this);

            return;
        }

        if (gm == null)
        {
            Debug.LogWarning(
                "[KEY] GameManager.Instance is missing.",
                this);

            return;
        }

        // Prevent duplicate inventory keys.
        if (!inv.HasItem(itemName))
        {
            bool added = inv.AddItem(
                itemName,
                icon,
                null,
                true,
                1);

            if (!added)
            {
                Debug.LogWarning(
                    "[KEY] Inventory full. " +
                    "Cannot pick up the key.",
                    this);

                return;
            }
        }

        collected = true;

        // Update quest/key progress.
        gm.CollectKey();

        if (showDebugLogs)
        {
            Debug.Log(
                "[KEY] SUCCESS! Key collected " +
                "and added to inventory.",
                this);
        }

        Destroy(gameObject);
    }
}
