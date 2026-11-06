
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HauntedItems;

// ====================================================
// THE LAST LULLABY - EQUIPPED DARTS HUD
//
// Displays DARTS 3 / 3 at the BOTTOM-RIGHT.
//
// - Only visible when Tranquilizer Gun is selected
// - Updates after shooting and reloading
// - Hides for other inventory items
// - Attempts to hide the old Reserve / Traps HUD
// - Does not modify ammo or inventory
//
// Attach to PLAYER.
// ====================================================

[DisallowMultipleComponent]
public class EquippedAmmoHUD : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;
    public TranquilizerGun gunReference;

    [Header("Gun Inventory Name")]
    public string gunItemName = "Tranquilizer Gun";

    [Header("Darts Counter")]
    public string counterLabel = "DARTS";

    public bool showCounterLabel = true;

    public TMP_FontAsset counterFont;

    [Min(10)]
    public int fontSize = 34;

    public Color textColor =
        new Color(0.95f, 0.88f, 0.77f, 1f);

    [Header("BOTTOM-RIGHT POSITION")]
    [Tooltip("X moves left/right. Y moves up/down.")]
    public Vector2 hudPosition =
        new Vector2(-25f, 25f);

    public Vector2 hudSize =
        new Vector2(320f, 65f);

    [Header("Hide Previous HUD")]
    public bool hideOldDartsReserveTraps = true;

    [Tooltip("Optional old TMP HUD text.")]
    public TMP_Text oldAmmoText;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private GameObject hudCanvasObject;
    private GameObject dartsPanel;

    private TextMeshProUGUI dartsText;

    private float nextScanTime;

    // ====================================================
    // INITIALIZATION
    // ====================================================

    private void Awake()
    {
        FindInventory();
        CreateDartsHUD();
    }

    private void Start()
    {
        FindInventory();
        RefreshHUD();
    }

    private void LateUpdate()
    {
        if (inventory == null)
            FindInventory();

        RefreshHUD();

        if (hideOldDartsReserveTraps &&
            Time.unscaledTime >= nextScanTime)
        {
            nextScanTime =
                Time.unscaledTime + 0.3f;

            HideOldHUD();
        }
    }

    private void FindInventory()
    {
        if (inventory != null)
            return;

        inventory = Inventory.Instance;

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (inventory == null)
        {
            inventory =
                GetComponentInChildren<Inventory>(true);
        }
    }

    // ====================================================
    // CREATE BOTTOM-RIGHT HUD
    // ====================================================

    private void CreateDartsHUD()
    {
        if (hudCanvasObject != null)
            return;

        hudCanvasObject =
            new GameObject(
                "EquippedDartsHUDCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

        Canvas canvas =
            hudCanvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.overrideSorting = true;
        canvas.sortingOrder = 2200;

        CanvasScaler scaler =
            hudCanvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode
                .MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        // --------------------------------------------
        // COUNTER CONTAINER
        // --------------------------------------------

        dartsPanel =
            new GameObject(
                "DartsPanel",
                typeof(RectTransform));

        dartsPanel.transform.SetParent(
            hudCanvasObject.transform,
            false);

        RectTransform panelRect =
            dartsPanel.GetComponent<RectTransform>();

        // Anchor at bottom-right.
        panelRect.anchorMin =
            new Vector2(1f, 0f);

        panelRect.anchorMax =
            new Vector2(1f, 0f);

        panelRect.pivot =
            new Vector2(1f, 0f);

        panelRect.anchoredPosition =
            hudPosition;

        panelRect.sizeDelta =
            hudSize;

        // --------------------------------------------
        // DARTS TEXT
        // --------------------------------------------

        GameObject textObject =
            new GameObject(
                "DartsCounterText",
                typeof(RectTransform));

        textObject.transform.SetParent(
            dartsPanel.transform,
            false);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        dartsText =
            textObject.AddComponent<TextMeshProUGUI>();

        if (counterFont != null)
            dartsText.font = counterFont;

        dartsText.fontSize = fontSize;
        dartsText.fontStyle = FontStyles.Bold;

        dartsText.color = textColor;

        dartsText.alignment =
            TextAlignmentOptions.Right;

        dartsText.textWrappingMode =
            TextWrappingModes.NoWrap;

        dartsText.raycastTarget = false;

        dartsText.text = "";

        Shadow shadow =
            textObject.AddComponent<Shadow>();

        shadow.effectColor =
            new Color(0f, 0f, 0f, 0.9f);

        shadow.effectDistance =
            new Vector2(2f, -2f);

        dartsPanel.SetActive(false);
    }

    // ====================================================
    // CHECK SELECTED INVENTORY ITEM
    // ====================================================

    private bool IsGunSelected()
    {
        if (inventory == null)
            return false;

        var selected = inventory.Selected;

        if (selected == null ||
            string.IsNullOrEmpty(selected.name))
        {
            return false;
        }

        return string.Equals(
            selected.name,
            gunItemName,
            StringComparison.OrdinalIgnoreCase);
    }

    // ====================================================
    // FIND THE ACTUAL EQUIPPED GUN
    // ====================================================

    private TranquilizerGun FindEquippedGun()
    {
        if (inventory == null)
            return null;

        var selected = inventory.Selected;

        if (selected != null &&
            selected.heldObject != null)
        {
            TranquilizerGun gun =
                selected.heldObject
                    .GetComponent<TranquilizerGun>();

            if (gun == null)
            {
                gun = selected.heldObject
                    .GetComponentInChildren<
                        TranquilizerGun>(true);
            }

            if (gun != null)
                return gun;
        }

        // Check guns under Player.
        TranquilizerGun[] guns =
            GetComponentsInChildren<
                TranquilizerGun>(true);

        foreach (TranquilizerGun gun in guns)
        {
            if (gun != null)
                return gun;
        }

        return gunReference;
    }

    // ====================================================
    // REFRESH DARTS COUNTER
    // ====================================================

    private void RefreshHUD()
    {
        if (dartsPanel == null ||
            dartsText == null)
        {
            return;
        }

        if (!IsGunSelected())
        {
            if (dartsPanel.activeSelf)
                dartsPanel.SetActive(false);

            return;
        }

        TranquilizerGun gun = FindEquippedGun();

        if (gun == null)
        {
            dartsPanel.SetActive(false);
            return;
        }

        int loaded =
            Mathf.Max(0, gun.ammoInMagazine);

        int capacity =
            Mathf.Max(0, gun.magazineSize);

        string counter =
            loaded + " / " + capacity;

        dartsText.text =
            showCounterLabel
                ? counterLabel + "  " + counter
                : counter;

        // Apply Inspector adjustments live.
        dartsText.fontSize = fontSize;
        dartsText.color = textColor;

        if (counterFont != null)
            dartsText.font = counterFont;

        RectTransform panelRect =
            dartsPanel.GetComponent<RectTransform>();

        panelRect.anchoredPosition =
            hudPosition;

        panelRect.sizeDelta = hudSize;

        if (!dartsPanel.activeSelf)
        {
            dartsPanel.SetActive(true);

            if (showDebugLogs)
            {
                Debug.Log(
                    "[DARTS HUD] Showing " +
                    dartsText.text,
                    this);
            }
        }
    }

    // ====================================================
    // HIDE OLD DARTS / RESERVE / TRAPS TEXT
    // ====================================================

    private void HideOldHUD()
    {
        if (!hideOldDartsReserveTraps)
            return;

        if (oldAmmoText != null)
            oldAmmoText.enabled = false;

        TMP_Text[] tmpTexts =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (TMP_Text text in tmpTexts)
        {
            if (text == null ||
                text == dartsText ||
                !text.enabled)
            {
                continue;
            }

            if (hudCanvasObject != null &&
                text.transform.IsChildOf(
                    hudCanvasObject.transform))
            {
                continue;
            }

            if (text.GetComponentInParent<Canvas>()
                == null)
            {
                continue;
            }

            if (IsOldHUDText(text.text))
                text.enabled = false;
        }

        Text[] legacyTexts =
            FindObjectsByType<Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Text text in legacyTexts)
        {
            if (text == null || !text.enabled)
                continue;

            if (text.GetComponentInParent<Canvas>()
                == null)
            {
                continue;
            }

            if (IsOldHUDText(text.text))
                text.enabled = false;
        }
    }

    private bool IsOldHUDText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string cleaned = value.Trim();

        // Example:
        // Darts - Reserve 6 Traps 0
        if (cleaned.StartsWith(
                "Darts",
                StringComparison.OrdinalIgnoreCase) &&
            cleaned.IndexOf(
                "Reserve",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (cleaned.StartsWith(
                "Reserve ",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cleaned.StartsWith(
                "Traps ",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    // ====================================================
    // CLEANUP
    // ====================================================

    private void OnDestroy()
    {
        if (hudCanvasObject != null)
        {
            Destroy(hudCanvasObject);
        }
    }
}
