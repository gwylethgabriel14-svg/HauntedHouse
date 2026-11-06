
using System;
using System.Collections;
using UnityEngine;

namespace HauntedItems
{
    [DisallowMultipleComponent]
    public class TranquilizerGun : MonoBehaviour
    {
        [Header("Inventory Integration")]
        public string inventoryItemName = "Tranquilizer Gun";
        public Sprite inventoryIcon;
        public bool addToInventoryOnEquip = true;
        public bool doNotAutoSelect = true;

        [Header("Ammo")]
        public int magazineSize = 3;
        public int ammoInMagazine = 3;

        [Header("Weapon Stats")]
        public float stunSeconds = 120f;
        public float dartSpeed = 45f;
        public float fireCooldown = 0.6f;
        public float reloadSeconds = 1.8f;
        public float maxAimRange = 100f;

        [Header("Aim Assistance")]
        [Tooltip("Sphere radius for easier enemy aiming.")]
        public float aimAssistRadius = 0.12f;

        [Header("Controls")]
        public int fireMouseButton = 0;
        public KeyCode reloadKey = KeyCode.R;

        [Header("Parts")]
        public Transform muzzle;
        public Transform magazine;
        public Transform bolt;
        public Transform chamberDart;
        public Renderer[] ammoLights;
        public GameObject dartPrefab;

        [Header("Held Pose")]
        public Vector3 heldPosition =
            new Vector3(0.24f, -0.22f, 0.5f);

        public Vector3 heldEuler = Vector3.zero;

        [Header("Debug")]
        public bool showDebugLogs = true;

        public bool IsReloading { get; private set; }

        public bool IsInInventory
        {
            get { return registeredInInventory; }
        }

        public bool IsEquipped
        {
            get
            {
                if (inventory == null)
                    return false;

                if (!gameObject.activeInHierarchy)
                    return false;

                Inventory.Item item = inventory.Selected;

                return item != null &&
                       item.heldObject == gameObject;
            }
        }

        private PlayerItemInteractor owner;
        private Camera cam;
        private Transform playerRoot;
        private Inventory inventory;

        private bool registeredInInventory;
        private float nextFire;

        private Vector3 magRest;
        private Vector3 boltRest;

        private Coroutine recoilRoutine;
        private Coroutine reloadRoutine;

        private static readonly Color LightOn =
            new Color(2.2f, 0.12f, 0.08f);

        private void Awake()
        {
            if (magazine != null)
                magRest = magazine.localPosition;

            if (bolt != null)
                boltRest = bolt.localPosition;

            magazineSize = Mathf.Max(1, magazineSize);

            ammoInMagazine = Mathf.Clamp(
                ammoInMagazine, 0, magazineSize);

            RefreshVisuals();
        }

        // ===================================================
        // INVENTORY PICKUP / EQUIP
        // ===================================================

        public void Equip(
            PlayerItemInteractor who,
            Camera camera)
        {
            if (who == null || camera == null)
            {
                Debug.LogWarning(
                    "[GUN] Missing PlayerItemInteractor or Camera.",
                    this);
                return;
            }

            owner = who;
            cam = camera;

            PlayerController controller =
                owner.GetComponentInParent<PlayerController>();

            if (controller == null)
            {
                controller =
                    cam.GetComponentInParent<PlayerController>();
            }

            playerRoot = controller != null
                ? controller.transform
                : owner.transform;

            inventory = Inventory.Instance;

            if (inventory == null)
                inventory = FindFirstObjectByType<Inventory>();

            if (addToInventoryOnEquip)
            {
                if (inventory == null)
                {
                    owner.ShowMessage("Inventory not found.");
                    return;
                }

                if (!registeredInInventory)
                {
                    if (inventory.HasItem(inventoryItemName))
                    {
                        owner.ShowMessage(
                            "You already have a tranquilizer gun.");
                        return;
                    }

                    // Attach first so inventory can safely
                    // activate/deactivate this held object.
                    transform.SetParent(cam.transform, false);
                    transform.localPosition = heldPosition;
                    transform.localRotation =
                        Quaternion.Euler(heldEuler);
                    transform.localScale = Vector3.one;

                    bool added = inventory.AddItem(
                        inventoryItemName,
                        inventoryIcon,
                        gameObject,
                        false,
                        1);

                    if (!added)
                    {
                        owner.ShowMessage("Inventory full.");
                        return;
                    }

                    registeredInInventory = true;
                }
            }

            // Prevent equipped gun from blocking raycasts.
            foreach (Collider col in
                GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            PickupItem pickup =
                GetComponent<PickupItem>();

            if (pickup != null)
                pickup.enabled = false;

            transform.SetParent(cam.transform, false);
            transform.localPosition = heldPosition;
            transform.localRotation =
                Quaternion.Euler(heldEuler);
            transform.localScale = Vector3.one;

            enabled = true;
            RefreshVisuals();

            if (registeredInInventory && inventory != null)
            {
                int previousSelection = inventory.SelectedIndex;
                int gunSlot = FindGunSlot();

                if (!doNotAutoSelect && gunSlot >= 0)
                    inventory.Select(gunSlot);
                else
                    inventory.Select(previousSelection);

                if (gunSlot >= 0)
                {
                    owner.ShowMessage(
                        "Tranquilizer Gun in slot " +
                        (gunSlot + 1) +
                        ". Press " + (gunSlot + 1) +
                        " to equip.");
                }
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    "[GUN] Registered in inventory. Player: " +
                    (playerRoot != null
                        ? playerRoot.name
                        : "None"),
                    this);
            }
        }

        private int FindGunSlot()
        {
            if (inventory == null)
                return -1;

            int original = inventory.SelectedIndex;
            int found = -1;

            for (int i = 0; i < inventory.slotCount; i++)
            {
                inventory.Select(i);

                Inventory.Item item = inventory.Selected;

                if (item != null &&
                    item.heldObject == gameObject)
                {
                    found = i;
                    break;
                }
            }

            inventory.Select(original);
            return found;
        }

        // ===================================================
        // INPUT
        // ===================================================

        private void Update()
        {
            if (cam == null || Time.timeScale <= 0f)
                return;

            if (addToInventoryOnEquip && !IsEquipped)
                return;

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (IsReloading)
                return;

            if (Input.GetMouseButtonDown(fireMouseButton))
                TryFire();

            if (Input.GetKeyDown(reloadKey))
                TryReload();
        }

        // ===================================================
        // FIRE
        // ===================================================

        private void TryFire()
        {
            if (Time.time < nextFire)
                return;

            if (ammoInMagazine <= 0)
            {
                nextFire = Time.time + 0.3f;
                owner?.ShowMessage("Empty - press R to reload.");
                return;
            }

            Vector3 aimPoint = GetAimPoint();

            Vector3 origin = muzzle != null
                ? muzzle.position
                : cam.transform.position +
                  cam.transform.forward * 0.45f;

            Vector3 direction = aimPoint - origin;

            if (direction.sqrMagnitude < 0.0001f)
                direction = cam.transform.forward;

            direction.Normalize();

            GameObject dartObject;

            if (dartPrefab != null)
            {
                dartObject = Instantiate(
                    dartPrefab,
                    origin,
                    Quaternion.LookRotation(direction));
            }
            else
            {
                dartObject = ItemModelFactory.BuildDart();

                if (dartObject != null)
                {
                    dartObject.transform.SetPositionAndRotation(
                        origin,
                        Quaternion.LookRotation(direction));
                }
            }

            if (dartObject == null)
            {
                Debug.LogError(
                    "[GUN] Could not spawn a dart.",
                    this);
                return;
            }

            TranquilizerDart dart =
                dartObject.GetComponent<TranquilizerDart>();

            if (dart == null)
            {
                dart =
                    dartObject.AddComponent<TranquilizerDart>();
            }

            dart.showDebugLogs = showDebugLogs;

            // Important:
            // Ignore the actual Player, never CHARACTERS.
            dart.Launch(
                direction,
                dartSpeed,
                stunSeconds,
                playerRoot);

            ammoInMagazine--;

            nextFire = Time.time + fireCooldown;

            RefreshVisuals();

            if (recoilRoutine != null)
                StopCoroutine(recoilRoutine);

            recoilRoutine = StartCoroutine(Recoil());

            if (showDebugLogs)
            {
                Debug.Log(
                    "[GUN] FIRED | Ammo: " +
                    ammoInMagazine + "/" + magazineSize +
                    " | Target: " + aimPoint,
                    this);
            }
        }

        // ===================================================
        // AIM AT CROSSHAIR
        // ===================================================

        private Vector3 GetAimPoint()
        {
            Vector3 origin = cam.transform.position;
            Vector3 forward = cam.transform.forward;

            Vector3 target =
                origin + forward * maxAimRange;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                Mathf.Max(0.01f, aimAssistRadius),
                forward,
                maxAimRange,
                ~0,
                QueryTriggerInteraction.Collide);

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;

                if (col == null)
                    continue;

                if (IsPlayerCollider(col))
                    continue;

                if (col.transform.IsChildOf(transform))
                    continue;

                bool enemyTrigger =
                    col.GetComponentInParent<EnemyAI>() != null ||
                    col.GetComponentInParent<StunnableEnemy>() != null ||
                    col.GetComponentInParent<EnemyController>() != null;

                // Ignore unrelated trigger volumes.
                if (col.isTrigger && !enemyTrigger)
                    continue;

                target = hit.point != Vector3.zero
                    ? hit.point
                    : origin + forward * hit.distance;

                if (showDebugLogs)
                {
                    Debug.Log(
                        "[GUN] Crosshair hit: " + col.name +
                        " | Enemy: " + enemyTrigger,
                        col);
                }

                break;
            }

            return target;
        }

        private bool IsPlayerCollider(Collider col)
        {
            if (col == null)
                return false;

            if (playerRoot != null &&
                col.transform.IsChildOf(playerRoot))
                return true;

            return
                col.GetComponentInParent<PlayerController>() != null;
        }

        // ===================================================
        // RELOAD
        // ===================================================

        private void TryReload()
        {
            if (IsReloading)
                return;

            if (ammoInMagazine >= magazineSize)
            {
                owner?.ShowMessage("Magazine full.");
                return;
            }

            if (owner == null || owner.darts <= 0)
            {
                owner?.ShowMessage(
                    "No darts left - find more.");
                return;
            }

            reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;

            if (recoilRoutine != null)
            {
                StopCoroutine(recoilRoutine);
                recoilRoutine = null;
            }

            Vector3 basePos = heldPosition;
            Quaternion baseRot =
                Quaternion.Euler(heldEuler);

            Vector3 lowPos =
                basePos + new Vector3(0.02f, -0.13f, -0.05f);

            Quaternion lowRot = Quaternion.Euler(
                heldEuler + new Vector3(28f, 12f, -22f));

            float t1 = reloadSeconds * 0.25f;
            float t2 = reloadSeconds * 0.50f;
            float t3 = reloadSeconds * 0.25f;

            owner?.ShowMessage("Reloading...");

            yield return LerpLocal(
                transform, lowPos, lowRot, t1);

            if (magazine != null)
            {
                yield return LerpLocal(
                    magazine,
                    magRest + Vector3.down * 0.09f,
                    magazine.localRotation,
                    t2 * 0.25f);
            }

            int needed = Mathf.Min(
                magazineSize - ammoInMagazine,
                owner.darts);

            for (int i = 0; i < needed; i++)
            {
                ammoInMagazine++;
                owner.darts--;

                RefreshVisuals();

                yield return new WaitForSeconds(
                    t2 * 0.5f / Mathf.Max(1, needed));
            }

            if (magazine != null)
            {
                yield return LerpLocal(
                    magazine,
                    magRest,
                    magazine.localRotation,
                    t2 * 0.25f);
            }

            if (bolt != null)
            {
                yield return LerpLocal(
                    bolt,
                    boltRest + Vector3.back * 0.05f,
                    bolt.localRotation,
                    0.08f);

                yield return LerpLocal(
                    bolt,
                    boltRest,
                    bolt.localRotation,
                    0.08f);
            }

            yield return LerpLocal(
                transform, basePos, baseRot, t3);

            IsReloading = false;
            reloadRoutine = null;

            owner?.ShowMessage("Reloaded.");
        }

        // ===================================================
        // RECOIL
        // ===================================================

        private IEnumerator Recoil()
        {
            Vector3 p = heldPosition;
            Quaternion r = Quaternion.Euler(heldEuler);

            yield return LerpLocal(
                transform,
                p + Vector3.back * 0.06f,
                r * Quaternion.Euler(-6f, 0f, 0f),
                0.05f);

            yield return LerpLocal(
                transform, p, r, 0.15f);

            recoilRoutine = null;
        }

        private IEnumerator LerpLocal(
            Transform target,
            Vector3 position,
            Quaternion rotation,
            float duration)
        {
            if (target == null)
                yield break;

            duration = Mathf.Max(0.01f, duration);

            Vector3 startPosition = target.localPosition;
            Quaternion startRotation = target.localRotation;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));

                target.localPosition = Vector3.Lerp(
                    startPosition, position, t);

                target.localRotation = Quaternion.Slerp(
                    startRotation, rotation, t);

                yield return null;
            }

            target.localPosition = position;
            target.localRotation = rotation;
        }

        // ===================================================
        // AMMO VISUALS
        // ===================================================

        public void RefreshVisuals()
        {
            if (ammoLights != null)
            {
                for (int i = 0; i < ammoLights.Length; i++)
                {
                    if (ammoLights[i] == null)
                        continue;

                    ItemMaterials.SetEmission(
                        ammoLights[i],
                        i < ammoInMagazine
                            ? LightOn
                            : Color.black);
                }
            }

            if (chamberDart != null)
            {
                chamberDart.gameObject.SetActive(
                    ammoInMagazine > 0);
            }
        }

        private void OnDisable()
        {
            if (reloadRoutine != null)
            {
                StopCoroutine(reloadRoutine);
                reloadRoutine = null;
                IsReloading = false;
            }

            if (recoilRoutine != null)
            {
                StopCoroutine(recoilRoutine);
                recoilRoutine = null;
            }

            if (registeredInInventory && cam != null)
            {
                transform.localPosition = heldPosition;
                transform.localRotation =
                    Quaternion.Euler(heldEuler);
            }
        }
    }
}
