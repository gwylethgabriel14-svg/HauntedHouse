
using System;
using UnityEngine;

namespace HauntedItems
{
    // =====================================================
    // THE LAST LULLABY - PLAYER ITEM INTERACTOR
    //
    // FIXES:
    // 1. REMOVED old Darts / Reserve / Traps HUD
    // 2. Removed automatic G trap placement
    // 3. Preserved E pickup interaction
    // 4. Preserved pickup prompts and messages
    // 5. Preserved Tranquilizer Gun pickup
    // 6. Preserved Dart Pack pickup
    //
    // TrapHotbarController now handles equipped
    // Bear Traps and left-click placement.
    //
    // EquippedAmmoHUD separately displays:
    // DARTS 3 / 3 only while gun is selected.
    // =====================================================

    public class PlayerItemInteractor : MonoBehaviour
    {
        [Header("Setup")]
        public Camera playerCamera;

        public float reach = 2.6f;

        public KeyCode pickupKey = KeyCode.E;

        [Header("Legacy Trap Placement")]
        [Tooltip(
            "Leave OFF when using TrapHotbarController. " +
            "Prevents G from placing traps."
        )]
        public bool enableLegacyTrapPlacement = false;

        public KeyCode dropTrapKey = KeyCode.G;

        public GameObject trapPrefab;

        public ItemSpawner settings;

        public bool showPrompts = true;

        [Header("Inventory")]
        public int darts = 0;
        public int maxDarts = 9;

        public int traps = 0;
        public int maxTraps = 3;

        public TranquilizerGun gun;

        [Header("Debug")]
        public bool showDebugLogs = false;

        private PickupItem focused;

        private string message;
        private float messageUntil;

        private GUIStyle centerStyle;

        // =================================================
        // START
        // =================================================

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera =
                    GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (settings == null)
            {
                settings = ItemUtil.FindSpawner();
            }

            if (trapPrefab == null && settings != null)
            {
                trapPrefab = settings.trapPrefab;
            }
        }

        // =================================================
        // SHOW PICKUP MESSAGE
        // =================================================

        public void ShowMessage(
            string text,
            float seconds = 2f)
        {
            message = text;

            messageUntil =
                Time.time + Mathf.Max(0f, seconds);
        }

        // =================================================
        // UPDATE
        // =================================================

        private void Update()
        {
            if (playerCamera == null)
                return;

            focused = FindFocus();

            if (focused != null &&
                Input.GetKeyDown(pickupKey))
            {
                TryPickup(focused);
            }

            // Legacy trap placement is OFF by default.
            // TrapHotbarController handles left-click.
            if (enableLegacyTrapPlacement &&
                Input.GetKeyDown(dropTrapKey))
            {
                TryPlaceTrap();
            }
        }

        // =================================================
        // FIND PICKUP ITEM
        // =================================================

        private PickupItem FindFocus()
        {
            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);

            RaycastHit[] hits =
                Physics.RaycastAll(
                    ray,
                    reach,
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

                // Ignore player's own colliders.
                if (hit.collider.transform.IsChildOf(
                    transform))
                {
                    continue;
                }

                PickupItem pickup =
                    hit.collider
                        .GetComponentInParent<PickupItem>();

                if (pickup != null && pickup.enabled)
                {
                    // Let the dedicated trap controller
                    // manage Bear Trap pickups.
                    if (pickup.kind == ItemKind.Trap &&
                        !enableLegacyTrapPlacement &&
                        GetComponent<TrapHotbarController>()
                            != null)
                    {
                        continue;
                    }

                    return pickup;
                }

                // Stop at solid walls/objects.
                if (!hit.collider.isTrigger)
                    break;
            }

            return null;
        }

        // =================================================
        // PICKUP
        // =================================================

        private void TryPickup(PickupItem pickup)
        {
            if (pickup == null)
                return;

            switch (pickup.kind)
            {
                // -----------------------------------------
                // TRANQUILIZER GUN
                // -----------------------------------------

                case ItemKind.TranquilizerGun:
                {
                    if (gun != null)
                    {
                        ShowMessage(
                            "You already have a tranquilizer gun");

                        return;
                    }

                    TranquilizerGun foundGun =
                        pickup.GetComponent<TranquilizerGun>();

                    if (foundGun == null)
                    {
                        foundGun =
                            pickup.GetComponentInChildren<
                                TranquilizerGun>(true);
                    }

                    if (foundGun == null)
                    {
                        Debug.LogWarning(
                            "[INTERACTOR] Gun has no " +
                            "TranquilizerGun component.",
                            pickup);

                        return;
                    }

                    gun = foundGun;

                    foundGun.Equip(
                        this,
                        playerCamera);

                    ShowMessage(
                        "Picked up Tranquilizer Gun " +
                        "(LMB fire, R reload)",
                        3f);

                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "[INTERACTOR] Gun picked up.",
                            this);
                    }

                    break;
                }

                // -----------------------------------------
                // DART PACK
                // -----------------------------------------

                case ItemKind.DartPack:
                {
                    int availableSpace =
                        Mathf.Max(0, maxDarts - darts);

                    if (availableSpace <= 0)
                    {
                        ShowMessage(
                            "Can't carry more darts");

                        return;
                    }

                    int amountToAdd =
                        Mathf.Min(
                            availableSpace,
                            pickup.amount);

                    if (amountToAdd <= 0)
                        return;

                    darts += amountToAdd;
                    pickup.amount -= amountToAdd;

                    if (pickup.amount <= 0)
                    {
                        Destroy(pickup.gameObject);
                    }

                    ShowMessage(
                        "+" + amountToAdd +
                        " tranquilizer darts");

                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "[INTERACTOR] Spare darts: " +
                            darts,
                            this);
                    }

                    break;
                }

                // -----------------------------------------
                // BEAR TRAP
                // -----------------------------------------

                case ItemKind.Trap:
                {
                    // If TrapHotbarController is installed,
                    // it handles inventory and left-click
                    // trap placement. Do not add a duplicate
                    // trap to legacy storage.

                    TrapHotbarController hotbar =
                        GetComponent<TrapHotbarController>();

                    if (hotbar != null &&
                        hotbar.enabled &&
                        !enableLegacyTrapPlacement)
                    {
                        return;
                    }

                    if (traps >= maxTraps)
                    {
                        ShowMessage(
                            "Can't carry more traps");

                        return;
                    }

                    traps++;

                    Destroy(pickup.gameObject);

                    ShowMessage(
                        "Picked up Bear Trap");

                    if (showDebugLogs)
                    {
                        Debug.Log(
                            "[INTERACTOR] Legacy trap " +
                            "picked up. Total: " + traps,
                            this);
                    }

                    break;
                }
            }
        }

        // =================================================
        // OPTIONAL LEGACY TRAP PLACEMENT
        //
        // Disabled by default.
        // =================================================

        private void TryPlaceTrap()
        {
            if (!enableLegacyTrapPlacement)
                return;

            if (traps <= 0)
            {
                ShowMessage("No traps to place");
                return;
            }

            if (playerCamera == null)
                return;

            Transform cameraTransform =
                playerCamera.transform;

            Vector3 spot =
                cameraTransform.position +
                cameraTransform.forward * 1.6f;

            RaycastHit wallHit;

            if (Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out wallHit,
                1.6f,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                spot =
                    wallHit.point -
                    cameraTransform.forward * 0.5f;
            }

            RaycastHit floorHit;

            if (!Physics.Raycast(
                spot + Vector3.up,
                Vector3.down,
                out floorHit,
                3f,
                ~0,
                QueryTriggerInteraction.Ignore) ||
                floorHit.normal.y < 0.7f)
            {
                ShowMessage(
                    "Can't place a trap here");

                return;
            }

            GameObject placedTrap =
                trapPrefab != null
                    ? Instantiate(trapPrefab)
                    : ItemModelFactory.BuildTrap();

            if (placedTrap == null)
                return;

            float yaw =
                cameraTransform.eulerAngles.y;

            placedTrap.transform.SetPositionAndRotation(
                floorHit.point,
                Quaternion.Euler(0f, yaw, 0f));

            Bounds bounds =
                ItemModelFactory.GetBounds(
                    placedTrap);

            // Correct height offset; do not add
            // floor Y twice.
            placedTrap.transform.position +=
                Vector3.up *
                (floorHit.point.y +
                 0.004f -
                 bounds.min.y);

            TrapItem trap =
                placedTrap.GetComponent<TrapItem>();

            if (trap != null)
            {
                if (settings != null)
                {
                    trap.stunDuration =
                        settings.trapStunSeconds;

                    trap.armDelay =
                        settings.trapArmDelay;
                }

                trap.armOnStart = false;
                trap.Place();
            }

            traps--;

            ShowMessage("Trap placed");
        }

        // =================================================
        // ONGUI
        //
        // ONLY pickup prompts and notifications!
        //
        // REMOVED:
        // Darts - Reserve X Traps X
        // =================================================

        private void OnGUI()
        {
            if (!showPrompts)
                return;

            if (centerStyle == null)
            {
                centerStyle =
                    new GUIStyle(GUI.skin.label);

                centerStyle.fontSize = 18;

                centerStyle.alignment =
                    TextAnchor.MiddleCenter;

                centerStyle.normal.textColor =
                    Color.white;
            }

            // Pickup prompt
            if (focused != null)
            {
                GUI.Label(
                    new Rect(
                        0f,
                        Screen.height * 0.62f,
                        Screen.width,
                        30f),
                    "[" + pickupKey + "] " +
                    focused.PromptText,
                    centerStyle);
            }

            // Temporary notification
            if (Time.time < messageUntil &&
                !string.IsNullOrEmpty(message))
            {
                GUI.Label(
                    new Rect(
                        0f,
                        Screen.height * 0.68f,
                        Screen.width,
                        30f),
                    message,
                    centerStyle);
            }

            // NO permanent ammo/reserve/traps HUD.
            // EquippedAmmoHUD handles DARTS x / x.
        }
    }
}
