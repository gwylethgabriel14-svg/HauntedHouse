
using System;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Nine-slot hotbar.
// Supports normal items and 3 separate music-note pieces.
// Preserves existing Inventory APIs.

public class Inventory : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public string name;
        public Sprite icon;
        public GameObject heldObject;
        public bool stackable;
        public int count = 1;
    }

    public static Inventory Instance { get; private set; }

    [Header("Hotbar")]
    [Range(1, 9)]
    public int slotCount = 9;

    public float slotSize = 70f;
    public float spacing = 6f;
    public float bottomMargin = 20f;
    public bool scrollWheelSelects = true;

    [Header("Kulay")]
    public Color slotColor =
        new Color(0f, 0f, 0f, 0.55f);

    public Color borderColor =
        new Color(1f, 1f, 1f, 0.25f);

    public Color selectedBorderColor = Color.white;

    public event Action<int, Item> OnSelectedChanged;

    Item[] items;
    int selectedIndex;

    Image[] borders;
    Image[] icons;
    Text[] names;
    Text[] counts;

    public int SelectedIndex => selectedIndex;

    public Item Selected =>
        items != null ? items[selectedIndex] : null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        items = new Item[slotCount];

        BuildUI();
        Select(0);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        // Number keys 1-9.
        for (int i = 0; i < slotCount; i++)
        {
            if (NumberKeyDown(i))
            {
                Select(i);
                break;
            }
        }

        // Scroll wheel.
        if (scrollWheelSelects)
        {
            float scroll = ScrollY();

            if (scroll > 0.01f)
            {
                Select(
                    (selectedIndex - 1 + slotCount)
                    % slotCount);
            }
            else if (scroll < -0.01f)
            {
                Select(
                    (selectedIndex + 1)
                    % slotCount);
            }
        }
    }

    // Add ordinary items to the hotbar.
    public bool AddItem(
        string itemName,
        Sprite icon = null,
        GameObject heldObject = null,
        bool stackable = false,
        int amount = 1)
    {
        if (string.IsNullOrEmpty(itemName) ||
            amount < 1)
        {
            return false;
        }

        // Stack items with matching names.
        if (stackable)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (items[i] != null &&
                    items[i].name == itemName &&
                    items[i].stackable)
                {
                    items[i].count += amount;

                    if (items[i].icon == null &&
                        icon != null)
                    {
                        items[i].icon = icon;
                    }

                    Refresh(i);
                    return true;
                }
            }
        }

        if (!stackable &&
            amount > FreeSlotCount())
        {
            return false;
        }

        if (stackable && FreeSlotCount() == 0)
        {
            return false;
        }

        int times = stackable ? 1 : amount;

        for (int j = 0; j < times; j++)
        {
            int free = FirstFreeSlot();

            if (free < 0)
                return false;

            items[free] = new Item
            {
                name = itemName,
                icon = icon,
                heldObject = heldObject,
                stackable = stackable,
                count = stackable ? amount : 1
            };

            Refresh(free);
        }

        return true;
    }

    public bool HasItem(string itemName)
    {
        return CountItem(itemName) > 0;
    }

    public bool IsHolding(string itemName)
    {
        return Selected != null &&
               Selected.name == itemName;
    }

    public int CountItem(string itemName)
    {
        int count = 0;

        foreach (Item item in items)
        {
            if (item != null &&
                item.name == itemName)
            {
                count += item.count;
            }
        }

        return count;
    }

    // Adds a unique music note to its own hotbar slot.
    public bool AddMusicNote(
        int pieceIndex,
        Sprite icon = null)
    {
        int index =
            Mathf.Clamp(pieceIndex, 1, 3);

        string itemName =
            "Music Note " + index;

        // Don't add the same piece twice.
        if (HasItem(itemName))
            return true;

        return AddItem(
            itemName,
            icon,
            null,
            false);
    }

    public int CountMusicNotes()
    {
        return
            CountItem("Music Note 1") +
            CountItem("Music Note 2") +
            CountItem("Music Note 3") +
            CountItem("Music Note") +
            CountItem("Page");
    }

    public bool HasThreeMusicNotes()
    {
        return CountMusicNotes() >= 3;
    }

    // Remove 3 music notes after activating music box.
    public void ConsumeThreeMusicNotes()
    {
        int remaining = 3;

        string[] namesToRemove =
        {
            "Music Note 1",
            "Music Note 2",
            "Music Note 3",
            "Music Note",
            "Page"
        };

        foreach (string itemName in namesToRemove)
        {
            while (remaining > 0 &&
                   HasItem(itemName))
            {
                RemoveItem(itemName);
                remaining--;
            }

            if (remaining == 0)
                break;
        }
    }

    public bool RemoveItem(
        string itemName,
        int amount = 1)
    {
        if (amount <= 0)
            return false;

        for (int i = 0; i < slotCount; i++)
        {
            if (items[i] != null &&
                items[i].name == itemName)
            {
                RemoveAt(i, amount);
                return true;
            }
        }

        return false;
    }

    public void RemoveSelected(int amount = 1)
    {
        if (items[selectedIndex] != null)
        {
            RemoveAt(selectedIndex, amount);
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= slotCount)
            return;

        selectedIndex = index;

        Refresh(-1);
    }

    int FirstFreeSlot()
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (items[i] == null)
                return i;
        }

        return -1;
    }

    int FreeSlotCount()
    {
        int free = 0;

        foreach (Item item in items)
        {
            if (item == null)
                free++;
        }

        return free;
    }

    void RemoveAt(int index, int amount)
    {
        Item item = items[index];

        if (item == null)
            return;

        item.count -= amount;

        if (item.count <= 0 || !item.stackable)
        {
            if (item.heldObject != null)
            {
                item.heldObject.SetActive(false);
            }

            items[index] = null;
        }

        Refresh(index);
    }

    void Refresh(int changed)
    {
        if (borders == null)
            return;

        for (int s = 0; s < slotCount; s++)
        {
            Item item = items[s];
            bool selected = s == selectedIndex;

            borders[s].color =
                selected
                ? selectedBorderColor
                : borderColor;

            borders[s].rectTransform.sizeDelta =
                Vector2.one *
                (selected ? slotSize + 6f : slotSize);

            bool hasIcon =
                item != null &&
                item.icon != null;

            icons[s].enabled = hasIcon;

            icons[s].sprite =
                hasIcon ? item.icon : null;

            names[s].text =
                item != null && !hasIcon
                ? item.name
                : "";

            counts[s].text =
                item != null &&
                item.stackable &&
                item.count > 1
                ? item.count.ToString()
                : "";

            if (item != null &&
                item.heldObject != null)
            {
                item.heldObject.SetActive(selected);
            }
        }

        OnSelectedChanged?.Invoke(
            selectedIndex,
            items[selectedIndex]);
    }

    void BuildUI()
    {
        GameObject canvasGO =
            new GameObject("InventoryCanvas");

        canvasGO.transform.SetParent(
            transform, false);

        Canvas canvas =
            canvasGO.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 50;

        CanvasScaler scaler =
            canvasGO.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920, 1080);

        scaler.matchWidthOrHeight = 0.5f;

        GameObject barGO = new GameObject(
            "Hotbar",
            typeof(RectTransform));

        barGO.transform.SetParent(
            canvasGO.transform, false);

        RectTransform bar =
            (RectTransform)barGO.transform;

        float totalW =
            slotCount * slotSize +
            (slotCount - 1) * spacing;

        bar.anchorMin =
            bar.anchorMax =
            new Vector2(0.5f, 0f);

        bar.pivot = new Vector2(0.5f, 0f);

        bar.anchoredPosition =
            new Vector2(0f, bottomMargin);

        bar.sizeDelta =
            new Vector2(
                totalW,
                slotSize + 10f);

        borders = new Image[slotCount];
        icons = new Image[slotCount];
        names = new Text[slotCount];
        counts = new Text[slotCount];

        Font font = GetFont();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotGO = new GameObject(
                "Slot" + (i + 1),
                typeof(RectTransform),
                typeof(Image));

            slotGO.transform.SetParent(
                barGO.transform, false);

            RectTransform slotRT =
                (RectTransform)slotGO.transform;

            slotRT.anchorMin =
                slotRT.anchorMax =
                new Vector2(0f, 0.5f);

            slotRT.pivot =
                new Vector2(0.5f, 0.5f);

            slotRT.anchoredPosition =
                new Vector2(
                    slotSize * 0.5f +
                    i * (slotSize + spacing),
                    0f);

            slotRT.sizeDelta =
                Vector2.one * slotSize;

            borders[i] =
                slotGO.GetComponent<Image>();

            borders[i].raycastTarget = false;

            GameObject innerGO = new GameObject(
                "Inner",
                typeof(RectTransform),
                typeof(Image));

            innerGO.transform.SetParent(
                slotGO.transform, false);

            Stretch(
                (RectTransform)innerGO.transform,
                3f);

            Image inner =
                innerGO.GetComponent<Image>();

            inner.color = slotColor;
            inner.raycastTarget = false;

            GameObject iconGO = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image));

            iconGO.transform.SetParent(
                innerGO.transform, false);

            Stretch(
                (RectTransform)iconGO.transform,
                6f);

            icons[i] =
                iconGO.GetComponent<Image>();

            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
            icons[i].enabled = false;

            names[i] = MakeText(
                innerGO.transform,
                "Name",
                font,
                13,
                TextAnchor.MiddleCenter,
                Color.white);

            Stretch(
                names[i].rectTransform,
                4f);

            Text number = MakeText(
                innerGO.transform,
                "Num",
                font,
                14,
                TextAnchor.UpperLeft,
                new Color(1f, 1f, 1f, 0.8f));

            Stretch(
                number.rectTransform,
                4f);

            number.text =
                (i + 1).ToString();

            counts[i] = MakeText(
                innerGO.transform,
                "Count",
                font,
                15,
                TextAnchor.LowerRight,
                Color.white);

            Stretch(
                counts[i].rectTransform,
                4f);
        }
    }

    static Text MakeText(
        Transform parent,
        string objName,
        Font font,
        int size,
        TextAnchor align,
        Color color)
    {
        GameObject go = new GameObject(
            objName,
            typeof(RectTransform),
            typeof(Text));

        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();

        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;

        text.horizontalOverflow =
            HorizontalWrapMode.Wrap;

        text.verticalOverflow =
            VerticalWrapMode.Truncate;

        text.resizeTextForBestFit = false;

        return text;
    }

    static void Stretch(
        RectTransform rt,
        float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;

        rt.offsetMin =
            new Vector2(inset, inset);

        rt.offsetMax =
            new Vector2(-inset, -inset);
    }

    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>(
            "Arial.ttf");
#endif
    }

    static bool NumberKeyDown(int i)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;

        if (kb == null)
            return false;

        return kb[
            (Key)((int)Key.Digit1 + i)
        ].wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.Alpha1 + i);
#endif
    }

    static float ScrollY()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var mouse = Mouse.current;

        return mouse == null
            ? 0f
            : mouse.scroll.ReadValue().y;
#else
        return Input.mouseScrollDelta.y;
#endif
    }
}
