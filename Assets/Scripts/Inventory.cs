using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Minecraft-style hotbar sa baba ng screen.
/// - Pindutin ang 1 hanggang 9 (o scroll wheel) para hawakan ang item sa slot na iyon.
/// - HINDI nito ginagalaw ang key mo sa "use item". Basahin lang ang Inventory.Instance.Selected
///   sa player script mo kapag pinindot mo yung use key.
/// - Ang UI ay binubuo ng code, kaya walang kailangang i-setup na Canvas o prefab.
///
/// Paano gamitin:
///   1. Ilagay ang script na ito sa kahit anong GameObject sa scene (hal. empty "Inventory").
///   2. Sa pickup mo:   Inventory.Instance.AddItem("Key", keyIcon);
///   3. Sa use key mo:  var item = Inventory.Instance.Selected;
///                      if (item != null && item.name == "Key") { ... Inventory.Instance.RemoveSelected(); }
/// </summary>
public class Inventory : MonoBehaviour
{
    // ---------- Item ----------
    [Serializable]
    public class Item
    {
        public string name;
        public Sprite icon;          // optional, kung wala, pangalan lang ang ipapakita
        public GameObject heldObject; // optional, lalabas sa kamay kapag napili (hal. flashlight)
        public bool stackable;
        public int count = 1;
    }

    public static Inventory Instance { get; private set; }

    [Header("Hotbar")]
    [Range(1, 9)] public int slotCount = 9;
    public float slotSize = 70f;
    public float spacing = 6f;
    public float bottomMargin = 20f;
    public bool scrollWheelSelects = true;

    [Header("Kulay")]
    public Color slotColor = new Color(0f, 0f, 0f, 0.55f);
    public Color borderColor = new Color(1f, 1f, 1f, 0.25f);
    public Color selectedBorderColor = Color.white;

    /// <summary>Tinatawag kapag nagbago ang napiling slot o ang laman nito. item ay pwedeng null.</summary>
    public event Action<int, Item> OnSelectedChanged;

    Item[] items;
    int selectedIndex;

    Image[] borders;
    Image[] icons;
    Text[] names;
    Text[] counts;

    public int SelectedIndex => selectedIndex;
    public Item Selected => items[selectedIndex];

    // ---------- Unity ----------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        items = new Item[slotCount];
        BuildUI();
        Select(0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Number keys 1-9
        for (int i = 0; i < slotCount; i++)
        {
            if (NumberKeyDown(i)) { Select(i); break; }
        }

        // Scroll wheel (optional)
        if (scrollWheelSelects)
        {
            float scroll = ScrollY();
            if (scroll > 0.01f) Select((selectedIndex - 1 + slotCount) % slotCount);
            else if (scroll < -0.01f) Select((selectedIndex + 1) % slotCount);
        }
    }

    // ---------- Public API ----------

    /// <summary>Magdagdag ng item sa unang bakanteng slot. False kapag puno na.</summary>
    public bool AddItem(string itemName, Sprite icon = null, GameObject heldObject = null, bool stackable = false, int amount = 1)
    {
        if (stackable)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (items[i] != null && items[i].name == itemName && items[i].stackable)
                {
                    items[i].count += amount;
                    Refresh(i);
                    return true;
                }
            }
        }

        for (int i = 0; i < slotCount; i++)
        {
            if (items[i] == null)
            {
                items[i] = new Item
                {
                    name = itemName,
                    icon = icon,
                    heldObject = heldObject,
                    stackable = stackable,
                    count = amount
                };
                Refresh(i);
                return true;
            }
        }

        Debug.Log("[Inventory] Puno na ang hotbar.");
        return false;
    }

    public bool HasItem(string itemName)
    {
        for (int i = 0; i < slotCount; i++)
            if (items[i] != null && items[i].name == itemName) return true;
        return false;
    }

    /// <summary>Hawak ba ngayon ang item na ito?</summary>
    public bool IsHolding(string itemName)
    {
        return Selected != null && Selected.name == itemName;
    }

    /// <summary>Tanggalin ang isang piraso ng item (by name). Para sa stackable, bawas count muna.</summary>
    public bool RemoveItem(string itemName, int amount = 1)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (items[i] != null && items[i].name == itemName)
            {
                RemoveAt(i, amount);
                return true;
            }
        }
        return false;
    }

    /// <summary>Tanggalin ang hawak na item (isang piraso).</summary>
    public void RemoveSelected(int amount = 1)
    {
        if (items[selectedIndex] != null) RemoveAt(selectedIndex, amount);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= slotCount) return;
        selectedIndex = index;
        Refresh(-1);
    }

    // ---------- Internals ----------
    void RemoveAt(int index, int amount)
    {
        Item it = items[index];
        if (it == null) return;

        it.count -= amount;
        if (it.count <= 0 || !it.stackable)
        {
            if (it.heldObject != null) it.heldObject.SetActive(false);
            items[index] = null;
        }
        Refresh(index);
    }

    // i = slot na nagbago (para sa info lang), -1 = napili lang ang slot
    void Refresh(int i)
    {
        for (int s = 0; s < slotCount; s++)
        {
            Item it = items[s];
            bool selected = s == selectedIndex;

            borders[s].color = selected ? selectedBorderColor : borderColor;
            borders[s].rectTransform.sizeDelta = Vector2.one * (selected ? slotSize + 6f : slotSize);

            bool hasIcon = it != null && it.icon != null;
            icons[s].enabled = hasIcon;
            icons[s].sprite = hasIcon ? it.icon : null;

            // Pangalan lang ang ipapakita kung walang icon
            names[s].text = (it != null && !hasIcon) ? it.name : "";
            counts[s].text = (it != null && it.stackable && it.count > 1) ? it.count.ToString() : "";

            // Ipakita lang ang hawak na object kapag napili ang slot
            if (it != null && it.heldObject != null)
                it.heldObject.SetActive(selected);
        }

        OnSelectedChanged?.Invoke(selectedIndex, items[selectedIndex]);
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var barGO = new GameObject("Hotbar", typeof(RectTransform));
        barGO.transform.SetParent(canvasGO.transform, false);
        var bar = (RectTransform)barGO.transform;
        float totalW = slotCount * slotSize + (slotCount - 1) * spacing;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, bottomMargin);
        bar.sizeDelta = new Vector2(totalW, slotSize + 10f);

        borders = new Image[slotCount];
        icons = new Image[slotCount];
        names = new Text[slotCount];
        counts = new Text[slotCount];

        Font font = GetFont();

        for (int i = 0; i < slotCount; i++)
        {
            // Border (nagpapalit ng kulay kapag napili)
            var slotGO = new GameObject("Slot" + (i + 1), typeof(RectTransform), typeof(Image));
            slotGO.transform.SetParent(barGO.transform, false);
            var slotRT = (RectTransform)slotGO.transform;
            slotRT.anchorMin = slotRT.anchorMax = new Vector2(0f, 0.5f);
            slotRT.pivot = new Vector2(0.5f, 0.5f);
            slotRT.anchoredPosition = new Vector2(slotSize * 0.5f + i * (slotSize + spacing), 0f);
            slotRT.sizeDelta = Vector2.one * slotSize;
            borders[i] = slotGO.GetComponent<Image>();
            borders[i].raycastTarget = false;

            // Madilim na loob
            var innerGO = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerGO.transform.SetParent(slotGO.transform, false);
            var innerRT = (RectTransform)innerGO.transform;
            Stretch(innerRT, 3f);
            var inner = innerGO.GetComponent<Image>();
            inner.color = slotColor;
            inner.raycastTarget = false;

            // Icon
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(innerGO.transform, false);
            Stretch((RectTransform)iconGO.transform, 6f);
            icons[i] = iconGO.GetComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
            icons[i].enabled = false;

            // Pangalan (kapag walang icon)
            names[i] = MakeText(innerGO.transform, "Name", font, 13, TextAnchor.MiddleCenter, Color.white);
            Stretch(names[i].rectTransform, 4f);

            // Numero ng slot (kaliwang taas)
            var num = MakeText(innerGO.transform, "Num", font, 14, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.8f));
            Stretch(num.rectTransform, 4f);
            num.text = (i + 1).ToString();

            // Bilang (kanang baba)
            counts[i] = MakeText(innerGO.transform, "Count", font, 15, TextAnchor.LowerRight, Color.white);
            Stretch(counts[i].rectTransform, 4f);
        }
    }

    static Text MakeText(Transform parent, string objName, Font font, int size, TextAnchor anchor, Color color)
    {
        var go = new GameObject(objName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = false;
        return t;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Font GetFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    // ---------- Input (gumagana sa luma at bagong Input System) ----------
    static bool NumberKeyDown(int i)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Alpha1 + i);
#endif
    }

    static float ScrollY()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var m = Mouse.current;
        return m == null ? 0f : m.scroll.ReadValue().y;
#else
        return Input.mouseScrollDelta.y;
#endif
    }
}